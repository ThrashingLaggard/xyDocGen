using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using xyDocumentor.Docs;
using xyDocumentor.Extractors;
using xyDocumentor.Renderer;
using xyToolz.Filesystem;
using xyToolz.Helper.Logging;
using xyToolz.Logging.Helper;

namespace xyDocumentor.Helpers
{
#nullable enable
    /// <summary>
    /// Collection of general-purpose helper routines used across the xyDocumentor pipeline.
    /// <para>
    /// Responsibilities include:
    /// <list type="bullet">
    ///   <item><description>Path and environment discovery for source roots.</description></item>
    ///   <item><description>Roslyn syntax helpers (attribute flattening, public-like checks).</description></item>
    ///   <item><description>Documentation string cleanup (XML/HTML removal and decoding).</description></item>
    ///   <item><description>Transformation of Roslyn members to <see cref="MemberDoc"/>.</description></item>
    ///   <item><description>Dominant root namespace detection and caching.</description></item>
    ///   <item><description>Rendering/writing type data to various output formats.</description></item>
    ///   <item><description>Filesystem utilities (unique path generation, exclusion checks).</description></item>
    /// </list>
    /// </para>
    /// The type is declared <c>partial</c> to allow feature-specific helpers to be split
    /// into additional files without bloating a single unit.
    /// </summary>
    internal static partial class Utils
    {
        /// <summary>
        /// Optional human-readable description injected by callers for diagnostics/tracing.
        /// Not used in control flow; safe to be null.
        /// </summary>
        public static string? Description { get; set; }

        /// <summary>
        /// Computes a reasonable default source root.
        /// <para>
        /// Behavior differs by build configuration:
        /// <list type="bullet">
        ///   <item><description><b>DEBUG</b>: Walks up from <c>/bin/Debug/…</c> to approximate the repository root.</description></item>
        ///   <item><description><b>RELEASE</b>: Uses <see cref="Environment.CurrentDirectory"/>.</description></item>
        /// </list>
        /// </para>
        /// </summary>
        internal static string GetDefaultRoot()
        {
#if DEBUG
            // Capture the current working directory (usually /bin/Debug/… for a dev run).
            var cwd = Directory.GetCurrentDirectory();

            // Step up parent directories to approximate the repository root.
            // The triple-parent is a heuristic that often lands at the project root.
            var d = Directory.GetParent(cwd);
            if (d?.Parent?.Parent != null)
                return d.Parent.Parent.FullName;

            // Fallback: return the current working directory if traversal failed.
            return cwd;
#else
            // In release builds, prefer the user's current directory as default root.
            return Environment.CurrentDirectory;
#endif
        }

        // --------------------------------------------------------------------
        // Dominant root namespace cache (computed once per process execution)
        // --------------------------------------------------------------------

        /// <summary>
        /// Holds the dominant root namespace once computed. Empty string means
        /// "no dominant root could be determined"; <see langword="null"/> means "not computed yet".
        /// </summary>
        private static string? _cachedDominantRoot = null;

        /// <summary>
        /// Flattens all attributes on a member into a simple list of attribute names.
        /// <para>
        /// This variant expands loops explicitly (two foreach loops) to emphasize
        /// clarity over LINQ compactness. It returns the textual representation of
        /// each attribute's <see cref="NameSyntax"/>.
        /// </para>
        /// </summary>
        /// <param name="listedAttributesFromMember_">Roslyn attribute lists attached to a declaration.</param>
        /// <returns>List of attribute name strings (order preserved as encountered).</returns>
        public static List<string> FlattenAttributes(SyntaxList<AttributeListSyntax> listedAttributesFromMember_)
        {
            // Allocate the result container once to avoid repeated list growth reallocation.
            List<string> listedResults = [];

            // Iterate through each attribute list (e.g., [Obsolete], [Serializable, CLSCompliant]).
            foreach (AttributeListSyntax als_ListOfAttributes in listedAttributesFromMember_)
            {
                // For each individual attribute in the list…
                foreach (AttributeSyntax as_Attribute in als_ListOfAttributes.Attributes)
                {
                    // Extract its syntactic name node, e.g., "Serializable".
                    NameSyntax ns_AttributeName = as_Attribute.Name;

                    // Convert the name to its textual form and append it to the results.
                    string attributeString = ns_AttributeName.ToString();
                    listedResults.Add(attributeString);
                }
            }

            // Return the flattened collection of attribute names.
            return listedResults;
        }









        // Precompiled regexes to strip doc comment prefixes.
        // Matches: optional leading whitespace + "///" + optional single space, at line start (multiline).
        private static readonly Regex _docLinePrefixRegex =new(@"^\s*///\s?", RegexOptions.Multiline | RegexOptions.Compiled);

