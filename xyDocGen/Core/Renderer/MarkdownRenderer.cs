using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using xyDocumentor.Core.Docs;

namespace xyDocumentor.Core.Renderer
{
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
        /// <param name="level">The starting heading level for the Markdown output (e.g., 1 for #, 2 for ##).</param>
        /// <returns>A string containing the generated Markdown documentation.</returns>
        public static string Render(TypeDoc td_Type, int level = 1)
        {
            // The StringBuilder is used for efficient string manipulation and building.
            StringBuilder sb_MarkdownBuilder = new();

            // Create the central context map for internal linking
            Dictionary<string, string> anchorMap = BuildAnchorMap(td_Type);

            // Passing the map to all helper methods for anchoring and linking.
            RenderHeader(sb_MarkdownBuilder, td_Type, level, anchorMap);
            RenderMetadata(sb_MarkdownBuilder, td_Type, anchorMap);
            RenderDescriptionFromXmlSummaryInTypeDoc(sb_MarkdownBuilder, td_Type, anchorMap);
            RenderAllMembers(sb_MarkdownBuilder, td_Type, anchorMap);
            RenderNestedTypes(sb_MarkdownBuilder, td_Type, level);


            // Returns the final Markdown string, removing any leading or trailing whitespace.
            return sb_MarkdownBuilder.ToString().Trim();
        }

        // --- Private helper methods for better structure and readability ---

        /// <summary>
        /// Builds the central map of all TypeDocs (root and nested) using the FlattenNested extension.
        /// The map stores the Unique Name (key) and the Anchor ID (value).
        /// </summary>
        /// <param name="td_RootType_"></param>
        /// <returns></returns>
        private static Dictionary<string, string> BuildAnchorMap(TypeDoc td_RootType_)
        {
            var map = new Dictionary<string, string>();

            // Use the existing extension method to traverse all types.
            foreach (var td in td_RootType_.FlattenNested())
            {
                // Create the unique name: Namespace.DisplayName 
                // This guarantees the key is unique across the entire document.
                string uniqueName = GetUniqueTypeName(td);

                map[uniqueName] = GetAnchorIDFromTypeName(uniqueName);
            }
            return map;
        }

        /// <summary>
        /// Creates the unique name (e.g., Namespace.Parent.Name) for anchoring and mapping.
        /// </summary>
        /// <param name="td_TargetType"></param>
        /// <returns></returns>
        private static string GetUniqueTypeName(TypeDoc td_TargetType) => !string.IsNullOrWhiteSpace(td_TargetType.Namespace)? $"{td_TargetType.Namespace}.{td_TargetType.DisplayName}" : td_TargetType.DisplayName;


        /// <summary>
        /// Cleans the type's name and changes it to lowercase 
        /// </summary>
        /// <param name="uniqueName_"></param>
        /// <returns></returns>
        private static string GetAnchorIDFromTypeName(string uniqueName_) => uniqueName_.Replace('.', '-').Replace('<', '-').Replace('>', '-').Replace(',', '-').ToLowerInvariant();

        /// <summary>
        /// Renders the main header for the type, including its kind and display name.
        /// </summary>
        /// <param name="sb_MarkdownBuilder_">The StringBuilder to append to.</param>
        /// <param name="type">The TypeDoc object containing the data.</param>
        /// <param name="level_">The heading level for the Markdown output.</param>
        /// <param name="dic_AnchorMap_"></param>
        private static void RenderHeader(StringBuilder sb_MarkdownBuilder_, TypeDoc type, int level_, Dictionary<string, string> dic_AnchorMap_)
        {
            // Retrieve the anchor ID using the unique name as the key.
            string uniqueName = GetUniqueTypeName(type);
            string anchorID = dic_AnchorMap_[uniqueName];

            // Insert the `invisible` HTML anchor (the target for internal links)
            sb_MarkdownBuilder_.AppendLine($"<a id=\"{anchorID}\"></a>");

            // Creates the heading prefix (e.g., "#", "##", "###") based on the level.
            string headingPrefix = new string('#', level_);

            // Testing the thick font for type.kind
            sb_MarkdownBuilder_.AppendLine($"{headingPrefix} **{type.Kind}** `{type.DisplayName}`");
            sb_MarkdownBuilder_.AppendLine();
        }

