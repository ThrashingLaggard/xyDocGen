namespace xyDocumentor.Core.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using xyDocumentor.Core.Models;
using xyToolz.Helper.Logging;

/// <summary>
/// Deterministic, testable CLI parser for xyDocumentor.
/// Supports "--flag", "--flag=value" and "--flag value".
/// </summary>
internal static class StringAnalyzer
{
    public static string Description { get; set; }

    private static readonly HashSet<string> AllowedFormats = new(StringComparer.OrdinalIgnoreCase) { "md", "html", "pdf", "json" };

    /// <summary>
    /// Parse args into CliOptions. Returns false with an error if invalid.
    /// </summary>
    public static bool TryParseOptions(string[] args, out CliOptions opts, out string error)
    {
        error = "";
        List<string> tokens = args?.ToList() ??[];
        var dictEq = BuildEqMap(tokens); // for --key=value
        var i = 0;

        // Local accumulators with sensible defaults
        string rootPath = null;
        string outPath = null;  // Benutzer kann --out setzen; wir wandeln später in outBase um
        string folder = "docs";
        List<string> listedFormats = ["pdf"];
        List<string> listedSubfolders = ["api"];
        bool includeNonPublic = true; // default: include non-public (unless --private)
        var excludes = CliOptions.DefaultExcludes();

        bool showOnly = false;
        bool buildIndex = false;
        bool buildTree = false;
        bool help = false;
        bool info = false;

        while (i < tokens.Count)
        {
            string t = tokens[i];
            if (!IsFlag(t)) { i++; continue; }

            var key = t.Trim();

            // `--key=value` → prefer equals map
            if (TryGetEq(dictEq, key, out string eqValue))
            {
                switch (KeyName(key))
                {
                    case "--root": rootPath = eqValue; break;
                    case "--out": outPath = eqValue; break;
                    case "--folder": folder = eqValue; break;

                    case "--subfolder":
                        listedSubfolders = NormalizeList(eqValue);
                        if (listedSubfolders.Count == 0)
                            listedSubfolders = ["api"];
                        break;

                    case "--format":
                        listedFormats = NormalizeFormats(eqValue);
                        if (listedFormats.Count == 0)
                            listedFormats = ["md"];
                        foreach (var f in listedFormats)
                        {
                            if (!AllowedFormats.Contains(f, StringComparer.OrdinalIgnoreCase))
                            {
                                error = $"Unsupported --format '{f}'. Allowed: {string.Join(", ", AllowedFormats)}";
                                opts = null; return false;
                            }
                        }
                        break;

                    case "--exclude":
                        foreach (var part in SplitList(eqValue))
                            excludes.Add(part);
                        break;

                    default:
                        // flags with boolean assignment
                        var boolVal = ParseBool(eqValue);
                        ApplyBooleanFlag(
                            KeyName(key), boolVal,
                            ref showOnly, ref buildIndex, ref buildTree, ref help, ref info, ref includeNonPublic
                        );
                        break;
                }
                i++; continue;
            }

            // `--key value` form
            switch (KeyName(key))
            {
                case "--root":
                    if (!TryReadNext(tokens, ref i, out rootPath))
                    { error = "Missing value after --root."; opts = null; return false; }
                    continue;

                case "--out":
                    if (!TryReadNext(tokens, ref i, out outPath))
                    { error = "Missing value after --out."; opts = null; return false; }
                    continue;

                case "--folder":
                    if (!TryReadNext(tokens, ref i, out folder))
                    { error = "Missing value after --folder."; opts = null; return false; }
                    continue;

                case "--subfolder":
                    if (!TryReadNext(tokens, ref i, out string rawSubs))
                    { error = "Missing value after --subfolder."; opts = null; return false; }
                    listedSubfolders = NormalizeList(rawSubs);
                    continue;

                case "--format":
                    if (!TryReadNext(tokens, ref i, out var fmt))
                    { error = "Missing value after --format."; opts = null; return false; }
                    listedFormats = NormalizeFormats(fmt);
                    foreach (var f in listedFormats)
                    {
                        if (!AllowedFormats.Contains(f, StringComparer.OrdinalIgnoreCase))
                        {
                            error = $"Unsupported --format '{f}'. Allowed: {string.Join(", ", AllowedFormats)}";
                            opts = null; return false;
                        }
                    }
                    continue;

                case "--exclude":
                    if (!TryReadNext(tokens, ref i, out var exStr))
                    { error = "Missing value after --exclude."; opts = null; return false; }
                    foreach (var part in SplitList(exStr))
                        excludes.Add(part);
                    continue;

                // boolean flags (presence = true)
                case "--show": showOnly = true; i++; continue;
                case "--index": buildIndex = true; i++; continue;
                case "--tree": buildTree = true; i++; continue;
                case "--help": help = true; i++; continue;
                case "--info": info = true; i++; continue;
                case "--private": includeNonPublic = false; i++; continue;

                default:
                    error = $"Unknown option '{key}'. Use --help.";
                    opts = null; return false;
            }
        }

        // -------- Pfade & Defaults auflösen --------
        if (string.IsNullOrWhiteSpace(rootPath))
            rootPath = GetDefaultRoot();

        // outBase = Basis-Ausgabeverzeichnis (z. B. <root>/docs oder explizites --out)
        var outBase = string.IsNullOrWhiteSpace(outPath)
            ? Path.Combine(rootPath, folder)
            : outPath;

        // Final normalization der Basis & Root
        outBase = NormalizePath(outBase);
        rootPath = NormalizePath(rootPath);

        // Root prüfen
        if (!Directory.Exists(rootPath))
        {
            error = $"Root path does not exist: '{rootPath}'.";
            opts = null; return false;
        }

        // -------- (E) 1:1-Abbildung & Mapping Format → Zielpfad --------
        // 1 Subfolder für alle Formate erlauben; sonst 1:1 verlangen
        if (listedSubfolders.Count == 1 && listedFormats.Count > 1)
        {
            listedSubfolders = [.. Enumerable.Repeat(listedSubfolders[0], listedFormats.Count)];
        }
        else if (listedSubfolders.Count != listedFormats.Count)
        {
            error = $"Number of --subfolder entries ({listedSubfolders.Count}) must be 1 or equal to number of formats ({listedFormats.Count}).";
            opts = null; return false;
        }

        var outputDirs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int idx = 0; idx < listedFormats.Count; idx++)
        {
            var f = listedFormats[idx].ToLowerInvariant();
            var sub = listedSubfolders[idx];
            var full = NormalizePath(Path.Combine(outBase, sub));
            outputDirs[f] = full;
        }

