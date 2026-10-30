using FluentAssertions;
using Xunit.v3;
using xyDocumentor.Helpers;

namespace xyDocumentor.Tests
{
    /// <summary>
    /// Awesome Tests for the even more awesome Utils class
    /// </summary>
    public class UtilsTests
    {

        /// <summary>
        /// Check for any left brackets
        /// </summary>
        [Fact]
        public void CleanDoc_ShouldRemoveBrackets()
        {
            var input = "<Hello>";
            var result = xyDocumentor.Helpers.Utils.CleanDoc(input);
            result.Should().Be("Hello");
        }

       
    }
}
