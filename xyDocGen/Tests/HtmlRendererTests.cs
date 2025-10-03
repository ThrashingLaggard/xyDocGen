using Xunit;
using xyDocumentor.Core;
using xyDocumentor.Core.Docs;

public class HtmlRendererTests
{
    [Fact]
    public void Render_Toplevel_Adds_Html_Scaffold()
    {
        var t = new TypeDoc { Name = "MyClass", Kind = "class" };
        var html = HtmlRenderer.Render(t);
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("<html", html);
        Assert.Contains("MyClass", html);
    }

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
