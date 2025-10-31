namespace xyDocumentor.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Text;
using xyDocumentor.Models;
using xyToolz.Helper.Logging;

/// <summary>
/// Parses CLI arguments for xyDocGen.
/// Supports:
/// - Multiple output formats in one run (e.g., "md,html,pdf,json").
/// - Optional per-format subfolders (e.g., "--subfolder api;site") matching formats 1:1.
/// - Console display modes that suppress file I/O: <c>--show</c>, <c>--show-index</c>, <c>--show-tree</c>.
/// - Boolean flags both as presence-only (e.g., <c>--show</c>) and as assignment (e.g., <c>--show=false</c>).
/// - Value flags as "--key value" or "--key=value".
///
/// The parser is intentionally permissive: it normalizes case, accepts aliases (e.g., "markdown" → "md"),
/// and falls back to sensible defaults when a flag is omitted.
/// </summary>
internal static class StringAnalyzer
{
#nullable enable

    /// <summary>  Human-readable description that can be set by the host; not used by the parser itself. </summary>
    public static string? Description { get; set; }

    /// <summary>  The set of formats this tool understands. Case-insensitive. "markdown" is normalized to "md".</summary>
    private static readonly HashSet<string> AllowedFormats = new(StringComparer.OrdinalIgnoreCase) { "md", "markdown", "html", "pdf", "json" };




