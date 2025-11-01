using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using xyDocumentor.Docs;
using xyDocumentor.Helpers;

namespace xyDocumentor.Extractors
{
#nullable enable
    internal partial class Extractor
    {

        // Top-Level: static readonly Regex
        internal static readonly Regex TagRemovalRegex = MyRegex();


        // Fallback string
        private const string NO_XML_SUMMARY_FALLBACK = "(No XML-Summary)";

        /// <summary>
        /// Extracts the clean text from the XML summary for a given syntax node.
        /// </summary>
        public static string ExtractXmlSummaryFromSyntaxNode(SyntaxNode syn_Node_)
        {
            // Read the trivia syntax element from the target node
            DocumentationCommentTriviaSyntax trivia = syn_Node_.GetLeadingTrivia().Select(t => t.GetStructure()).OfType<DocumentationCommentTriviaSyntax>().FirstOrDefault()!;

            if (trivia == null) goto Fallback;

            // Read the content from the XmlSummary section
            XmlElementSyntax xes_XmlSummary = trivia.Content.OfType<XmlElementSyntax>().FirstOrDefault(x => x.StartTag.Name.LocalName.Text.Equals("summary", System.StringComparison.OrdinalIgnoreCase))!;

            if (xes_XmlSummary == null) goto Fallback;

            // For consistent String handling
            StringBuilder sb_SummaryBuilder = new();

            // For every item in the content list
            foreach (XmlNodeSyntax content in xes_XmlSummary.Content)
            {
                // Check if the content is an XML text token, which holds the actual text.
                if (content is XmlTextSyntax textNode)
                {
                    // Append the raw text from the token, which does not contain the "///" characters.
                    foreach (var token in textNode.TextTokens)
                    {
                        sb_SummaryBuilder.Append(token.ValueText);
                    }
                }
                else
                {
                    sb_SummaryBuilder.Append(content.ToString());
                }
            }

            // If theres something in the buffer
            if (!(sb_SummaryBuilder.Length > 0))
            {
                goto Fallback;
            }
            else
            {
                // Write the string from the builder
                string summary = sb_SummaryBuilder.ToString();

                // Remove xml tags and other unwanted bullshit
                return Utils.CleanDoc(summary);
            }

        Fallback:
            return NO_XML_SUMMARY_FALLBACK;
        }




        /// <summary>
        /// Extracts the content of the &lt;remarks&gt; XML documentation tag from a given member declaration.
        /// </summary>
        /// <param name="mds_Member_">The MemberDeclarationSyntax node to inspect.</param>
        /// <returns>The cleaned content of the remarks tag, or an empty string.</returns>
        internal static string ExtractXmlRemarksFromSyntaxNode(MemberDeclarationSyntax mds_Member_)
        {
            return ExtractXmlTagContent(mds_Member_, "remarks");
        }

        /// <summary>
        /// Extracts the content of the &lt;returns&gt; XML documentation tag.
        /// </summary>
        /// <param name="memberNode">The member node (Method, Property, Delegate) to inspect.</param>
        /// <returns>The cleaned content of the returns tag, or an empty string.</returns>
        internal static string ExtractXmlReturnSummary(MemberDeclarationSyntax memberNode)
        {
            // This single implementation covers methods, properties, and delegates.
            return ExtractXmlTagContent(memberNode, "returns");
        }

        // Overloads for consistency with the original TypeExtractor structure
        internal static string ExtractXmlReturnSummary(MethodDeclarationSyntax methodNode) => ExtractXmlReturnSummary((MemberDeclarationSyntax)methodNode);
        internal static string ExtractXmlReturnSummary(PropertyDeclarationSyntax propertyNode) => ExtractXmlReturnSummary((MemberDeclarationSyntax)propertyNode);
        internal static string ExtractXmlReturnSummary(DelegateDeclarationSyntax delegateNode) => ExtractXmlReturnSummary((MemberDeclarationSyntax)delegateNode);

