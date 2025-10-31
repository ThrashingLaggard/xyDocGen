
using PdfSharpCore.Drawing;
using PdfSharpCore.Drawing.Layout;
using PdfSharpCore.Pdf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XBrushes = PdfSharpCore.Drawing.XBrushes;
using XColor = PdfSharpCore.Drawing.XColor;
using XColors = PdfSharpCore.Drawing.XColors;
using XFont = PdfSharpCore.Drawing.XFont;
using XGraphics = PdfSharpCore.Drawing.XGraphics;
using XPen = PdfSharpCore.Drawing.XPen;
using XRect = PdfSharpCore.Drawing.XRect;
using XSolidBrush = PdfSharpCore.Drawing.XSolidBrush;
using XStringFormats = PdfSharpCore.Drawing.XStringFormats;

namespace xyDocumentor.Pdf
{
#nullable enable
    /// <summary>
    /// Writes content to a pdf page according to the given RenderContext and PdfTheme
    /// </summary>
    public sealed class PageWriter : IDisposable
    {
        /// <summary>Add useful infos here </summary>
        public string? Description { get; set; }
        
        /// <summary> PdfPage to write on </summary>
        public PdfPage Page { get; private set; }

        /// <summary> XGrafix element </summary>
        public XGraphics Gfx { get; private set; }
        
        /// <summary> Height value </summary>
        public double Y { get; private set; }
        
        /// <summary> Data storage </summary>
        public RenderContext Ctx { get; }
        
        /// <summary>
        /// Get the pdf theme from the RenderContext
        /// </summary>
        public PdfTheme Theme => Ctx.Theme;

        /// <summary>
        /// Draw Header and Footer
        /// </summary>
        public bool DrawHeaderFooter { get; set; } = true;

        /// <summary>
        /// Optional: override header text (e.g., parent type)
        /// </summary>
        public string? PageHeaderOverride { get; set; }

        internal readonly double _left, _top, _right, _bottom, _contentWidth;

        private readonly XTextFormatter _formatter;

        /// <summary>
        /// Basic constructor
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="page"></param>
        public PageWriter(RenderContext ctx, PdfPage page)
        {
            Ctx = ctx;
            Page = page;
            if (Page.Owner == null)
            {
                Ctx.Document.Pages.Add(Page);
            }
            Gfx = XGraphics.FromPdfPage(page);
            _formatter = new XTextFormatter(Gfx);

            _left = Theme.MarginLeft;
            _right = page.Width - Theme.MarginRight;
            _top = Math.Max(8, Theme.MarginTop);
            _bottom = page.Height - Math.Max(8, Theme.MarginBottom - 24);

            _contentWidth = _right - _left;

            Y = _top;

            if (DrawHeaderFooter) DrawHeaderFooterArea();
        }

        /// <summary>
        /// Define how much space comes between the [lines/...????]
        /// </summary>
        /// <param name="points"></param>
        public void Spacer(double points) => Y += points;

        /// <summary>
        /// Draw the header and write the enclosed text
        /// </summary>
        /// <param name="level"></param>
        /// <param name="text"></param>
        public void DrawHeading(int level, string text)
        {
            var (font, color, spacing) = level switch
            {
                1 => (Theme.FontH1, Theme.ColorPrimary, 10d),
                2 => (Theme.FontH2, Theme.ColorPrimary, 8d),
                _ => (Theme.FontH3, Theme.ColorDark, 6d),
            };
            EnsureSpace(font!, text, lineSpacing: Theme.LineSpacingHeading);
            Gfx.DrawString(text, font, new XSolidBrush(color), new XRect(_left, Y, _contentWidth, Theme.LineHeight(font!)), XStringFormats.TopLeft);
            Y += Theme.LineHeight(font!) + spacing;
            DrawHairline();
            Spacer(level == 1 ? 8 : 6);
        }