    /// <summary>
    /// Parse raw command-line arguments into a strongly-typed <see cref="CliOptions"/>.
    /// Returns <c>true</c> on success. On failure, <paramref name="opts"/> is <c>null</c>
    /// and <paramref name="error"/> contains a user-facing message.
    ///
    /// Parsing rules:
    /// - Flags are case-insensitive.
    /// - "--key=value" is preferred over "--key value" when both are present (first one wins).
    /// - Multiple formats and subfolders can be comma/semicolon separated.
    /// - If exactly one subfolder is provided for many formats, it is replicated for all formats.
    /// - If subfolder count is not 0/1/format-count, parsing fails.
    /// - If <c>--root</c> is omitted, a sensible default is chosen (see <see cref="GetDefaultRoot"/>).
    /// </summary>
    public static bool TryParseOptions(string[] args, out CliOptions opts, out string error)
    {
        // Default error placeholder; will be overridden with a specific message on actual error.
        error = "Wtf is this convoluded piece of shit?";

        // Tokenize input safely; allow null args (empty list).
        List<string> tokens = args?.ToList() ?? [];

        // Build a quick lookup for "--key=value" forms so we can prefer them during parsing.
        var dictEq = BuildEqMap(tokens);

        // Cursor for scanning tokens left-to-right.
        var i = 0;

        // Inbound paths (root of source and base out directory).
        string? rootPath = null;
        string? outPath = null;  // User may pass --out; we later resolve a normalized base "outBase".

        // Default folder used if --out is not provided (e.g., <root>/docs).
        string folder = "api";

        // Default formats to generate. Multiple formats are supported and may be expanded later.
        List<string> listedFormats = ["pdf", "md", "html", "json"];

        // Optional per-format subfolders, 1:1 with formats if provided.
        List<string> listedSubfolders = [];

        // By default include non-public members, unless --private is set.
        bool includeNonPublic = true;

        // Exclude folders/files by simple path-part matching (caller may extend).
        var excludes = CliOptions.DefaultExcludes();

        // Output/printing switches
        bool showOnly = false;           // If true, suppress file output and print content to console.
        bool buildIndex = false;         // Generate namespace index file(s) unless suppressed by showOnly.
        bool buildTree = false;          // Generate project structure file(s) unless suppressed by showOnly.
        bool showIndexToConsole = false; // Print index only to console (no files).
        bool showTreeToConsole = false;  // Print tree only to console (no files).
        bool help = false;               // Print help text and exit.
        bool info = false;               // Print current configuration + README and exit.

        // -----------------------
        // First pass over tokens
        // -----------------------
        while (i < tokens.Count)
        {
            string t = tokens[i];

            // Skip non-flag tokens (positional args are not used by this parser).
            if (!IsFlag(t)) { i++; continue; }

            // Canonicalize the current flag token (e.g., "--format", or "--format=md,html").
            var key = t.Trim();

            // Prefer "--key=value" when present, to avoid ambiguity with "--key value".
            if (TryGetEq(dictEq, key, out string eqValue))
            {
                switch (KeyName(key)) // Strip "=value" for switch matching
                {
                    case "--root":
                        // Source code root; the directory that will be scanned for *.cs files.
                        rootPath = eqValue;
                        break;

                    case "--out":
                        // Base output directory (e.g., "<repo>/docs"); combined with per-format subfolders later.
                        outPath = eqValue;
                        break;

                    case "--folder":
                        // Alternative to --out: use "<root>/<folder>" (default "docs") as base output directory.
                        folder = eqValue;
                        break;

                    case "--subfolder":
                        // Optional: map formats to subfolders (0/1/N entries).
                        // Examples:
                        //   --format md,html  --subfolder api;site
                        //   --format pdf      --subfolder pdf
                        listedSubfolders = NormalizeList(eqValue);
                        if (listedSubfolders.Count == 0)
                            listedSubfolders = [];
                        break;

                    case "--format":
                        {
                            // Accept aliases & duplicates; normalize to "md|html|pdf|json".
                            List<string> normalizedListedFormats = NormalizeFormats(eqValue);

                            // If user passed empty list (e.g., "--format="), default to md.
                            if (normalizedListedFormats.Count == 0) normalizedListedFormats = ["md"];

                            foreach (var f in normalizedListedFormats)
                            {
                                string nf = NormalizeFormatAlias(f);
                                if (!AllowedFormats.Contains(nf))
                                {
                                    error = $"Unsupported --format '{f}'. Allowed: {string.Join(", ", AllowedFormats)}";
                                    opts = null!; return false;
                                }

                                // Append only if not already present (case-insensitive).
                                if (!listedFormats.Any(x => x.Equals(nf, StringComparison.OrdinalIgnoreCase)))
                                    listedFormats.Add(nf);
                            }
                            break;
                        }

                    case "--exclude":
                        // Add extra path parts to exclude (semicolon/comma separated).
                        foreach (var part in SplitList(eqValue))
                            excludes.Add(part);
                        break;

                    default:
                        // Treat unknown keys here as potential boolean assignment, e.g., "--show=false".
                        var boolVal = ParseBool(eqValue);
                        ApplyBooleanFlag(
                            KeyName(key), boolVal,
                            ref showOnly, ref buildIndex, ref buildTree, ref help, ref info, ref includeNonPublic
                        );

                        // Mirror special console-only flags, which piggyback on the boolean assignment path.
                        if (KeyName(key).Equals("--show-index", StringComparison.OrdinalIgnoreCase)) showIndexToConsole = boolVal;
                        if (KeyName(key).Equals("--show-tree", StringComparison.OrdinalIgnoreCase)) showTreeToConsole = boolVal;
                        break;
                }

                // Move to the next token (we consumed "--key=value" entirely).
                i++;
                continue;
            }

            // If no "--key=value" was present, parse the "--key value" form (or presence-only boolean).
            switch (KeyName(key))
            {
                case "--root":
                    // Require the next token as the value for --root.
                    if (!TryReadNext(tokens, ref i, out rootPath))
                    { error = "Missing value after --root."; opts = null!; return false; }
                    continue;

                case "--out":
                    if (!TryReadNext(tokens, ref i, out outPath))
                    { error = "Missing value after --out."; opts = null!; return false; }
                    continue;

                case "--folder":
                    if (!TryReadNext(tokens, ref i, out folder))
                    { error = "Missing value after --folder."; opts = null!; return false; }
                    continue;

                case "--subfolder":
                    if (!TryReadNext(tokens, ref i, out string rawSubs))
                    { error = "Missing value after --subfolder."; opts = null!; return false; }
                    listedSubfolders = NormalizeList(rawSubs);
                    continue;

                case "--format":
                    if (!TryReadNext(tokens, ref i, out var fmt))
                    { error = "Missing value after --format."; opts = null!; return false; }

                    // Same normalization logic as above, but for the space-separated form.
                    var fmtList = NormalizeFormats(fmt);
                    foreach (var f in fmtList)
                    {
                        var nf = NormalizeFormatAlias(f);
                        if (!AllowedFormats.Contains(nf))
                        {
                            error = $"Unsupported --format '{f}'. Allowed: {string.Join(", ", AllowedFormats)}";
                            opts = null!; return false;
                        }
                        if (!listedFormats.Any(x => x.Equals(nf, StringComparison.OrdinalIgnoreCase)))
                            listedFormats.Add(nf);
                    }
                    continue;

                case "--exclude":
                    if (!TryReadNext(tokens, ref i, out var exStr))
                    { error = "Missing value after --exclude."; opts = null!; return false; }
                    foreach (var part in SplitList(exStr))
                        excludes.Add(part);
                    continue;

                // Presence-only boolean flags (if present → true).
                case "--show": showOnly = true; i++; continue;
                case "--index": buildIndex = true; i++; continue;
                case "--tree": buildTree = true; i++; continue;
                case "--help": help = true; i++; continue;
                case "--info": info = true; i++; continue;
                case "--private": includeNonPublic = false; i++; continue;

                default:
                    // Unknown flag → hard error (fail fast and hint at --help).
                    error = $"Unknown option '{key}'. Use --help.";
                    opts = null!; return false;
            }
        }

        // -----------------------
        // Resolve paths & defaults
        // -----------------------

        // If user did not provide a root, compute a sensible default that works in Debug/Release.
        if (string.IsNullOrWhiteSpace(rootPath))
            rootPath = GetDefaultRoot();

        // Decide base output directory:
        //   - If --out is specified, use it as-is.
        //   - Else derive from <root>/<folder>.
        var outBase = string.IsNullOrWhiteSpace(outPath)
            ? Path.Combine(rootPath, folder)
            : outPath;

        // Normalize both paths to absolute canonical forms.
        outBase = NormalizePath(outBase);
        rootPath = NormalizePath(rootPath);

        // Validate the root exists before proceeding; otherwise we cannot enumerate source files.
        if (!Directory.Exists(rootPath))
        {
            error = $"Root path does not exist: '{rootPath}'.";
            opts = null!; return false;
        }

        // -----------------------
        // Subfolder consistency
        // -----------------------

        // If exactly one subfolder is provided but multiple formats are requested,
        // replicate the single subfolder across all formats (common UX shortcut).
        if (listedSubfolders.Count == 1 && listedFormats.Count > 1)
        {
            listedSubfolders = [.. Enumerable.Repeat(listedSubfolders[0], listedFormats.Count)];
        }
        // If a non-zero number of subfolders is provided, it must match format count 1:1.
        else if (listedSubfolders.Count != 0 && listedSubfolders.Count != listedFormats.Count)
        {
            error = $"Number of --subfolder entries ({listedSubfolders.Count}) must be 0, 1, or equal to number of formats ({listedFormats.Count}).";
            opts = null!; return false;
        }

        // Build a map of format → absolute output directory (per-format subfolder under outBase).
        var outputDirs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int idx = 0; idx < listedFormats.Count; idx++)
        {
            var f = listedFormats[idx].ToLowerInvariant();

            // If subfolders were provided, use them; else default folder name = format name.
            var folderName = (listedSubfolders.Count > 0) ? listedSubfolders[idx] : f;

            // Normalize the combined path.
            var full = NormalizePath(Path.Combine(outBase, folderName));
            outputDirs[f] = full;
        }

