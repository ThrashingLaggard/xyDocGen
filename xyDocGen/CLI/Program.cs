using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using xyDocumentor.Core;
using xyDocumentor.Core.Docs;
using xyDocumentor.Core.Extractors;
using xyDocumentor.Core.Helpers;
using xyDocumentor.Core.Renderer;
using xyToolz.Filesystem;
using xyToolz.Helper.Logging;


/// <summary>
/// Basic startup class for the project
/// </summary>
public class Program
{

    /// <summary>
    /// Mimimi, i dont want to be async, or else im not a valid starting point for the program, mimimi!
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public static int Main(string[] args)
    {
        try
        {
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
            xyLog.Log(StringAnalyzer.BuildInfoText());
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
            bool written = await Utils.WriteDataToFilesOrderedByNamespace(dataFromFiles, opt.OutPath, opt.Format);
            if (!written) xyLog.Log("⚠️ One or more files could not be written.");
        }

        // Index/Tree only if requested
        if (opt.BuildIndex || opt.BuildTree)
        {
            bool writeToDisk = !opt.ShowOnly;

            if (opt.BuildIndex)
            {
                var index = await FileTreeRenderer.BuildProjectIndex(flattened, opt.Format, opt.OutPath, writeToDisk);
                if (opt.ShowOnly) Console.WriteLine(index.ToString());
            }

            if (opt.BuildTree)
            {
                var tree = await FileTreeRenderer.BuildProjectTree(new StringBuilder(), opt.Format, opt.RootPath, opt.OutPath, opt.ExcludedParts, writeToDisk);
                if (opt.ShowOnly) Console.WriteLine(tree.ToString());
            }
        }

        xyLog.Log($"\n✅ Finished. Types: {flattened.Count()}, Format: {opt.Format}, Output: {opt.OutPath}\n");
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
