using FluentAssertions;
using System;
using Xunit;
using xyDocumentor.Core.Helpers;

namespace xyDocumentor.Tests
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
