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

    /// <summary>
    ///  Responsible for parsing arguments, collecting source files, 
    /// extracting type information, and writing documentation output.
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    async static Task MainAsync(string[] args) 
    {
        string format = "";
        string outPath = "";
        string rootPath = "";
        bool includeNonPublic = true;
        HashSet<string> excludedParts = [];
        IEnumerable<TypeDoc> flattenedTypes = [];
        List<string> externalArguments = args.ToList();

        // If the --help keyword is detected in the parameter, output the list of commands and refrain from anything else
        if (await StringAnalyzer.AskForHelp(externalArguments))
        {
            return;    
        }

            (rootPath, outPath, format, includeNonPublic, excludedParts) = StringAnalyzer.AnalyzeArgs(externalArguments, args);

        if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(outPath))
        {
            xyLog.Log("❌ Error: Source path (`--root`) or output path (`--out`/`--folder`) was not correctly specified or is empty. Please check arguments.");
            return;
        }
        // Setting the output path 
        Directory.CreateDirectory(outPath);

        IEnumerable<string> files = Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories).Where(p => !Utils.IsExcluded(p, excludedParts));

        if (!files.Any())
        {
            xyLog.Log($"⚠️ Warning: No relevant `.cs` files found in path '{rootPath}'. Aborting documentation generation.");
            return;
        }

        List<TypeDoc> dataFromFiles = await TypeExtractor.TryParseDataFromFile(files, includeNonPublic );
        bool isWritten = await Utils.WriteDataToFilesOrderedByNamespace(dataFromFiles, outPath, format);

        flattenedTypes = TypeDocExtensions.FlattenTypes(dataFromFiles);
        await FileTreeRenderer.BuildIndexAndTree(flattenedTypes,format,rootPath,outPath,excludedParts);

        string output = $"\n✅ Finished. Types: {flattenedTypes.Count()}, Format: {format}, Output: {outPath}\n";
        xyLog.Log(output);
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