        /// <summary>
        /// Extracts all parameter names and their corresponding descriptions from the &lt;param&gt; XML tags.
        /// </summary>
        /// <param name="parentNode">The MemberDeclarationSyntax (Method or Constructor) containing the parameters.</param>
        /// <returns>A dictionary where the key is the parameter name and the value is its summary.</returns>
        internal static IDictionary<string, string> ExtractXmlParamSummaries(MemberDeclarationSyntax parentNode)
        {
            var summaries = new Dictionary<string, string>();

            // 1. Retrieve the XML documentation comment structure (if it exists) from the leading trivia.
            DocumentationCommentTriviaSyntax xmlComment = parentNode.GetLeadingTrivia().Select(t => t.GetStructure()).OfType<DocumentationCommentTriviaSyntax>().FirstOrDefault()!;

            if (xmlComment == null)
                return summaries;

            // 2. Filter the comment content for <param> tags, which are represented as XmlElementSyntax.
            IEnumerable<XmlElementSyntax> paramElements = xmlComment.Content.OfType<XmlElementSyntax>()
                                                .Where(e => e.StartTag.Name.ToString() == "param");

            foreach (XmlElementSyntax paramElement in paramElements)
            {
                // 3. Extract the 'name' attribute from the start tag to get the parameter identifier.
                XmlNameAttributeSyntax nameAttribute = paramElement.StartTag.Attributes.OfType<XmlNameAttributeSyntax>().FirstOrDefault(a => a.Name.LocalName.Text == "name")!;

                if (nameAttribute == null)
                    continue;

                string paramName = nameAttribute.Identifier.ToString().Trim();

                // 4. Extract and clean the content of the parameter tag.
                string content = GetTextFromXmlContent(paramElement.Content);

                summaries[paramName] = content;
            }

            return summaries;
        }


        /// <summary>
        /// Extracts base types and interfaces
        /// </summary>
        /// <param name="baseList_"></param>
        /// <returns>
        /// IEnumerable filled with strings from the baseList
        /// Empty IEnumerable if parameter is not filled or invalid
        /// </returns>
        public static IEnumerable<string> ExtractBaseTypes(BaseListSyntax baseList_)
        {
            // Checks for an invalid parameter
            if (baseList_ is null || baseList_.Span.IsEmpty)
            {
                return [];
            }

            // Project every type in the externally provided list into a string 
            return baseList_.Types.Select(type => type.Type.ToString());
        }

        /// <summary>
        /// Extracts constraints for generic methods/types (e.g., 'where T : class, new()').
        /// </summary>
        internal static List<string> ExtractGenericConstraints(MemberDeclarationSyntax mds_MemberNode_)
        {
            List<string> listedConstraints = [];

            // Constraints sind nur bei MethodDeclarationSyntax oder TypeDeclarationSyntax vorhanden.
            // Wir prüfen hier nur Methoden, da dies der häufigste Member-Typ ist, der generisch sein kann.
            if (mds_MemberNode_ is MethodDeclarationSyntax methodNode)
            {
                if (methodNode.ConstraintClauses.Any())
                {
                    // Jeder ConstraintClauseSyntax repräsentiert eine volle "where"-Klausel.
                    foreach (TypeParameterConstraintClauseSyntax tpcc_Clause in methodNode.ConstraintClauses)
                    {
                        // Wir verwenden ToFullString(), um die vollständige Klausel zu erhalten (z.B. "where T : struct")
                        // und trimmen sie, um unnötige Leerzeichen zu entfernen.
                        listedConstraints.Add(tpcc_Clause.ToFullString().Trim());
                    }
                }
            }
            // TODO: Fügen Sie TypeDeclarationSyntax (Class/Struct) hinzu, falls MemberDoc auch für diese verwendet wird.
            // Beispiel: else if (memberNode is TypeDeclarationSyntax typeNode) { ... }

            return listedConstraints;
        }

