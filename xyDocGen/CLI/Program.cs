using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        bool includeNonPublic = false;
        HashSet<string> excludedParts = [];
        IEnumerable<TypeDoc> flattenedTypes = [];
        List<string> externalArguments = args.ToList();

        if (await AskForHelp(externalArguments))
        {
            return;    
        }

            (rootPath, outPath, format, includeNonPublic, excludedParts) = StringAnalyser.AnalyzeArgs(externalArguments, args); 

        // Setting the output path 
        Directory.CreateDirectory(outPath);

        IEnumerable<string> files = Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories).Where(p => !Utils.IsExcluded(p, excludedParts));
        List<TypeDoc> dataFromFiles = await TypeExtractor.TryParseDataFromFile(externalArguments, args, files,includeNonPublic );
        bool isWritten = await Utils.WriteDataToFilesOrderedByNamespace(dataFromFiles, outPath, format);

        flattenedTypes = TypeDocExtensions.FlattenTypes(dataFromFiles);
        await FileTreeRenderer.BuildIndexAndTree(flattenedTypes,format,rootPath,outPath,excludedParts);

        string output = $"\n✅ Finished. Types: {flattenedTypes.Count()}, Format: {format}, Output: {outPath}\n";
        xyLog.Log(output);
    }

    public static async Task<bool> AskForHelp(List<string> externalArguments)
    {
        if (externalArguments.First() is "--help")
        {
            string commands = await OutputCommands();
            Console.WriteLine(commands);
            Console.Out.Flush();
            return true;
        }
        return false;
    }

    public static async Task<string> OutputCommands()
    {
        string commands =
            "xydocgen     ===      Base command\n"+
            "--root     ===      Root path\n" +
            "--folder     ===     Target folder\n" +
            "--subfolder     -->     Allways comes with folder, default is 'api'\n" +
            "--out     ===     Target folder and subfolder together in one\n" +
            "--exclude     ===     Components to ignore\n" +
            "--format     ===     Specify output flavour\n" +
            "--private     ===     Add to ignore unpublic components\n"+
            "--help     ===     Output list of commands";


        return commands;
    }

 



}
