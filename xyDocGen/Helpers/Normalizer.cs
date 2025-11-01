using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace xyDocumentor.Helpers
{
    internal class Normalizer
    {

        /// <summary>
        /// Normalizes a single format alias (“markdown” → “md”) and lower-cases it.
        /// </summary>
        private static string NormalizeFormatAlias(string f) => string.Equals(f, "markdown", StringComparison.OrdinalIgnoreCase) ? "md" : f.ToLowerInvariant();


        /// <summary>
        /// Splits and normalizes a list of formats (comma/semicolon separated), de-duplicated case-insensitively.
        /// </summary>
        private static List<string> NormalizeFormats(string s) => [.. NormalizeList(s).Select(x => NormalizeFormatAlias(x)).Distinct(StringComparer.OrdinalIgnoreCase)];


        /// <summary>
        /// Splits a list value on "," and ";" while trimming entries and removing empty ones.
        /// Returns an empty list if <paramref name="s"/> is null or whitespace.
        /// </summary>
        private static List<string> NormalizeList(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return [];
            return [.. s.Split([';', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
        }

    }
}
