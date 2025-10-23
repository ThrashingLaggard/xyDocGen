using XFont = PdfSharpCore.Drawing.XFont;

namespace xyDocumentor.Core.Pdf
{ 
        public class TableColumnSpec
        {
            public string Header { get; }
            public double WidthRatio { get; }
            public XFont? Font { get; }

            public TableColumnSpec(string header, double widthRatio, XFont? font = null)
            {
                Header = header; WidthRatio = widthRatio; Font = font;
            }
        }
    }