        /// <summary>
        /// Extracts detailed parameter documentation by combining Roslyn's parameter structure 
        /// with the extracted XML documentation summaries.
        /// </summary>
        internal static IList<ParameterDoc> ExtractParameterDocs(MemberDeclarationSyntax mds_MemberNode_)
        {
            List<ParameterDoc> paramDocs = [];

            // 1. Hole die XML-Zusammenfassungen für alle Parameter
            IDictionary<string, string> xmlSummaries = ExtractXmlParamSummaries(mds_MemberNode_);

            ParameterListSyntax? parameterList = null;

            // 2. Finde die ParameterList basierend auf dem Typ des Members
            if (mds_MemberNode_ is MethodDeclarationSyntax m)
                parameterList = m.ParameterList;
            else if (mds_MemberNode_ is ConstructorDeclarationSyntax c)
                parameterList = c.ParameterList;
            else if (mds_MemberNode_ is DelegateDeclarationSyntax d)
                parameterList = d.ParameterList;

            if (parameterList == null)
                return paramDocs;

            // 3. Durchlaufe die Roslyn-Parameter und erstelle ParameterDoc-Objekte
            foreach (var roslynParam in parameterList.Parameters)
            {
                // Achtung: SyntaxToken hat .Text, was meistens robuster ist als .ValueText
                string paramName = roslynParam.Identifier.Text;
                string paramType = roslynParam.Type?.ToString() ?? "var"; // Sicherstellen, dass Type existiert

                // Versuche, die Beschreibung aus der XML-Dokumentation zu holen
                xmlSummaries.TryGetValue(paramName, out string? paramSummary);

                // Erstelle das ParameterDoc-Objekt
                paramDocs.Add(new ParameterDoc
                {
                    Name = paramName,
                    TypeDisplayName = paramType,
                    Summary = paramSummary ?? string.Empty,
                    IsOptional = roslynParam.Default != null
                });
            }

            return paramDocs;
        }

        /// <summary>
        /// Extracts the return type for methods, properties, and field-like events.
        /// </summary>
        internal static string ExtractReturnType(MemberDeclarationSyntax mds_memberNode_)
        {
            return mds_memberNode_ switch
            {
                MethodDeclarationSyntax m => m.ReturnType.ToString(),
                PropertyDeclarationSyntax p => p.Type.ToString(),
                EventDeclarationSyntax e => e.Type.ToString(), // Custom events
                EventFieldDeclarationSyntax ef => ef.Declaration.Type.ToString(), // Field-like events
                FieldDeclarationSyntax f => f.Declaration.Type.ToString(),
                DelegateDeclarationSyntax d => d.ReturnType.ToString(),
                _ => string.Empty // Keine Rückgabetypen für Konstruktoren, Klassen etc.
            };
        }

        /// <summary>
        /// Check the DeclarationSyntax and return the corresponding signature as a readable string
        /// </summary>
        /// <param name="mds_Member_"></param>
        /// <returns>string s_Signature = "depends on the type of member declaration"</returns>
        internal  static string ExtractSignature(MemberDeclarationSyntax mds_Member_)
        {

            // Get modifiers for use in signatures
            string modifiers = string.Join(" ", mds_Member_.Modifiers.Select(m => m.Text));

            // Add a trailing space only if there are modifiers
            modifiers = string.IsNullOrWhiteSpace(modifiers) ? "" : $"{modifiers} ";

            switch (mds_Member_)
            {
                case MethodDeclarationSyntax m:
                    {
                        // Including return type and modifiers for a complete signature.
                        string s_MethodString = $"{modifiers}{m.ReturnType} {m.Identifier}{m.TypeParameterList}{m.ParameterList}";
                        return s_MethodString;
                    }
                case PropertyDeclarationSyntax p:
                    {
                        // Including modifiers
                        string s_PropertyString = $"{modifiers}{p.Type} {p.Identifier} {{ ... }}";
                        return s_PropertyString;
                    }
                case FieldDeclarationSyntax f:
                    {
                        // Handling multiple declarators and including modifiers.
                        string typeName = f.Declaration.Type.ToString();
                        string variableNames = string.Join(", ", f.Declaration.Variables.Select(v => v.Identifier.Text));
                        return $"{modifiers}{typeName} {variableNames}";

                    }
                case EventDeclarationSyntax e:
                    {
                        // EventDeclarationSyntax is for custom events (add/remove accessors)
                        string s_EventString = $"{modifiers}event {e.Type} {e.Identifier}";
                        return s_EventString;
                    }
                case EventFieldDeclarationSyntax ef:
                    {
                        // EventFieldDeclarationSyntax is for field-like events
                        return $"{modifiers}event {ef.Declaration.Type} {string.Join(", ", ef.Declaration.Variables.Select(v => v.Identifier.Text))}";
                    }
                case ConstructorDeclarationSyntax c:
                    {
                        // Include modifiers for a complete signature.
                        string s_ConstructorString = $"{modifiers}{c.Identifier}{c.ParameterList}";
                        return s_ConstructorString;
                    }
                default:
                    {
                        string s_DefaultString = mds_Member_.ToString().Split('\n').FirstOrDefault()?.Trim() ?? "(unknown)";
                        return s_DefaultString;
                    }
            }
        }

