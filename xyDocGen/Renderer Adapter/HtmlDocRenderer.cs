using xyDocumentor.Docs;
using xyDocumentor.Interfaces;
using xyDocumentor.Renderer;

namespace xyDocumentor.RendererAdapter;


/// <summary>
/// Adapter for <see cref="HtmlRenderer"/>.
/// </summary>
internal sealed class HtmlDocRenderer : IDocRenderer
{
    public string Description { get; set; } = "HTML renderer";
    public string FileExtension => "html";
    public string Render(TypeDoc td_Type) => HtmlRenderer.Render(td_Type, cssPath: null);
}