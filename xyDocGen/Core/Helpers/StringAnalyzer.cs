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
            string rootPath = GetStartingPath(args);
            string outPath = GetOutputPath(externalArguments, args, rootPath);
            string format = GetFormat(externalArguments, args);

            bool isPrivate = IsPrivate(externalArguments);
            HashSet<string> excludedParts = GetIgnorableFiles(args);
            return new(rootPath, outPath, format, !isPrivate, excludedParts);
        }


        /// <summary>
        /// Define the path to start the workflow from
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static string GetStartingPath(string[] args)
        {
            const string flag = "--root";
            int index = Array.IndexOf(args, flag);

            if (index >= 0 && index + 1 < args.Length)
            {
                return args[index + 1];
            }

// Das ist so ziemlich das intelligenteste, was mir jemals eingefallen ist, lol
#if DEBUG
            
            // Stringeling!
            string projectFolder = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetCurrentDirectory().ToString()).ToString()).ToString()).ToString();
           
#else

            // Weirdly once worked as intended in debug mode...
            string projectFolder = Environment.CurrentDirectory;

#endif

            return projectFolder;
        }


        /// <summary>
        /// Checking the CLI arguments for specific target location, or setting default
        /// </summary>
        /// <param name="externalArguments"></param>
        /// <param name="args"></param>
        /// <param name="rootpath"></param>
        /// <returns></returns>
        public static string GetOutputPath(List<string> externalArguments, string[] args, string rootpath)
        {
            // 1. Check for the single '--out' flag which overrides all other path settings.
            const string outFlag = "--out";
            int outIndex = Array.IndexOf(args, outFlag);
            if (outIndex >= 0 && outIndex + 1 < args.Length)
            {
                return args[outIndex + 1];
            }

            // 2. Determine default or user-defined segments.
            string folderName = "docs"; // Default folder name
            string subfolderName = "api"; // Default subfolder name

            // Check for --folder
            const string folderFlag = "--folder";
            int folderIndex = Array.IndexOf(args, folderFlag);
            if (folderIndex >= 0 && folderIndex + 1 < args.Length)
            {
                folderName = args[folderIndex + 1];
            }

            // Check for --subfolder
            const string subfolderFlag = "--subfolder";
            int subfolderIndex = Array.IndexOf(args, subfolderFlag);
            if (subfolderIndex >= 0 && subfolderIndex + 1 < args.Length)
            {
                subfolderName = args[subfolderIndex + 1];
            }

            // Combine the final path: rootpath/folderName/subfolderName
            return Path.Combine(rootpath, folderName, subfolderName);
        }


        /// <summary>
        ///  Determines the output format 
        ///  
        /// pdf/html/json
        /// 
        /// standard is md
        /// 
        /// </summary>
        /// <param name="externalArguments"></param>
        /// <param name="args"></param>
        /// <returns>"..." or default "md"</returns>
        public static string GetFormat(List<string> externalArguments, string[] args)
        {
            const string flag = "--format";
            int index = Array.IndexOf(args, flag);

            if (index >= 0 && index + 1 < args.Length)
            {
                return args[index + 1].ToLower();
            }

            return "md"; // Default: Markdown
        }


        /// <summary>
        /// Checks how to handle non public data, looks for the --private keyword
        /// </summary>
        /// <param name="externalArguments"></param>
        /// <returns>
        /// TRUE if "--private" 
        ///           else FALSE
        /// </returns>
        public static bool IsPrivate(List<string> externalArguments) => externalArguments.Contains("--private");


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
        public static HashSet<string> GetIgnorableFiles(IList<string> args)
        {
            // The default list of directories to exclude
            const string defaultExcludes = ".git;bin;obj;node_modules;.vs;TestResults";
            const string flag = "--exclude";

            // 1. Determine the argument (Default or CLI value)
            string arguments = args.Contains(flag)
                // Check index safety
                ? (args.IndexOf(flag) + 1 < args.Count ? args[args.IndexOf(flag) + 1] : defaultExcludes)
                : defaultExcludes;

            // 2. Split and return as HashSet
            return new HashSet<string>(SplitString(arguments, ';'));
        }


        /// <summary>
        /// 
        /// Does the same as GetIgnorableFiles() but without using the ternary operator and with the creation of some new variables for better debugging...:
        /// 
        /// Collects folder names that should be excluded 
        /// Default exclusion list can be overridden via --exclude.
        /// 
        /// standard fallback: 
        /// .git;bin;obj;node_modules;.vs;TestResults
        /// 
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static HashSet<string> GetIgnorableFilesDebug(IList<string> args)
        {
            // All arguments for this keyword in one string 
            string arguments = "";
            // All arguments from the string have their own entry in here
            IEnumerable<string> splitArguments = [];
            // Stores the unwanted files
            HashSet<string> hs_FilesToIgnore = [];

            // If the keyword is found
            if (!args.Contains("--exclude"))
            {
                // default arguments
                arguments = ".git;bin;obj;node_modules;.vs;TestResults";
            }
            else
            {
                // Check for its index and take the following
                int indexOfArguments = args.IndexOf("--exclude") + 1;

                // Read the string at that positon
                arguments = args[indexOfArguments];
            }
            // Split up the string for better use
            splitArguments = SplitString(arguments);

            // Add the arguments to the hashset to return them as results!
            hs_FilesToIgnore = new(splitArguments);
            return hs_FilesToIgnore;
        }

        private static HashSet<string> GetIgnorableFilesShorterDebug(IList<string> args)
        {
            // All arguments for this keyword in one string 
            string arguments = "";

            // If the keyword is found
            if (!args.Contains("--exclude"))
            {
                // Get the default arguments
                arguments = ".git;bin;obj;node_modules;.vs;TestResults";
            }
            else
            {
                // Check for its index and take the following
                int indexOfArguments = args.IndexOf("--exclude") + 1;

                // Read the string at that positon
                arguments = args[indexOfArguments];
            }
            // Split up the string for better use and add the arguments to the hashset to return them as results!
            return new HashSet<string>(SplitString(arguments));
        }
        private static HashSet<string> GetIgnorableFilesEvenShorterDebug(IList<string> args)
        {
            // All arguments for this keyword in one string 
            string arguments = !args.Contains("--exclude") ? ".git;bin;obj;node_modules;.vs;TestResults" : args[args.IndexOf("--exclude") + 1];

            //  Split up the string for better use 
            IEnumerable<string> splitArguments = SplitString(arguments);
            
            //Add the arguments to the hashset to return them as results!
            return new HashSet<string>(splitArguments);
        }



        private static IEnumerable<string> SplitString(string target, char separator = ';') => target.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);


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

        public static async Task<bool> AskForTree(List<string> externalArguments)
        {
            if (externalArguments.Contains("--tree"))
            {
                // Irgendwie den tree rendern
                return true;
            }
            return false;
        }

        public static async Task<bool> AskForIndex(List<string> externalArguments)
        {
            if (externalArguments.Contains("--index"))
            {
                // Irgendwie den index rendern
                return true;
            }
            return false;
        }

        public static async Task<bool> ShowOnly(List<string> externalArguments)
        {
            if (externalArguments.Contains("--show"))
            {
                // Ergebnisse nur in der Konsole ausgeben
                return true;
            }
            return false;
        }

        public static async Task<bool> AskForInformation(List<string> externalArguments)
        {
            if (externalArguments.Contains("--info"))
            {
                string commands = await OutputInformation();
                return true;
            }
            return false;
        }

        private static Task<string> OutputInformation()
        {
            // Set the string for the ouputing the commands
            string information = "\nInfo for xyDocGen:\nCurrently im working on expanding the parameters, after that ill work through the pdf renderer";

            // Output asap
            {
                Console.WriteLine(information);
                Console.Out.Flush();
            }

            return Task.FromResult(information);
        }


        /// <summary>
        /// Output the commands for this tool into the console
        /// </summary>
        /// <returns></returns>
        private static Task<string> OutputCommands()
        {
            
            string commands =
                "xydocgen        ===      Base command\n" +
        //__________ignoring all other parameters_______________
                "--help             ===      Output list of commands\n" +
                "--info              ===       Information regarding current problems, fixes and updates\n"+
        //_______________________________________________
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

            return Task.FromResult(commands);
        }

        internal static async Task<bool> AskForPrint(List<string> listedArguments_)
        {
            throw new NotImplementedException();
        }

        internal static async Task<bool> AskForPrintOnly(List<string> listedArguments_)
        {
            throw new NotImplementedException();
        }
    }
}
