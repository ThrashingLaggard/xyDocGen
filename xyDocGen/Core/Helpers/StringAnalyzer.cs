using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace xyDocumentor.Core.Helpers
{
    internal class StringAnalyzer
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
            return new(rootPath, outPath, format, !isPrivate, excludedParts);
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
        /// <param name="args"></param>
        /// <returns></returns>
        public static HashSet<string> GetIgnorableFiles(string[] args) => new(// Nice now its readable, lol
                                                                                                                        (
                                                                                                                            args.Contains("--exclude") ? args[Array.IndexOf(args, "--exclude") + 1] : ".git;bin;obj;node_modules;.vs;TestResults"
                                                                                                                        )
                                                                                                                        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                                                                                                    );



        public static HashSet<string> GetIgnorableFiles(IList<string> args)
        {
            HashSet<string> hs_FilesToIgnore = new();

            if (args.Contains("--exclude"))
            {
                int index = args.IndexOf("--exclude");
                string[] splitIgnoredInput = args[index + 1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                hs_FilesToIgnore = new(splitIgnoredInput);
            }
            else
            {
                string ignoredByDefault = ".git;bin;obj;node_modules;.vs;TestResults";
                string[] splitDefaultIgnorableFiles = ignoredByDefault.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                hs_FilesToIgnore = new(splitDefaultIgnorableFiles);
            }
            return hs_FilesToIgnore;
        }




        /// <summary>
        /// Checks for the --help keyword and if found calls the OutputCommands() method
        /// </summary>
        /// <param name="externalArguments"></param>
        /// <returns>
        /// True if keyword is found 
        /// else returns false
        /// </returns>
        public static async Task<bool> AskForHelp(List<string> externalArguments)
        {
            if (externalArguments.Contains("--help"))
            {
                string commands = await OutputCommands();
                return true;
            }
            return false;
        }


        /// <summary>
        /// Output the commands for this tool into the console
        /// </summary>
        /// <returns></returns>
        private static async Task<string> OutputCommands()
        {
            // Set the string for the ouputing the commands
            string commands =
                "xydocgen        ===      Base command\n" +
                "--help             ===      Output list of commands\n" +
                "--private         ===      Add to ignore unpublic components\n" +
                "--root             ===      Root path, default is 'the current working directory'\n" +
                "--folder          ===      Target folder, default is 'docs'\n" +
                "--subfolder     -->       Allways comes with folder, default is 'api'\n" +
                "--out               ===      Target folder and subfolder together in one\n" +
                "--exclude        ===      Components to ignore, default are: '.git;bin;obj;node_modules;.vs;TestResults' \n" +
                "--format         ===      Specify output flavour, default is 'Markdown'\n";

            // Output asap
            {
                Console.WriteLine(commands);
                Console.Out.Flush();
            }

            return commands;
        }


    }
}
