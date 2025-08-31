using FluentAssertions;
using System;
using Xunit;
using xyDocGen.Core.Helpers;

namespace xyDocGen.Tests
{
    public class UtilsTests
    {
        [Fact]
        public void CleanDoc_ShouldRemoveBrackets()
        {
            var input = "<Hello>";
            var result = Utils.CleanDoc(input);
            result.Should().Be("Hello");
        }

       
    }
}
