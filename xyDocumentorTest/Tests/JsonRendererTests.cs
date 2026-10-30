namespace xyDocumentor.Tests;


using Xunit;
using xyDocumentor.Docs;
using xyDocumentor.Renderer;

/// <summary>
/// Great tests for the JsonRenderer
/// </summary>
public class JsonRendererTests
{
    /// <summary>
    /// Check for important section header in rendered stuff
    /// </summary>
    [Fact]
    public void Render_Returns_Json_With_Name_And_Kind()
    {
        var t = new TypeDoc { Name = "MyClass", Kind = "class" };
        var json = JsonRenderer.Render(t);
        Assert.Contains("\"Name\"", json);
        Assert.Contains("MyClass", json);
        Assert.Contains("\"Kind\"", json);
        Assert.Contains("class", json);
    }
}
