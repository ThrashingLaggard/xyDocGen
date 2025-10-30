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

        string md = MarkdownRenderer.Render(td_TestClass, level_: 2);
        Assert.StartsWith("## ", md.TrimStart());
        Assert.Contains("TestClass", md);
    }


    /// <summary>
    /// Check if nested types get rendered right
    /// </summary>
    [Fact]
    public void Render_Includes_Nested_Types()
    {
        // Arrange: build a small nested type tree
        var root = new TypeDoc { Kind = "class", Name = "Outer", Namespace = "Demo" };
        var inner = new TypeDoc { Kind = "class", Name = "Inner", Namespace = "Demo", Parent = "Outer" };
        var deeper = new TypeDoc { Kind = "enum", Name = "E", Namespace = "Demo", Parent = "Outer.Inner" };
        inner.NestedTypes.Add(deeper);
        root.NestedTypes.Add(inner);

        // Act
        var md = MarkdownRenderer.Render(root);

        // Assert
        Assert.Contains("**class** `Outer`", md);
        Assert.Contains("**class** `Outer.Inner`", md);  // nested class
        Assert.Contains("**enum** `Outer.Inner.E`", md); // nested enum

    }
}