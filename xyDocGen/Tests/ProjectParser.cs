using Xunit;
using FluentAssertions;
using xyDocGen.Core.Parser;

namespace xyDocGen.Tests
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
