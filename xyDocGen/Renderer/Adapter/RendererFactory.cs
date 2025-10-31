using System;
using xyDocumentor.Interfaces;
using xyDocumentor.Renderer.Adapter.xyDocumentor.Renderer.Adapters;

namespace xyDocumentor.Renderer.Adapter;

/// <summary>
/// Lightweight factory that maps format strings to <see cref="IDocRenderer"/> adapters.
// Keeps existing static API intact while enabling composition/DI.
/// </summary>
public static class RendererFactory
{
    public static IDocRenderer Create(string format)=> format?.Trim().ToLowerInvariant() 
        switch
        {
            "md" or "markdown" => new MarkdownDocRenderer(),
            "html"             => new HtmlDocRenderer(),
            "json"             => new JsonDocRenderer(),
            "pdf"              => new PdfDocRenderer(),
            _                  => throw new ArgumentOutOfRangeException(nameof(format), $"Unknown format '{format}'.")
        };
}