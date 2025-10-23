using XColor = PdfSharpCore.Drawing.XColor;
using XFont = PdfSharpCore.Drawing.XFont;

namespace xyDocumentor.Core.Pdf
{
        public class PdfTheme
        {
            // Margins
            public double MarginLeft { get; init; } = 54;   // 0.75"
            public double MarginRight { get; init; } = 54;
            public double MarginTop { get; init; } = 72;    // 1.0"
            public double MarginBottom { get; init; } = 72;
            public double PageHeaderTop { get; init; } = 36;

            // Spacing
            public double ParagraphSpacing { get; init; } = 6;
            public double TableColGap { get; init; } = 10;
            public double LineSpacingHeading { get; init; } = 1.0;

            // Colors
            public XColor ColorPrimary { get; init; } = XColor.FromArgb(40, 40, 40);
            public XColor ColorDark { get; init; } = XColor.FromArgb(30, 30, 30);

            // Fonts (you can plug a FontResolver for embedding)
            public XFont FontH1 { get; init; }
            public XFont FontH2 { get; init; }
            public XFont FontH3 { get; init; }
            public XFont FontH4 { get; init; }
            public XFont FontNormal { get; init; }
            public XFont FontNormalBold { get; init; }
            public XFont FontSmall { get; init; }
            public XFont FontSmallBold { get; init; }
            public XFont FontMono { get; init; }

            public static PdfTheme CreateDefault()
            {
                // NOTE: If you want guaranteed embedding + Unicode, register a FontResolver (see notes below).
                var h1 = new XFont("Verdana", 18, PdfSharpCore.Drawing.XFontStyle.Bold);
                var h2 = new XFont("Verdana", 14, PdfSharpCore.Drawing.XFontStyle.Bold);
                var h3 = new XFont("Verdana", 12, PdfSharpCore.Drawing.XFontStyle.Bold);
                var h4 = new XFont("Verdana", 11, PdfSharpCore.Drawing.XFontStyle   .Bold);
                var body = new XFont("Verdana", 10, PdfSharpCore.Drawing.XFontStyle.Regular);
                var bodyBold = new XFont("Verdana", 10, PdfSharpCore.Drawing.XFontStyle.Bold);
                var small = new XFont("Verdana", 8, PdfSharpCore.Drawing.XFontStyle.Regular);
                var smallBold = new XFont("Verdana", 8, PdfSharpCore.Drawing.XFontStyle.Bold);
                var mono = new XFont("Consolas", 9.5, PdfSharpCore.Drawing.XFontStyle.Regular);

                return new PdfTheme
                {
                    FontH1 = h1,
                    FontH2 = h2,
                    FontH3 = h3,
                    FontH4 = h4,
                    FontNormal = body,
                    FontNormalBold = bodyBold,
                    FontSmall = small,
                    FontSmallBold = smallBold,
                    FontMono = mono
                };
            }

            public double LineHeight(XFont f) => f.Size * 1.5;
        }
    }
