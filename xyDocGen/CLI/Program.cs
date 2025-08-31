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
using xyDocGen.Core;
using xyDocGen.Core.Docs;
using xyDocGen.Core.Extractors;
using xyDocGen.Core.Renderer;
using xyToolz.Filesystem;
using xyToolz.Helper.Logging;

public class Program
{

    /// <summary>
    /// Test
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public static int Main(string[] args)
    {
        try
        {
            // Run the async main method synchronously
            MainAsync(args).GetAwaiter().GetResult();
            return 0; // success
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1; // failure
        }
    }

    /// <summary>
    /// Entry point of the CLI program.
    /// Responsible for parsing arguments, collecting source files, 
    /// extracting type information, and writing documentation output.
    /// </summary>
    async static Task MainAsync(string[] args) 
    {
        string rootPath = "";
        string outPath = "";
        string format = "";
        bool includeNonPublic = false;
        HashSet<string> excludedParts = [];
        List<string> externalArguments = args.ToList();

        (rootPath,outPath,format,includeNonPublic,excludedParts)= AnalyzeArgs(externalArguments, args); 

        // Setting the output path 
        Directory.CreateDirectory(outPath);

        IEnumerable<string> files = CollectFiles(externalArguments, args, rootPath, excludedParts);
        List<TypeDoc> dataFromFiles = await TryParseDataFromFile(externalArguments, args, files,includeNonPublic );
        bool isWritten = await WriteDataToFilesOrderedByNamespace(dataFromFiles, outPath, format);

        IEnumerable<TypeDoc> flattenedTypes = FlattenTypes(dataFromFiles);
        await BuildIndexAndTree(flattenedTypes,format,rootPath,outPath,excludedParts);

        xyLog.Log($"\n✅ Finished. Types: {flattenedTypes.Count()}, Format: {format}, Output: {outPath}\n");
    }


    /// <summary>
    /// Analyzes command line arguments and returns a tuple with all relevant values.
    /// </summary>
    static (string, string, string, bool, HashSet<string>) AnalyzeArgs(List<string> externalArguments, string[] args)
    {
        string rootPath = GetStartingPath(externalArguments, args);
       string outPath = GetOutputPath(externalArguments, args, rootPath);
       string format = GetFormat(externalArguments, args);

        bool isPrivate = GetPublicityHandling(externalArguments);
        HashSet<string> excludedParts = GetIgnorableFiles(externalArguments, args);
        return new(rootPath, outPath, format, isPrivate, excludedParts);
    }

    /// <summary>
    /// Define the path to start the workflow from
    /// </summary>
    /// <param name="ExternalArguments"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public static string GetStartingPath(List<string> ExternalArguments, string[] args) => ExternalArguments.Contains("--root") ? args[Array.IndexOf(args, "--root") + 1] : Directory.GetCurrentDirectory();

    /// <summary>
    /// Checking the CLI arguments for specific target location, or setting default
    /// </summary>
    /// <param name="externalarguments"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public static string GetOutputPath(List<string> externalarguments, string[] args,string rootpath)
    {
        string outPath = "";
        string folder = "";
        string subfolder = "";

        if (externalarguments.Contains("--out"))
        {
            outPath = args[Array.IndexOf(args, "--out") + 1];
        }
        else
        {
           folder = externalarguments.Contains("--folder") ? args[Array.IndexOf(args, "--folder") + 1] : Path.Combine(rootpath, "docs");
           subfolder = externalarguments.Contains("--subfolder") ? args[Array.IndexOf(args, "--subfolder") + 1] :    Path.Combine(rootpath, folder, "api");
            outPath = subfolder;
        }
        return outPath;
    }

    /// <summary>
    ///  Determining output format 
    ///  
    /// 
    /// </summary>
    /// <param name="ExternalArguments"></param>
    /// <param name="args"></param>
    /// <returns>"..." or default "md"</returns>
    public static string GetFormat(List<string> ExternalArguments, string[] args) => ExternalArguments.Contains("--format")? args[Array.IndexOf(args, "--format") + 1].ToLower(): "md";          // default: Markdown!

