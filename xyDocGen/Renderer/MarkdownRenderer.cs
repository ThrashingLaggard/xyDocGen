using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using xyDocumentor.Docs;
using xyDocumentor.Markdown;
using xyToolz.QOL;

namespace xyDocumentor.Renderer
{
#nullable enable
    /// <summary>
    /// Provides static methods to generate structured code documentation
    /// in Markdown format.
    /// </summary>
    public static class MarkdownRenderer
    {
        /// <summary>
        /// Renders a TypeDoc object and all its nested types recursively
        /// into a Markdown string.
        /// </summary>
        /// <param name="td_Type">The TypeDoc object to be rendered.</param>
        /// <param name="level_">The starting heading level for the Markdown output (e.g., 1 for #, 2 for ##).</param>
        /// <param name="prebuiltAnchorMap"></param>
        /// <returns>A string containing the generated Markdown documentation.</returns>
        public static string Render(TypeDoc td_Type, int level_ = 1, Dictionary<string, string>? prebuiltAnchorMap = null)
        {
            StringBuilder sb_MarkdownBuilder = new();

            var anchorMap = prebuiltAnchorMap ?? MdAnchor.BuildAnchorMap(td_Type);

            sb_MarkdownBuilder.AppendLine("<span id=\"top\"></span>");

            if (level_ == 1)
            {
                MdMembersTable.RenderTableOfContents(sb_MarkdownBuilder, td_Type, anchorMap);
                sb_MarkdownBuilder.AppendLine(); 
            }

            MdSections.RenderHeader(sb_MarkdownBuilder, td_Type, level_, anchorMap);
            MdSections.RenderMetadata(sb_MarkdownBuilder, td_Type,level_ ,anchorMap);
            MdSections.RenderDescriptionFromXmlSummaryInTypeDoc(sb_MarkdownBuilder, td_Type, level_,anchorMap);
            MdMembersTable.RenderAllMembers(sb_MarkdownBuilder, td_Type,level_, anchorMap);
            MdSections.RenderNestedTypes(sb_MarkdownBuilder, td_Type, level_, anchorMap);
            sb_MarkdownBuilder.AppendLine();
            sb_MarkdownBuilder.AppendLine("↩︎ [Back to top](#top)");
            return sb_MarkdownBuilder.ToString().Trim();
        }

    }
}