        /// <summary>
        /// Renders the metadata section for the type.
        /// </summary>
        /// <param name="sb_MarkdownBuilder_">The StringBuilder to append to.</param>
        /// <param name="td_TargetType_">The TypeDoc object containing the data.</param>
        private static void RenderMetadata(StringBuilder sb_MarkdownBuilder_, TypeDoc td_TargetType_, Dictionary<string, string> dic_AnchorMap_)
        {
            sb_MarkdownBuilder_.AppendLine("## Metadata");
            sb_MarkdownBuilder_.AppendLine($"**Namespace**: `{td_TargetType_.Namespace ?? "Global (No specific namespace)"}`");
            sb_MarkdownBuilder_.AppendLine($"**Visibility:** `{td_TargetType_.Modifiers}`");
            sb_MarkdownBuilder_.AppendLine($"**Source File:** `{td_TargetType_.FilePath}`");

            // Helper method simplifies adding lists of metadata, such as attributes or base types.
            AppendMetadataList(sb_MarkdownBuilder_, "Attributes", td_TargetType_.Attributes);
            AppendMetadataList(sb_MarkdownBuilder_, "Base Classes/Interfaces", td_TargetType_.BaseTypes);

            sb_MarkdownBuilder_.AppendLine();
        }

        /// <summary>
        /// Appends a list of metadata items to the StringBuilder only if the list is not empty.
        /// </summary>
        /// <param name="sb_MarkdownRenderer_">The StringBuilder to append to.</param>
        /// <param name="title_">The title for the list (e.g., "Attributes").</param>
        /// <param name="listedItems_">The list of strings to be appended.</param>
        private static void AppendMetadataList(StringBuilder sb_MarkdownRenderer_, string title_, IReadOnlyList<string> listedItems_)
        {
            // Checks if the list exists and contains any elements.
            if (listedItems_?.Count > 0)
            {
                // Formats each item as inline code for better readability in Markdown.
                IEnumerable<string> formattedItems = listedItems_.Select(item => $"`{item}`");

                sb_MarkdownRenderer_.AppendLine($"**{title_}:** {string.Join(", ", formattedItems)}");
            }
        }

        /// <summary>
        /// Renders the summary or description section for the type.
        /// </summary>
        /// <param name="sb_MarkdownBuilder_">The StringBuilder to append to.</param>
        /// <param name="td_Type_">The TypeDoc object containing the data.</param>
        /// <param name="dic_AnchorMap_">Not yet used</param>
        private static void RenderDescriptionFromXmlSummaryInTypeDoc(StringBuilder sb_MarkdownBuilder_, TypeDoc td_Type_, Dictionary<string, string> dic_AnchorMap_)
        {
            sb_MarkdownBuilder_.AppendLine("## Description");

            // Appends the summary if it's not null or whitespace.
            sb_MarkdownBuilder_.AppendLine((!string.IsNullOrWhiteSpace(td_Type_.Summary) ? td_Type_.Summary.Trim() : "(No description available)"));

            sb_MarkdownBuilder_.AppendLine();
        }

        /// <summary>
        /// Calls the rendering methods for all member types (constructors, properties, methods, etc.).
        /// </summary>
        /// <param name="sb_Markdownbuilder_">The StringBuilder to append to.</param>
        /// <param name="td_TargetObject_">The TypeDoc object containing the data.</param>
        /// <param name="dic_AnchorMap_"></param>
        private static void RenderAllMembers(StringBuilder sb_Markdownbuilder_, TypeDoc td_TargetObject_, Dictionary<string, string> dic_AnchorMap_)
        {
            // Pass the anchor map down to the table renderer
            RenderMembersAsTable(sb_Markdownbuilder_, "Constructors", td_TargetObject_.Constructors, dic_AnchorMap_);
            RenderMembersAsTable(sb_Markdownbuilder_, "Properties", td_TargetObject_.Properties, dic_AnchorMap_);
            RenderMembersAsTable(sb_Markdownbuilder_, "Methods", td_TargetObject_.Methods, dic_AnchorMap_);
            RenderMembersAsTable(sb_Markdownbuilder_, "Events", td_TargetObject_.Events, dic_AnchorMap_);
            RenderMembersAsTable(sb_Markdownbuilder_, "Fields", td_TargetObject_.Fields, dic_AnchorMap_);
        }

