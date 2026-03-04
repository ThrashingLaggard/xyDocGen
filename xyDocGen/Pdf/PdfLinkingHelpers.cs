
using PdfSharpCore.Drawing;              // XRect
using PdfSharpCore.Pdf;                  // PdfDocument, PdfPage, PdfRectangle
using PdfSharpCore.Pdf.Annotations;
using System.Reflection.Metadata;

namespace xyDocumentor.Pdf
{
    /// <summary>
    /// Internal helper utilities for creating intra-document links in PDFs using PdfSharpCore.
    /// /// <para>
    /// <strong>Coordinate convention:</strong> All public APIs in this helper use the
    /// <em>XGraphics-style</em> coordinate system (origin at the top-left of the page,
    /// +Y going down). PdfSharpCore's low-level PDF objects, however, use the native PDF
    /// coordinate system (origin at the bottom-left, +Y going up). These helpers convert
    /// between the two systems where necessary.
    /// </para>
    /// <para>
    /// This class is <c>internal</c> by design: it centralizes the PDF linking logic
    /// behind a small API surface so the rest of the codebase can remain agnostic of the
    /// PDF coordinate system and annotation details.
    /// </para>
    /// </summary>
    public static class PdfLinkingHelpers
    {

        /// <summary>
        /// Optional free-form description for diagnostics or tooling.
        /// Not used by the PDF runtime logic; safe to leave empty.
        /// </summary>
        public static string Description { get; set; }


        /// <summary>
        /// Converts a top-left based rectangle (x, y, width, height) — i.e., using the same
        /// axis directions as <see cref="PdfSharpCore.Drawing.XGraphics"/> — into a
        /// <see cref="PdfRectangle"/> that uses the native PDF coordinate system
        /// (bottom-left origin).
        /// </summary>
        /// <param name="page">The target <see cref="PdfPage"/> the rectangle belongs to.</param>
        /// <param name="x">Left coordinate in top-left origin space.</param>
        /// <param name="y">Top coordinate in top-left origin space.</param>
        /// <param name="width">Rectangle width in points. Negative values are clamped to 0.</param>
        /// <param name="height">Rectangle height in points. Negative values are clamped to 0.</param>
        /// <returns>
        /// A <see cref="PdfRectangle"/> that represents the same region but in the PDF
        /// bottom-left origin space, clamped to the page bounds where necessary.
        /// </returns>
        public static PdfRectangle ToPdfRect(PdfPage page, double x, double y, double width, double height)
        {
            // In PDF, the origin is at the bottom-left. 
            // The formula is: lly = pageHeight - (top + height).
            double llx = x;
            double lly = page.Height - (y + height);
            

            double w = width < 0 ? 0 : width;
            double h = height < 0 ? 0 : height;

       
            if (lly < 0) 
            { 
                h += lly; 
                lly = 0; 
            }         
            XRect rect = new (llx, lly, w, h);

            return new PdfRectangle(rect);
        }

       /// <summary>
        /// Adds a clickable document link annotation to <paramref name="fromPage"/> for the
        /// given <paramref name="rect"/> (already expressed in the PDF coordinate system).
        /// The link navigates to the specified <paramref name="toPage"/> within the same
        /// <paramref name="owningDoc"/>.
        /// <para>
        /// Note: This overload expects the rectangle to already be a <see cref="PdfRectangle"/>
        /// (i.e., in bottom-left origin space). Use <see cref="ToPdfRect"/> to convert
        /// from top-left coordinates when needed.
        /// </para>
        /// </summary>
       /// <param name="fromPage">The page where the clickable area will be added.</param>
        /// <param name="rect">The clickable area in PDF (bottom-left) coordinates.</param>
        /// <param name="toPage">The page the link navigates to.</param>
        /// <param name="owningDoc">The owning document which must contain <paramref name="toPage"/>.</param>
       ///<returns> </returns>
        public static void AddInternalLink(PdfPage fromPage, PdfRectangle rect, PdfPage toPage, PdfDocument owningDoc)
        {
            
            int pageNumber = GetPageNumber1Based(owningDoc, toPage);
            if (pageNumber <= 0)
            {
                pageNumber = 1;
                throw new System.ArgumentException("Target page does not belong to the provided document.", nameof(toPage));
            }
          
            fromPage.AddDocumentLink(rect, pageNumber);
        }

