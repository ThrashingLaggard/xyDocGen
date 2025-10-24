
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
        public static void AddInternalLink(PdfPage fromPage, PdfRectangle rect, PdfPage toPage, double targetYTopLeft)
        {
            // Ziel-Top-Y in PDF-Koordinaten (unten-links)
            double pdfTop = toPage.Height - targetYTopLeft;

            // Destination (XYZ = absolute Position; Zoom 0 = aktuellen Zoom beibehalten)
            var dest = new PdfDestination(toPage)
            {
                Mode = PdfDestinationMode.XYZ,
                Left = 0,
                Top = pdfTop,
                Zoom = 0
            };

            // WICHTIG: PdfLinkAnnotation hat in PdfSharpCore üblicherweise den parameterlosen Ctor.
            var link = new PdfLinkAnnotation
            {
                Rectangle = rect,
                Destination = dest
            };

            fromPage.Annotations.Add(link);
        }
    }
}
