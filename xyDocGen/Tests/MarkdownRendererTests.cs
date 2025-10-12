namespace  xyDocumentor.Tests;

using Xunit;
using xyDocumentor.Core.Docs;
using xyDocumentor.Core.Renderer;

/// <summary>
/// Test for the md renderer
/// </summary>
public class MarkdownRendererTests
{
    /// <summary>
    ///  Yes yes, sure sure
    /// </summary>
    [Fact]
    public void Render_Root_Uses_Level_Heading()
    {
        TypeDoc td_TestClass = new TypeDoc { Name = "TestType", Kind = "class" };

        string md = MarkdownRenderer.Render(td_TestClass, level: 2);
        Assert.StartsWith("## ", md.TrimStart());
        Assert.Contains("TestClass", md);
    }
}
