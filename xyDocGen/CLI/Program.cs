namespace xyDocumentor.CLI
{
    using Microsoft.EntityFrameworkCore.Metadata.Internal;
    using PdfSharpCore.Fonts;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.Intrinsics.X86;
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
            // ### Needed to localize where the chosen formats go missing
            //args = [ "--show-tree","--show-index", "--format", "pdf,json"];

            // Try to translate raw CLI args into a strongly typed options object.
            if (!OptionsParser.TryParseOptions(args, out var opt, out var err))
            {
                xyLog.Log("❌ " + err);
                xyLog.Log(OptionsParser.BuildHelpText());
                return;
            }

            // List the commands
            if (opt.Help)
            {
                xyLog.Log(OptionsParser.BuildHelpText());
                return;
            }

            // Print a small configuration summary (and README if present)
            if (opt.Info)
            {
                PrintReadmeToConsole(opt);
            }

            if (!opt.ShowOnly)
                Directory.CreateDirectory(opt.OutPath);

            IEnumerable<string> files = Directory.EnumerateFiles(opt.RootPath, "*.cs", SearchOption.AllDirectories).Where(p => !Utils.IsExcluded(p, opt.ExcludedParts));

            if (!files.Any())
            {
                xyLog.Log($"⚠️ No `.cs` files found in '{opt.RootPath}'. Aborting.");
                return;
            }


            List<TypeDoc> dataFromFiles = await TypeExtractor.TryParseDataFromFile(files, opt.IncludeNonPublic);

            // Flatten nested type hierarchies into a linear sequence to simplify downstream processing.
            IEnumerable<TypeDoc> flattened = TypeDocExtensions.FlattenTypes(dataFromFiles);

            // Precompute commonly referenced roots to speed lookups/rendering for large repos.
            CliRuntimeHelper.EnsureDominantRootCached(flattened);
            
            StringBuilder? index = null;
            StringBuilder? tree = null;

            if (opt.BuildIndex || opt.ShowIndexToConsole)
            {
                index = await FileTreeRenderer.BuildProjectIndex(flattened,"md",writeToDisk: false);
            }

            if (opt.BuildTree || opt.ShowTreeToConsole)
            {
                tree = await FileTreeRenderer.BuildProjectTree(new StringBuilder(),"md",opt.RootPath,opt.OutPath,opt.ExcludedParts,writeToDisk: false);
            }

            // Output "strategy"
            if (opt.ShowOnly)
            {
                PrintAllToConsole(opt, dataFromFiles);
            }
            else
            {
                await WriteDataToFiles(opt, dataFromFiles);
            }

            CheckShowIndexAndTreeInConsole(opt, index, tree);

            // Disk persistence (NO rebuild) 
            if (!opt.ShowOnly && (opt.BuildIndex || opt.BuildTree))
            {
                // Per flag umschalten? Oder ist das egal, lohnt der Aufwand?
                //WriteIndexAndTreeIntoAllSubFolders(opt, index, tree);

                WriteIndexAndTreeInSameFolderAsFormatFolders(opt,index,tree);
            }

            PrintSummary(opt, flattened);
        }

        private static bool WriteIndexAndTreeInSameFolderAsFormatFolders(CliOptions opt, StringBuilder index, StringBuilder tree)
        {
            try
            {
                string formatDir = CliRuntimeHelper.ResolveFormatDir(opt, "md");
                string targetDir = Directory.GetParent(formatDir).FullName;

                Directory.CreateDirectory(formatDir);

                if (opt.BuildIndex && index != null)
                    File.WriteAllText(Path.Combine(targetDir, "index.md"), index.ToString());

                if (opt.BuildTree && tree != null)
                    File.WriteAllText(Path.Combine(targetDir, "tree.md"), tree.ToString());
            }
            catch (Exception ex)
            {
                xyLog.ExLog(ex);
                return false;
            }
            return true;
        }


        private static bool PrintAllToConsole(CliOptions opt, List<TypeDoc> dataFromFiles)
        {
                if (!string.Equals(opt.Format, "md", StringComparison.OrdinalIgnoreCase))
                    xyLog.Log("ℹ️ '--show' active: using Markdown in console (ignoring --format).");

                foreach (TypeDoc t in dataFromFiles)
                {
                    xyLog.Log(MarkdownRenderer.Render(t));
                    xyLog.Log("\n---\n");
                }
            
            return true;
        }


        private static async Task<bool> WriteDataToFiles(CliOptions opt, List<TypeDoc> dataFromFiles )
        {
            try
            {
                foreach (string fmt in opt.Formats.Select(f => f.ToLowerInvariant()))
                {
                    string formatDir = CliRuntimeHelper.ResolveFormatDir(opt, fmt);
                    Directory.CreateDirectory(formatDir);
                                        
                    bool written = await Utils.WriteDataToFilesOrderedByNamespace(dataFromFiles, formatDir, fmt);
                    if (!written) xyLog.Log($"⚠️ One or more files could not be written for format '{fmt}'.");
                }
                return true;
            }
            catch(Exception ex)
            {
                xyLog.ExLog(ex);
                return false;
            }
        }

        private static bool WriteIndexAndTreeIntoAllSubFolders(CliOptions opt, StringBuilder index, StringBuilder tree)
        {
            try
            {
                foreach (var fmt in opt.Formats.Select(f => f.ToLowerInvariant()))
                {
                    var formatDir = CliRuntimeHelper.ResolveFormatDir(opt, fmt);
                    Directory.CreateDirectory(formatDir);

                    if (opt.BuildIndex && index != null)
                        File.WriteAllText(Path.Combine(formatDir, "index.md"), index.ToString());

                    if (opt.BuildTree && tree != null)
                        File.WriteAllText(Path.Combine(formatDir, "tree.md"), tree.ToString());
                }
                return true;
            }
            catch (Exception ex)
            {
                xyLog.ExLog(ex);
                return false;
            }
        }


        /// <summary>
        /// If the flag is raised, print index and/or tree to console
        /// </summary>
        /// <param name="opt"></param>
        /// <param name="index"></param>
        /// <param name="tree"></param>
        private static void CheckShowIndexAndTreeInConsole(CliOptions opt, StringBuilder index, StringBuilder tree)
        {
            if (opt.ShowIndexToConsole && index != null)
                Console.WriteLine(index.ToString());

            if (opt.ShowTreeToConsole && tree != null)
                Console.WriteLine(tree.ToString());
        }

        /// <summary>
        /// Producing a compact mapping of format -> output directory for quick operator feedback.
        /// </summary>
        /// <param name="opt"></param>
        /// <param name="flattened"></param>
        private static void PrintSummary(CliOptions opt, IEnumerable<TypeDoc> flattened) 
        { 
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

