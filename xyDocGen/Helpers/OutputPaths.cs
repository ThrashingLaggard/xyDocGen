namespace xyDocumentor.Helpers;

using System.IO;

internal static class OutputPaths
{
    public static string FormatDir(string outRoot, string format) =>Path.Combine(outRoot, format.ToLowerInvariant());

    // Index/Tree JE FORMAT innerhalb des jeweiligen Format-Ordners
    public static string IndexPath(string outRoot, string formatExt) => Path.Combine(FormatDir(outRoot, formatExt), $"index.{formatExt.ToLowerInvariant()}");

    public static string TreePath(string outRoot, string formatExt) => Path.Combine(FormatDir(outRoot, formatExt), $"tree.{formatExt.ToLowerInvariant()}");
}
namespace xyDocumentor.Helpers
{
    using System.IO;

    /// <summary>
    /// Provides helper methods for resolving standardized output paths for generated
    /// documentation artifacts within the <c>xyDocumentor</c> toolchain.
    /// <para>
    /// This static utility class encapsulates the directory and file-naming conventions
    /// used when writing generated files (per format) into structured subfolders.
    /// </para>
    /// <para>
    /// Example layout for an output root of <c>C:\Docs</c>:
    /// <code>
    /// C:\Docs\
    ///   ├── md\
    ///   │    ├── index.md
    ///   │    └── tree.md
    ///   ├── pdf\
    ///   │    ├── index.pdf
    ///   │    └── tree.pdf
    ///   └── html\
    ///        ├── index.html
    ///        └── tree.html
    /// </code>
    /// </para>
    /// </summary>
    internal static class OutputPaths
    {
        /// <summary>
        /// Builds an absolute path to a subfolder corresponding to a specific output format.
        /// <para>
        /// Example:
        /// <code>
        /// string path = OutputPaths.FormatDir("C:\\Docs", "pdf");
        /// // Result: "C:\\Docs\\pdf"
        /// </code>
        /// </para>
        /// The <paramref name="format"/> string is normalized to lowercase to ensure
        /// consistency across case-insensitive filesystems.
        /// </summary>
        /// <param name="outRoot">The root output directory (e.g. <c>--out</c> CLI argument).</param>
        /// <param name="format">The output format name (e.g. "md", "html", "pdf").</param>
        /// <returns>
        /// A combined, normalized directory path where files of the given format should be stored.
        /// </returns>
        public static string FormatDir(string outRoot, string format) =>Path.Combine(outRoot, format.ToLowerInvariant());

        /// <summary>
        /// Constructs the fully qualified file path to the generated "index" artifact for
        /// a specific output format.
        /// <para>
        /// The file is placed inside the format-specific subdirectory determined by
        /// <see cref="FormatDir(string, string)"/> and is named <c>index.&lt;format&gt;</c>.
        /// </para>
        /// Example:
        /// <code>
        /// string path = OutputPaths.IndexPath("C:\\Docs", "html");
        /// // Result: "C:\\Docs\\html\\index.html"
        /// </code>
        /// </summary>
        /// <param name="outRoot">The base output directory (shared among all formats).</param>
        /// <param name="formatExt">The file extension or format identifier (e.g. "md").</param>
        /// <returns>The absolute file path to the index file for the given format.</returns>
        public static string IndexPath(string outRoot, string formatExt) =>Path.Combine(FormatDir(outRoot, formatExt),$"index.{formatExt.ToLowerInvariant()}");

        /// <summary>
        /// Constructs the fully qualified file path to the generated "tree" artifact for
        /// a specific output format.
        /// <para>
        /// The resulting path follows the same structure as <see cref="IndexPath"/>,
        /// but the file name is <c>tree.&lt;format&gt;</c>.
        /// </para>
        /// Example:
        /// <code>
        /// string path = OutputPaths.TreePath("C:\\Docs", "pdf");
        /// // Result: "C:\\Docs\\pdf\\tree.pdf"
        /// </code>
        /// </summary>
        /// <param name="outRoot">The root output directory.</param>
        /// <param name="formatExt">The format or file extension (e.g. "pdf", "md").</param>
        /// <returns>The absolute file path to the tree file for the given format.</returns>
        public static string TreePath(string outRoot, string formatExt) =>Path.Combine(FormatDir(outRoot, formatExt),$"tree.{formatExt.ToLowerInvariant()}");
    }
}
