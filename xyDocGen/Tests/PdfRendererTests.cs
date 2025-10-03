using Xunit;
using xyDocumentor.Core.Docs;
using xyDocumentor.Core.Renderer;
using System.IO;

public class PdfRendererTests
{
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
}
