using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using xyDocumentor.Docs;
using xyDocumentor.Helpers;
using xyDocumentor.Models;
using xyDocumentor.Renderer;

namespace xyDocumentor.CLI
{
    /// <summary>
    /// Centralized helper for CLI runtime operations.
    /// Keeps Program.cs clean and organized.
    /// </summary>
    internal static class CliRuntimeHelper
    {
        /// <summary>
        /// Create or resolve an absolute output directory for a given format.
        /// </summary>
        public static string ResolveFormatDir(CliOptions opt, string fmt)
        {
            if (opt.OutputDirs != null &&
                opt.OutputDirs.TryGetValue(fmt, out var mapped) &&
                !string.IsNullOrWhiteSpace(mapped))
                return mapped;

            return FormatDir(opt.OutPath, fmt);
        }

        /// <summary>
        /// Build directory path for a format, ensuring existence.
        /// </summary>
        public static string FormatDir(string baseOutPath, string fmt)
        {
            baseOutPath ??= Environment.CurrentDirectory;
            fmt = fmt?.Trim().ToLowerInvariant() ?? "unknown";
            var combined = Path.Combine(baseOutPath, fmt);
            Directory.CreateDirectory(combined);
            return Path.GetFullPath(combined);
        }

        /// <summary>
        /// Display README and configuration info for the current run.
        /// </summary>
        public static void ShowRuntimeInfo(CliOptions opt)
        {
            Console.WriteLine("xyDocGen – current configuration:");
            Console.WriteLine($"  Root: {opt.RootPath}");
            Console.WriteLine($"  Out : {opt.OutPath}");
            Console.WriteLine($"  Formats: {string.Join(", ", opt.Formats)}");
            Console.WriteLine($"  Subfolders: {string.Join(", ", opt.Subfolders)}");

            var readmePath = FindReadme(opt.RootPath);
            if (readmePath is not null && File.Exists(readmePath))
            {
                Console.WriteLine("\n--- README.md ---\n");
                Console.WriteLine(File.ReadAllText(readmePath));
            }
            else
            {
                Console.WriteLine("README.md not found near root.");
            }
        }

        /// <summary>
        /// Find the README.md file near the given root or in the current directory.
        /// </summary>
        internal static string FindReadme(string root)
        {
            var candidate1 = Path.Combine(root ?? Environment.CurrentDirectory, "README.md");
            var candidate2 = Path.Combine(Environment.CurrentDirectory, "README.md");
            if (File.Exists(candidate1)) return candidate1;
            if (File.Exists(candidate2)) return candidate2;
            return null;
        }

        /// <summary>
        /// Render project index and tree either to disk or console.
        /// </summary>
        public static async Task RenderIndexAndTree(CliOptions opt, System.Collections.Generic.IEnumerable<TypeDoc> flattened)
        {
            EnsureDominantRootCached(flattened);

            bool writeToDisk = !opt.ShowOnly && !(opt.ShowIndexToConsole || opt.ShowTreeToConsole);

            foreach (var fmt in opt.Formats.Select(f => f.ToLowerInvariant()))
            {
                var formatDir = ResolveFormatDir(opt, fmt);
                if (writeToDisk) Directory.CreateDirectory(formatDir);

                if (opt.BuildIndex || opt.ShowIndexToConsole)
                {
                    var index = await FileTreeRenderer.BuildProjectIndex(flattened, fmt, formatDir, writeToDisk);
                    if (!writeToDisk) Console.WriteLine(index.ToString());
                }

                if (opt.BuildTree || opt.ShowTreeToConsole)
                {
                    var tree = await FileTreeRenderer.BuildProjectTree(new StringBuilder(), fmt, opt.RootPath, formatDir, opt.ExcludedParts, writeToDisk);
                    if (!writeToDisk) Console.WriteLine(tree.ToString());
                }
            }
        }


        // CliRuntimeHelper.cs – in der Klasse CliRuntimeHelper

        /// <summary>
        /// Sorgt dafür, dass der dominante Root-Namespace pro Prozesslauf genau einmal ermittelt
        /// und im Utils-Cache hinterlegt wird (bevor irgendetwas geschrieben/gerendert wird).
        /// </summary>
        public static void EnsureDominantRootCached(System.Collections.Generic.IEnumerable<TypeDoc> allTypes)
        {
            // Löst die Erkennung aus, wenn noch nicht vorhanden:
            var root = Utils.GetDominantRoot(allTypes);
            Utils.PrimeDominantRoot(root);
        }

    }
}
