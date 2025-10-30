namespace xyDocumentor.Tests;

using Xunit;
using xyDocumentor.Core.Docs;
using xyDocumentor.Core.Renderer;
using System.IO;

/// <summary>
/// Super spectecular unit tests for the pdf renderer
/// </summary>
public class PdfRendererTests
{
    /// <summary>
    /// Check for the creation of PDF files
    /// </summary>
    [Fact]
    public void RenderToFile_Creates_Pdf_File()
    {
        var t = new TypeDoc { Name = "MyClass", Kind = "class" };
        var tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".pdf");
        try
        {
            PdfRenderer.RenderToFile(t, tmp);
            Assert.True(File.Exists(tmp));
            Assert.True(new FileInfo(tmp).Length > 0);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    /// <summary>
    /// Check for thrown exceptions
    /// </summary>
    [Fact]
    public void RenderToFile_DoesNotThrow_With_Nested_Types()
    {
        var root = new TypeDoc { Kind = "class", Name = "Outer", Namespace = "Demo" };
        root.NestedTypes.Add(new TypeDoc { Kind = "class", Name = "Inner", Namespace = "Demo", Parent = "Outer" });

        // If NestedTypes() returns empty, RenderType would miss recursion.
        // We just ensure that rendering runs through with nested present.
        PdfRenderer.RenderToFile(root, "test_output.pdf");
        // Optionally: File.Exists("test_output.pdf") etc.
    }
}