        // Matches a leading "*" in block-style doc comments when lines were split out of "/** ... */".
        // Example line: " * Some text" → "Some text"
        private static readonly Regex _blockAsteriskPrefixRegex =new(@"^\s*\*\s?", RegexOptions.Multiline | RegexOptions.Compiled);


        /// <summary>
        /// Cleans a raw XML documentation string into plain text suitable for rendering.
        /// <para>
        /// Steps:
        /// <list type="number">
        ///   <item>Replace <c>&lt;para&gt;</c> blocks with line breaks.</item>
        ///   <item>Strip any remaining XML/HTML tags via <see cref="Extractor.TagRemovalRegex"/>.</item>
        ///   <item>Decode HTML entities using <see cref="WebUtility.HtmlDecode(string)"/>.</item>
        ///   <item>Trim leading/trailing whitespace.</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="rawXmlString_">The raw XML doc comment content.</param>
        /// <returns>Cleaned and human-readable text.</returns>
        public static string CleanDoc(string rawXmlString_)
        {
            // 0) Strip C# doc comment markers if raw trivia was passed (e.g., lines starting with "///" or "*")
            //    - Handles both line-docs (///) and block-docs (/** ... * ... */).
            //    - Multiline to catch each line; Compiled for performance (called often).
            string cleanedResult = _docLinePrefixRegex.Replace(rawXmlString_, string.Empty);
            cleanedResult = _blockAsteriskPrefixRegex.Replace(cleanedResult, string.Empty);
            
            // 1) Replace paragraph tags with line breaks to preserve intended paragraphing.
            cleanedResult = cleanedResult.Replace("<para>", "\n").Replace("</para>", "\n");

            // 2) Remove any remaining XML/HTML tags using the precompiled extractor regex.
            cleanedResult = Extractor.TagRemovalRegex.Replace(cleanedResult, string.Empty);

            // 3) Decode HTML entities (e.g., &lt;, &gt;, &amp;) to their character equivalents.
            cleanedResult = WebUtility.HtmlDecode(cleanedResult);

            // 4) Normalize surrounding whitespace.
            cleanedResult = cleanedResult.Trim();

            // Return the sanitized documentation text.
            return cleanedResult;
        }

        /// <summary>
        /// Determines whether the given modifiers indicate a "public-like" accessibility.
        /// <para>
        /// Treats both <c>public</c> and <c>protected</c> as public-facing for documentation
        /// purposes (i.e., they should be included when <c>IncludeNonPublic</c> is false).
        /// </para>
        /// </summary>
        /// <param name="listedModifiers_">The Roslyn modifier token list of a declaration.</param>
        /// <returns><see langword="true"/> if public or protected; otherwise <see langword="false"/>.</returns>
        public static bool HasPublicLike(SyntaxTokenList listedModifiers_) =>(listedModifiers_.Any(m => m.IsKind(SyntaxKind.PublicKeyword) || m.IsKind(SyntaxKind.ProtectedKeyword)));

        /// <summary>
        /// Creates a <see cref="MemberDoc"/> from a Roslyn <see cref="MemberDeclarationSyntax"/>.
        /// <para>
        /// This performs a one-stop extraction of common metadata:
        /// kind, signature, summaries (XML), modifiers, attributes, returns, parameters,
        /// generic constraints, and type parameter summaries.
        /// </para>
        /// </summary>
        /// <param name="mds_Member_">The Roslyn member declaration node.</param>
        /// <returns>A populated <see cref="MemberDoc"/> describing the member.</returns>
        public static MemberDoc CreateMemberDoc(MemberDeclarationSyntax mds_Member_)
        {
            // Collect textual modifiers (e.g., "public static").
            string modifiers = string.Join(" ", mds_Member_.Modifiers.Select(m => m.Text));

            // Instantiate and populate the MemberDoc with extracted metadata.
            MemberDoc md_Member = new()
            {
                // Reduce Roslyn kind (e.g., MethodDeclaration) to a user-facing kind (e.g., "method").
                Kind = mds_Member_.Kind().ToString().Replace("Declaration", "").ToLower(),

                // Extract a readable signature (includes name, parameters, type parameters).
                Signature = Extractor.ExtractSignature(mds_Member_),

                // Pull XML summary text if present (cleaning is handled within extractor).
                Summary = Extractor.ExtractXmlSummaryFromSyntaxNode(mds_Member_),

                // Raw modifiers string as seen in source.
                Modifiers = modifiers,

                // Flatten attribute lists to a name-only representation for rendering.
                Attributes = [.. Utils.FlattenAttributes(mds_Member_.AttributeLists)],

                // Additional remarks (XML <remarks>) if available.
                Remarks = Extractor.ExtractXmlRemarksFromSyntaxNode(mds_Member_),

                // Extract XML <returns> summary (may be empty for void members).
                ReturnSummary = Extractor.ExtractXmlReturnSummary(mds_Member_),

                // Return type as textual representation (e.g., "Task<string>").
                ReturnType = Extractor.ExtractReturnType(mds_Member_),

                // XML <param> name→summary mapping, projected to the public model.
                Parameters = (IList<ParameterDoc>)Extractor.ExtractXmlParamSummaries(mds_Member_).ToList(),

                // Generic where-constraints (if any).
                GenericConstraints = Extractor.ExtractGenericConstraints(mds_Member_),

                // XML <typeparam> summaries (if any).
                TypeParameterSummaries = Extractor.ExtractXmlTypeParameterSummaries(mds_Member_)
            };

            // Return the fully populated descriptor.
            return md_Member;
        }