        // -----------------------
        // Materialize options object
        // -----------------------
        opts = new CliOptions
        {
            RootPath = rootPath,                         // Final source root directory
            OutPath = outBase,                           // Base output directory
            Formats = [.. listedFormats.Distinct(StringComparer.OrdinalIgnoreCase)],
            Subfolders = listedSubfolders,               // For reference / diagnostics
            OutputDirs = outputDirs,                     // Format → absolute target directory
            IncludeNonPublic = includeNonPublic,         // Include/protect members toggle
            ExcludedParts = excludes,                    // Exclusion filters (path parts)
            ShowOnly = showOnly,                         // Console-only (no file output)
            ShowIndexToConsole = showIndexToConsole,     // Print index to console only
            ShowTreeToConsole = showTreeToConsole,       // Print tree to console only
            BuildIndex = buildIndex,                     // Build index file(s)
            BuildTree = buildTree,                       // Build project tree file(s)
            Help = help,                                 // Show help and exit
            Info = info                                  // Show info+README and exit
        };

        // Success: the caller can now decide to write files, print to console, or exit early for --help/--info.
        return true;
    }


    /// <summary>
    /// Build user-facing help text describing all CLI options, examples, and notes.
    /// This text is printed when <c>--help</c> is passed.
    /// </summary>
    public static string BuildHelpText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("xyDocGen — generate documentation from C# source files");
        sb.AppendLine();
        sb.AppendLine("Usage:");
        sb.AppendLine("  xydocgen --root <path> --out <folder> --format <md|html|pdf|json> [options]");
        sb.AppendLine();
        sb.AppendLine("Examples:");
        sb.AppendLine("  xydocgen --root src --out docs --format md,html,pdf --index --tree");
        sb.AppendLine("  xydocgen --root src --out docs --format md --show-index");
        sb.AppendLine("  xydocgen --root src --out docs --format pdf --private");
        sb.AppendLine();
        sb.AppendLine("Options:");
        sb.AppendLine("  --help                                         Show this help and exit");
        sb.AppendLine("  --info                                         Show current configuration and README.md");
        sb.AppendLine("  --root <path>                              Source root (default: current project dir)");
        sb.AppendLine("  --out <path>                               Base output directory (overrides --folder)");
        sb.AppendLine("  --folder <name>                          Default: 'docs'");
        sb.AppendLine("  --subfolder <a;b;c>                     Optional; per-format subfolders (1:1 with formats)");
        sb.AppendLine("  --format <md|html|pdf|json>     Default: 'pdf,md,html,json' (supports multiple, comma/semicolon separated)");
        sb.AppendLine("  --exclude <a;b;c>                        Additional directories to exclude");
        sb.AppendLine("  --private                                    Exclude non-public members from documentation");
        sb.AppendLine("  --index                                       Build namespace index (INDEX.md)");
        sb.AppendLine("  --tree                                        Build project structure (PROJECT-STRUCTURE.md)");
        sb.AppendLine("  --show                                        Print documentation to console (no files written)");
        sb.AppendLine("  --show-index                              Print namespace index to console only");
        sb.AppendLine("  --show-tree                               Print project tree to console only");
        sb.AppendLine();
        sb.AppendLine("Notes:");
        sb.AppendLine("- You can specify multiple formats: --format md,html,pdf");
        sb.AppendLine("- '--show-*' options suppress file output and print directly to console.");
        sb.AppendLine();
        return sb.ToString();
    }




















    /// <summary>
    /// Returns <c>true</c> if the token syntactically looks like a flag (starts with "--").
    /// Positional values do not start with "--".
    /// </summary>
    private static bool IsFlag(string token) => token.StartsWith("--") && token.Length > 2;

    /// <summary>
    /// Converts "--key" or "--key=value" into the canonical key name "--key".
    /// </summary>
    private static string KeyName(string key)
    {
        // Normalize "--key=value" → "--key"
        var eq = key.IndexOf('=');
        return eq > 0 ? key[..eq] : key;
    }

    /// <summary>
    /// For a given token, tries to look up its "--key=value" mapping from <paramref name="eqMap"/>.
    /// Returns <c>true</c> and the <c>value</c> if present; otherwise <c>false</c>.
    /// </summary>
    private static bool TryGetEq(Dictionary<string, string> eqMap, string token, out string value)
    {
        // token might be "--key" or "--key=value"
        var k = KeyName(token);
        return eqMap.TryGetValue(k, out value!);
    }



    /// <summary>
    /// Builds a dictionary of "--key=value" mappings from the raw token list.
    /// Keys are case-insensitive; the right-hand side value has surrounding quotes trimmed.
    /// </summary>
    private static Dictionary<string, string> BuildEqMap(List<string> tokens)
    {
        // Support case-insensitive keys
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tokens)
        {
            if (!IsFlag(t)) continue;

            // Accept "--key=value" only if there is at least one char after '='.
            int idx = t.IndexOf('=');
            if (idx > 0 && idx < t.Length - 1)
            {
                var key = t[..idx];
                var val = t[(idx + 1)..].Trim('"');
                map[key] = val;
            }
        }
        return map;
    }


    /// <summary>
    /// Reads the next token as a value for the current flag in "--key value" form.
    /// Returns <c>true</c> and advances the index by 2 on success; otherwise returns <c>false</c>
    /// and consumes just the flag (advances by 1).
    /// </summary>
    private static bool TryReadNext(List<string> tokens, ref int i, out string value)
    {
        // The next token is a value only if it exists and is not another flag.
        if (i + 1 < tokens.Count && !IsFlag(tokens[i + 1]))
        {
            value = tokens[i + 1].Trim('"');
            i += 2; // consume flag + value
            return true;
        }
        value = null!;
        i++; // Consume just the flag; caller will treat as "missing value".
        return false;
    }
    /// <summary>
    /// Normalizes a single format alias (“markdown” → “md”) and lower-cases it.
    /// </summary>
    private static string NormalizeFormatAlias(string f) => string.Equals(f, "markdown", StringComparison.OrdinalIgnoreCase) ? "md" : f.ToLowerInvariant();


    /// <summary>
    /// Splits and normalizes a list of formats (comma/semicolon separated), de-duplicated case-insensitively.
    /// </summary>
    private static List<string> NormalizeFormats(string s)=> [.. NormalizeList(s).Select(x => NormalizeFormatAlias(x)).Distinct(StringComparer.OrdinalIgnoreCase)];


    /// <summary>
    /// Splits a list value on "," and ";" while trimming entries and removing empty ones.
    /// Returns an empty list if <paramref name="s"/> is null or whitespace.
    /// </summary>
    private static List<string> NormalizeList(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return [];
        return [.. s.Split([';', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
    }


    /// <summary>
    /// Enumerates the items in a list value (comma/semicolon separated). Yields nothing when <paramref name="s"/> is blank.
    /// </summary>
    private static IEnumerable<string> SplitList(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) yield break;
        foreach (var part in s.Split([';', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            yield return part;
    }

    /// <summary>
    /// Converts a path to its absolute/canonical form if non-empty; returns the original when blank.
    /// </summary>
    private static string NormalizePath(string p)
    {
        if (string.IsNullOrWhiteSpace(p)) return p;
        return Path.GetFullPath(p);
    }


    /// <summary>
    /// Computes a reasonable default source root:
    /// - In DEBUG builds, walk up from /bin/Debug/... to approximate the repo root.
    /// - In RELEASE builds, use the current working directory.
    /// </summary>
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


    /// <summary>
    /// Parses booleans in a forgiving way:
    /// - Blank/null → true (presence-style flags like "--show" treated as true)
    /// - "1", "true", "yes" (case-insensitive) → true
    /// - Everything else → false
    /// </summary>
    private static bool ParseBool(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return true;
        return s.Equals("1") || s.Equals("true", StringComparison.OrdinalIgnoreCase) || s.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Applies a boolean flag to the correct field, including the special semantics of <c>--private</c>.
    /// Unknown keys are ignored by this helper (validation happens earlier).
    /// </summary>
    private static void ApplyBooleanFlag(string key, bool value, ref bool showOnly, ref bool buildIndex, ref bool buildTree, ref bool help, ref bool info, ref bool includeNonPublic)
    {
        switch (key)
        {
            case "--show": showOnly = value; break;
            case "--index": buildIndex = value; break;
            case "--tree": buildTree = value; break;
            case "--help": help = value; break;
            case "--info": info = value; break;
            case "--private": if (value) includeNonPublic = false; break;   // "--private" flips IncludeNonPublic off when true.
        }
    }

    /// <summary>
    /// Backward-compatible API used by older code paths and unit tests.
    /// Prefer <see cref="TryParseOptions(string[], out CliOptions, out string)"/>
    /// Returns a tuple with resolved root, a single effective out-path, the selected (single) format,
    /// whether non-public members should be included, and the exclude set.
    /// </summary>
    internal static (string root, string outPath, string format, bool includeNonPublic, HashSet<string> excludedParts) AnalyzeArgs(string[] args_)
    {
        // Try the new parser first; if it fails, fall back to a minimal default, also logging the parse error for visibility.
        if (!TryParseOptions(args_, out var o, out string parseError))
        {
            // Fallback: alter Default-Pfad mit 'docs/api'
            var defRoot = GetDefaultRoot();
            xyLog.Log(parseError);
            return (defRoot, Path.Combine(defRoot, "docs"), "md", true, CliOptions.DefaultExcludes());
        }

        // Legacy behavior wants a single output directory:
        // If a specific format was selected and mapped, use that mapping. Otherwise, fall back to "<OutPath>/<firstSubfolder-or-format>".
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
            var sub = o.Subfolders?.FirstOrDefault() ?? o.Format;
            legacyOut = Path.Combine(o.OutPath ?? GetDefaultRoot(), sub);
        }

        return (o.RootPath, legacyOut, selectedFormat, o.IncludeNonPublic, o.ExcludedParts);
    }
}

