namespace xyDocumentor.Core.Helpers;

using System.IO;

internal static class OutputPaths
{
    public static string FormatDir(string outRoot, string format) =>
        Path.Combine(outRoot, format.ToLowerInvariant());

    // Index/Tree JE FORMAT innerhalb des jeweiligen Format-Ordners
    public static string IndexPath(string outRoot, string formatExt) =>
        Path.Combine(FormatDir(outRoot, formatExt), $"index.{formatExt.ToLowerInvariant()}");

    public static string TreePath(string outRoot, string formatExt) =>
        Path.Combine(FormatDir(outRoot, formatExt), $"tree.{formatExt.ToLowerInvariant()}");
}