        /// <summary>
        /// Returns the 1-based page index of <paramref name="page"/> within <paramref name="doc"/>.
        /// <para>
        /// This method does not rely on <c>page.Owner</c> equality to be defensive across
        /// different PdfSharpCore builds; it performs an identity search instead.
        /// </para>
        /// </summary>
        /// <param name="doc">The document that should contain <paramref name="page"/>.</param>
        /// <param name="page">The page whose index is requested.</param>
        /// <returns>The 1-based index if found; otherwise <c>-1</c>.</returns>
        private static int GetPageNumber1Based(PdfDocument doc, PdfPage page)
        {
            if (doc == null || page == null) return -1;

            for (int i = 0; i < doc.Pages.Count; i++)
            {
                if (ReferenceEquals(doc.Pages[i], page))
                    return i + 1; // 1-basiert
            }
            return -1;
        }

    /// <summary>
        /// Creates a clickable rectangular area on <paramref name="viewPage"/> that navigates
       /// to a specific vertical position on <paramref name="targetPage"/>.
      /// <para>
       /// <strong>Input coordinates:</strong> The rectangle (x, yTop, width, height) is given
        /// in top-left origin space (XGraphics-style). This method converts them to the native
        /// PDF coordinate system as required by <see cref="PdfLinkAnnotation"/>.
        /// </para>
        /// <para>
       /// The destination is expressed as a PDF /Dest array with a <c>/FitH</c> destination:
        /// it preserves the horizontal zoom and scrolls vertically to the given top position.
        /// </para>
        /// </summary>
        /// <param name="viewPage">The page that will contain the clickable annotation.</param>
        /// <param name="x">Left coordinate of the clickable area (top-left origin).</param>
        /// <param name="yTop">Top coordinate of the clickable area (top-left origin).</param>
        /// <param name="width">Width of the clickable area in points.</param>
        /// <param name="height">Height of the clickable area in points.</param>
        /// <param name="targetPage">The page to navigate to when the annotation is clicked.</param>
        /// <param name="targetYTop">Target top Y on <paramref name="targetPage"/> (top-left origin).</param>
internal static void AddGoToLink(PdfPage viewPage, double x, double yTop, double width, double height,PdfPage targetPage, double targetYTop)
    {
            
       if (viewPage is null || targetPage is null) return;
        if (width <= 0 || height <= 0) return;

      
           double viewPageHeightPt = viewPage.Height.Point;
            double rectY = viewPageHeightPt - (yTop + height);

   
            var link = new PdfLinkAnnotation
            {
                Rectangle = new PdfRectangle(new XRect(x, rectY, width, height))
            };

           
            double targetPageHeightPt = targetPage.Height.Point;
            double destTop = targetPageHeightPt - targetYTop;

   
            var dest = new PdfArray(viewPage.Owner);
            dest.Elements.Add(targetPage);           
            dest.Elements.Add(new PdfName("/FitH")); 
            dest.Elements.Add(new PdfReal(destTop)); 

            link.Elements["/Dest"] = dest;
            link.Elements["/Dest"] = dest;

            // Optional (uncomment to hide the visible border around the clickable area).
            // Most PDF viewers honor this setting:
            // link.Elements["/Border"] = new PdfArray(viewPage.Owner)
            // {
            //     new PdfReal(0),  // horizontal corner radius
            //     new PdfReal(0),  // vertical corner radius
            //     new PdfReal(0)   // line width (0 = invisible)
            // };

           
            viewPage.Annotations.Add(link);
        }

}
}
