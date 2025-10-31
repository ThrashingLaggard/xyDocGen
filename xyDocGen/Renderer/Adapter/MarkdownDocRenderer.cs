using xyDocumentor.Docs;
using xyDocumentor.Interfaces;

namespace xyDocumentor.Renderer.Adapter;

/// <summary>
/// Adapter to expose the existing static <see cref="MarkdownRenderer"/> via <see cref="IDocRenderer"/>.
/// This allows future DI without breaking current static API or tests.
/// </summary>
internal sealed class MarkdownDocRenderer : IDocRenderer
{
    public string Description { get; set; } = "Markdown renderer";
    public string FileExtension => "md";
    public string Render(TypeDoc td_Type) => MarkdownRenderer.Render(td_Type);
}

