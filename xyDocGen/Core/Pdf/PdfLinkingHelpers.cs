
using PdfSharpCore.Drawing;              // XRect
using PdfSharpCore.Pdf;                  // PdfDocument, PdfPage, PdfRectangle
using PdfSharpCore.Pdf.Actions;          // PdfGoToAction
using PdfSharpCore.Pdf.Advanced;         // PdfDestination, PdfDestinationMode
using PdfSharpCore.Pdf.Annotations;      // PdfLinkAnnotation

namespace xyDocumentor.Core.Pdf
{
    /// <summary>
    /// Internal PDF linking helpers (PdfSharpCore).
    /// Coordinates in den Public-APIs sind IMMER im XGraphics-Sinn (Ursprung oben-links).
    /// </summary>
    internal static class PdfLinkingHelpers
    {
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
    }
}
