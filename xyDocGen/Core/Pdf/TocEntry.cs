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
        public string? Infos { get; set; }


        public string? Signature { get; set; }     // i.e., generic signature
        
        public string Description { get; set; } // i.e., summary snippet



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
