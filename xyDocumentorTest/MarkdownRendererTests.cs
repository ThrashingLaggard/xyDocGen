namespace  xyDocumentorTests;

using Xunit;
using xyDocumentor.Docs;
using xyDocumentor.Renderer;

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
        Assert.Contains("## ", md);
        Assert.Contains("TestType", md);
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
        Assert.Contains("`Outer` **class**", md);
        Assert.Contains("`Outer.Inner` **class**", md);  // nested class
        Assert.Contains("`Outer.Inner.E` **enum**", md); // nested enum

    }
}