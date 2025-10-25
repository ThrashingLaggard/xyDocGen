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
        /// <summary>
        /// Add usefull information
        /// </summary>
        public static string Description { get; set; }

        /// <summary>
        /// Create a new PdfDocument with basic placeholder information and set compression
        /// </summary>
        /// <param name="td_Root_"></param>
        /// <returns></returns>
        private static PdfDocument CreatePdfDocumentWithBasicValues(TypeDoc td_Root_)
        {
            PdfDocument document = new();
            document.Info.Title = $"{td_Root_.DisplayName} API Documentation";
            document.Info.Author = $"xyDocumentor@{Environment.CurrentDirectory}";
            document.Info.Subject = "C# API Reference";
            document.Options.NoCompression = false;
            document.Options.CompressContentStreams = true;

            return document;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="root"></param>
        /// <param name="outputPath"></param>
        public static void RenderToFile(TypeDoc root, string outputPath)
        {
            using PdfDocument document = CreatePdfDocumentWithBasicValues(root);

            var theme = PdfTheme.CreateDefault();
            var ctx = new RenderContext(document, theme);
            
            // Reserve TOC page as the very first page (we fill it after content is rendered)
            var tocPage = ctx.AddPage();

            // Render content starting on a new page
            ctx.Writer = new PageWriter(ctx, ctx.AddPage());


            ctx.Writer.DrawHeaderFooter = false; // disable on TOC page for a clean look

            // Collect TOC entries while rendering
            var tocEntries = new List<TocEntry>();


            // Root heading + bookmark
            //AddBookmark(document, ctx.Writer.Page, $"{root.DisplayName} ({root.Kind})");
            //tocEntries.Add(new TocEntry { Title = $"{root.DisplayName} ({root.Kind})", PageNumber = ctx.PageNumber });

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
            // Remember top Y before drawing the heading (for the link destination)
            double yTop = ctx.Writer.Y;

            // Heading
            ctx.Writer.DrawHeading(level, $"{t.DisplayName}  [{t.Kind}]");
            if (level <= 2) ctx.CurrentSectionTitle = t.DisplayName;

            // Build richer TOC text (all optional, safe fallbacks)
            string? signature = BuildTypeSignatureForToc(t);      
            string? descr = BuildSummarySnippet(t.Summary);   

            // Add TOC entry with link target info
            toc.Add(new TocEntry
            {
                Title = $"{t.DisplayName} ({t.Kind})",
                Signature = signature,
                Description = descr,
                PageNumber = ctx.PageNumber,
                Page = ctx.Writer.Page,
                Y = yTop
            });

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

        private static string BuildSummarySnippet(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary)) return null;
            var s = summary.Trim();

            // First sentence heuristic: split on '.', '!' or '?'.
            int cut = s.IndexOfAny(new[] { '.', '!', '?' });
            string first = cut > 0 ? s[..(cut + 1)] : s;

            // Normalize whitespace
            first = System.Text.RegularExpressions.Regex.Replace(first, @"\s+", " ");

            // Trim for TOC use
            const int max = 180;
            if (first.Length > max) first = first[..(max - 1)] + "…";
            return first;
        }

        private static string BuildTypeSignatureForToc(TypeDoc t)
        {
            // Prefer a real signature if you have it; otherwise use the display name.
            // If you have generics, you can include them here.
            // Keep it compact for TOC.
            var sig = t.Signature ?? ""; // if such a field exists
            if (string.IsNullOrWhiteSpace(sig)) sig = t.DisplayName;

            // Hard-trim very long signatures (optional, keeps TOC tidy)
            const int max = 140;
            if (!string.IsNullOrWhiteSpace(sig) && sig!.Length > max)
                sig = sig.Substring(0, max - 1) + "…";
            return sig;
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
        private static void RenderToc(RenderContext ctx, string title, List<TocEntry> listedEntries_)
        {
            ctx.Writer.DrawHeading(1, title);
            ctx.Writer.Spacer(8);

            foreach (TocEntry te_ContentTableEntry in listedEntries_)
            {
                // Compose left column: Title — Signature — Description (only if present)
                string leftText = te_ContentTableEntry.Title;
                
                if (!string.IsNullOrWhiteSpace(te_ContentTableEntry.Signature))
                    leftText += " — " + te_ContentTableEntry.Signature;
                
                if (!string.IsNullOrWhiteSpace(te_ContentTableEntry.Description))
                    leftText += " — " + te_ContentTableEntry.Description;

                // Now with a wrap-friendly TOC line....yay
                //var rect = ctx.Writer.DrawTocLine(e.Title, e.PageNumber);
                var rect = ctx.Writer.DrawTocLineWrapped(leftText, te_ContentTableEntry.PageNumber);
                
                if (te_ContentTableEntry.Page != null)
                {
                    PdfLinkingHelpers.AddGoToLink(ctx.Writer.Page, rect.X, rect.Y, rect.Width, rect.Height,te_ContentTableEntry.Page, te_ContentTableEntry.Y);
                }
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
