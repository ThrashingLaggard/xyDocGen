using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using System;
using System.Linq;
using System.Xml.Linq;
using xyDocumentor.Core.Docs;

namespace xyDocumentor.Core.Renderer
{
    public static class PdfRenderer
    {
        public static void RenderToFile(TypeDoc type, string outputPath)
        {
            using var document = new PdfDocument();
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);
            var font = new XFont("Verdana", 10, XFontStyle.Regular);
            double y = 20;

            void RenderType(TypeDoc t, int indent = 0)
            {
                var indentStr = new string(' ', indent * 4);

                void DrawLine(string text)
                {
                    gfx.DrawString(indentStr + text, font, XBrushes.Black, new XRect(20, y, page.Width - 40, page.Height - 20), XStringFormats.TopLeft);
                    y += 14;
                }

                DrawLine($"{t.Kind} {t.DisplayName}");
                DrawLine($"Namespace: {t.Namespace}");
                DrawLine($"Visibility: {t.Modifiers}");
                if (t.Attributes.Any()) DrawLine($"Attributes: {string.Join(", ", t.Attributes)}");
                if (t.BaseTypes.Any()) DrawLine($"Base/Interfaces: {string.Join(", ", t.BaseTypes)}");
                DrawLine($"Source: {t.FilePath}");
                DrawLine($"Description: {t.Summary}");

                void RenderMembers(string title, System.Collections.Generic.List<MemberDoc> members)
                {
                    if (!members.Any()) return;
                    DrawLine($"{title}:");
                    foreach (var m in members)
                    {
                        DrawLine($"- {m.Kind}: {m.Signature}");
                        if (!string.IsNullOrWhiteSpace(m.Summary))
                            DrawLine($"  {m.Summary}");
                    }
                }

                RenderMembers("Constructors", t.Constructors);
                RenderMembers("Methods", t.Methods);
                RenderMembers("Properties", t.Properties);
                RenderMembers("Events", t.Events);
                RenderMembers("Fields", t.Fields);

                foreach (var nested in t.NestedTypes())
                    RenderType(nested, indent + 1);
            }

            RenderType(type);

            document.Save(outputPath);
        }
    }
}