    /// <summary>
    /// Checking how to handle non public data 
    /// </summary>
    /// <param name="ExternalArguments"></param>
    /// <returns></returns>
    public static bool GetPublicityHandling(List<string> ExternalArguments) => ExternalArguments.Contains("--private");

    /// <summary>
    /// Collects folder names that should be excluded (e.g. bin, obj, node_modules).
    /// Default exclusion list can be overridden via --exclude.
    /// </summary>
    public static HashSet<string> GetIgnorableFiles(List<string> ExternalArguments, string[] args) => new((ExternalArguments.Contains("--exclude")? args[Array.IndexOf(args, "--exclude") + 1]: ".git;bin;obj;node_modules;.vs;TestResults").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));


    /// <summary>
    /// Builds both the namespace-based API index (INDEX.md) 
    /// and the project folder structure (PROJECT-STRUCTURE.md).
    /// </summary>
    public static async Task BuildIndexAndTree(IEnumerable<TypeDoc> flattenedTypes, string format, string rootPath, string outPath, HashSet<string> excludedParts)
    {
        StringBuilder indexBuilder = await BuildProjectIndex(flattenedTypes, format, outPath);
        indexBuilder.Clear();
        StringBuilder projectBuilder = await BuildProjectTree(indexBuilder, format, rootPath, outPath, excludedParts);
        indexBuilder = null;
    }
    /// <summary>
    /// Builds an INDEX.md file listing all documented types, grouped by namespace.
    /// </summary>
    public static async Task<StringBuilder> BuildProjectIndex(IEnumerable<TypeDoc> flattenedtypes, string format,string outpath)
    {
        // Rendering INDEX.md  ordered by namespace
        StringBuilder indexBuilder = new StringBuilder();

        // Adding the headline
        indexBuilder.AppendLine("# API‑Index (by namespace)\n");

        // Bringing everything into the right order
        IEnumerable<IGrouping<string, TypeDoc>> flattenedTypesGroupedAndInOrder =flattenedtypes.GroupBy(t => t.Namespace).OrderBy(g => g.Key);

        foreach (IGrouping<string, TypeDoc> group in flattenedTypesGroupedAndInOrder)
        {
            // Adding subheadline
            indexBuilder.AppendLine($"## `{group.Key}`");
            foreach (TypeDoc tD in group.OrderBy(t => t.DisplayName))
            {
                // Choosing the file extension
                string fileExt = format == "pdf" ? "pdf" : format == "html" ? "html" : format == "json" ? "json" : "md";

                // Building the group data 
                string rel = $"./{group.Key.Replace('<', '_').Replace('>', '_')}/{tD.DisplayName.Replace(' ', '_')}.{fileExt}";

                // Appending data
                indexBuilder.AppendLine($"- [{tD.DisplayName}]({rel})");
            }
            // Adding empty row
            indexBuilder.AppendLine();
        }

        // Setting target path
        string indexPath = Path.Combine(outpath, "INDEX.md");

        // Saving the Index
        await xyFiles.SaveToFile(indexBuilder.ToString(), indexPath);

        return indexBuilder;
    }
    /// <summary>
    /// Builds PROJECT-STRUCTURE.md, a visual tree representation of the file system.
    /// </summary>
    public static async Task<StringBuilder> BuildProjectTree(StringBuilder treeBuilder, string format, string rootPath, string outPath, HashSet<string> excludedParts)
    {

        // Adding the headline
        treeBuilder.AppendLine("# Project structure\n");

        // Rendering PROJECT-STRUCTURE.md 
        FileTreeRenderer.RenderTree(new DirectoryInfo(rootPath), "", true, treeBuilder, excludedParts);

        // Combining the target path
        string targetPath = Path.Combine(outPath, "PROJECT-STRUCTURE.md");

        // Write the tree into the target file
        await xyFiles.SaveToFile(treeBuilder.ToString(), targetPath);

        return treeBuilder;
    }



    /// <summary>
    /// Collects all .cs files recursively, excluding ignored folders.
    /// </summary>
    public static IEnumerable<string> CollectFiles(List<string> ExternalArguments, string[] args, string rootPath, HashSet<string> excluded)
    {
        // Collecting all the relevant files
        return Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories).Where(p => !IsExcluded(p, excluded));
    }

    /// <summary>
    /// Parses all collected .cs files into <see cref="TypeDoc"/> objects.
    /// Uses Roslyn to analyze syntax trees and namespaces.
    /// </summary>
    public static async Task<List<TypeDoc>> TryParseDataFromFile(List<string> ExternalArguments, string[] args, IEnumerable<string> relevantFiles, bool includeNonPublic)
    {
        // Getting the data from the files
        TypeExtractor extractor = new(includeNonPublic);

        // Storing data from the files
        List<TypeDoc> allTypes = new();

        // Parsing each file to C# and collect them
        foreach (string file in relevantFiles)
        {
            // Read data
            string text = await xyFiles.LoadFileAsync(file);

            // Parse to C#
            SyntaxTree tree = CSharpSyntaxTree.ParseText(text);
            
            // Get the root of the syntax tree
            CompilationUnitSyntax compilationUnitRoot = tree.GetCompilationUnitRoot();

            // Collect the declared namespaces
            List<BaseNamespaceDeclarationSyntax> namespaceDeclarations = compilationUnitRoot.Members.OfType<BaseNamespaceDeclarationSyntax>().ToList();

            // Check if there are namespaces
            if (!namespaceDeclarations.Any())
            {
                // Processing all members without namespace
                allTypes.AddRange(extractor.ProcessMembers(compilationUnitRoot.Members, null, file));

            }
            else
            {
                // Processing all members within all the "super secret" (sub)namespaces
                foreach (BaseNamespaceDeclarationSyntax nsD in namespaceDeclarations)
                {
                    // Collect all members in a namespace
                    allTypes.AddRange(extractor.ProcessMembers(nsD.Members, nsD.Name.ToString(), file));
                }
            }
        }

        return allTypes;
    }

    /// <summary>
    /// Flattening all the nested types for top-level listing
    /// </summary>
    /// <param name="types"></param>
    /// <returns></returns>
    public static IEnumerable<TypeDoc> FlattenTypes(List<TypeDoc> types) =>types.SelectMany(t => t.FlattenNested());
    
    /// <summary>
    /// Writes each TypeDoc to a file, grouped by namespace, in the requested format.
    /// Supports JSON, HTML, PDF, Markdown.
    /// </summary>
    public static async Task<bool> WriteDataToFilesOrderedByNamespace(IEnumerable<TypeDoc> alltypes, string outpath, string format)
    {
        bool isWritten = false;

        // Iterating through the list 
        foreach (TypeDoc tD in alltypes)
        {
            // Creating a folder for each namespace
            string namespaceFolder = Path.Combine(outpath, tD.Namespace.Replace('<', '_').Replace('>', '_'));
            Directory.CreateDirectory(namespaceFolder);

            string fileName = Path.Combine(namespaceFolder, tD.DisplayName.Replace(' ', '_'));
            string content;

            // Choosing the format and saving converted data to the target file
            switch (format)
            {
                case "json":
                    fileName += ".json";
                    content = JsonRenderer.Render(tD);
                    isWritten = await xyFiles.SaveToFile(content,fileName);
                    break;

                case "html":
                    fileName += ".html";
                    content = HtmlRenderer.Render(tD, cssPath: null);
                    isWritten = await xyFiles.SaveToFile(content, fileName);
                    break;

                case "pdf":
                    fileName += ".pdf";
                    PdfRenderer.RenderToFile(tD, fileName);
                    isWritten = true;
                    break;

                default: // Markdown
                    fileName += ".md";
                    content = MarkdownRenderer.Render(tD);
                    isWritten = await xyFiles.SaveToFile(content, fileName);
                    break;
            }
        }
        return isWritten;
    }


    /// <summary>
    /// Checks if a given file path contains any excluded folder parts.
    /// </summary>
    static bool IsExcluded(string path, HashSet<string> excludeParts)
    {
        var parts = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
        return parts.Any(p => excludeParts.Contains(p));
    }
}
