using Xunit;
using xyDocGen.Core.Docs;
using xyDocGen.Core.Renderer;

public class MarkdownRendererTests
{
    [Fact]
    public void Render_Root_Uses_Level_Heading()
    {
        var t = new TypeDoc { Name = "MyClass", Kind = "class" };
        var md = MarkdownRenderer.Render(t, level: 2);
        Assert.StartsWith("## ", md.TrimStart());
        Assert.Contains("MyClass", md);
    }
}
