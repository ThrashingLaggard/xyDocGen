using FluentAssertions;

namespace xyDocumentorTests
{
    /// <summary>
    /// Awesome Tests for the even more awesome Utils class
    /// </summary>
    public class UtilsTests
    {

        /// <summary>
        /// Remove XML Tag
        /// </summary>
        [Fact]
        public void CleanDoc_ShouldRemoveTags()
        {
            var input = "<Hello>";
            var result = xyDocumentor.Helpers.Utils.CleanDoc(input);
            result.Should().Be("");
        }

       
    }
}