        /// <summary>
        /// Extrahiert Parameterdetails und deren XML-Dokumentation.
        /// </summary>
        internal static IList<ParameterDoc> ExtractParameters(ParameterListSyntax parameterList, MemberDeclarationSyntax parentNode)
        {
            IList<ParameterDoc> parameters = [];
            IDictionary<string, string> paramSummaries = ExtractXmlParamSummaries(parentNode);

            foreach (ParameterSyntax param in parameterList.Parameters)
            {
                string? defaultValueExpression = param.Default?.Value?.ToString();

                // Mal schauen, was es noch für lustige Keywords gibt
                bool isRef = param.Modifiers.Any(t => t.IsKind(SyntaxKind.RefKeyword));
                bool isOut = param.Modifiers.Any(t => t.IsKind(SyntaxKind.OutKeyword));
                bool isIn = param.Modifiers.Any(t => t.IsKind(SyntaxKind.InKeyword));
                bool isParams = param.Modifiers.Any(t => t.IsKind(SyntaxKind.ParamsKeyword));

                bool isRefReadonly = param.Modifiers.Any(t => t.IsKind(SyntaxKind.RefKeyword))
                                    && param.Modifiers.Any(t => t.IsKind(SyntaxKind.ReadOnlyKeyword));

                paramSummaries.TryGetValue(param.Identifier.Text, out string? summary);

                parameters.Add(new ParameterDoc
                {
                    Name = param.Identifier.Text,
                    TypeDisplayName = param.Type?.ToString() ?? "var",
                    Summary = summary ?? string.Empty,
                    DefaultValueExpression = defaultValueExpression,
                    IsOptional = defaultValueExpression is not null,
                    IsRef = isRef,
                    IsOut = isOut,
                    IsIn = isIn,
                    IsParams = isParams,
                    IsRefReadonly = isRefReadonly
                });
            }
            return parameters;
        }



        /// <summary>
        /// Core method to extract the content of a specific XML documentation tag from a syntax node.
        /// </summary>
        /// <param name="node">The SyntaxNode that precedes the documentation comment.</param>
        /// <param name="tagName">The name of the tag to extract ("summary", "returns", "remarks", etc.).</param>
        /// <returns>The cleaned content of the tag, or an empty string.</returns>
        public static string ExtractXmlTagContent(SyntaxNode node, string tagName)
        {
            // 1. Get the DocumentationCommentTriviaSyntax structure from the node's leading trivia.
            var xmlComment = node.GetLeadingTrivia()
                                 .Select(t => t.GetStructure())
                                 .OfType<DocumentationCommentTriviaSyntax>()
                                 .FirstOrDefault();

            if (xmlComment == null)
                return string.Empty;

            // 2. Find the specific tag element (e.g., <summary>...</summary>).
            var tagElement = xmlComment.Content.OfType<XmlElementSyntax>()
                                             .FirstOrDefault(e => e.StartTag.Name.ToString() == tagName);

            if (tagElement == null)
            {
                // Fallback: Check for self-closing tags like <summary/> (represented as XmlEmptyElementSyntax)
                var emptyTag = xmlComment.Content.OfType<XmlEmptyElementSyntax>()
                                                .FirstOrDefault(e => e.Name.ToString() == tagName);

                if (emptyTag != null)
                    return string.Empty; // Content is empty for self-closing tags

                return string.Empty;
            }

            // 3. Extract and clean the content within the tag.
            return GetTextFromXmlContent(tagElement.Content);
        }