        /// <summary>
        /// Draw the sub header and write the txt in it
        /// </summary>
        /// <param name="text"></param>
        public void DrawSubheading(string text)
        {
            EnsureSpace(Theme.FontH4!, text);
            Gfx.DrawString(text, Theme.FontH4, XBrushes.Black, new XRect(_left, Y, _contentWidth, Theme.LineHeight(Theme.FontH4!)), XStringFormats.TopLeft);
            Y += Theme.LineHeight(Theme.FontH4!) / 2;
            DrawHairline(alpha: 0.25);
            Spacer(4);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="title"></param>
        /// <param name="items"></param>
        public void DrawDefinitionList(string title, IEnumerable<(string Key, string Value)> items)
        {
            // Subheading stays as-is
            DrawSubheading(title);

            var keyFont = Theme.FontNormalBold;
            var valFont = Theme.FontNormal;

            // 1) Measure the widest key to auto-size the key column (tight but safe)
            double maxKey = 0;
            foreach (var (k, _) in items)
            {
                var txt = k ?? string.Empty;
                var w = Gfx.MeasureString(txt, keyFont).Width;
                if (w > maxKey) maxKey = w;
            }

            // Padding on the key column so the text doesn't touch the gutter
            const double keyPad = 1.5; // pt (very small – keeps columns visually tight)
                                       // Horizontal space between columns (the "gutter")
            const double gutter = 3.0; // ↓ reduce if you still want it tighter

            // Clamp key column between 12% and 35% of the width, using measured maxKey
            double keyWidth = Math.Min(_contentWidth * 0.35, Math.Max(_contentWidth * 0.12, maxKey + keyPad));
            double valWidth = Math.Max(24, _contentWidth - keyWidth - gutter);


            const double lineHeightFactor = 1.5;// 0.90/ 0.85 /0.95
            double keyLH = Theme.LineHeight(keyFont!) * lineHeightFactor;
            double valLH = Theme.LineHeight(valFont!) * lineHeightFactor;
            double rowLH = Math.Max(keyLH, valLH); // same baseline step for both columns

            foreach (var (k, v) in items)
            {
                string keyText = k ?? string.Empty;
                string valText = v ?? string.Empty;

                // Wrap both sides using the actual column widths
                // (Use the gfx-based WrapText so measurement matches drawing)
                var keyLines = WrapText(keyText, keyFont!, keyWidth, Gfx);
                var valLines = WrapText(valText, valFont!, valWidth, Gfx);

                int linesCount = Math.Max(keyLines.Length, valLines.Length);
                double rowHeight = linesCount * rowLH;

                // Ensure there is enough space for the whole row (keys + values)
                EnsureSpace(rowHeight + 1);

                // --- Draw key column (left, bold) ---
                double yKey = Y;
                for (int i = 0; i < keyLines.Length; i++)
                {
                    var r = new XRect(_left, yKey, keyWidth, rowLH);
                    Gfx.DrawString(keyLines[i], keyFont, XBrushes.Black, r, XStringFormats.TopLeft);
                    yKey += rowLH;
                }

                // --- Draw value column (right, normal) ---
                double xVal = _left + keyWidth + gutter;
                double yVal = Y;
                for (int i = 0; i < valLines.Length; i++)
                {
                    var r = new XRect(xVal, yVal, valWidth, rowLH);
                    Gfx.DrawString(valLines[i], valFont, XBrushes.Black, r, XStringFormats.TopLeft);
                    yVal += rowLH;
                }

                // Advance Y by the full row height, then a tiny gap between rows
                Y += rowHeight;
                Spacer(1); // tighten: was 2
                           // Optional hairline between rows (very subtle):
                           // var pen = new XPen(XColors.Black, 0.2) { Transparency = 0.1 };
                           // Gfx.DrawLine(pen, _left, Y, _right, Y);
                           // Spacer(1);
            }

            // Smaller gap after the whole block
            Spacer(2); // was 4
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="title"></param>
        /// <param name="value"></param>
        public void DrawBulletLine(string title, string value)
        {
            EnsureSpace(Theme.FontNormal!, $"{title}: {value}");
            var text = $"{title}: {value}";
            DrawParagraph(text, new XRect(_left, Y, _contentWidth, 0), Theme.FontNormal!);
            Spacer(4);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text"></param>
        /// <param name="font"></param>
        public void DrawParagraph(string text, XFont? font = null)
        {
            font ??= Theme.FontNormal;
            EnsureSpace(font!, text);
            DrawParagraph(text, new XRect(_left, Y, _contentWidth, 0), font!);
            Spacer(Theme.ParagraphSpacing);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="columns"></param>
        /// <param name="rows"></param>
        public void DrawTable(TableColumnSpec[] columns, IEnumerable<string[]> rows)
        {
            double gap = Theme.TableColGap;
            // Subtract (n-1) gaps from the content width to avoid overflow
            double availableWidth = _contentWidth - (columns.Length - 1) * gap;

            // Column widths by ratio
            var widths = CalcColumnPixelWidths(columns.Select(c => c.WidthRatio).ToArray(), availableWidth);

            // Header
            var headerHeight = Theme.LineHeight(Theme.FontSmallBold!);
            EnsureSpace(headerHeight + 4);
            double x = _left;
            for (int c = 0; c < columns.Length; c++)
            {
                var rect = new XRect(x, Y, widths[c], headerHeight);
                Gfx.DrawString(columns[c].Header, Theme.FontSmallBold, XBrushes.Black, rect, XStringFormats.TopLeft);
                x += widths[c] + gap;
            }
            Y += headerHeight;
            DrawHairline(alpha: 0.5);
            Spacer(4);

            // Rows
            foreach (var row in rows)
            {
                // Wrap each cell to lines
                var wrappedCells = new List<string[]>();
                var innerWidths = new List<double>();

                double rowHeight = 0;

                for (int c = 0; c < columns.Length; c++)
                {
                    var font = columns[c].Font ?? Theme.FontNormal;

                    // Use an *inner* cell width with small left/right padding to prevent touching borders
                    const double cellPad = 2.0;
                    double innerWidth = Math.Max(1, widths[c] - 2 * cellPad);
                    innerWidths.Add(innerWidth);

                    var cellText = row.Length > c ? row[c] ?? "" : "";
                    var lines = WrapText(cellText, font!, innerWidth - 0.5, Gfx);
                    wrappedCells.Add(lines);
                    rowHeight = Math.Max(rowHeight, lines.Length * Theme.LineHeight(font!));
                }

                EnsureSpace(rowHeight + 4);

                x = _left;
                for (int c = 0; c < columns.Length; c++)
                {
                    var font = columns[c].Font ?? Theme.FontNormal;

                    var cellRect = new XRect(x, Y, widths[c], rowHeight);
                    const double cellPad = 2.0;
                    var innerRect = new XRect(cellRect.X + cellPad, cellRect.Y,Math.Max(1, cellRect.Width - 2 * cellPad),cellRect.Height);

                    var state = Gfx.Save();
                    var clipRect = new XRect(innerRect.X - 0.5, innerRect.Y, innerRect.Width + 1.0, innerRect.Height + 0.5);
                    
                    Gfx.IntersectClip(clipRect);
                    DrawLines(wrappedCells[c], font!, innerRect);
                    Gfx.Restore(state);
                    
                    x += widths[c] + gap;
                }
                // Vertical line
                double xx = _left;
                for (int c = 0; c < columns.Length - 1; c++)
                {
                    xx += widths[c];
                    // draw a faint separator in the gap center
                    double sepX = xx + gap / 2.0;
                    var pen = new XPen(XColors.LightGray, 0.3);
                    pen.DashStyle = XDashStyle.Dot;
                    Gfx.DrawLine(pen, sepX, Y, sepX, Y + rowHeight);
                    xx += gap;
                }
                Y += rowHeight;
                Spacer(4);
                DrawHairline(alpha: 0.1);
                Spacer(2);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="title"></param>
        /// <param name="pageNumber"></param>
        /// <returns></returns>
        public XRect DrawTocLine(string title, int pageNumber)
        {
            XFont? font = Theme.FontNormal;
            double lineHeight = Theme.LineHeight(font!);

            // Left and right text parts: title and page number
            string left = $"{title}";
            string right = pageNumber.ToString();

            // Measure widths of both parts
            double rightWidth = Gfx.MeasureString(right, font).Width + 6; // small padding
            double avail = _contentWidth - rightWidth; // available width for the title
            double leftWidth = Gfx.MeasureString(left, font).Width;

            // Ellipsize title if it exceeds available space
            if (leftWidth > avail)
            {
                const string ell = "…";
                while (left.Length > 4 && Gfx.MeasureString(left + ell, font).Width > avail)
                    left = left[..^1];
                left += ell;
                leftWidth = Gfx.MeasureString(left, font).Width;
            }

            // Compute how many dots fit between the title and the page number
            double dotWidth = Math.Max(1.0, Gfx.MeasureString(".", font).Width);
            double remaining = Math.Max(0, avail - leftWidth);
            int dotCount = (int)Math.Floor(Math.Max(0, remaining - 1) / dotWidth);
            string dots = dotCount > 0 ? new string('.', dotCount) : string.Empty;

            // Ensure enough vertical space for the line
            EnsureSpace(lineHeight);

            // Calculate text rectangles
            var leftRect = new XRect(_left, Y, avail, lineHeight);
            var rightRect = new XRect(_left + avail, Y, rightWidth, lineHeight);

            // Draw the left part (title + dots) using the text formatter
            _formatter.DrawString(left + dots, font, XBrushes.Black, leftRect);

            // Draw the right part (page number), right-aligned
            Gfx.DrawString(right, font, XBrushes.Black, rightRect, XStringFormats.TopRight);

            // Define the overall line area (used for clickable TOC links later)
            var lineRect = new XRect(_left, Y, _contentWidth, lineHeight);

            // Move Y down for the next line
            Y += lineHeight + 2;

            // Return the drawn area
            return lineRect;
        }

        /// <summary>
        /// Word-wrap into lines that fit into 'maxWidth'. Keeps words intact.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="font"></param>
        /// <param name="maxWidth"></param>
        /// <returns></returns>
        private List<string> WrapText(string text, XFont font, double maxWidth)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                lines.Add(string.Empty);
                return lines;
            }

            var words = text.Split(' ');
            var line = new StringBuilder();
            foreach (var w in words)
            {
                var candidate = line.Length == 0 ? w : line.ToString() + " " + w;
                if (Gfx.MeasureString(candidate, font).Width <= maxWidth)
                {
                    if (line.Length == 0) line.Append(w); else line.Append(' ').Append(w);
                }
                else
                {
                    if (line.Length > 0) lines.Add(line.ToString());
                    line.Clear();
                    // If single word longer than maxWidth: hard-cut (rare for wide generics)
                    if (Gfx.MeasureString(w, font).Width > maxWidth)
                    {
                        var cut = new StringBuilder();
                        foreach (var ch in w)
                        {
                            var cand2 = cut.ToString() + ch;
                            if (Gfx.MeasureString(cand2, font).Width > maxWidth)
                            {
                                if (cut.Length > 0) lines.Add(cut.ToString());
                                cut.Clear();
                            }
                            cut.Append(ch);
                        }
                        if (cut.Length > 0) line.Append(cut.ToString());
                    }
                    else
                    {
                        line.Append(w);
                    }
                }
            }
            if (line.Length > 0) lines.Add(line.ToString());
            return lines;
        }

        // Fits text into maxWidth by trimming and appending an ellipsis if needed.
        private string FitWithEllipsis(string text, XFont font, double maxWidth)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (Gfx.MeasureString(text, font).Width <= maxWidth) return text;

            const string ell = "…";
            string t = text;
            while (t.Length > 1 && Gfx.MeasureString(t + ell, font).Width > maxWidth)
                t = t[..^1];
            return t + ell;
        }


        /// <summary>
        /// Draws a TOC entry consisting of:
        ///  - a narrow left "kind" column (e.g., "class", "interface"),
        ///  - the main text with dot leaders,
        ///  - the page number aligned on the far right.
        /// It does NOT change any global layout; it only splits the line horizontally.
        /// </summary>
        /// <param name="kind"></param>
        /// <param name="leftText"></param>
        /// <param name="pageNumber"></param>
        /// <param name="kindWidth"></param>
        /// <param name="gap"></param> 
        public XRect DrawTocLineWrapped(string kind, string leftText, int pageNumber, double kindWidth, double gap = 3.0)
        {
            var font = Theme.FontNormal;
            double lineHeight = Theme.LineHeight(font!);

            // Right side (page number)
            string right = pageNumber.ToString();
            double rightWidth = Gfx.MeasureString(right, font).Width + 6; // small padding for readability

            // Available width for the main text between kind column and page number
            double avail = _contentWidth - kindWidth - gap - rightWidth;
            if (avail < 20) avail = 20; // guard against extremely narrow cases

            // Word-wrap the main text into lines that fit the available width
            var linesArr = WrapText(leftText ?? string.Empty, font!, avail, Gfx); // uses your gfx-based wrap
            var lines = linesArr.Length == 0 ? new List<string> { string.Empty } : new List<string>(linesArr);

            // Compute dot leaders for the first line only
            double firstLeftWidth = Gfx.MeasureString(lines[0], font).Width;
            double dotWidth = Math.Max(1.0, Gfx.MeasureString(".", font).Width);
            double remaining = Math.Max(0, avail - firstLeftWidth);
            int dotCount = (int)Math.Floor(Math.Max(0, remaining - 1) / dotWidth);
            string dots = dotCount > 0 ? new string('.', dotCount) : string.Empty;

            // Ensure vertical space for the whole block (all wrapped lines)
            double blockHeight = lines.Count * lineHeight + 2;
            EnsureSpace(blockHeight);

            // 1) Left "kind" column (right-aligned so it stays visually narrow)
            var kindRect = new XRect(_left, Y, kindWidth, lineHeight);

            // Clip to kind cell to guarantee no bleed into the main text
            var kindState = Gfx.Save();
            Gfx.IntersectClip(kindRect);

            string kindFitted = FitWithEllipsis(kind ?? string.Empty, font!, Math.Max(1, kindWidth - 1));
            Gfx.DrawString(kindFitted, font, XBrushes.Gray, kindRect, XStringFormats.TopRight);

            Gfx.Restore(kindState);

            // 2) First text line (title + dots), directly to the right of the kind column
            double textX = _left + kindWidth + gap;
            var firstRect = new XRect(textX, Y, avail, lineHeight);
            _formatter.DrawString(lines[0] + dots, font, XBrushes.Black, firstRect);

            // 3) Page number on the far right, aligned to the first line
            var rightRect = new XRect(textX + avail, Y, rightWidth, lineHeight);
            Gfx.DrawString(right, font, XBrushes.Black, rightRect, XStringFormats.TopRight);

            // 4) Subsequent wrapped lines (no dots), same text column
            double yCursor = Y + lineHeight;
            for (int i = 1; i < lines.Count; i++)
            {
                var r = new XRect(textX, yCursor, avail, lineHeight);
                _formatter.DrawString(lines[i], font, XBrushes.Black, r);
                yCursor += lineHeight;
            }

            // Clickable area for link annotations
            var lineRect = new XRect(_left, Y, _contentWidth, lines.Count * lineHeight);

            // Advance Y for the next TOC line
            Y = yCursor + 2;
            return lineRect;
        }


        internal void DrawHairline(double alpha = 0.35)
        {
            var pen = new XPen(XColor.FromArgb((int)(alpha * 255), 0, 0, 0), 0.5);
            Gfx.DrawLine(pen, _left, Y, _right, Y);
        }

        private void DrawParagraph(string text, XRect rect, XFont font)
        {
            var lines = WrapText(text, font, rect.Width, Gfx);
            var h = lines.Length * Theme.LineHeight(font);
            rect = new XRect(rect.X, rect.Y, rect.Width, h);
            DrawLines(lines, font, rect);
            Y += h;
        }

        private void DrawLines(IEnumerable<string> lines, XFont font, XRect rect)
        {
            double y = rect.Y;
            foreach (var line in lines)
            {
                Gfx.DrawString(line, font, XBrushes.Black, new XRect(rect.X, y, rect.Width, Theme.LineHeight(font)), XStringFormats.TopLeft);
                y += Theme.LineHeight(font);
            }
        }

        private void DrawHeaderFooterArea()
        {
            // 1) Create locally smaller fonts (actual glyph size down) without changing PdfTheme
            var baseHeader = Theme.FontSmall;
            var baseFooter = Theme.FontSmall;

            // scale ~85%; clamp to a sane minimum
            double headerSize = Math.Max(6.0, baseHeader!.Size * 0.85);
            double footerSize = Math.Max(6.0, baseFooter!.Size * 0.85);

            var headerFont = new XFont(baseHeader.FontFamily.Name, headerSize, baseHeader.Style);
            var footerFont = new XFont(baseFooter.FontFamily.Name, footerSize, baseFooter.Style);

            double headerLH = Theme.LineHeight(headerFont); // line height for the smaller font
            double footerLH = Theme.LineHeight(footerFont);

            // 2) Draw header inside the top margin so it doesn't steal content height.
            // Place the header so its bottom sits just a couple of points above _top.
            double headerBottom = _top - 2;                 // just above content area
            double headerTop = Math.Max(6, _top - 2 - headerLH);  // header box top

            string header = PageHeaderOverride
                            ?? Ctx.CurrentSectionTitle
                            ?? (Ctx.Document.Info?.Title ?? "xyDocumentor");

            var headerRect = new XRect(_left, headerTop, _contentWidth, headerLH);
            Gfx.DrawString(header, headerFont, XBrushes.Gray, headerRect, XStringFormats.TopLeft);

            // Thin rule right above content
            var pen = new XPen(XColors.LightGray, 0.4);
            Gfx.DrawLine(pen, _left, _top - 2, _right, _top - 2);
            
            // IMPORTANT: Do NOT push Y down — keep content starting at _top (no hidden spacer)
            Y = _top;

            // 3) Draw footer (page number) just inside the bottom margin, compact
            string pn = Ctx.PageNumber.ToString();

            // Place the baseline a couple of points inside the bottom margin area
            double pageHeight = Page.Height;                    // in points (consistent with XRect usage)
            var footerRect = new XRect(_left, pageHeight - footerLH - 2, _contentWidth, footerLH);
            Gfx.DrawString(pn, footerFont, XBrushes.Gray, footerRect, XStringFormats.BottomRight);
        }


        private void EnsureSpace(XFont font, string text, double lineSpacing = 1.0)
        {
            var lines = WrapText(text, font, _contentWidth, Gfx);
            var needed = lines.Length * Theme.LineHeight(font) * lineSpacing + 2;
            EnsureSpace(needed);
        }

        private void EnsureSpaceRow(XFont f1, string t1, XFont f2, string t2)
        {
            var l1 = WrapText(t1, f1, _contentWidth * 0.20, Gfx).Length * Theme.LineHeight(f1);
            var l2 = WrapText(t2, f2, _contentWidth * 0.80, Gfx).Length * Theme.LineHeight(f2);
            EnsureSpace(Math.Max(l1, l2) + 4);
        }

        private void EnsureSpace(double heightNeeded)
        {
            if (Y + heightNeeded <= _bottom) return;

            // New page
            Page = Ctx.AddPage();
            Gfx.Dispose();
            Gfx = XGraphics.FromPdfPage(Page);

            Y = _top;
            if (DrawHeaderFooter) DrawHeaderFooterArea();
        }

        /// <summary> Dispose of the XGraphix instance in the Gfx property </summary>
        public void Dispose()
        {
            Gfx?.Dispose();
        }

        private static double[] CalcColumnPixelWidths(double[] ratios, double totalWidth)
        {
            var sum = ratios.Sum();
            return ratios.Select(r => Math.Max(30, totalWidth * (r / sum))).ToArray();
        }

        private static string[] WrapText(string text, XFont font, double width, XGraphics gfx)
        {
            if (string.IsNullOrEmpty(text)) return new[] { "" };
            var words = text.Replace("\r", "").Split('\n')
                            .SelectMany(line => line.Split(' ').DefaultIfEmpty(""))
                            .ToArray();

            var lines = new List<string>();
            var sb = new StringBuilder();
            foreach (var w in words)
            {
                var probe = sb.Length == 0 ? w : sb + " " + w;
                var size = gfx.MeasureString(probe, font).Width;
                if (size > width && sb.Length > 0)
                {
                    lines.Add(sb.ToString());
                    sb.Clear();
                    sb.Append(w);
                }
                else
                {
                    if (sb.Length == 0) sb.Append(w);
                    else sb.Append(' ').Append(w);
                }
            }
            if (sb.Length > 0) lines.Add(sb.ToString());
            return lines.Count == 0 ? new[] { "" } : lines.ToArray();
        }
        
     // when you need a new page during layout:
        private void NewPage()
        {
            // Create a brand new page owned by this document
            var next = Ctx.Document.AddPage();
            next.Size = PdfSharpCore.PageSize.A4;

            // swap graphics to the new page
            Gfx.Dispose();
            Page = next;
            Gfx = XGraphics.FromPdfPage(Page);
            // reset Y / header/footer as you already do...
        }
    }
    }

