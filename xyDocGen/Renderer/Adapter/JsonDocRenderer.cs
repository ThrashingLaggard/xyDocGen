using xyDocumentor.Docs;
using xyDocumentor.Interfaces;

namespace xyDocumentor.Renderer.Adapter;

namespace xyDocumentor.Renderer.Adapters;

internal sealed class JsonDocRenderer : IDocRenderer
{
    public string Description { get; set; } = "JSON renderer";
    public string FileExtension => "json";
    public string Render(TypeDoc td_Type) => JsonRenderer.Render(td_Type);
}