        /// <summary>
        /// Stores the detected dominant root namespace in the local cache if not already set.
        /// <para>
        /// Safe no-op when the cache is already populated (non-empty string).
        /// </para>
        /// </summary>
        /// <param name="root">The dominant root namespace (or <see langword="null"/>).</param>
        internal static void PrimeDominantRoot(string? root)
        {
            // Avoid overwriting once a value has been cached (including empty string).
            if (!string.IsNullOrWhiteSpace(_cachedDominantRoot)) return;

            // Set to provided value or empty string to signal "computed but none".
            _cachedDominantRoot = root ?? string.Empty;
        }

        /// <summary>
        /// Detects the dominant root namespace across all types (e.g., "xyDocumentor") once per run.
        /// <para>
        /// The algorithm:
        /// <list type="number">
        ///   <item>Take each non-empty namespace.</item>
        ///   <item>Split on '.' and take the first segment (the root).</item>
        ///   <item>Group and count occurrences per root.</item>
        ///   <item>Pick the most frequent root; if none, cache empty string.</item>
        /// </list>
        /// Subsequent calls reuse the cached value.
        /// </para>
        /// </summary>
        /// <param name="types">All parsed <see cref="TypeDoc"/> instances.</param>
        /// <returns>The dominant root namespace, or empty string if not determinable.</returns>
        internal static string GetDominantRoot(IEnumerable<TypeDoc> types)
        {
            // Return cached value if already computed (empty string means "no dominant root found").
            if (!string.IsNullOrEmpty(_cachedDominantRoot))
                return _cachedDominantRoot!;

            // Compute the most common first segment of the namespace across all types.
            string? root = types
                .Select(t => t.Namespace)
                .Where(ns => !string.IsNullOrWhiteSpace(ns))
                .Select(ns => ns!.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
                .Where(first => !string.IsNullOrEmpty(first))
                .GroupBy(first => first)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key;

            // Cache the result (or empty string if none) and return it.
            _cachedDominantRoot = root ?? string.Empty;
            return _cachedDominantRoot!;
        }

        /// <summary>
        /// Writes each <see cref="TypeDoc"/> to a file, grouped by namespace, in the requested format.
        /// <para>
        /// Supported formats: <c>json</c>, <c>html</c>, <c>pdf</c>, and markdown (default).
        /// The file output path is derived from the type's namespace and display name,
        /// sanitized for filesystem compatibility and uniqueness.
        /// </para>
        /// </summary>
        /// <param name="listedAllTypes_">All types to render.</param>
        /// <param name="outPath_">Base output directory for the current format.</param>
        /// <param name="format_">Format selector ("json" | "html" | "pdf" | "md").</param>
        /// <returns>
        /// <see langword="true"/> if all files were written successfully; otherwise <see langword="false"/>.
        /// </returns>
        public static async Task<bool> WriteDataToFilesOrderedByNamespace(IEnumerable<TypeDoc> listedAllTypes_, string outPath_, string format_)
        {
            // Scratch variables reused per iteration.
            string content;
            bool isWrittenCurrent;

            // Assume overall success; flip to false if any single file fails.
            bool isWrittenAll = true;

            // Normalize the format string once for comparison.
            string lowerFormat = format_.ToLowerInvariant();

            // Determine the dominant root to optionally strip from the first namespace segment.
            var dominantRoot = GetDominantRoot(listedAllTypes_);

            // Iterate all types to render and persist them one by one.
            foreach (TypeDoc td_TypeInList in listedAllTypes_)
            {
                // Normalize namespace: replace generic angle brackets that would break file paths.
                var ns = td_TypeInList.Namespace ?? string.Empty;
                ns = ns.Replace('<', '_').Replace('>', '_');

                // Split namespace into segments (e.g., "X.Y.Z" → ["X","Y","Z"]).
                var parts = ns.Split('.', StringSplitOptions.RemoveEmptyEntries);

                // If a dominant root exists and matches the first segment, drop it (for cleaner folders).
                if (!string.IsNullOrEmpty(dominantRoot) &&
                    parts.Length > 1 &&
                    string.Equals(parts[0], dominantRoot, StringComparison.Ordinal))
                {
                    parts = [.. parts.Skip(1)];
                }

                // Build a relative path derived from the (possibly shortened) namespace.
                // Use "_" if the namespace is empty to avoid writing directly into base folder.
                var relNsPath = parts.Length > 0 ? Path.Combine(parts) : "_";

                // Create the namespace directory under the format output root.
                var namespaceFolder = Path.Combine(outPath_, relNsPath);
                Directory.CreateDirectory(namespaceFolder);

                // Sanitize the display name for file system usage (spaces/brackets → underscores).
                string cleanedDisplayName = td_TypeInList.DisplayName
                    .Replace(' ', '_')
                    .Replace('<', '_')
                    .Replace('>', '_');

                // Combine directory and base filename (without extension yet).
                string fileName = Path.Combine(namespaceFolder, cleanedDisplayName);

                // Render content and persist according to the selected format.
                switch (lowerFormat)
                {
                    case "json":
                        // Ensure unique final path with extension to avoid overwriting existing files.
                        var filePath = EnsureUniquePath(fileName, ".json");

                        // Render JSON representation of the type.
                        content = JsonRenderer.Render(td_TypeInList);

                        // Save to disk using the filesystem helper; capture success flag.
                        isWrittenCurrent = await xyFiles.SaveToFile(content, filePath);
                        break;

                    case "html":
                        filePath = EnsureUniquePath(fileName, ".html");
                        content = HtmlRenderer.Render(td_TypeInList, cssPath: null);
                        isWrittenCurrent = await xyFiles.SaveToFile(content, filePath);
                        break;

                    case "pdf":
                        filePath = EnsureUniquePath(fileName, ".pdf");

                        // PDF renderer writes directly to the file path (no string content to save).
                        PdfRenderer.RenderToFile(td_TypeInList, filePath);
                        isWrittenCurrent = true; // assume success; renderer throws on failure
                        break;

                    default: // Markdown (fallback)
                        filePath = EnsureUniquePath(fileName, ".md");
                        content = MarkdownRenderer.Render(td_TypeInList);
                        isWrittenCurrent = await xyFiles.SaveToFile(content, filePath);
                        break;
                }

                // If a write failed, record overall failure and emit a diagnostic.
                if (isWrittenCurrent is false)
                {
                    isWrittenAll = isWrittenCurrent;
                    xyLog.Log($"Wrote the current type {isWrittenCurrent}");
                }
            }

            // Return aggregate success/failure across all attempts.
            return isWrittenAll;
        }

        /// <summary>
        /// Ensures the file path is unique by appending a suffix (e.g., <c>~1</c>, <c>~2</c>) if needed.
        /// <para>
        /// The check is performed against the file system (existing files). If a collision
        /// is found, the next numeric suffix is tried until a free name is discovered.
        /// </para>
        /// </summary>
        /// <param name="baseWithoutExt">Base path without extension.</param>
        /// <param name="ext">File extension including the dot (e.g., <c>".md"</c>).</param>
        /// <returns>A unique, non-existing path with the requested extension.</returns>
        private static string EnsureUniquePath(string baseWithoutExt, string ext)
        {
            // Start with the direct path (no suffix).
            var path = baseWithoutExt + ext;

            // Increment a suffix counter while a file exists at the candidate path.
            int i = 1;
            while (File.Exists(path))
                path = $"{baseWithoutExt}~{i++}{ext}";

            // Return the first available path.
            return path;
        }

        /// <summary>
        /// Determines whether a given path contains any excluded directory segment.
        /// <para>
        /// Splits the path on both platform directory separators and checks each segment
        /// against the provided <paramref name="excludeParts"/> set.
        /// </para>
        /// </summary>
        /// <param name="path">A full or relative file path.</param>
        /// <param name="excludeParts">Set of directory names to exclude (case-sensitive by default).</param>
        /// <returns><see langword="true"/> if any segment is excluded; otherwise <see langword="false"/>.</returns>
        public static bool IsExcluded(string path, HashSet<string> excludeParts)
        {
            // Split using both the primary and alternate directory separators (cross-platform).
            var parts = path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);

            // Return true if any segment matches an excluded name.
            return parts.Any(p => excludeParts.Contains(p));
        }
    }
}