        /// <summary>
        /// Renders a list of members (methods, properties, etc.) as a clear Markdown table.
        /// </summary>
        /// <param name="sb_MarkdownBuilder_">The StringBuilder to append to.</param>
        /// <param name="title_">The title for the member table (e.g., "Methods").</param>
        /// <param name="listedMembers_">The list of MemberDoc objects to be rendered.</param>
        private static void RenderMembersAsTable(StringBuilder sb_MarkdownBuilder_, string title_, IReadOnlyList<MemberDoc> listedMembers_, Dictionary<string, string> dic_AnchorMap_)
        {
            // If the list is empty or null, nothing is rendered.
            if (listedMembers_?.Any() != true) return;

            sb_MarkdownBuilder_.AppendLine($"## {title_}");
            sb_MarkdownBuilder_.AppendLine();

            // Renders the table header for Signature and Description columns.
            sb_MarkdownBuilder_.AppendLine("| Signature | Description |");
            sb_MarkdownBuilder_.AppendLine("|-----------|-------------|");

            // Iterates through each member to create a new table row.
            foreach (var member in listedMembers_)
            {
                // Cleans up the summary for display in a single table row by removing newlines.
                string summary = member.Summary?.Trim().Replace("\r\n", " ").Replace("\n", " ") ?? "XXX";
                string linkedSignature = FormatSignatureWithLinks(member.Signature, dic_AnchorMap_);
                sb_MarkdownBuilder_.AppendLine($"| **`{linkedSignature}`** | {summary} |");

            }
            sb_MarkdownBuilder_.AppendLine();
        }

        /// <summary>
        /// Replaces any unique type names in a signature string with a corresponding internal Markdown link.
        /// </summary>
        /// <param name="signature_"></param>
        /// <param name="dic_AnchorIdMapping_"></param>
        /// <returns></returns>
        private static string FormatSignatureWithLinks(string signature_, Dictionary<string, string> dic_AnchorIdMapping_)
        {
            string result = signature_;

            // Ensuring longer names (fully qualified names/...) are replaced before shorter parts, preventing partial replacement errors.
            IOrderedEnumerable<string> sortedTypeNames = dic_AnchorIdMapping_.Keys.OrderByDescending(keyFromDictionary => keyFromDictionary.Length);

            foreach (string typeName in sortedTypeNames)
            {
                if (result.Contains(typeName))
                {
                    string anchorId = dic_AnchorIdMapping_[typeName];

                    // Creating the Markdown link: [Visible Text](#Anchor-ID)
                    string markdownLink = $"[{typeName}](#{anchorId})";

                    // Replace the plain type name string with the link string.
                    result = result.Replace(typeName, markdownLink);
                }
            }

            return result;
        }

        /// <summary>
        /// Renders all nested types contained within the current type.
        /// </summary>
        /// <param name="sb_MarkdownRenderer">The StringBuilder to append to.</param>
        /// <param name="td_TargetType_">The TypeDoc object containing the data.</param>
        /// <param name="level_">The heading level for the Markdown output.</param>
        private static void RenderNestedTypes(StringBuilder sb_MarkdownRenderer, TypeDoc td_TargetType_, int level_)
        {
            // FlattenNested() returns the type itself and all nested types.
            // Skip(1) skips the root element so we only process the nested types.
            IEnumerable<TypeDoc> nestedTypes = td_TargetType_.FlattenNested().Skip(1);
            if (!nestedTypes.Any()) return;

            sb_MarkdownRenderer.AppendLine("---"); // A horizontal rule for better readability.

            foreach (TypeDoc td_nestedType in nestedTypes)
            {
                // Recursively calls the Render method for the nested type, incrementing the heading level.
                sb_MarkdownRenderer.AppendLine(Render(td_nestedType, level_ + 1));
                sb_MarkdownRenderer.AppendLine();
            }
        }
    }
}
