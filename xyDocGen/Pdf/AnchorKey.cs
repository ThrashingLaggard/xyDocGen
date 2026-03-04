namespace xyDocumentor.Pdf
{
#nullable enable
    /// <summary>
    /// Utility for generating the canonical anchor key your pipeline uses consistently.
    /// </summary>
    internal static class AnchorKey
    {
        public static string? Description { get; set; }

        public static string Canonical(string? ns, string displayName)
        {
            string nsPart = string.IsNullOrWhiteSpace(ns) ? "Global (Default)" : ns!;
            return $"{nsPart}.{displayName}";
        }
    }
}
