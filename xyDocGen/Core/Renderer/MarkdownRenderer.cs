using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using xyDocumentor.Docs;
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
            // The StringBuilder is used for efficient string manipulation and building.
            StringBuilder sb_MarkdownBuilder = new();

            // Create the central context map for internal linking
            var anchorMap = prebuiltAnchorMap ?? BuildAnchorMap(td_Type);

            // Add a global top anchor so we can link "Back to top" reliably
            // Keep it invisible and at the very beginning of the document.
            sb_MarkdownBuilder.AppendLine("<span id=\"top\"></span>");

            // Optional: Table of Contents only for top-level calls (level==1) to avoid duplication when recursing
            if (level_ == 1)
            {
                RenderTableOfContents(sb_MarkdownBuilder, td_Type, anchorMap);
                sb_MarkdownBuilder.AppendLine(); // spacing after TOC
            }


            // Passing the map to all helper methods for anchoring and linking.
            RenderHeader(sb_MarkdownBuilder, td_Type, level_, anchorMap);
            RenderMetadata(sb_MarkdownBuilder, td_Type,level_ ,anchorMap);
            RenderDescriptionFromXmlSummaryInTypeDoc(sb_MarkdownBuilder, td_Type, level_,anchorMap);
            RenderAllMembers(sb_MarkdownBuilder, td_Type,level_, anchorMap);
            RenderNestedTypes(sb_MarkdownBuilder, td_Type, level_, anchorMap);

            // Add a subtle back-to-top link at the end of each rendered type block
            sb_MarkdownBuilder.AppendLine();
            sb_MarkdownBuilder.AppendLine("↩︎ [Back to top](#top)");
            // Returns the final Markdown string, removing any leading or trailing whitespace.
            return sb_MarkdownBuilder.ToString().Trim();
        }






        /// <summary>
        /// Creates the unique name (e.g., Namespace.Parent.Name) for anchoring and mapping.
        /// </summary>
        /// <param name="td_TargetType"></param>
        /// <returns></returns>
        private static string GetUniqueTypeName(TypeDoc td_TargetType) => !string.IsNullOrWhiteSpace(td_TargetType.Namespace) ? $"{td_TargetType.Namespace}.{td_TargetType.DisplayName}" : $"Global (Default).{td_TargetType.DisplayName}";
        //private static string GetUniqueTypeName(TypeDoc td_TargetType) => !string.IsNullOrWhiteSpace(td_TargetType.Namespace)? $"{td_TargetType.Namespace}.{td_TargetType.DisplayName}" : td_TargetType.DisplayName;

        /// <summary>
        /// Cleans the type's name and changes it to lowercase 
        /// </summary>
        /// <param name="name_"></param>
        /// <returns></returns>
        //private static string GetAnchorIDFromTypeName(string uniqueName_) => new string(uniqueName_.ToLowerInvariant().Replace(" ", "-").Replace('.', '-').Replace('<', '-').Replace('>', '-').Replace(',', '-').Replace("(", "-").Replace(")", "-").Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_').ToArray());
        private static string GetAnchorIDFromTypeName(string name_)
        {
            string clean = Regex.Replace(name_.ToLowerInvariant(), @"[^a-z0-9\-]+", "-");
            int hash = name_.GetHashCode();
            return $"{clean}-{Math.Abs(hash % 10000)}";
        }

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

            if (!dic_AnchorMap_.TryGetValue(uniqueName, out string? anchorID))
            {
                // Be forgiving: if the key is missing (edge-case), generate and cache deterministically
                anchorID = GetAnchorIDFromTypeName(uniqueName);
                dic_AnchorMap_[uniqueName] = anchorID;
            }
            // Insert the `invisible` HTML anchor (the target for internal links)
            //sb_MarkdownBuilder_.AppendLine($"<a id=\"{anchorID}\"></a>");

            // Not more than 6!!!
            string headingPrefix = new string('#', Math.Min(level_, 6));

            var visibility = string.IsNullOrWhiteSpace(type.Modifiers) ? "" : $" · `{type.Modifiers}`";
            // Put anchor as invisible <span> at end of the heading line
            sb_MarkdownBuilder_.AppendLine($"{headingPrefix} `{type.DisplayName}` **{type.Kind}**{visibility} <span id=\"{anchorID}\"></span>");
            sb_MarkdownBuilder_.AppendLine();
        }


        /// <summary>
        /// Renders the metadata section for the type.
        /// </summary>
        /// <param name="sb_MarkdownBuilder_">The StringBuilder to append to.</param>
        /// <param name="td_TargetType_">The TypeDoc object containing the data.</param>
        /// <param name="level_"></param>
        /// <param name="dic_AnchorMap_"></param>
        private static void RenderMetadata(StringBuilder sb_MarkdownBuilder_, TypeDoc td_TargetType_, int level_,Dictionary<string, string> dic_AnchorMap_)
        {
            sb_MarkdownBuilder_.AppendLine($"{xy.Repeat("#",(ushort)(level_ +1))} Metadata");
            sb_MarkdownBuilder_.AppendLine($"**Namespace**: `{(string.IsNullOrWhiteSpace(td_TargetType_.Namespace) ? "Global (Default)" : td_TargetType_.Namespace)}`");

            if (!string.IsNullOrWhiteSpace(td_TargetType_.Modifiers))
            {
                sb_MarkdownBuilder_.AppendLine($"**Visibility:** `{td_TargetType_.Modifiers}`");
            }

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
        /// <param name="level_"></param>
        /// <param name="dic_AnchorMap_">Not yet used</param>
        private static void RenderDescriptionFromXmlSummaryInTypeDoc(StringBuilder sb_MarkdownBuilder_, TypeDoc td_Type_, int level_, Dictionary<string, string> dic_AnchorMap_)
        {
            sb_MarkdownBuilder_.AppendLine($"{xy.Repeat("#", (ushort)(level_ + 1))} Description");
            // Appends the summary if it's not null or whitespace.
            sb_MarkdownBuilder_.AppendLine((!string.IsNullOrWhiteSpace(td_Type_.Summary) ? td_Type_.Summary.Trim() : "(No description available)"));

            sb_MarkdownBuilder_.AppendLine();
        }

        /// <summary>
        /// Calls the rendering methods for all member types (constructors, properties, methods, etc.).
        /// </summary>
        /// <param name="sb_Markdownbuilder_">The StringBuilder to append to.</param>
        /// <param name="td_TargetObject_">The TypeDoc object containing the data.</param>
        /// <param name="level_"></param>
        /// <param name="dic_AnchorMap_"></param>
        private static void RenderAllMembers(StringBuilder sb_Markdownbuilder_, TypeDoc td_TargetObject_, int level_, Dictionary<string, string> dic_AnchorMap_)
        {
            // Pass the anchor map down to the table renderer
            RenderMembersAsTable(sb_Markdownbuilder_, "Constructors", td_TargetObject_.Constructors, level_,dic_AnchorMap_);
            RenderMembersAsTable(sb_Markdownbuilder_, "Properties", td_TargetObject_.Properties, level_, dic_AnchorMap_);
            RenderMembersAsTable(sb_Markdownbuilder_, "Methods", td_TargetObject_.Methods, level_, dic_AnchorMap_);
            RenderMembersAsTable(sb_Markdownbuilder_, "Events", td_TargetObject_.Events, level_, dic_AnchorMap_);
            RenderMembersAsTable(sb_Markdownbuilder_, "Fields", td_TargetObject_.Fields, level_, dic_AnchorMap_);
        }

        /// <summary>
        /// Renders a list of members (methods, properties, etc.) as a clear Markdown table.
        /// </summary>
        /// <param name="sb_MarkdownBuilder_">The StringBuilder to append to.</param>
        /// <param name="title_">The title for the member table (e.g., "Methods").</param>
        /// <param name="listedMembers_">The list of MemberDoc objects to be rendered.</param>
        /// <param name="level"></param>
        /// <param name="dic_AnchorMap_"></param>
        private static void RenderMembersAsTable(StringBuilder sb_MarkdownBuilder_, string title_, IReadOnlyList<MemberDoc> listedMembers_, int level, Dictionary<string, string> dic_AnchorMap_)
        {
            //// If the list is empty or null, nothing is rendered.
            //if (listedMembers_?.Any() != true) return;

            //sb_MarkdownBuilder_.AppendLine($"## {title_}");
            //sb_MarkdownBuilder_.AppendLine();

            ////// Renders the table header for Signature and Description columns.
            ////sb_MarkdownBuilder_.AppendLine("|-------------|\n|  Signature  |\n|-------------|\n| Description |\n|-------------|");
            ////sb_MarkdownBuilder_.AppendLine("");


            //foreach (MemberDoc member in listedMembers_)
            //{
            //    // Cleans up the summary for display in a single table row by removing newlines.
            //    string summary = member.Summary?.Trim().Replace("\r\n", " ").Replace("\n", " ") ?? "XXX";
            //    string linkedSignature = FormatSignatureWithLinks(member.Signature, dic_AnchorMap_);
            //    sb_MarkdownBuilder_.AppendLine($"| **`{linkedSignature}`** |\n| {summary} |");

            //}
            //sb_MarkdownBuilder_.AppendLine();

            if (listedMembers_?.Any() != true) return;

            // Replace with actual member name when there is time
            //sb_MarkdownBuilder_.AppendLine($"{xy.Repeat("#", (ushort)(level + 1))} Title");
            sb_MarkdownBuilder_.AppendLine();
            sb_MarkdownBuilder_.AppendLine("| Signature |  Summary  |");
            sb_MarkdownBuilder_.AppendLine("|-----------|-----------|");
            foreach (var m in listedMembers_)
            {
                string summary = (m.Summary ?? string.Empty).Trim().Replace("\r\n", " ").Replace("\n", " ");
                if (string.IsNullOrWhiteSpace(summary)) summary = "—";
                string linkedSignature = FormatSignatureWithLinks(m.Signature, dic_AnchorMap_);
                sb_MarkdownBuilder_.AppendLine($"| `{linkedSignature}` | {summary} |");
            }
            sb_MarkdownBuilder_.AppendLine();


        }


        /// <summary>
        /// Renders a simple, GitHub-friendly table of contents for the root + all nested types.
        /// </summary>
        private static void RenderTableOfContents(StringBuilder sb, TypeDoc root, Dictionary<string, string> anchorMap)
        {
            //sb.AppendLine("## Table of Contents");
            //sb.AppendLine();

            foreach (var t in root.FlattenNested())
            {
                // Derive the same unique key we use everywhere else
                string key = GetUniqueTypeName(t);
                if (!anchorMap.TryGetValue(key, out string? anchor))
                {
                    anchor = GetAnchorIDFromTypeName(key);
                    anchorMap[key] = anchor; // cache for consistent linking
                }
                // Indent nested entries with bullets for a visual hierarchy
                int depth = Math.Max(0, t.DisplayName.Count(c => c == '.')); // "Outer.Inner" → depth 1
                string bullet = new string(' ', depth * 2) + "-";             // 2 spaces per depth level
                sb.AppendLine($"{bullet} [{t.DisplayName}](#{anchor})");
            }
            sb.AppendLine();
        }


        /// <summary>
        /// Builds the central map of all TypeDocs (root and nested) using the FlattenNested extension.
        /// The map stores the Unique Name (key) and the Anchor ID (value).
        /// </summary>
        /// <param name="td_RootType_"></param>
        /// <returns></returns>
        private static Dictionary<string, string> BuildAnchorMap(TypeDoc td_RootType_)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);

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
        /// Replaces any unique type names in a signature string with a corresponding internal Markdown link.
        /// </summary>
        /// <param name="signature_"></param>
        /// <param name="dic_AnchorIdMapping_"></param>
        /// <returns></returns>
        private static string FormatSignatureWithLinks(string signature_, Dictionary<string, string> dic_AnchorIdMapping_)
        {
            //string result = signature_;

            //// Ensuring longer names (fully qualified names/...) are replaced before shorter parts, preventing partial replacement errors.
            //IOrderedEnumerable<string> sortedTypeNames = dic_AnchorIdMapping_.Keys.OrderByDescending(keyFromDictionary => keyFromDictionary.Length);

            //foreach (string typeName in sortedTypeNames)
            //{
            //    if (result.Contains(typeName))
            //    {
            //        string anchorId = dic_AnchorIdMapping_[typeName];

            //        // Creating the Markdown link: [Visible Text](#Anchor-ID)
            //        string markdownLink = $"[{typeName}](#{anchorId})";

            //        // Replace the plain type name string with the link string.
            //        result = result.Replace(typeName, markdownLink);
            //    }
            //}

            //return result;


            // Build alias map once per call – if you link viele Signaturen in einem Rutsch,
            // kannst du das auch vorziehen/cachen.
            var aliasMap = BuildAnchorAliasMap(dic_AnchorIdMapping_);

            // Sort longer keys first to avoid partial replacements
            var keys = aliasMap.Keys.OrderByDescending(k => k.Length).ToList();

            string result = signature_;
            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;
                var anchorId = aliasMap[key];
                // Replace whole words / identifier boundaries: avoid "Program" inside "Programmer"
                // \b passt bei ., <, > etc. nicht immer; identifizierbar per custom boundaries:
                // left boundary: start or non-identifier; right boundary: end or non-identifier
                var pattern = $@"(?<![A-Za-z0-9_]){RegexEscape(key)}(?![A-Za-z0-9_])";
                result = Regex.Replace(result,pattern,$"[{key}](#{anchorId})");
            }
            return result;
        }

        private static string RegexEscape(string s) => Regex.Escape(s);

        /// <summary>
        /// Renders all nested types contained within the current type.
        /// </summary>
        /// <param name="sb_MarkdownRenderer">The StringBuilder to append to.</param>
        /// <param name="td_TargetType_">The TypeDoc object containing the data.</param>
        /// <param name="level_">The heading level for the Markdown output.</param>
        /// <param name="dic_AnchorMap_"></param>
        private static void RenderNestedTypes(StringBuilder sb_MarkdownRenderer, TypeDoc td_TargetType_, int level_, Dictionary<string, string> dic_AnchorMap_)
        {
            // FlattenNested() returns the type itself and all nested types.
            // Skip(1) skips the root element so we only process the nested types.
            IEnumerable<TypeDoc> nestedTypes = td_TargetType_.FlattenNested().Skip(1);

            if (!nestedTypes.Any()) return;

            sb_MarkdownRenderer.AppendLine("---"); // A horizontal rule for better readability.

            foreach (TypeDoc td_nestedType in nestedTypes)
            {
                // Use the SAME anchor map for all nested levels to keep links stable
                sb_MarkdownRenderer.AppendLine(Render(td_nestedType, level_ + 1, dic_AnchorMap_));
                sb_MarkdownRenderer.AppendLine();
            }
        }

        private static Dictionary<string, string> BuildAnchorAliasMap(Dictionary<string, string> anchorMap)
        {
            // Result: maps several textual aliases → same anchor id
            var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in anchorMap)
            {
                string uniqueKey = kv.Key;     // e.g., "Global (Default).Program" or "Demo.Outer.Inner"
                string anchorId = kv.Value;

                // 1) Full unique key (bereits in anchorMap)
                if (!aliases.ContainsKey(uniqueKey)) aliases[uniqueKey] = anchorId;

                // 2) DisplayName = last segment(s) after namespace, we can infer it by splitting on first '.' from left of namespace part.
                //    In deinem GetUniqueTypeName setzt du key = "<NS>.<DisplayName>" oder "Global (Default).<DisplayName>".
                //    Also ist DisplayName einfach der Teil nach dem ersten '.'.
                var dotIndex = uniqueKey.IndexOf('.');
                if (dotIndex >= 0 && dotIndex + 1 < uniqueKey.Length)
                {
                    string displayName = uniqueKey.Substring(dotIndex + 1); // e.g., "Program" or "Outer.Inner"
                    if (!aliases.ContainsKey(displayName)) aliases[displayName] = anchorId;

                    // 3) Simple name (letztes Segment von DisplayName)
                    var lastDot = displayName.LastIndexOf('.');
                    string simple = lastDot >= 0 ? displayName.Substring(lastDot + 1) : displayName; // e.g., "Inner" from "Outer.Inner"
                    if (!aliases.ContainsKey(simple)) aliases[simple] = anchorId;

                    // 4) Generic short form (strip type parameters), e.g., "List<T>" -> "List"
                    string genericShort = StripGenericArity(displayName);
                    if (genericShort != displayName && !aliases.ContainsKey(genericShort))
                        aliases[genericShort] = anchorId;
                }
            }
            return aliases;
        }

        /// <summary>
        ///  Strips generic type parameters like "T,U" (simple textual approach)
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static string StripGenericArity(string name)
        {
            int lt = name.IndexOf('<');
            return lt > 0 ? name.Substring(0, lt) : name;
        }
    }
}
