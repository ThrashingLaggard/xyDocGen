using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using xyDocumentor.Docs;
using xyDocumentor.Helpers;
using xyDocumentor.Models;
using xyDocumentor.Renderer;

namespace xyDocumentor.CLI
{
    /// <summary>
    /// Provides centralized runtime utilities for the CLI execution layer of <c>xyDocumentor</c>.
    /// <para>
    /// The <see cref="CliRuntimeHelper"/> class encapsulates reusable logic that is needed
    /// across multiple CLI commands or startup phases. Its goal is to keep
    /// <see cref="Program"/> concise and delegate operational details here.
    /// </para>
    /// <para>
    /// Responsibilities include:
    /// <list type="bullet">
    ///   <item><description>Resolving or creating per-format output directories.</description></item>
    ///   <item><description>Locating project-level metadata such as README files.</description></item>
    ///   <item><description>Rendering indexes and trees either to disk or console.</description></item>
    ///   <item><description>Caching the dominant root namespace before rendering begins.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    internal static class CliRuntimeHelper
    {
        /// <summary>
        /// Resolves the absolute output directory for a specific documentation format.
        /// <para>
        /// This method first checks whether a manual mapping exists in
        /// <see cref="CliOptions.OutputDirs"/> (for example, when the user explicitly
        /// configured format-specific output locations via CLI or configuration file).
        /// If no mapping is found, it falls back to <see cref="FormatDir(string, string)"/>.
        /// </para>
        /// </summary>
        /// <param name="opt">The current set of parsed CLI options.</param>
        /// <param name="fmt">The desired output format (e.g. "md", "html", "pdf").</param>
        /// <returns>
        /// The absolute directory path that should contain the output for the given format.
        /// </returns>
        public static string ResolveFormatDir(CliOptions opt, string fmt)
        {
            // Check if a mapping exists in the user's OutputDirs dictionary.
            // Only use it if it has a non-empty, valid path string.
            if (opt.OutputDirs != null &&
                opt.OutputDirs.TryGetValue(fmt, out var mapped) &&
                !string.IsNullOrWhiteSpace(mapped))
                return mapped;

            // Otherwise, build a standard folder path from the base output path.
            return FormatDir(opt.OutPath, fmt);
        }

        /// <summary>
        /// Constructs (and ensures existence of) a directory for a specific output format.
        /// <para>
        /// If <paramref name="baseOutPath"/> is null or empty, the current working directory
        /// (<see cref="Environment.CurrentDirectory"/>) is used as fallback.
        /// </para>
        /// </summary>
        /// <param name="baseOutPath">The base output directory (e.g. from CLI option <c>--out</c>).</param>
        /// <param name="fmt">The output format (lower-cased folder name).</param>
        /// <returns>
        /// The full, absolute path to the directory for this format. The directory
        /// is created if it does not yet exist.
        /// </returns>
        public static string FormatDir(string baseOutPath, string fmt)
        {
            // Default to the current directory if the base output path is not provided.
            baseOutPath ??= Environment.CurrentDirectory;

            // Normalize the format string (trim and lowercase) for consistent folder naming.
            fmt = fmt?.Trim().ToLowerInvariant() ?? "unknown";

            // Combine base directory and format name.
            var combined = Path.Combine(baseOutPath, fmt);

            // Ensure the directory physically exists on disk before returning.
            Directory.CreateDirectory(combined);

            // Return the fully qualified absolute path.
            return Path.GetFullPath(combined);
        }

        /// <summary>
        /// Displays runtime configuration and the contents of <c>README.md</c> (if available)
        /// in the console. This is primarily used in response to the <c>--info</c> flag.
        /// </summary>
        /// <param name="opt">The resolved CLI options for the current execution.</param>
        public static void ShowRuntimeInfo(CliOptions opt)
        {
            // Print high-level configuration for visibility and debugging.
            Console.WriteLine("xyDocGen – current configuration:");
            Console.WriteLine($"  Root: {opt.RootPath}");
            Console.WriteLine($"  Out : {opt.OutPath}");
            Console.WriteLine($"  Formats: {string.Join(", ", opt.Formats)}");
            Console.WriteLine($"  Subfolders: {string.Join(", ", opt.Subfolders)}");

            // Attempt to locate a README.md near the root directory.
            var readmePath = FindReadme(opt.RootPath);

            if (readmePath is not null && File.Exists(readmePath))
            {
                // If found, print its contents directly.
                Console.WriteLine("\n--- README.md ---\n");
                Console.WriteLine(File.ReadAllText(readmePath));
            }
            else
            {
                // Fallback notice when no README file is found.
                Console.WriteLine("README.md not found near root.");
            }
        }

        /// <summary>
        /// Attempts to locate a <c>README.md</c> file near the given root directory.
        /// <para>
        /// It checks both:
        /// <list type="number">
        ///   <item>The specified <paramref name="root"/> path.</item>
        ///   <item>The current working directory.</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="root">The starting directory to search in.</param>
        /// <returns>
        /// The absolute path to a found README file, or <see langword="null"/> if none exists.
        /// </returns>
        internal static string FindReadme(string root)
        {
            // Candidate path 1: directly under the given root directory.
            var candidate1 = Path.Combine(root ?? Environment.CurrentDirectory, "README.md");

            // Candidate path 2: directly under the current working directory.
            var candidate2 = Path.Combine(Environment.CurrentDirectory, "README.md");

            // Return whichever exists first, or null if neither is found.
            if (File.Exists(candidate1)) return candidate1;
            if (File.Exists(candidate2)) return candidate2;
            return null;
        }

        /// <summary>
        /// Renders the project index and type tree either to disk or to the console,
        /// depending on the CLI options provided.
        /// <para>
        /// This method consolidates all rendering logic for multiple output formats
        /// (e.g. Markdown, HTML, PDF) and ensures consistent behavior across all modes.
        /// </para>
        /// </summary>
        /// <param name="opt">The parsed CLI options that define output behavior.</param>
        /// <param name="flattened">A flattened enumeration of all <see cref="TypeDoc"/> instances.</param>
        /// <returns>
        /// A task representing the asynchronous rendering operation.
        /// </returns>
        public static async Task RenderIndexAndTree(CliOptions opt, System.Collections.Generic.IEnumerable<TypeDoc> flattened)
        {
            // Ensure that the dominant root namespace is computed once before writing.
            EnsureDominantRootCached(flattened);

            // Determine whether to persist outputs to disk or display in console.
            bool writeToDisk = !opt.ShowOnly && !(opt.ShowIndexToConsole || opt.ShowTreeToConsole);

            // Iterate through all requested output formats (normalized to lowercase).
            foreach (var fmt in opt.Formats.Select(f => f.ToLowerInvariant()))
            {
                // Resolve or create the per-format output directory.
                var formatDir = ResolveFormatDir(opt, fmt);
                if (writeToDisk) Directory.CreateDirectory(formatDir);

                // --- Build and optionally write the project index ----------------------------
                if (opt.BuildIndex || opt.ShowIndexToConsole)
                {
                    var index = await FileTreeRenderer.BuildProjectIndex(flattened, fmt, formatDir, writeToDisk);

                    // Print to console if not writing to disk.
                    if (!writeToDisk) Console.WriteLine(index.ToString());
                }

                // --- Build and optionally write the project tree -----------------------------
                if (opt.BuildTree || opt.ShowTreeToConsole)
                {
                    var tree = await FileTreeRenderer.BuildProjectTree(
                        new StringBuilder(), fmt, opt.RootPath, formatDir, opt.ExcludedParts, writeToDisk);

                    // Print to console if not writing to disk.
                    if (!writeToDisk) Console.WriteLine(tree.ToString());
                }
            }
        }

        /// <summary>
        /// Ensures that the dominant root namespace of the project is detected and cached
        /// exactly once per execution.  
        /// <para>
        /// This cache allows consistent relative path generation for subsequent rendering
        /// and output operations (especially for large projects with nested namespaces).
        /// </para>
        /// <para>
        /// The logic is delegated to <see cref="Utils.GetDominantRoot"/> and
        /// <see cref="Utils.PrimeDominantRoot"/>.
        /// </para>
        /// </summary>
        /// <param name="allTypes">
        /// The complete set of parsed <see cref="TypeDoc"/> elements used for analysis.
        /// </param>
        public static void EnsureDominantRootCached(System.Collections.Generic.IEnumerable<TypeDoc> allTypes)
        {
            // Trigger detection of the dominant namespace root.
            var root = Utils.GetDominantRoot(allTypes);

            // Store the result in the shared Utils cache for downstream access.
            Utils.PrimeDominantRoot(root);
        }
    }
}
