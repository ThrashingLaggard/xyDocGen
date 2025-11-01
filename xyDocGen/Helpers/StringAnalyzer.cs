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

