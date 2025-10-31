using System;
using xyDocumentor.Interfaces;
using xyDocumentor.RendererAdapter;


namespace xyDocumentor.RendererAdapter;

/// <summary>
/// Lightweight factory that maps format strings to <see cref="IDocRenderer"/> adapters.
/// Keeps existing static API intact while enabling composition/DI.
/// </summary>
public static class RendererFactory
{

    public static IDocRenderer Create(string format)=> format?.Trim().ToLowerInvariant() 
        switch
        {
            "md" or "markdown" => new MarkdownDocRenderer(),
            "html" or "hypertext" => new HtmlDocRenderer(),
            "json"             => new JsonDocRenderer(),
            "pdf"              => new PdfDocRenderer(),
            _                  => throw new ArgumentOutOfRangeException(nameof(format), $"Unknown format '{format}'.")
        };
}