using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using Xunit;
using xyDocumentor.Core;
using xyDocumentor.Core.Extractors;

/// <summary>
/// Useless Test
/// </summary>
public class TypeExtractorTests
{
    [Fact]
    public void ExtractsClassName()
    {
        var code = "public class MyClass {}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetCompilationUnitRoot();
        var extractor = new TypeExtractor(true);
        var types = extractor.ProcessMembers(root.Members, null, "MyFile.cs");
        Assert.Single(types);
        Assert.Equal("MyClass", types[0].Name);
    }
}
