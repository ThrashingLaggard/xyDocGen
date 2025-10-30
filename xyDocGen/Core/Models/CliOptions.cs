using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace xyDocumentor.Core.Models
{
    /// <summary>
    /// Strongly typed command line options for xyDocumentor.
    /// </summary>
    internal sealed class CliOptions
    {
        public string RootPath { get; init; } = "";
        public string OutPath { get; set; } // Basis (z. B. <repo>/docs oder --out)
        public List<string> Formats { get; set; } = [];
        public List<string> Subfolders { get; set; } = []; // "md" | "html" | "pdf" | "json"

        public Dictionary<string, string> OutputDirs { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        // Rückwärtskompatibel für alten Code:
        public string Format
        {
            get => Formats.FirstOrDefault() ?? "md";
            set
            {
                Formats.Clear();
                if (!string.IsNullOrWhiteSpace(value)) Formats.Add(value);
            }
        }

        public bool IncludeNonPublic { get; init; } = true;
        public HashSet<string> ExcludedParts { get; init; } = DefaultExcludes();

        // Feature flags
        public bool ShowOnly { get; init; } = false; // --show
        public bool BuildIndex { get; init; } = true; // --index
        public bool BuildTree { get; init; } = true; // --tree

        // Meta flags (handled early in Program.cs)
        public bool Help { get; init; } = false; // --help
        public bool Info { get; init; } = false; // --info

        public static HashSet<string> DefaultExcludes() =>
            new([".git", "bin", "obj", "node_modules", ".vs", "TestResults"],
                                StringComparer.OrdinalIgnoreCase);
    }
}
