using xyDocumentor.Docs;
using xyDocumentor.Interfaces;
using xyDocumentor.Renderer;

namespace xyDocumentor.RendererAdapter;

internal sealed class JsonDocRenderer : IDocRenderer
{
    public string Description { get; set; } = "JSON renderer";
    public string FileExtension => "json";
    public string Render(TypeDoc td_Type) => JsonRenderer.Render(td_Type);
}