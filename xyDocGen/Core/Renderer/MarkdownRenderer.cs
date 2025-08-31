using System.Collections.Generic;
using System.Linq;
using System.Text;
using xyDocGen.Core.Docs;

namespace xyDocGen.Core.Renderer
{
    /// <summary>
    /// Rendering the documentation in Markdown file
    /// </summary>
    public static class MarkdownRenderer
    {
        /// <summary>
        /// Renders a TypeDoc object as Markdown, including all nested types recursively.
        /// </summary>
        public static string Render(TypeDoc type, int level = 1)
        {
            var sb = new StringBuilder();
            string headingPrefix = new string('#', level);

            // Header
            sb.AppendLine($"{headingPrefix} {type.Kind} {type.DisplayName}");
            sb.AppendLine();
            sb.AppendLine();

            // Metadata
            sb.AppendLine($"**Namespace:** {type.Namespace}  ");
            sb.AppendLine($"**Visibility:** {type.Modifiers}  ");
            sb.AppendLine();
            if (type.Attributes?.Count > 0)
            {
                sb.AppendLine($"**Attributes:** {string.Join(", ", type.Attributes)}  ");
                sb.AppendLine();
            }
            if (type.BaseTypes?.Count > 0)
            {
                sb.AppendLine($"**Base/Interfaces:** {string.Join(", ", type.BaseTypes)}  ");
                sb.AppendLine();
            }
            sb.AppendLine($"**Source:** {type.FilePath}  ");
            sb.AppendLine();
            sb.AppendLine();

            // Description
            sb.AppendLine("**Description:**");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(type.Summary))
            {
                sb.AppendLine(type.Summary.Trim());
            }
            else
            {
                sb.AppendLine("_No description._");
            }
            sb.AppendLine();
            sb.AppendLine();

            // Helper to render member lists with extra spacing between groups
            void RenderMembers(string title, List<MemberDoc> members)
            {
                if (members == null || !members.Any()) return;

                sb.AppendLine($"### {title}");
                sb.AppendLine();
                foreach (var m in members)
                {
                    sb.AppendLine($"- **{m.Kind}:** `{m.Signature}`");
                    if (!string.IsNullOrWhiteSpace(m.Summary))
                    {
                        var summary = m.Summary.Trim().Replace("\r\n", " ").Replace("\n", " ");
                        sb.AppendLine($"  - {summary}");
                    }
                    sb.AppendLine();
                    sb.AppendLine();
                }
                sb.AppendLine();
            }

            RenderMembers("Constructors", type.Constructors);
            RenderMembers("Methods", type.Methods);
            RenderMembers("Properties", type.Properties);
            RenderMembers("Events", type.Events);
            RenderMembers("Fields", type.Fields);

            // Nested types (recursive) with spacing
            foreach (var nested in type.FlattenNested().Skip(1)) // skip self
            {
                sb.AppendLine();
                sb.AppendLine(Render(nested, level + 1).TrimEnd());
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }
    }
}
