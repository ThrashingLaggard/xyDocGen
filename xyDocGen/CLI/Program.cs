namespace xyDocumentor.CLI
{
    using PdfSharpCore.Fonts;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using xyDocumentor.Docs;
    using xyDocumentor.Extractors;
    using xyDocumentor.Helpers;
    using xyDocumentor.Models;
    using xyDocumentor.Renderer;
    using xyToolz.Helper.Logging;


    /// <summary>
    /// Console application entrypoint and top-level coordinator for xyDocumentor.
    /// <para>
    /// This type wires together CLI parsing, extraction, rendering, and file output.
    /// It intentionally keeps orchestration logic in one place and delegates the heavy
    /// lifting to specialized helpers (extractors, renderers, utilities).
    /// </para>
    /// <para>
    /// The class is <c>partial</c> to allow splitting platform-specific or testing helpers
    /// into separate files without polluting the primary control flow.
    /// </para>
    /// </summary>
    public partial class Program
    {

        /// /// <summary>
        /// Synchronous program entry point required by .NET host.
        /// <para>
        /// This method performs minimal bootstrap:
        /// <list type="number">
        ///   <item>Registers a <see cref="IFontResolver"/> for PdfSharpCore.</item>
        ///   <item>Optionally verifies a test font face to fail fast on missing resources.</item>
        ///   <item>Invokes the asynchronous <see cref="MainAsync"/> and synchronously waits.</item>
        ///   <item>Returns a POSIX-style exit code (0 = success, 1 = failure).</item>
        /// </list>
        /// Exceptions are caught at this boundary to ensure a clean, single point of logging.
        /// </para>
        /// </summary>
        /// <param name="args">Raw command-line arguments as provided by the host.</param>
        /// <returns>Exit code (0 on success; non-zero on error).</returns>
        public static int Main(string[] args)
        {
            try
            {
                // 1) Register a font resolver so PdfSharpCore can find embedded or bundled fonts.
                //    This must be done before any PDF rendering to ensure consistent typography.
                GlobalFontSettings.FontResolver = new AutoResourceFontResolver();

                // 2) Optional sanity check: try resolving a known test typeface early.
                //    This helps catch missing embedded fonts at startup instead of during rendering.
                IFontResolver resolver = GlobalFontSettings.FontResolver;
                FontResolverInfo testFace = resolver.ResolveTypeface(AutoResourceFontResolver.FamilySans, false, false);
                if (testFace == null)
                {
                    xyLog.Log("⚠️ Warning: FontResolver returned null for FamilySans. Check embedded font resources.");
                }

                // 3) Hand over to the async workflow and block until it completes.
                //    Using GetAwaiter().GetResult() preserves original exceptions without AggregateException wrapping.
                MainAsync(args).GetAwaiter().GetResult();
                return 0; // weirdly its success 
            }
            catch (Exception ex)
            {
                // Unhandled exception at the top-level gets logged once here.
                xyLog.ExLog(ex);
                return 1; // what a failure
            }
        }


        /// <summary>
        /// Asynchronous core of the application.
        /// <para>
        /// High-level flow:
        /// <list type="number">
        ///   <item>Parse and validate CLI options.</item>
        ///   <item>Optionally print help/info and exit.</item>
        ///   <item>Enumerate source files, respecting exclude patterns.</item>
        ///   <item>Extract type metadata and flatten the resulting hierarchy.</item>
        ///   <item>Render outputs per requested format (or show in console).</item>
        ///   <item>Optionally build index/tree artifacts per format.</item>
        ///   <item>Log a concise summary.</item>
        /// </list>
        /// All filesystem work is guarded and favors fail-fast logging on misconfiguration.
        /// </para>
        /// </summary>
        /// <param name="args">Raw command-line arguments forwarded from <see cref="Main(string[])"/>.</param>
        /// <returns>A task that completes when the CLI workflow finishes.</returns>
        async static Task MainAsync(string[] args)
        {
            // --- Parse typed options ---------------------------------------------------------
            // Try to translate raw CLI args into a strongly typed options object.
            // If parsing fails, print a helpful message and the auto-generated help text.
            if (!OptionsParser.TryParseOptions(args, out var opt, out var err))
            {
                xyLog.Log("❌ " + err);
                xyLog.Log(OptionsParser.BuildHelpText());
                return;
            }

            // --- Early exits for meta flags --------------------------------------------------
            // --help: print full help and exit without performing any work.
            if (opt.Help)
            {
                xyLog.Log(OptionsParser.BuildHelpText());
                return;
            }

            // --info: print a small configuration summary (and README if present), then continue.
            if (opt.Info)
            {
                PrintReadmeToConsole(opt);
            }

            // --- Prepare output --------------------------------------------------------------
            // Ensure the base output directory exists when we actually write files.
            // In --show (dry-run) mode, outputs are not written, so we skip creation.
            if (!opt.ShowOnly)
                Directory.CreateDirectory(opt.OutPath);

            // --- Discover input files --------------------------------------------------------
            // Enumerate all *.cs files under the root, exclude well-known directories/patterns.
            IEnumerable<string> files = Directory.EnumerateFiles(opt.RootPath, "*.cs", SearchOption.AllDirectories).Where(p => !Utils.IsExcluded(p, opt.ExcludedParts));

            // Defensive check: abort gracefully if nothing to process.
            if (!files.Any())
            {
                xyLog.Log($"⚠️ No `.cs` files found in '{opt.RootPath}'. Aborting.");
                return;
            }

            // --- Extract model ---------------------------------------------------------------
            // Parse source files into structured type metadata (optionally including non-publics).
            List<TypeDoc> dataFromFiles = await TypeExtractor.TryParseDataFromFile(files, opt.IncludeNonPublic);

            // Flatten nested type hierarchies into a linear sequence to simplify downstream processing.
            IEnumerable<TypeDoc> flattened = TypeDocExtensions.FlattenTypes(dataFromFiles);

            // Precompute commonly referenced roots to speed lookups/rendering for large repos.
            CliRuntimeHelper.EnsureDominantRootCached(flattened);

            // --- Output strategy -------------------------------------------------------------
            if (opt.ShowOnly)
            {
                // In show-only mode, prefer markdown output for console readability.
                if (!string.Equals(opt.Format, "md", StringComparison.OrdinalIgnoreCase))
                    xyLog.Log("ℹ️ '--show' active: using Markdown in console (ignoring --format).");

                // Render each top-level type to markdown and stream to console.
                foreach (var t in dataFromFiles)
                {
                    string md = MarkdownRenderer.Render(t);
                    xyLog.Log(md);
                    xyLog.Log("\n---\n");
                }
            }
            else
            {
                // For each requested format:
                // 1) Resolve the per-format target directory (may come from mapping or subfolder).
                // 2) Ensure the directory exists.
                // 3) Write files using the utils method, log warnings if any file fails.
                foreach (var fmt in opt.Formats.Select(f => f.ToLowerInvariant()))
                {
                    var formatDir = CliRuntimeHelper.ResolveFormatDir(opt, fmt);
                    Directory.CreateDirectory(formatDir);

                    // Write type files into that per-format directory using YOUR Utils.
                    bool written = await Utils.WriteDataToFilesOrderedByNamespace(dataFromFiles, formatDir, fmt);
                    if (!written) xyLog.Log($"⚠️ One or more files could not be written for format '{fmt}'.");
                }
            }

            // --- Index / Tree artifacts ------------------------------------------------------
            // Build index/tree only if requested. These can be written to disk or shown in console.
            if (opt.BuildIndex || opt.BuildTree || opt.ShowIndexToConsole || opt.ShowTreeToConsole)
            {
                // Decide whether to persist artifacts to disk:
                // - In --show mode, we never write.
                // - If either "show index" or "show tree" is requested, we display instead of saving.
                bool writeToDisk = !opt.ShowOnly && !(opt.ShowIndexToConsole || opt.ShowTreeToConsole);

                foreach (var fmt in opt.Formats.Select(f => f.ToLowerInvariant()))
                {
                    // Resolve target directory for this format (top-level holder for index/tree).
                    var formatDir = CliRuntimeHelper.ResolveFormatDir(opt, fmt); // e.g., <OutPath>/<fmt> or mapping
                    if (writeToDisk) Directory.CreateDirectory(formatDir);

                    // Build index if requested or display requested; render into formatDir when persisting.
                    if (opt.BuildIndex || opt.ShowIndexToConsole)
                    {
                        var index = await FileTreeRenderer.BuildProjectIndex(flattened, fmt, formatDir, writeToDisk);
                        if (!writeToDisk) Console.WriteLine(index.ToString());
                    }

                    // Build tree if requested or display requested; same persistence rule as above.
                    if (opt.BuildTree || opt.ShowTreeToConsole)
                    {
                        var tree = await FileTreeRenderer.BuildProjectTree(
                        new StringBuilder(), fmt, opt.RootPath, formatDir, opt.ExcludedParts, writeToDisk);
                        if (!writeToDisk) Console.WriteLine(tree.ToString());

                    }
                }
            }

            // --- Final summary ---------------------------------------------------------------
            // Produce a compact mapping of format -> output directory for quick operator feedback.
            var summary = string.Join(',', opt.Formats.Select(f => $"\n{f}→{CliRuntimeHelper.ResolveFormatDir(opt, f.ToLowerInvariant())}"));
            xyLog.Log($"✅ Finished. Types: {flattened.Count()}, Formats: {summary}\n");
        }


        /// <summary>
        /// Prints a short, human-readable configuration summary to the console and,
        /// when present, streams the repository's README.md for quick context.
        /// <para>
        /// Intended to be called when <c>--info</c> is specified. This is a side-effecting
        /// utility that reads from disk and writes to <see cref="Console.Out"/>.
        /// </para>
        /// </summary>
        /// <param name="opt">The resolved CLI options to display.</param>
        public static void PrintReadmeToConsole(CliOptions opt)
        {
            // Attempt to locate a README near the root path. This is best-effort.
            var readmePath = CliRuntimeHelper.FindReadme(opt.RootPath);

            // Print the effective configuration so operators can verify the run context.
            Console.WriteLine("xyDocGen – current configuration:");
            Console.WriteLine($"  Root: {opt.RootPath}");
            Console.WriteLine($"  Out : {opt.OutPath}");
            Console.WriteLine($"  Formats: {string.Join(", ", opt.Formats)}");
            Console.WriteLine($"  Subfolders: {string.Join(", ", opt.Subfolders)}");

            // If a README exists, print a delineated section with its full contents.
            if (readmePath is not null && File.Exists(readmePath))
            {
                Console.WriteLine("\n\n--- README.md ---\n");
                Console.WriteLine(File.ReadAllText(readmePath));
            }
            else
                // Otherwise, note absence without failing the run.
                Console.WriteLine("README.md not found near root.");
            // Explicit return for clarity (no further side effects).
            return;
        }


        /// <summary>
        /// Ahuhu ma Awawawa
        /// Private nested sample class (placeholder).
        /// <para>
        /// This type has no behavior and is not referenced by the main flow;
        /// it exists solely as a stub for local experimentation or future extensions.
        /// </para>
        /// </summary>
        private class Awawa()
        {
            // Intentionally left blank.
            // Add private fields, methods, or constructors here if/when this stub is used.
        }

        /// <summary>
        /// Awawa 'nd Ahuhu
        /// Private nested sample static class (placeholder).
        /// <para>
        /// As a static type, this class cannot be instantiated and is typically used
        /// for grouping static helpers/constants that are local to the Program class.
        /// </para>
        /// </summary>
        private static class Ahuhu
        {
            // Intentionally left blank.
            // Add static methods or constants here if/when this stub is used.
        }

    }
}

