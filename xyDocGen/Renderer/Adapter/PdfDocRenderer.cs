using System.IO;
using xyDocumentor.Docs;
using xyDocumentor.Interfaces;

namespace xyDocumentor.Renderer.Adapter;

namespace xyDocumentor.Renderer.Adapters;

/// <summary>
/// Adapter that renders to a temporary file and returns its content so it fits <see cref="IDocRenderer"/>.
/// </summary>
internal sealed class PdfDocRenderer : IDocRenderer
{
    public string Description { get; set; } = "PDF renderer";
    public string FileExtension => "pdf";
    public string Render(TypeDoc td_Type)
    {
       var tmp = Path.GetTempFileName();
        var pdfPath = Path.ChangeExtension(tmp, ".pdf");
        PdfRenderer.RenderToFile(td_Type, pdfPath);
        return File.ReadAllText(pdfPath);
    }
}