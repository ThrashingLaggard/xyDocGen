namespace xyDocumentor.CLI
{
    using Microsoft.CodeAnalysis;
    using Microsoft.EntityFrameworkCore.Metadata.Internal;
    using PdfSharpCore.Fonts;
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.Intrinsics.X86;
    using System.Text;
    using System.Threading.Tasks;
    using xyDocumentor.Core.Docs;
    using xyDocumentor.Core.Extractors;
    using xyDocumentor.Core.Helpers;
    using xyDocumentor.Core.Models;
    using xyDocumentor.Core.Renderer;
    using xyToolz.Helper.Logging;


    /// <summary>
    /// Basic startup class for the project
    /// </summary>
    public partial class Program
    {

        /// <summary>
        /// Builds the absolute directory path for a given format,
        /// ensuring the path exists and uses normalized separators.
        /// This keeps each format's output isolated under OutPath/format
        /// </summary>
        private static string FormatDir(string baseOutPath, string fmt)
        {
            if (string.IsNullOrWhiteSpace(baseOutPath))
                baseOutPath = Environment.CurrentDirectory;

            // Fallback: ensure format is safe to use as folder name
            fmt = fmt?.Trim().ToLowerInvariant() ?? "unknown";

            // Combine and normalize path
            var combined = Path.Combine(baseOutPath, fmt);
            var full = Path.GetFullPath(combined);

            // Create the directory if needed
            if (!Directory.Exists(full))
                Directory.CreateDirectory(full);

            return full;
        }


        /// <summary>Resolve the absolute output directory for a given format.</summary>
        private static string ResolveFormatDir(CliOptions opt, string fmt)
            => opt.OutputDirs != null && opt.OutputDirs.TryGetValue(fmt, out var mapped) && !string.IsNullOrWhiteSpace(mapped)
               ? mapped
               : FormatDir(opt.OutPath, fmt);

        /// <summary>Return path to README.md near the given root; fallback to current directory.</summary>
        private static string FindReadme(string root)
        {
            var candidate1 = Path.Combine(root ?? Environment.CurrentDirectory, "README.md");
            var candidate2 = Path.Combine(Environment.CurrentDirectory, "README.md");
            if (File.Exists(candidate1)) return candidate1;
            if (File.Exists(candidate2)) return candidate2;
            return null;
        }

        /// <summary>
        /// Mimimi, i dont want to be async, or else im not a valid starting point for the program, mimimi!
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static int Main(string[] args)
        {
            try
            {
                GlobalFontSettings.FontResolver ??= new AutoResourceFontResolver();

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

        //private static async Task<bool> CheckForBreakingKeyWords(List<string> listedArguments_) 
        //{
        //    bool isIndex = false;
        //    bool isTree = false;
        //    bool isPrintOnly = false;
        //    if (await StringAnalyzer.AskForHelp(listedArguments_))
        //    {
        //        return true;
        //    }

        //    if (await StringAnalyzer.AskForInformation(listedArguments_))
        //    {
        //        return true;
        //    }

        //    if (await StringAnalyzer.AskForIndex(listedArguments_))
        //    {
        //        isIndex = true;
        //    }

        //    if (await StringAnalyzer.AskForTree(listedArguments_))
        //    {
        //        isTree = true;
        //    }

        //    if (await StringAnalyzer.O(listedArguments_))
        //    {
        //        if (await StringAnalyzer.AskForPrintOnly(listedArguments_))
        //        {
        //            isPrintOnly = true;
        //        }


        //        // Print all the stuff into the console; only console?


        //    }

        //    return false;
        //}

        /// <summary>
        ///  Responsible for parsing arguments, collecting source files, 
        /// extracting type information, and writing documentation output.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        //async static Task MainAsync(string[] args) 
        //{
        //    string format = "";
        //    string outPath = "";
        //    string rootPath = "";
        //    bool includeNonPublic = true;
        //    HashSet<string> excludedParts = [];
        //    IEnumerable<TypeDoc> flattenedTypes = [];
        //    List<string> externalArguments = args.ToList();

        //    // If the --help keyword is detected in the parameter, output the list of commands and refrain from anything else
        //    //if (await CheckForBreakingKeyWords(externalArguments)) return;

        //    (rootPath, outPath, format, includeNonPublic, excludedParts) = StringAnalyzer.AnalyzeArgs(externalArguments, args);

        //    if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(outPath))
        //    {
        //        xyLog.Log("❌ Error: Source path (`--root`) or output path (`--out`/`--folder`) was not correctly specified or is empty. Please check arguments.");
        //        return;
        //    }
        //    // Setting the output path 
        //    Directory.CreateDirectory(outPath);

        //    IEnumerable<string> files = Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories).Where(p => !Utils.IsExcluded(p, excludedParts));

        //    if (!files.Any())
        //    {
        //        xyLog.Log($"⚠️ Warning: No relevant `.cs` files found in path '{rootPath}'. Aborting documentation generation.");
        //        return;
        //    }

        //    List<TypeDoc> dataFromFiles = await TypeExtractor.TryParseDataFromFile(files, includeNonPublic );
        //    bool isWritten = await Utils.WriteDataToFilesOrderedByNamespace(dataFromFiles, outPath, format);

        //    flattenedTypes = TypeDocExtensions.FlattenTypes(dataFromFiles);

        //    // Hier irgendwo die --Tree und --Show Flaggen abfangen!
        //    await FileTreeRenderer.BuildIndexAndTree(flattenedTypes,format,rootPath,outPath,excludedParts);

        //    string output = $"\n✅ Finished. Types: {flattenedTypes.Count()}, Format: {format}, Output: {outPath}\n";
        //    xyLog.Log(output);
        //}
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
                var readmePath = FindReadme(opt.RootPath);
                if (readmePath is not null && File.Exists(readmePath))
                    Console.WriteLine(File.ReadAllText(readmePath));
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

  
                GlobalFontSettings.FontResolver ??= new AutoResourceFontResolver();

            // Set the custom resolver ASAP and test it via the INSTANCE, not the static helper
            var fr = GlobalFontSettings.FontResolver = new AutoResourceFontResolver();

            var info = fr.ResolveTypeface(AutoResourceFontResolver.FamilySans, false, false);
            System.Diagnostics.Debug.WriteLine("Resolved face via instance resolver: " + info?.FaceName);

            //// Optional: list embedded resources to verify the fonts are really there
            //foreach (var n in typeof(AutoResourceFontResolver).Assembly.GetManifestResourceNames())
            //    System.Diagnostics.Debug.WriteLine("RES: " + n);


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
                //bool written = await Utils.WriteDataToFilesOrderedByNamespace(dataFromFiles, opt.OutPath, opt.Format);
                //if (!written) xyLog.Log("⚠️ One or more files could not be written.");

                foreach (var fmt in opt.Formats.Select(f => f.ToLowerInvariant()))
                {
                    // Use YOUR helper to resolve the correct per-format directory.
                    var formatDir = ResolveFormatDir(opt, fmt);
                    Directory.CreateDirectory(formatDir);
                
                    // Write type files into that per-format directory using YOUR Utils.
                    bool written = await Utils.WriteDataToFilesOrderedByNamespace(dataFromFiles, formatDir, fmt);
                    if (!written) xyLog.Log($"⚠️ One or more files could not be written for format '{fmt}'.");
                }
            }

            // Index/Tree only if requested — per-format, in the top level of each format folder.
           if (opt.BuildIndex || opt.BuildTree)
           {
                bool writeToDisk = !opt.ShowOnly;
                
                foreach (var fmt in opt.Formats.Select(f => f.ToLowerInvariant()))
                {
                    var formatDir = ResolveFormatDir(opt, fmt); // e.g., <OutPath>/<fmt> or mapping
                    if (writeToDisk) Directory.CreateDirectory(formatDir);
                    
                    // BuildProjectIndex/Tree are YOUR existing helpers; we only change the OUT PATH
                    // to the per-format directory so that "index" and "tree" land at the top level
                    // INSIDE the format folder (README requirement).
                    if (opt.BuildIndex)
                    {
                        var index = await FileTreeRenderer.BuildProjectIndex(flattened, fmt, formatDir, writeToDisk);
                        if (opt.ShowOnly) Console.WriteLine(index.ToString());
                    }
                    
                    if (opt.BuildTree)
                    {
                        var tree = await FileTreeRenderer.BuildProjectTree(
                        new StringBuilder(), fmt, opt.RootPath, formatDir, opt.ExcludedParts, writeToDisk);
                        if (opt.ShowOnly) Console.WriteLine(tree.ToString());
                    }
                }
           }

            var summary = string.Join(", ", opt.Formats.Select(f => $"{f}→{ResolveFormatDir(opt, f.ToLowerInvariant())}"));
            xyLog.Log($"\n✅ Finished. Types: {flattened.Count()}, Formats: [{summary}]\n");
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