        // -------- Optionen befüllen --------
        opts = new CliOptions
        {
            RootPath = rootPath,
            OutPath = outBase,             // Basis (z. B. <repo>/docs oder --out)
            Formats = listedFormats,
            Subfolders = listedSubfolders,  // Liste der Subfolder (in gleicher Reihenfolge wie Formats)
            OutputDirs = outputDirs,        // Format → absoluter Zielpfad
            Format = listedFormats.FirstOrDefault(), // Backcompat

            IncludeNonPublic = includeNonPublic,
            ExcludedParts = excludes,
            ShowOnly = showOnly,
            BuildIndex = buildIndex,
            BuildTree = buildTree,
            Help = help,
            Info = info
        };
        return true;
    }


    // ------------------------
    // Help / Info text
    // ------------------------
    public static string BuildHelpText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("xydocgen  —  generate documentation from C# sources");
        sb.AppendLine();
        sb.AppendLine("Usage:");
        sb.AppendLine("  xydocgen --root <path> --out <folder> --format <md|html|pdf|json> [options]");
        sb.AppendLine();
        sb.AppendLine("Options:");
        sb.AppendLine("  --help                                      Show this help and exit");
        sb.AppendLine("  --info                                      Show current status info and exit");
        sb.AppendLine("  --root <path>                           Source root (default: current project dir)");
        sb.AppendLine("  --out <path>                             Output directory (overrides --folder/--subfolder)");
        sb.AppendLine("  --folder <name>                       Default: 'docs'");
        sb.AppendLine("  --subfolder <name>                  Default: 'api'");
        sb.AppendLine("  --format <md|html|pdf|json>  Default: 'md'");
        sb.AppendLine("  --exclude <a;b;c>                      Extra directories to exclude; delimiter ';' or ','");
        sb.AppendLine("  --private                                  Exclude non-public members (i.e., IncludeNonPublic=false)");
        sb.AppendLine("  --index                                    Build namespace index (INDEX.md)");
        sb.AppendLine("  --tree                                      Build project structure (PROJECT-STRUCTURE.md)");
        sb.AppendLine("  --show                                     Print Markdown to console only (no files written)");
        return sb.ToString();
    }

    public static string BuildInfoText()
    {
        return "xyDocGen — reduced the package size by removing transistive packages.";
    }

    // ------------------------
    // Helpers
    // ------------------------

    private static bool IsFlag(string token) => token.StartsWith("--") && token.Length > 2;

    private static string KeyName(string key)
    {
        // Normalize "--key=value" → "--key"
        var eq = key.IndexOf('=');
        return eq > 0 ? key[..eq] : key;
    }

    private static bool TryGetEq(Dictionary<string, string> eqMap, string token, out string value)
    {
        // token might be "--key" or "--key=value"
        var k = KeyName(token);
        return eqMap.TryGetValue(k, out value);
    }

    private static Dictionary<string, string> BuildEqMap(List<string> tokens)
    {
        // Support case-insensitive keys
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tokens)
        {
            if (!IsFlag(t)) continue;
            var idx = t.IndexOf('=');
            if (idx > 0 && idx < t.Length - 1)
            {
                var key = t[..idx];
                var val = t[(idx + 1)..].Trim('"');
                map[key] = val;
            }
        }
        return map;
    }

    private static bool TryReadNext(List<string> tokens, ref int i, out string value)
    {
        // current is a flag; the next token must be a value
        if (i + 1 < tokens.Count && !IsFlag(tokens[i + 1]))
        {
            value = tokens[i + 1].Trim('"');
            i += 2; // consume flag + value
            return true;
        }
        value = null;
        i++; // consume flag anyway
        return false;
    }

    private static List<string> NormalizeFormats(string s)
    => [.. NormalizeList(s)
        .Select(x => x.ToLowerInvariant())
        .Distinct(StringComparer.OrdinalIgnoreCase)];

    private static List<string> NormalizeList(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return [];
        return [.. s.Split([ ';', ',' ], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
    }

    private static IEnumerable<string> SplitList(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) yield break;
        foreach (var part in s.Split([';', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            yield return part;
    }

    private static string NormalizePath(string p)
    {
        if (string.IsNullOrWhiteSpace(p)) return p;
        return Path.GetFullPath(p);
    }

    private static string GetDefaultRoot()
    {
#if DEBUG
        // project directory: /bin/Debug/... → step up to repo root-ish
        var cwd = Directory.GetCurrentDirectory();
        var d = Directory.GetParent(cwd);
        if (d?.Parent?.Parent != null)
            return d.Parent.Parent.FullName;
        return cwd;
#else
            return Environment.CurrentDirectory;
#endif
    }

    private static bool ParseBool(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return true;
        return s.Equals("1") || s.Equals("true", StringComparison.OrdinalIgnoreCase) || s.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyBooleanFlag(string key, bool value,
        ref bool showOnly, ref bool buildIndex, ref bool buildTree, ref bool help, ref bool info, ref bool includeNonPublic)
    {
        switch (key)
        {
            case "--show": showOnly = value; break;
            case "--index": buildIndex = value; break;
            case "--tree": buildTree = value; break;
            case "--help": help = value; break;
            case "--info": info = value; break;
            case "--private": if (value) includeNonPublic = false; break;
        }
    }

    // Backwards-compatible API (only used by older code paths). Prefer TryParseOptions above.
    internal static (string root, string outPath, string format, bool includeNonPublic, HashSet<string> excludedParts)AnalyzeArgs(string[] args_)
    {
        if (!TryParseOptions(args_, out var o, out string parseError))
        {
            // Fallback: alter Default-Pfad mit 'docs/api'
            var defRoot = GetDefaultRoot();
            xyLog.Log(parseError);
            return (defRoot, Path.Combine(defRoot, "docs", "api"), "md", true, CliOptions.DefaultExcludes());
        }

        string legacyOut;
        string selectedFormat = o.Format;
        if (!string.IsNullOrWhiteSpace(selectedFormat) && o.OutputDirs != null
            && o.OutputDirs.TryGetValue(selectedFormat, out var mapped))
        {
            legacyOut = mapped; // z. B. <OutPath>/<Subfolder_for_that_format>
        }
        else
        {
            // Fallback: OutPath + erster Subfolder (falls kein Mapping verfügbar)
            var sub = o.Subfolders?.FirstOrDefault() ?? "api";
            legacyOut = Path.Combine(o.OutPath ?? GetDefaultRoot(), sub);
        }

        return (o.RootPath, legacyOut, selectedFormat, o.IncludeNonPublic, o.ExcludedParts);
    }
}

