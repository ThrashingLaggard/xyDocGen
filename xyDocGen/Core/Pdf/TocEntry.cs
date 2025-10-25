using PdfSharpCore.Pdf;

namespace xyDocumentor.Core.Pdf
{
    /// <summary>
    /// Represents a specific dataset in a content table
    /// </summary>
    public class TocEntry
    {
        /// <summary>
        /// The dataset's name
        /// </summary>
        public string Title { get; set; } = "";
        
        /// <summary>
        /// Add usefull information
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// The number of the page in a pdf document
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// The page itself
        /// </summary>
        public PdfPage? Page { get; set; }
        
        /// <summary>
        /// Value of the Y - coordinate
        /// </summary>
        public double Y { get; set; }
    }

}
