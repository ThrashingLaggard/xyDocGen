using Xunit;
using xyDocumentor.Core;
using xyDocumentor.Core.Docs;

public class JsonRendererTests
{
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
