using System;
using System.Collections.Generic;
using xyDocumentor.Pdf;
using XBrushes = PdfSharpCore.Drawing.XBrushes;
using XFont = PdfSharpCore.Drawing.XFont;
using XRect = PdfSharpCore.Drawing.XRect;
using XStringFormats = PdfSharpCore.Drawing.XStringFormats;

namespace xyDocumentor.Pdf_Layout
{
#nullable enable
    internal class PdfTocLayout
    {
        public PdfTocLayout(PageWriter pageWriter, PdfTextLayout text, PdfPageFlow pageFlow)
        {
            _pw = pageWriter;
            _text = text;
            _pf = pageFlow;
        }

        public PageWriter _pw { get; }
        public PdfTextLayout _text { get; }
        public PdfPageFlow _pf { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="title"></param>
        /// <param name="pageNumber"></param>
        /// <returns></returns>
        public XRect DrawTocLine(string title, int pageNumber)
        {
            XFont? font = _pw.Theme.FontNormal;
            double lineHeight = _pw.Theme.LineHeight(font!);

            // Left and right text parts: title and page number
            string left = $"{title}";
            string right = pageNumber.ToString();

            double rightWidth = _pw.Gfx.MeasureString(right, font).Width + 6; // small padding
            double avail = _pw._contentWidth - rightWidth; // available width for the title
            double leftWidth = _pw.Gfx.MeasureString(left, font).Width;

            if (leftWidth > avail)
            {
                const string ell = "…";
                while (left.Length > 4 && _pw.Gfx.MeasureString(left + ell, font).Width > avail)
                    left = left[..^1];
                left += ell;
                leftWidth = _pw.Gfx.MeasureString(left, font).Width;
            }

            double dotWidth = Math.Max(1.0, _pw.Gfx.MeasureString(".", font).Width);
            double remaining = Math.Max(0, avail - leftWidth);
            int dotCount = (int)Math.Floor(Math.Max(0, remaining - 1) / dotWidth);
            string dots = dotCount > 0 ? new string('.', dotCount) : string.Empty;

            _pf.EnsureSpace(lineHeight);

            var leftRect = new XRect(_pw._left, _pw.Y, avail, lineHeight);
            var rightRect = new XRect(_pw._left + avail, _pw.Y, rightWidth, lineHeight);

            _pw.Gfx.DrawString(left + dots, font, XBrushes.Black, leftRect, XStringFormats.TopLeft);

            _pw.Gfx.DrawString(right, font, XBrushes.Black, rightRect, XStringFormats.TopRight);
      
            var lineRect = new XRect(_pw._left, _pw.Y, _pw._contentWidth, lineHeight);

            _pw.Y += lineHeight + 2;

            return lineRect;
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
            var font = _pw.Theme.FontNormal;
            double lineHeight = _pw.Theme.LineHeight(font!);

            string right = pageNumber.ToString();
            double rightWidth = _pw.Gfx.MeasureString(right, font).Width + 6; // small padding for readability

            double avail = _pw._contentWidth - kindWidth - gap - rightWidth;
            if (avail < 20) avail = 20; 
            var linesArr = PdfTextLayout.WrapText(leftText ?? string.Empty, font!, avail, _pw.Gfx); 
            var lines = linesArr.Length == 0 ? new List<string> { string.Empty } : new List<string>(linesArr);

            double firstLeftWidth = _pw.Gfx.MeasureString(lines[0], font).Width;
            double dotWidth = Math.Max(1.0, _pw.Gfx.MeasureString(".", font).Width);
            double remaining = Math.Max(0, avail - firstLeftWidth);
            int dotCount = (int)Math.Floor(Math.Max(0, remaining - 1) / dotWidth);
            string dots = dotCount > 0 ? new string('.', dotCount) : string.Empty;

            double blockHeight = lines.Count * lineHeight + 2;
            _pf.EnsureSpace(blockHeight);

            var kindRect = new XRect(_pw._left, _pw.Y, kindWidth, lineHeight);

            var kindState = _pw.Gfx.Save();
            _pw.Gfx.IntersectClip(kindRect);

            string kindFitted = _text.FitWithEllipsis(kind ?? string.Empty, font!, Math.Max(1, kindWidth - 1));
            _pw.Gfx.DrawString(kindFitted, font, XBrushes.Gray, kindRect, XStringFormats.TopRight);

            _pw.Gfx.Restore(kindState);

            double textX = _pw._left + kindWidth + gap;
            var firstRect = new XRect(textX, _pw.Y, avail, lineHeight);
            _pw.Gfx.DrawString(lines[0] + dots, font, XBrushes.Black, firstRect, XStringFormats.TopLeft);

            var rightRect = new XRect(textX + avail, _pw.Y, rightWidth, lineHeight);
            _pw.Gfx.DrawString(right, font, XBrushes.Black, rightRect, XStringFormats.TopRight);

            double yCursor = _pw.Y + lineHeight;
            for (int i = 1; i < lines.Count; i++)
            {
                var r = new XRect(textX, yCursor, avail, lineHeight);
                _pw.Gfx.DrawString(lines[i], font, XBrushes.Black, r, XStringFormats.TopLeft);
                yCursor += lineHeight;
            }

            var lineRect = new XRect(_pw._left, _pw.Y, _pw._contentWidth, lines.Count * lineHeight);

            _pw.Y = yCursor + 2;
            return lineRect;
        }
    }
}
