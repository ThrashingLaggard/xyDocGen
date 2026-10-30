namespace xyDocumentor.Tests;

using Xunit;
using xyDocumentor.Docs;
using xyDocumentor.Renderer;

/// <summary>
/// Critically acclaimed tests for the html renderer
/// </summary>
public class HtmlRendererTests
{
    /// <summary>
    /// Check if  html scaffolding gets added to the top level
    /// </summary>
    [Fact]
    public void Render_Toplevel_Adds_Html_Scaffold()
    {
        var t = new TypeDoc { Name = "MyClass", Kind = "class" };
        var html = HtmlRenderer.Render(t);
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("<html", html);
        Assert.Contains("MyClass", html);
    }

    /// <summary>
    /// Check if nested typed render their own html scaffold
    /// </summary>
    [Fact]
    public void Render_Nested_Does_Not_Add_Html_Scaffold()
    {
        var t = new TypeDoc { Name = "NestedClass", Kind = "class" };
        var html = HtmlRenderer.Render(t, null, true);
        Assert.DoesNotContain("<html", html);
        Assert.DoesNotContain("<head", html);
        Assert.Contains("NestedClass", html);
    }
}
