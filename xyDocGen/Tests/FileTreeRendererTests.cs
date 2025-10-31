namespace xyDocumentor.Tests;

using Xunit;
using System.IO;
using System.Text;
using System.Collections.Generic;
using xyDocumentor.Renderer;

/// <summary>
/// Hyper sick tests for the FileTreeRenderer
/// </summary>
public class FileTreeRendererTests
{
    /// <summary>
    /// Test if the renderer produces entries and respects excludes
    /// </summary>
    [Fact]
    public void RenderTree_Produces_Entries_And_Respects_Excludes()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "xyDocGen_Test_" + Path.GetRandomFileName()));
        try
        {
            var subA = Directory.CreateDirectory(Path.Combine(root.FullName, "A"));
            var subB = Directory.CreateDirectory(Path.Combine(root.FullName, "bin")); // excluded
            File.WriteAllText(Path.Combine(root.FullName, "root.txt"), "x");
            File.WriteAllText(Path.Combine(subA.FullName, "a.txt"), "x");
            File.WriteAllText(Path.Combine(subB.FullName, "b.txt"), "x");

            var sb = new StringBuilder();
            var exclude = new HashSet<string>(new [] { "bin", "obj" });

            FileTreeRenderer.RenderTree(root, "", true, sb, exclude);

            var output = sb.ToString();

            Assert.Contains("A", output);
            Assert.Contains("root.txt", output);
            Assert.Contains("├", output); // tree glyph present
            Assert.DoesNotContain("bin", output); // excluded
        }
        finally
        {
            Directory.Delete(root.FullName, true);
        }
    }
}
