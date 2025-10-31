using Xunit;
using FluentAssertions;
using xyDocumentor.Parser;

namespace xyDocumentor.Tests
{
#nullable enable

    /// <summary>
    /// Very nice tests for the project parser
    /// </summary>
    public class ProjectParserTests
    {
        /// <summary>
        /// Check if the parser returns types
        /// </summary>
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
