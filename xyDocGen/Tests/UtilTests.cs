using FluentAssertions;
using System;
using Xunit;
using xyDocumentor.Core.Helpers;

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
            var result = Utils.CleanDoc(input);
            result.Should().Be("Hello");
        }

       
    }
}
