using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace xyDocumentor.Helpers
{
    /// <summary>
    /// Provides normalization utilities for strings, lists, and filesystem paths
    /// used throughout the <c>xyDocumentor</c> toolchain.
    /// <para>
    /// The <see cref="Normalizer"/> class is designed to sanitize and standardize
    /// user-provided or CLI-derived input, ensuring consistent downstream behavior.
    /// </para>
    /// <para>
    /// Common responsibilities include:
    /// <list type="bullet">
    ///   <item><description>Mapping format aliases (e.g. <c>"markdown"</c> → <c>"md"</c>).</description></item>
    ///   <item><description>Case normalization (lower-casing format and folder names).</description></item>
    ///   <item><description>Cleaning comma/semicolon-separated lists of formats or folders.</description></item>
    ///   <item><description>Resolving paths to absolute, canonical forms.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    internal class Normalizer
    {
        /// <summary>
        /// Normalizes a single format alias and lowercases it for consistent comparison.
        /// <para>
        /// Maps known human-friendly names (for example, "markdown") to their canonical
        /// short codes (for example, "md"), and ensures uniform lowercase output.
        /// </para>
        /// </summary>
        /// <param name="f">
        /// The raw format string provided by the user (e.g. <c>"Markdown"</c>, <c>"PDF"</c>, etc.).
        /// </param>
        /// <returns>
        /// The normalized, lowercase format identifier (for example, <c>"md"</c>, <c>"pdf"</c>).
        /// </returns>
        internal static string NormalizeFormatAlias(string f) =>string.Equals(f, "markdown", StringComparison.OrdinalIgnoreCase)? "md": f.ToLowerInvariant();

        /// <summary>
        /// Splits a delimited string of format names and returns a normalized, de-duplicated list.
        /// <para>
        /// Accepts comma (<c>,</c>) and semicolon (<c>;</c>) separators, trims whitespace,
        /// and removes empty entries. Each entry is passed through
        /// <see cref="NormalizeFormatAlias(string)"/> and the resulting collection is
        /// de-duplicated using case-insensitive comparison.
        /// </para>
        /// </summary>
        /// <param name="s">
        /// A delimited string of format names, such as <c>"markdown;pdf;HTML"</c>.
        /// </param>
        /// <returns>
        /// A list of unique, normalized format identifiers, e.g. <c>["md", "pdf", "html"]</c>.
        /// </returns>
        internal static List<string> NormalizeFormats(string s) =>[.. NormalizeList(s).Select(x => NormalizeFormatAlias(x)).Distinct(StringComparer.OrdinalIgnoreCase)];

        /// <summary>
        /// Splits an input string into a list of trimmed entries using <c>,</c> and <c>;</c> as separators.
        /// <para>
        /// Removes empty or whitespace-only items. If the input is null or whitespace,
        /// returns an empty list instead of <see langword="null"/>.
        /// </para>
        /// </summary>
        /// <param name="s">
        /// The delimited input string, e.g. <c>"pdf, html; md"</c>.
        /// </param>
        /// <returns>
        /// A list of non-empty, trimmed strings, e.g. <c>["pdf", "html", "md"]</c>.
        /// </returns>
        internal static List<string> NormalizeList(string s)
        {
            // Guard clause: return an empty list if the input is null or all whitespace.
            if (string.IsNullOrWhiteSpace(s)) return [];

            // Split on both semicolons and commas, remove empty results, and trim whitespace.
            return [.. s.Split([';', ','],
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
        }

        /// <summary>
        /// Converts a file system path to its absolute, canonical form.
        /// <para>
        /// Returns the original input when blank or whitespace-only.
        /// This method ensures that relative paths are consistently resolved
        /// before being used to read or write files.
        /// </para>
        /// </summary>
        /// <param name="p">The input path (absolute or relative).</param>
        /// <returns>
        /// The absolute, normalized path string. Returns the original value if empty.
        /// </returns>
        internal static string NormalizePath(string p)
        {
            // Guard clause: no processing for empty or whitespace inputs.
            if (string.IsNullOrWhiteSpace(p)) return p;

            // Convert the path to an absolute, fully qualified form using the OS path resolver.
            return Path.GetFullPath(p);
        }
    }
}
