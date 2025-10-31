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
    /// <summary>
    /// Create a nre Renderer (Adapter) according to the input string
    /// 
    ///     "md" or "markdown" 
    ///     "html" or "hypertext
    ///     "json" 
    ///     "pdf"
    /// </summary>
    /// <param name="format"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
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