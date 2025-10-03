using Xunit;
using FluentAssertions;
using xyDocumentor.Core.Parser;

namespace xyDocumentor.Tests
{
    public class ProjectParserTests
    {
        [Fact]
        public void ParseProject_Should_ReturnTypes()
        {
            var parser = new ProjectParser(true);
            var result = parser.ParseProject("xyDocGen.csproj");

            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
        }
    }
}
