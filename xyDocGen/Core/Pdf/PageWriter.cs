
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

namespace xyDocumentor.Core.Pdf
{
    /// <summary>
    /// Writes content to a pdf page according to the given RenderContext and PdfTheme
    /// </summary>
    public sealed class PageWriter : IDisposable
    {
        public string Description { get; set; }
        public PdfPage Page { get; private set; }
        public XGraphics Gfx { get; private set; }
        public double Y { get; private set; }
        public RenderContext Ctx { get; }
        public PdfTheme Theme => Ctx.Theme;
        public bool DrawHeaderFooter { get; set; } = true;

        // Optional: override header text (e.g., parent type)
        public string? PageHeaderOverride { get; set; }

        private readonly double _left, _top, _right, _bottom, _contentWidth;

        private readonly XTextFormatter _formatter;

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
            _top = Theme.MarginTop;
            _bottom = page.Height - Theme.MarginBottom;
            _contentWidth = _right - _left;

            Y = _top;

            if (DrawHeaderFooter) DrawHeaderFooterArea();
        }

        public void Spacer(double pt) => Y += pt;

        public void DrawHeading(int level, string text)
        {
            var (font, color, spacing) = level switch
            {
                1 => (Theme.FontH1, Theme.ColorPrimary, 10d),
                2 => (Theme.FontH2, Theme.ColorPrimary, 8d),
                _ => (Theme.FontH3, Theme.ColorDark, 6d),
            };
            EnsureSpace(font, text, lineSpacing: Theme.LineSpacingHeading);
            Gfx.DrawString(text, font, new XSolidBrush(color), new XRect(_left, Y, _contentWidth, Theme.LineHeight(font)), XStringFormats.TopLeft);
            Y += Theme.LineHeight(font) + spacing;
            DrawHairline();
            Spacer(level == 1 ? 8 : 6);
        }

        public void DrawSubheading(string text)
        {
            EnsureSpace(Theme.FontH4, text);
            Gfx.DrawString(text, Theme.FontH4, XBrushes.Black, new XRect(_left, Y, _contentWidth, Theme.LineHeight(Theme.FontH4)), XStringFormats.TopLeft);
            Y += Theme.LineHeight(Theme.FontH4) / 2;
            DrawHairline(alpha: 0.25);
            Spacer(4);
        }

        public void DrawDefinitionList(string title, IEnumerable<(string Key, string Value)> items)
        {
            DrawSubheading(title);
            var keyFont = Theme.FontNormalBold;
            var valFont = Theme.FontNormal;
            double keyWidth = _contentWidth * 0.20;
            double valWidth = _contentWidth - keyWidth;

            foreach (var (k, v) in items)
            {
                var keyRect = new XRect(_left, Y, keyWidth, Theme.LineHeight(keyFont));
                var valRect = new XRect(_left + keyWidth + 6, Y, valWidth - 6, Theme.LineHeight(valFont));

                EnsureSpaceRow(keyFont, k, valFont, v);

                _formatter.DrawString(k, keyFont, XBrushes.Black, keyRect);
                DrawParagraph(v, valRect, valFont);

                Y = Math.Max(Y + Theme.LineHeight(keyFont), valRect.Bottom);
                Spacer(2);
            }
            Spacer(4);
        }

        public void DrawBulletLine(string title, string value)
        {
            EnsureSpace(Theme.FontNormal, $"{title}: {value}");
            var text = $"{title}: {value}";
            DrawParagraph(text, new XRect(_left, Y, _contentWidth, 0), Theme.FontNormal);
            Spacer(4);
        }

        public void DrawParagraph(string text, XFont? font = null)
        {
            font ??= Theme.FontNormal;
            EnsureSpace(font, text);
            DrawParagraph(text, new XRect(_left, Y, _contentWidth, 0), font);
            Spacer(Theme.ParagraphSpacing);
        }

        public void DrawTable(TableColumnSpec[] columns, IEnumerable<string[]> rows)
        {
            // Column widths by ratio
            var widths = CalcColumnPixelWidths(columns.Select(c => c.WidthRatio).ToArray(), _contentWidth);

            // Header
            var headerHeight = Theme.LineHeight(Theme.FontSmallBold);
            EnsureSpace(headerHeight + 4);
            double x = _left;
            for (int c = 0; c < columns.Length; c++)
            {
                var rect = new XRect(x, Y, widths[c], headerHeight);
                Gfx.DrawString(columns[c].Header, Theme.FontSmallBold, XBrushes.Black, rect, XStringFormats.TopLeft);
                x += widths[c] + Theme.TableColGap;
            }
            Y += headerHeight;
            DrawHairline(alpha: 0.5);
            Spacer(4);

            // Rows
            foreach (var row in rows)
            {
                // Wrap each cell to lines
                var wrappedCells = new List<string[]>();
                double rowHeight = 0;

                for (int c = 0; c < columns.Length; c++)
                {
                    var font = columns[c].Font ?? Theme.FontNormal;
                    var cellText = row.Length > c ? row[c] ?? "" : "";
                    var lines = WrapText(cellText, font, widths[c], Gfx);
                    wrappedCells.Add(lines);
                    rowHeight = Math.Max(rowHeight, lines.Length * Theme.LineHeight(font));
                }

                EnsureSpace(rowHeight + 4);

                x = _left;
                for (int c = 0; c < columns.Length; c++)
                {
                    var font = columns[c].Font ?? Theme.FontNormal;
                    var rect = new XRect(x, Y, widths[c], rowHeight);
                    DrawLines(wrappedCells[c], font, rect);
                    x += widths[c] + Theme.TableColGap;
                }
                Y += rowHeight;
                Spacer(4);
                DrawHairline(alpha: 0.1);
                Spacer(2);
            }
        }

