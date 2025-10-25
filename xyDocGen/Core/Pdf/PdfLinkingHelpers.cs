
using PdfSharpCore.Drawing;              // XRect
using PdfSharpCore.Pdf;                  // PdfDocument, PdfPage, PdfRectangle
using PdfSharpCore.Pdf.Annotations;

namespace xyDocumentor.Core.Pdf
{
    /// <summary>
    /// Internal PDF linking helpers (PdfSharpCore).
    /// Coordinates in den Public-APIs sind IMMER im XGraphics-Sinn (Ursprung oben-links).
    /// </summary>
    internal static class PdfLinkingHelpers
    {
        /// <summary>
        /// Add usefull information
        /// </summary>
        public static string Description { get; set; }

        /// <summary>
        /// Wandelt ein oben-links-basiertes Rechteck (X/Y/Width/Height) in ein PdfRectangle um.
        /// </summary>
        public static PdfRectangle ToPdfRect(PdfPage page, double x, double y, double width, double height)
        {
            // PDF: Ursprung unten-links → y invertieren
            double llx = x;
            double lly = page.Height - (y + height);
            double w = width < 0 ? 0 : width;
            double h = height < 0 ? 0 : height;

            if (lly < 0) { h += lly; lly = 0; }         // clamp, falls über Seitenrand
            var rect = new XRect(llx, lly, w, h);
            return new PdfRectangle(rect);              // <- richtiger Ctor in PdfSharpCore
        }

        /// <summary>
        /// Legt auf 'fromPage' einen klickbaren Bereich 'rect' an, der zu 'toPage' scrollt.
        /// 'targetYTopLeft' ist die Ziel-Y-Position im oben-links-Koordinatensystem.
        /// </summary>
        public static void AddInternalLink(PdfPage fromPage, PdfRectangle rect, PdfPage toPage, PdfDocument owningDoc)
        {
            int pageNumber = GetPageNumber1Based(owningDoc, toPage);
            if (pageNumber <= 0)
                throw new System.ArgumentException("Target page does not belong to the provided document.", nameof(toPage));

            fromPage.AddDocumentLink(rect, pageNumber); // PdfSharpCore: (rect, 1-basierter Seitenindex)
        }

        /// <summary>
        /// Liefert die 1-basierte Seitennummer von 'page' in 'doc'.
        /// </summary>
        private static int GetPageNumber1Based(PdfDocument doc, PdfPage page)
        {
            if (doc == null || page == null) return -1;

            // Manche Builds haben page.Owner == doc; wir verlassen uns aber nicht darauf.
            for (int i = 0; i < doc.Pages.Count; i++)
            {
                if (ReferenceEquals(doc.Pages[i], page))
                    return i + 1; // 1-basiert
            }
            return -1;
        }


internal static void AddGoToLink(PdfPage viewPage, double x, double yTop, double width, double height,PdfPage targetPage, double targetYTop)
    {
        // Guard clauses: nothing to do if pages are missing or the rect has no area.
        if (viewPage is null || targetPage is null) return;
        if (width <= 0 || height <= 0) return;

            // PDF uses a bottom-left origin, while your drawing code uses top-left origin.
            // Convert the annotation rectangle from drawing coords (top-down) to PDF coords (bottom-up).
            // viewAnnY = pageHeight - (topY + height)
            double viewPageHeightPt = viewPage.Height.Point;
            double rectY = viewPageHeightPt - (yTop + height);

            // Build the link annotation and set its rectangle (in PDF coordinates).
            var link = new PdfLinkAnnotation
            {
                Rectangle = new PdfRectangle(new XRect(x, rectY, width, height))
            };

            // Build a /Dest array: [ targetPage /FitH top ]
            // For /FitH, 'top' is a bottom-up Y coordinate on the target page.
            // So convert from top-down targetYTop:
            // destTop = targetPageHeight - targetYTop
            double targetPageHeightPt = targetPage.Height.Point;
            double destTop = targetPageHeightPt - targetYTop;

            // Create the destination array in the context of the document that owns viewPage.
            // PdfArray requires a PdfDocument; use viewPage.Owner.
            var dest = new PdfArray(viewPage.Owner);
            dest.Elements.Add(targetPage);           // the target page object
            dest.Elements.Add(new PdfName("/FitH")); // fit horizontally
            dest.Elements.Add(new PdfReal(destTop)); // vertical position (bottom-up)

            // Assign the destination to the annotation.
            link.Elements["/Dest"] = dest;

            // Optionally make the link border invisible (supported in most builds):
            // link.Elements["/Border"] = new PdfArray(viewPage.Owner) {
            //     new PdfReal(0), new PdfReal(0), new PdfReal(0)
            // };

            // Add annotation to the source page so the TOC entry becomes clickable.
            viewPage.Annotations.Add(link);
        }

}
}
