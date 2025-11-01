using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using xyDocumentor.Helpers;
using xyDocumentor.Models;

namespace xyDocumentor.CLI
{
#nullable enable
    
    internal class OptionsParser
    {

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
        /// </summary>
        public static bool TryParseOptions(string[] args, out CliOptions opts, out string error)
        {
            // Default error placeholder; will be overridden with a specific message on actual error.
            error = "Invalid arguments. Use --help to see available options.";

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
                            listedSubfolders = Normalizer.NormalizeList(eqValue);
                            if (listedSubfolders.Count == 0)
                                listedSubfolders = [];
                            break;

                        case "--format":
                            {
                                // Accept aliases & duplicates; normalize to "md|html|pdf|json".
                                List<string> normalizedListedFormats = Normalizer.NormalizeFormats(eqValue);

                                // If user passed empty list (e.g., "--format="), default to md.
                                if (normalizedListedFormats.Count == 0) normalizedListedFormats = ["md"];

                                foreach (var f in normalizedListedFormats)
                                {
                                    string nf = Normalizer.NormalizeFormatAlias(f);
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
                            foreach (var part in Normalizer.NormalizeList(eqValue))
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
                        listedSubfolders = Normalizer.NormalizeList(rawSubs);
                        continue;

                    case "--format":
                        if (!TryReadNext(tokens, ref i, out var fmt))
                        { error = "Missing value after --format."; opts = null!; return false; }

                        // Same normalization logic as above, but for the space-separated form.
                        var fmtList = Normalizer.NormalizeFormats(fmt);
                        foreach (var f in fmtList)
                        {
                            var nf = Normalizer.NormalizeFormatAlias(f);
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
                        foreach (var part in Normalizer.NormalizeList(exStr))
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
                rootPath = Utils.GetDefaultRoot();

            // Decide base output directory:
            //   - If --out is specified, use it as-is.
            //   - Else derive from <root>/<folder>.
            var outBase = string.IsNullOrWhiteSpace(outPath)
                ? Path.Combine(rootPath, folder)
                : outPath;

            // Normalize both paths to absolute canonical forms.
            outBase = Normalizer.NormalizePath(outBase);
            rootPath = Normalizer.NormalizePath(rootPath);

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
                string f = listedFormats[idx].ToLowerInvariant();

                // If subfolders were provided, use them; else default folder name = format name.
                string folderName = (listedSubfolders.Count > 0) ? listedSubfolders[idx] : f;

                // Normalize the combined path.
                string full = Normalizer.NormalizePath(Path.Combine(outBase, folderName));
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

    }
}
