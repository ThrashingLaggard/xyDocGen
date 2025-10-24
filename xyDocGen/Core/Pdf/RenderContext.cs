using PdfSharpCore.Pdf;
using xyDocumentor.Core.Pdf;

namespace xyDocumentor.Core.Pdf
{

        // ============================================================
        // Infrastructure: layout, theme, helpers
        // ============================================================

        public class RenderContext
        {
            public PdfDocument Document { get; }
            public PdfTheme Theme { get; }
            public PageWriter Writer { get; set; }

           // public AnchorRegistry Anchors { get; } = new AnchorRegistry();


        public RenderContext(PdfDocument doc, PdfTheme theme)
            {
                Document = doc;
                Theme = theme;
            }

            public PdfPage AddPage()
            {
                var page = Document.AddPage();
                page.Size = PdfSharpCore.PageSize.A4;
                return page;
            }

            public int PageNumber => Document.Pages.Count; // 1-based user expectation
        }



}
