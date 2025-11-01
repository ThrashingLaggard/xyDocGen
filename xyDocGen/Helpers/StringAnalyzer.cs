namespace xyDocumentor.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Text;
using xyDocumentor.CLI;
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

  




    /// <summary>
    /// Backward-compatible API used by older code paths and unit tests.
    /// Prefer <see cref="TryParseOptions(string[], out CliOptions, out string)"/>
    /// Returns a tuple with resolved root, a single effective out-path, the selected (single) format,
    /// whether non-public members should be included, and the exclude set.
    /// </summary>
    internal static (string root, string outPath, string format, bool includeNonPublic, HashSet<string> excludedParts) AnalyzeArgs(string[] args_)
    {
        // Try the new parser first; if it fails, fall back to a minimal default, also logging the parse error for visibility.
        if (!OptionsParser.TryParseOptions(args_, out var o, out string parseError))
        {
            // Fallback: alter Default-Pfad mit 'docs/api'
            var defRoot = Utils.GetDefaultRoot();
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
            legacyOut = Path.Combine(o.OutPath ?? Utils.GetDefaultRoot(), sub);
        }

        return (o.RootPath, legacyOut, selectedFormat, o.IncludeNonPublic, o.ExcludedParts);
    }
}

