using PdfSharpCore.Pdf;
using System;
using System.Collections.Generic;
using System.Linq;
using xyDocumentor.Core.Docs;
using xyDocumentor.Core.Pdf;

namespace xyDocumentor.Core.Renderer
{
    /// <summary>
    /// A layouted, multi-page PDF renderer with headings, TOC, member tables, bookmarks,
    /// wrapping, and automatic page breaks. Minimal dependencies: PdfSharpCore only.
    /// </summary>
    public static partial class PdfRenderer
    {
        // -----------------------------
        // Public entry point
        // -----------------------------
        public static void RenderToFile(TypeDoc root, string outputPath)
        {
            using var document = new PdfDocument();

            var theme = PdfTheme.CreateDefault();
            var ctx = new RenderContext(document, theme);

            // Reserve TOC page as the very first page (we fill it after content is rendered)
            var tocPage = ctx.AddPage();
            ctx.Writer.DrawHeaderFooter = false; // disable on TOC page for a clean look

            // Collect TOC entries while rendering
            var tocEntries = new List<TocEntry>();

            // Render content starting on a new page
            ctx.Writer = new PageWriter(ctx, ctx.AddPage());

            // Root heading + bookmark
            AddBookmark(document, ctx.Writer.Page, $"{root.DisplayName} ({root.Kind})");
            tocEntries.Add(new TocEntry { Title = $"{root.DisplayName} ({root.Kind})", PageNumber = ctx.PageNumber });

            RenderTypeRecursive(ctx, root, level: 1, tocEntries);

            // Render TOC at the beginning (now we know page numbers)
            ctx.Writer = new PageWriter(ctx, tocPage);
            ctx.Writer.DrawHeaderFooter = false;
            RenderToc(ctx, "Table of Contents", tocEntries);

            // Re-enable header/footer for all other pages (already drawn per page).
            document.Save(outputPath);
        }

        // -----------------------------
        // Core type rendering
        // -----------------------------
        private static void RenderTypeRecursive(RenderContext ctx, TypeDoc t, int level, List<TocEntry> toc)
        {
            // Heading
            ctx.Writer.DrawHeading(level, $"{t.DisplayName}  [{t.Kind}]");
            ctx.Writer.Spacer(6);

            // Metadata block
            var meta = new[]
            {
                ("Namespace", string.IsNullOrWhiteSpace(t.Namespace) ? "Global (Default)" : t.Namespace),
                ("Visibility", string.IsNullOrWhiteSpace(t.Modifiers) ? "(n/a)" : t.Modifiers),
                ("Source", t.FilePath ?? "(n/a)")
            };
            ctx.Writer.DrawDefinitionList("Metadata", meta);

            // Attributes / Base
            if (t.Attributes.Any())
                ctx.Writer.DrawBulletLine("Attributes", string.Join(", ", t.Attributes));
            if (t.BaseTypes.Any())
                ctx.Writer.DrawBulletLine("Base/Interfaces", string.Join(", ", t.BaseTypes));

            ctx.Writer.Spacer(4);

            // Summary
            var summaryText = string.IsNullOrWhiteSpace(t.Summary) ? "(No description available)" : t.Summary.Trim();
            ctx.Writer.DrawSubheading("Description");
            ctx.Writer.DrawParagraph(summaryText);

            // Members table(s)
            RenderMembers(ctx, "Constructors", t.Constructors);
            RenderMembers(ctx, "Properties", t.Properties);
            RenderMembers(ctx, "Methods", t.Methods);
            RenderMembers(ctx, "Events", t.Events);
            RenderMembers(ctx, "Fields", t.Fields);

            // Nested types
            foreach (var nested in t.NestedInnerTypes())
            {
                ctx.Writer.PageHeaderOverride = t.DisplayName; // helpful header on nested pages
                // Bookmark + TOC
                AddBookmark(ctx.Document, ctx.Writer.Page, $"{nested.DisplayName} ({nested.Kind})");
                toc.Add(new TocEntry { Title = $"{new string(' ', Math.Max(0, level - 1) * 2)}• {nested.DisplayName} ({nested.Kind})", PageNumber = ctx.PageNumber });

                RenderTypeRecursive(ctx, nested, level + 1, toc);
            }

            // Small divider between sibling types (if any follow on same page)
            ctx.Writer.Spacer(6);
            ctx.Writer.DrawHairline();
            ctx.Writer.Spacer(6);
        }

        private static void RenderMembers(RenderContext ctx, string title, List<MemberDoc> members)
        {
            if (members == null || members.Count == 0) return;
            ctx.Writer.Spacer(6);
            ctx.Writer.DrawSubheading(title);

            // Table columns: Kind | Signature (mono) | Summary
            var cols = new[]
            {
                new TableColumnSpec("Kind",   widthRatio: 0.15, font: ctx.Theme.FontNormalBold),
                new TableColumnSpec("Signature", widthRatio: 0.45, font: ctx.Theme.FontMono),
                new TableColumnSpec("Summary", widthRatio: 0.40, font: ctx.Theme.FontNormal)
            };

            var rows = members.Select(m => new string[]
            {
                m.Kind ?? "",
                m.Signature ?? "",
                string.IsNullOrWhiteSpace(m.Summary) ? "" : m.Summary.Trim()
            });

            ctx.Writer.DrawTable(cols, rows);
        }

        // -----------------------------
        // TOC Rendering
        // -----------------------------
        private static void RenderToc(RenderContext ctx, string title, List<TocEntry> entries)
        {
            ctx.Writer.DrawHeading(1, title);
            ctx.Writer.Spacer(8);

            foreach (var e in entries)
            {
                // Simple TOC line: Title ....... Page
                ctx.Writer.DrawTocLine(e.Title, e.PageNumber);
            }
        }

        // -----------------------------
        // PDF Outline / Bookmarks
        // -----------------------------
        private static void AddBookmark(PdfDocument doc, PdfPage page, string title)
        {
            // PdfSharpCore: just add an outline entry pointing to the page
            doc.Outlines.Add(title, page, true);
        }

     
    }
}