        public XRect DrawTocLine(string title, int pageNumber)
        {
            var font = Theme.FontNormal;
            double lineHeight = Theme.LineHeight(font);

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

        /// <summary>
        /// Draws a wrapped TOC entry: leftText (e.g., Title + Signature + Description) 
        /// with hanging indent, page number on the right, dots only on the first line.
        /// </summary>
        /// <param name="leftText"></param>
        /// <param name="pageNumber"></param>
        /// <param name="hangingIndent"></param>
        /// <returns></returns>
        public XRect DrawTocLineWrapped(string leftText, int pageNumber, double? hangingIndent = null)
        {
            var font = Theme.FontNormal;                    // same size as before
            double lineHeight = Theme.LineHeight(font);
            double indent = Math.Max(0, hangingIndent ?? 10); // 10pt hanging indent by default

            string right = pageNumber.ToString();
            double rightWidth = Gfx.MeasureString(right, font).Width + 6;
            double avail = _contentWidth - rightWidth;

            // Word-wrap the left text into lines that fit avail
            var lines = WrapText(leftText, font, avail);

            // Compute dots for the FIRST line only
            double firstLeftWidth = Gfx.MeasureString(lines[0], font).Width;
            double dotWidth = Math.Max(1.0, Gfx.MeasureString(".", font).Width);
            double remaining = Math.Max(0, avail - firstLeftWidth);
            int dotCount = (int)Math.Floor(Math.Max(0, remaining - 1) / dotWidth);
            string dots = dotCount > 0 ? new string('.', dotCount) : string.Empty;

            // Ensure vertical space for all needed lines
            double blockHeight = lines.Count * lineHeight + 2;
            EnsureSpace(blockHeight);

            // Right rect for page number (aligned to the top line)
            var rightRect = new XRect(_left + avail, Y, rightWidth, lineHeight);

            // Draw first line with dots
            var firstRect = new XRect(_left, Y, avail, lineHeight);
            _formatter.DrawString(lines[0] + dots, font, XBrushes.Black, firstRect);
            Gfx.DrawString(right, font, XBrushes.Black, rightRect, XStringFormats.TopRight);

            double yCursor = Y + lineHeight;

            // Draw subsequent lines with a hanging indent (no dots)
            for (int i = 1; i < lines.Count; i++)
            {
                var lr = new XRect(_left + indent, yCursor, avail - indent, lineHeight);
                _formatter.DrawString(lines[i], font, XBrushes.Black, lr);
                yCursor += lineHeight;
            }

            // Overall area (for link annotation)
            var lineRect = new XRect(_left, Y, _contentWidth, lines.Count * lineHeight);
            Y = yCursor + 2; // small padding after the block
            return lineRect;
        }


        public void DrawHairline(double alpha = 0.35)
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
            // Header
            var header = PageHeaderOverride ?? Ctx.CurrentSectionTitle?? (Ctx.Document.Info?.Title ?? "xyDocumentor");
            var headerHeight = Theme.LineHeight(Theme.FontSmall);
            Gfx.DrawString(header, Theme.FontSmall, XBrushes.Gray, new XRect(_left, Theme.PageHeaderTop, _contentWidth, headerHeight), XStringFormats.TopLeft);

            // Footer (page number)
            var pn = Ctx.PageNumber.ToString();
            var footerHeight = Theme.LineHeight(Theme.FontSmall);
            Gfx.DrawString(header, Theme.FontSmall, XBrushes.Gray,new XRect(_left, Theme.PageHeaderTop, _contentWidth, headerHeight), XStringFormats.TopLeft);

            // Move Y below header
            Y = _top;

            // Light rule under header
            var pen = new XPen(XColors.LightGray, 0.5);
            Gfx.DrawString(pn, Theme.FontSmall, XBrushes.Gray,new XRect(_left, _bottom + 6, _contentWidth, footerHeight), XStringFormats.TopRight);

            Y += 6;
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

