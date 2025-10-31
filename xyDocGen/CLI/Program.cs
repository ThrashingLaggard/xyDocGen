namespace xyDocumentor.CLI
{
    using PdfSharpCore.Fonts;
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using xyDocumentor.Docs;
    using xyDocumentor.Extractors;
    using xyDocumentor.Helpers;
    using xyDocumentor.Renderer;
    using xyToolz.Helper.Logging;


    /// <summary>
    /// Basic startup class for the project
    /// </summary>
    public partial class Program
    {


        /// <summary>
        /// Mimimi, i dont want to be async, or else im not a valid starting point for the program, mimimi!
        /// </summary>
        /// <param name="args"></param>
        /// 
        /// <returns></returns>
        public static int Main(string[] args)
        {
            try
            {
                GlobalFontSettings.FontResolver = new AutoResourceFontResolver();
                
                // 🔍 Optional verification (helps catch missing embedded fonts early)
                var resolver = GlobalFontSettings.FontResolver;
                var testFace = resolver.ResolveTypeface(AutoResourceFontResolver.FamilySans, false, false);
                if (testFace == null)
                    xyLog.Log("⚠️ Warning: FontResolver returned null for FamilySans. Check embedded font resources.");

                // Run the async main method synchronously
                MainAsync(args).GetAwaiter().GetResult();
                return 0; // weirdly its success 
            }
            catch (Exception ex)
            {
                xyLog.ExLog(ex);
                return 1; // what a failure
            }
        }

        async static Task MainAsync(string[] args)
        {
            // Parse typed options
            if (!StringAnalyzer.TryParseOptions(args, out var opt, out var err))
            {
                xyLog.Log("❌ " + err);
                xyLog.Log(StringAnalyzer.BuildHelpText());
                return;
            }

            if (opt.Help)
            {
                xyLog.Log(StringAnalyzer.BuildHelpText());
                return;
            }
            if (opt.Info)
            {
                // Print README.md to the console as requested by your README.
                var readmePath = CliRuntimeHelper.FindReadme(opt.RootPath);
                Console.WriteLine("xyDocGen – current configuration:");
                Console.WriteLine($"  Root: {opt.RootPath}");
                Console.WriteLine($"  Out : {opt.OutPath}");
                Console.WriteLine($"  Formats: {string.Join(", ", opt.Formats)}");
                Console.WriteLine($"  Subfolders: {string.Join(", ", opt.Subfolders)}");
                if (readmePath is not null && File.Exists(readmePath))
                {
                    Console.WriteLine("\n--- README.md ---\n");
                    Console.WriteLine(File.ReadAllText(readmePath));
                }
                else
                    Console.WriteLine("README.md not found near root.");
                return;
            }

            // Ensure output folder exists (when writing files)
            if (!opt.ShowOnly)
                Directory.CreateDirectory(opt.OutPath);

            // Enumerate .cs files (respect excludes)
            var files = Directory.EnumerateFiles(opt.RootPath, "*.cs", SearchOption.AllDirectories)
                                 .Where(p => !Utils.IsExcluded(p, opt.ExcludedParts));
            if (!files.Any())
            {
                xyLog.Log($"⚠️ No `.cs` files found in '{opt.RootPath}'. Aborting.");
                return;
            }

            // Extract types
            var dataFromFiles = await TypeExtractor.TryParseDataFromFile(files, opt.IncludeNonPublic);
            var flattened = TypeDocExtensions.FlattenTypes(dataFromFiles);

            // Output strategy
            if (opt.ShowOnly)
            {
                if (!string.Equals(opt.Format, "md", StringComparison.OrdinalIgnoreCase))
                    xyLog.Log("ℹ️ '--show' active: using Markdown in console (ignoring --format).");

                foreach (var t in dataFromFiles)
                {
                    string md = MarkdownRenderer.Render(t);
                    xyLog.Log(md);
                    xyLog.Log("\n---\n");
                }
            }
            else
            {
                foreach (var fmt in opt.Formats.Select(f => f.ToLowerInvariant()))
                {
                    var formatDir = CliRuntimeHelper.ResolveFormatDir(opt, fmt);
                    Directory.CreateDirectory(formatDir);

                    // Write type files into that per-format directory using YOUR Utils.
                    bool written = await Utils.WriteDataToFilesOrderedByNamespace(dataFromFiles, formatDir, fmt);
                    if (!written) xyLog.Log($"⚠️ One or more files could not be written for format '{fmt}'.");
                }
            }

            // Index/Tree only if requested — per-format, in the top level of each format folder.
            if (opt.BuildIndex || opt.BuildTree || opt.ShowIndexToConsole || opt.ShowTreeToConsole)
            {
                bool writeToDisk = !opt.ShowOnly && !(opt.ShowIndexToConsole || opt.ShowTreeToConsole);

                foreach (var fmt in opt.Formats.Select(f => f.ToLowerInvariant()))
                {
                    var formatDir = CliRuntimeHelper.ResolveFormatDir(opt, fmt); // e.g., <OutPath>/<fmt> or mapping
                    if (writeToDisk) Directory.CreateDirectory(formatDir);

                    if (opt.BuildIndex || opt.ShowIndexToConsole)
                    {
                        var index = await FileTreeRenderer.BuildProjectIndex(flattened, fmt, formatDir, writeToDisk);
                        if (!writeToDisk) Console.WriteLine(index.ToString());
                    }

                    if (opt.BuildTree || opt.ShowTreeToConsole)
                    {
                        var tree = await FileTreeRenderer.BuildProjectTree(
                        new StringBuilder(), fmt, opt.RootPath, formatDir, opt.ExcludedParts, writeToDisk);
                        if (!writeToDisk) Console.WriteLine(tree.ToString());

                    }
                }
            }

            var summary = string.Join(',', opt.Formats.Select(f => $"\n{f}→{CliRuntimeHelper.ResolveFormatDir(opt, f.ToLowerInvariant())}"));
            xyLog.Log($"✅ Finished. Types: {flattened.Count()}, Formats: {summary}\n");
        }

        /// <summary>
        /// Ahuhu ma Awawawa
        /// </summary>
        private class Awawa()
        {

        }

        /// <summary>
        /// Awawa 'nd Ahuhu
        /// </summary>
        private static class Ahuhu
        {

        }
    }
}