        /// <summary>
        /// Extracts and cleans the raw text content from a SyntaxList of XML-related nodes.
        /// This is crucial for removing Roslyn-specific trivia like the '/// ' prefix and excessive whitespace.
        /// </summary>
        /// <param name="content">The content nodes (e.g., TextTrivia, Cref, CData) within an XML element.</param>
        /// <returns>The cleaned, normalized text content.</returns>
        internal static string GetTextFromXmlContent(SyntaxList<XmlNodeSyntax> content)
        {
            if (!content.Any())
                return string.Empty;

            // Concatenate all content nodes to get the full raw string, including trivia/markers.
            string text = string.Concat(content.Select(n => n.ToFullString()));

            // Text Cleaning Process:
            string cleanedText = string.Join(" ",
                                    text.Split('\n') // Split the content by newline
                                        .Select(line => line.TrimStart()) // Trim leading whitespace from each line
                                        .Select(line => line.TrimStart('/', '*')) // Remove '///' or '**' at the start of documentation lines
                                        .Select(line => line.Trim())) // Trim remaining whitespace
                                        .Trim(); // Final trim of the whole block

            // Replace multiple spaces with a single space to normalize formatting (e.g., for multi-line summaries).
            cleanedText = CleanRegex().Replace(cleanedText, " ");

            return cleanedText;
        }



        ///<summary> Extracts documentation for generic type parameters from the XML &lt;typeparam&gt; tags </summary>
        internal static Dictionary<string, string> ExtractXmlTypeParameterSummaries(MemberDeclarationSyntax parentNode)
        {
            var summaries = new Dictionary<string, string>();

            // 1. Roslyn-Kommentarstruktur abrufen.
            DocumentationCommentTriviaSyntax xmlComment = parentNode.GetLeadingTrivia().Select(t => t.GetStructure()).OfType<DocumentationCommentTriviaSyntax>().FirstOrDefault()!;

            if (xmlComment == null)
                return summaries;

            // 2. Nach dem <typeparam>-Tag filtern.
            IEnumerable<XmlEmptyElementSyntax> typeparamElements = xmlComment.Content.OfType<XmlEmptyElementSyntax>()
                                                                .Where(e => e.Name.ToString() == "typeparam");

            // NOTE: <typeparam> wird oft als XmlEmptyElementSyntax dargestellt (selbstschließend), 
            // ist aber in neueren Versionen oder bei Inhalt auch XmlElementSyntax.
            // Wir fangen hier beide ab, fokussieren aber auf den Identifier-Teil.

            // *Für XmlElementSyntax (wenn Inhalt vorhanden)*
            IEnumerable<XmlElementSyntax> typeparamContentElements = xmlComment.Content.OfType<XmlElementSyntax>()
                                                                .Where(e => e.StartTag.Name.ToString() == "typeparam");

            // Zusammenführen und Verarbeiten (wir gehen davon aus, dass der Name immer als Attribut 'name' drin ist)

            // A) Verarbeitung der XmlEmptyElementSyntax (<typeparam name="T"/>)
            foreach (XmlEmptyElementSyntax typeparamElement in typeparamElements)
            {
                XmlNameAttributeSyntax nameAttribute = typeparamElement.Attributes.OfType<XmlNameAttributeSyntax>().FirstOrDefault(a => a.Name.LocalName.Text == "name")!;

                if (nameAttribute != null)
                {
                    string typeParamName = nameAttribute.Identifier.ToString().Trim();
                    // Inhalt ist leer für selbstschließendes Tag, daher muss der Inhalt 
                    // direkt nach dem Attribut im Text-Trivia gesucht werden. (Komplex)
                    // Für's Erste: Wir setzen den Summary auf leer, wenn es ein EmptyElement ist.
                    summaries[typeParamName] = string.Empty;
                }
            }

            // B) Verarbeitung der XmlElementSyntax (<typeparam name="T">Description</typeparam>)
            foreach (XmlElementSyntax typeparamElement in typeparamContentElements)
            {
                XmlNameAttributeSyntax nameAttribute = typeparamElement.StartTag.Attributes.OfType<XmlNameAttributeSyntax>().FirstOrDefault(a => a.Name.LocalName.Text == "name")!;

                if (nameAttribute == null)
                    continue;

                string typeParamName = nameAttribute.Identifier.ToString().Trim();

                // Den Inhalt extrahieren und bereinigen
                string content = GetTextFromXmlContent(typeparamElement.Content);

                summaries[typeParamName] = content;
            }

            return summaries;
        }

        [GeneratedRegex("<.*?>", RegexOptions.Compiled)]
        public static partial  Regex MyRegex();

        [GeneratedRegex(@"\s+")]
        public static partial Regex CleanRegex();
    }
}
