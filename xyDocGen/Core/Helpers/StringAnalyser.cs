using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace xyDocumentor.Core.Helpers
{
    internal class StringAnalyser
    {

        /// <summary>
        /// Analyzes command line arguments and returns a tuple with all relevant values.
        /// </summary>
        internal static (string, string, string, bool, HashSet<string>) AnalyzeArgs(List<string> externalArguments, string[] args)
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
        public static string GetOutputPath(List<string> externalarguments, string[] args, string rootpath)
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
                subfolder = externalarguments.Contains("--subfolder") ? args[Array.IndexOf(args, "--subfolder") + 1] : Path.Combine(rootpath, folder, "api");
                outPath = subfolder;
            }
            return outPath;
        }

        /// <summary>
        ///  Determines the output format 
        ///  
        /// pdf/html/json
        /// 
        /// standard is md
        /// 
        /// </summary>
        /// <param name="ExternalArguments"></param>
        /// <param name="args"></param>
        /// <returns>"..." or default "md"</returns>
        public static string GetFormat(List<string> ExternalArguments, string[] args) => ExternalArguments.Contains("--format") ? args[Array.IndexOf(args, "--format") + 1].ToLower() : "md";          // default: Markdown!

        /// <summary>
        /// Checks how to handle non public data, looks for the --private keyword
        /// </summary>
        /// <param name="ExternalArguments"></param>
        /// <returns>
        /// TRUE if "--private" 
        ///           else FALSE
        /// </returns>
        public static bool GetPublicityHandling(List<string> ExternalArguments) => ExternalArguments.Contains("--private");

        /// <summary>
        /// Collects folder names that should be excluded 
        /// Default exclusion list can be overridden via --exclude.
        /// 
        /// standard fallback: 
        /// .git;bin;obj;node_modules;.vs;TestResults
        /// 
        /// </summary>
        /// <param name="ExternalArguments"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static HashSet<string> GetIgnorableFiles(List<string> ExternalArguments, string[] args) => new((ExternalArguments.Contains("--exclude") ? args[Array.IndexOf(args, "--exclude") + 1] : ".git;bin;obj;node_modules;.vs;TestResults").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    }
}
