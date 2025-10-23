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
        public string OutPath { get; init; } = "";
        public string Format { get; init; } = "md"; // "md" | "html" | "pdf" | "json"
        public bool IncludeNonPublic { get; init; } = true;
        public HashSet<string> ExcludedParts { get; init; } = DefaultExcludes();

        // Feature flags
        public bool ShowOnly { get; init; } = false; // --show
        public bool BuildIndex { get; init; } = false; // --index
        public bool BuildTree { get; init; } = false; // --tree

        // Meta flags (handled early in Program.cs)
        public bool Help { get; init; } = false; // --help
        public bool Info { get; init; } = false; // --info

        public static HashSet<string> DefaultExcludes() =>
            new HashSet<string>(new[] { ".git", "bin", "obj", "node_modules", ".vs", "TestResults" },
                                StringComparer.OrdinalIgnoreCase);
    }
}
