using PdfSharpCore.Fonts;
using xyDocumentor.Core.Fonts;
using XColor = PdfSharpCore.Drawing.XColor;
using XFont = PdfSharpCore.Drawing.XFont;

namespace xyDocumentor.Core.Pdf
{
    /// <summary>
    /// Visual theme for PDF output (margins, spacing, colors, fonts).
    /// Fonts are resolved by your IFontResolver (e.g., AutoResourceFontResolver),
    /// so font family names must match the resolver's logical families.
    /// </summary>
    public class PdfTheme
    {

        static PdfTheme()
        {
            if (GlobalFontSettings.FontResolver == null)
                GlobalFontSettings.FontResolver = new AutoResourceFontResolver();
        }

        // Margins
        public double MarginLeft { get; init; } = 54;  // 0.75"
        public double MarginRight { get; init; } = 54;
        public double MarginTop { get; init; } = 72;  // 1.0"
        public double MarginBottom { get; init; } = 72;
        public double PageHeaderTop { get; init; } = 36;

        // Spacing
        public double ParagraphSpacing { get; init; } = 6;
        public double TableColGap { get; init; } = 10;
        public double LineSpacingHeading { get; init; } = 1.0;

        // Colors
        public XColor ColorPrimary { get; init; } = XColor.FromArgb(40, 40, 40);
        public XColor ColorDark { get; init; } = XColor.FromArgb(30, 30, 30);

        // Fonts (resolved by the global IFontResolver)
        public XFont FontH1 { get; init; }
        public XFont FontH2 { get; init; }
        public XFont FontH3 { get; init; }
        public XFont FontH4 { get; init; }
        public XFont FontNormal { get; init; }
        public XFont FontNormalBold { get; init; }
        public XFont FontSmall { get; init; }
        public XFont FontSmallBold { get; init; }
        public XFont FontMono { get; init; }

        /// <summary>
        /// Creates a default theme using logical families provided by the resolver:
        ///   - "XY Sans" for text/headings
        ///   - "XY Mono" for code
        /// </summary>
        public static PdfTheme CreateDefault()
        {
            const string Sans = xyDocumentor.Core.Fonts.AutoResourceFontResolver.FamilySans; // "XY Sans"
            const string Mono = xyDocumentor.Core.Fonts.AutoResourceFontResolver.FamilyMono; // "XY Mono"

            System.Diagnostics.Debug.WriteLine("Resolver=" + (PdfSharpCore.Fonts.GlobalFontSettings.FontResolver?.GetType().FullName ?? "null"));

            // NOTE: keep it simple—no system font names like "Verdana"/"Consolas".
            var h1 = new XFont(Sans, 18, PdfSharpCore.Drawing.XFontStyle.Bold);
            var h2 = new XFont(Sans, 14, PdfSharpCore.Drawing.XFontStyle.Bold);
            var h3 = new XFont(Sans, 12, PdfSharpCore.Drawing.XFontStyle.Bold);
            var h4 = new XFont(Sans, 11, PdfSharpCore.Drawing.XFontStyle.Bold);
            var body = new XFont(Sans, 10, PdfSharpCore.Drawing.XFontStyle.Regular);
            var bodyBold = new XFont(Sans, 10, PdfSharpCore.Drawing.XFontStyle.Bold);
            var small = new XFont(Sans, 8, PdfSharpCore.Drawing.XFontStyle.Regular);
            var smallBold = new XFont(Sans, 8, PdfSharpCore.Drawing.XFontStyle.Bold);
            var mono = new XFont(Mono, 9.5, PdfSharpCore.Drawing.XFontStyle.Regular);

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

        /// <summary>
        /// Returns a simple line-height estimate for the given font.
        /// </summary>
        public double LineHeight(XFont f) => f.Size * 1.5;
    }
}
