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


public class TypeExtractorNestedTests
{
    [Fact]
    public void ProcessMembers_Collects_Nested_Types()
    {
        var code = @"
namespace Demo {
    public class Outer {
        public class Inner {
            public enum E { A, B }
        }
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetCompilationUnitRoot();
        var extractor = new TypeExtractor(includeNonPublic_: false);
        var types = extractor.ProcessMembers(root.Members, null, "Demo.cs");

        // Outer must be present
        var outer = Assert.Single(types, t => t.Name == "Outer");

        // Nested must be present under Outer
        Assert.Contains(outer.NestedTypes, t => t.Name == "Inner");
        var inner = Assert.Single(outer.NestedTypes, t => t.Name == "Inner");
        Assert.Contains(inner.NestedTypes, t => t.Name == "E");
    }
}