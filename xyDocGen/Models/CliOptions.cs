using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace xyDocumentor.Models
{
    /// <summary>
    /// Represents all command-line options accepted by the <c>xyDocumentor</c> tool
    /// in a strongly typed, immutable structure.
    /// <para>
    /// Instances of this class are typically created by parsing CLI arguments
    /// (for example, using System.CommandLine or manual parsing logic)
    /// and then passed into the main execution pipeline.
    /// </para>
    /// <para>
    /// Each option corresponds directly to a supported command-line flag or
    /// configuration argument. Default values are chosen to allow safe,
    /// minimal invocations without requiring explicit arguments.
    /// </para>
    /// </summary>
    public sealed class CliOptions
    {
        /// <summary>
        /// The root path of the source repository or solution from which
        /// documentation is generated.
        /// <para>
        /// Typically, this is the path provided via <c>--root</c> or defaults to
        /// the current working directory when omitted.
        /// </para>
        /// </summary>
        public string RootPath { get; init; } = "";

        /// <summary>
        /// The base output directory where generated documentation will be stored.
        /// <para>
        /// This corresponds to the <c>--out</c> command-line option and usually points
        /// to a folder such as <c>&lt;repo&gt;/docs</c>.
        /// </para>
        /// </summary>
        public string OutPath { get; set; }

        /// <summary>
        /// A collection of output formats that the tool should generate.
        /// <para>
        /// Example values: <c>["md", "html", "pdf"]</c>.  
        /// The list may be empty, in which case a default fallback (<c>md</c>) is used.
        /// </para>
        /// </summary>
        public List<string> Formats { get; set; } = [];

        /// <summary>
        /// A list of subfolder names corresponding to each format.
        /// <para>
        /// For example, <c>["md", "pdf", "json"]</c> would indicate that the tool
        /// should place the generated files for each format into these subfolders.
        /// </para>
        /// </summary>
        public List<string> Subfolders { get; set; } = []; // "md" | "html" | "pdf" | "json"

        /// <summary>
        /// A mapping of output format identifiers to their respective output directories.
        /// <para>
        /// Example:
        /// <code>
        /// {
        ///   "md": "C:\\Docs\\Markdown",
        ///   "pdf": "C:\\Docs\\PDF"
        /// }
        /// </code>
        /// The dictionary uses a case-insensitive key comparer to ensure consistent lookups.
        /// </para>
        /// </summary>
        public Dictionary<string, string> OutputDirs { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        // --------------------------------------------------------------------
        // Compatibility section (for older code paths)
        // --------------------------------------------------------------------

        /// <summary>
        /// Backward-compatible single-format accessor for older code paths that
        /// assume a single <c>Format</c> instead of the newer <see cref="Formats"/> list.
        /// <para>
        /// Returns the first entry from <see cref="Formats"/> if available,
        /// otherwise defaults to <c>"md"</c>.
        /// </para>
        /// </summary>
        public string Format
        {
            get => Formats.FirstOrDefault() ?? "md";
        }

        // --------------------------------------------------------------------
        // Inclusion / exclusion filters
        // --------------------------------------------------------------------

        /// <summary>
        /// Determines whether non-public (internal or private) types should be included
        /// in the documentation output.
        /// <para>
        /// This corresponds to an <c>--include-nonpublic</c> style flag.
        /// When <c>true</c> (default), all symbols are considered during extraction.
        /// </para>
        /// </summary>
        public bool IncludeNonPublic { get; init; } = true;

        /// <summary>
        /// A predefined set of directory or file names that should be excluded
        /// from traversal when scanning the source tree.
        /// <para>
        /// Common build and system directories (e.g. <c>bin</c>, <c>obj</c>,
        /// <c>.git</c>, etc.) are filtered out to reduce noise and speed up parsing.
        /// </para>
        /// </summary>
        public HashSet<string> ExcludedParts { get; init; } = DefaultExcludes();

        // --------------------------------------------------------------------
        // Feature flags: control which operations the CLI performs
        // --------------------------------------------------------------------

        /// <summary>
        /// When true, performs a dry-run that shows what would be built
        /// without actually writing output to disk.  
        /// Equivalent to the <c>--show</c> flag.
        /// </summary>
        public bool ShowOnly { get; init; } = false;

        /// <summary>
        /// Prints the index file contents (if built) directly to the console.
        /// Controlled by the <c>--show-index</c> flag.
        /// </summary>
        public bool ShowIndexToConsole { get; init; } = false;

        /// <summary>
        /// Prints the documentation tree structure to the console.
        /// Controlled by the <c>--show-tree</c> flag.
        /// </summary>
        public bool ShowTreeToConsole { get; init; } = false;

        /// <summary>
        /// Indicates whether the tool should build the global index of documented items.
        /// Controlled by the <c>--index</c> flag.
        /// </summary>
        public bool BuildIndex { get; init; } = true;

        /// <summary>
        /// Indicates whether the tool should build the hierarchical documentation tree.
        /// Controlled by the <c>--tree</c> flag.
        /// </summary>
        public bool BuildTree { get; init; } = true;

        // --------------------------------------------------------------------
        // Meta flags: high-level operational switches
        // --------------------------------------------------------------------

        /// <summary>
        /// Displays a help message and exits.  
        /// Triggered by the <c>--help</c> flag, typically handled early in <c>Program.cs</c>.
        /// </summary>
        public bool Help { get; init; } = false;

        /// <summary>
        /// Displays runtime information (such as version, environment, etc.) and exits.  
        /// Triggered by the <c>--info</c> flag.
        /// </summary>
        public bool Info { get; init; } = false;

        // --------------------------------------------------------------------
        // Helper method for initialization
        // --------------------------------------------------------------------

        /// <summary>
        /// Provides a default exclusion list used when scanning for source files.
        /// <para>
        /// This includes typical build artifacts and IDE metadata directories:
        /// <list type="bullet">
        ///   <item><description><c>.git</c></description></item>
        ///   <item><description><c>bin</c></description></item>
        ///   <item><description><c>obj</c></description></item>
        ///   <item><description><c>node_modules</c></description></item>
        ///   <item><description><c>.vs</c></description></item>
        ///   <item><description><c>TestResults</c></description></item>
        /// </list>
        /// </para>
        /// </summary>
        /// <returns>
        /// A case-insensitive <see cref="HashSet{T}"/> containing the default excluded paths.
        /// </returns>
        public static HashSet<string> DefaultExcludes() =>
            new([".git", "bin", "obj", "node_modules", ".vs", "TestResults"],
                StringComparer.OrdinalIgnoreCase);
    }
}
