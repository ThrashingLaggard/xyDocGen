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

        internal static readonly Regex TagRemovalRegex = MyRegex();

        private const string NO_XML_SUMMARY_FALLBACK = "(No XML-Summary)";

        /// <summary>
        /// Extracts the clean text from the XML summary for a given syntax node.
        /// </summary>
        public static string ExtractXmlSummaryFromSyntaxNode(SyntaxNode syn_Node_)
        {
            DocumentationCommentTriviaSyntax trivia = syn_Node_.GetLeadingTrivia().Select(t => t.GetStructure()).OfType<DocumentationCommentTriviaSyntax>().FirstOrDefault()!;

            if (trivia == null) goto Fallback;

            XmlElementSyntax xes_XmlSummary = trivia.Content.OfType<XmlElementSyntax>().FirstOrDefault(x => x.StartTag.Name.LocalName.Text.Equals("summary", System.StringComparison.OrdinalIgnoreCase))!;

            if (xes_XmlSummary == null) goto Fallback;

            StringBuilder sb_SummaryBuilder = new();

            foreach (XmlNodeSyntax content in xes_XmlSummary.Content)
            {
                if (content is XmlTextSyntax textNode)
                {
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

            if (!(sb_SummaryBuilder.Length > 0))
            {
                goto Fallback;
            }
            else
            {
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
        internal static string ExtractXmlRemarksFromSyntaxNode(MemberDeclarationSyntax mds_Member_) =>ExtractXmlTagContent(mds_Member_, "remarks");
        

        /// <summary>
        /// Extracts the content of the &lt;returns&gt; XML documentation tag.
        /// </summary>
        /// <param name="memberNode">The member node (Method, Property, Delegate) to inspect.</param>
        /// <returns>The cleaned content of the returns tag, or an empty string.</returns>
        internal static string ExtractXmlReturnSummary(MemberDeclarationSyntax memberNode) => ExtractXmlTagContent(memberNode, "returns");
        
        // Freakness overload
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

            DocumentationCommentTriviaSyntax xmlComment = parentNode.GetLeadingTrivia().Select(t => t.GetStructure()).OfType<DocumentationCommentTriviaSyntax>().FirstOrDefault()!;

            if (xmlComment == null)
                return summaries;

            IEnumerable<XmlElementSyntax> paramElements = xmlComment.Content.OfType<XmlElementSyntax>().Where(e => e.StartTag.Name.ToString() == "param");

            foreach (XmlElementSyntax paramElement in paramElements)
            {
                XmlNameAttributeSyntax nameAttribute = paramElement.StartTag.Attributes.OfType<XmlNameAttributeSyntax>().FirstOrDefault(a => a.Name.LocalName.Text == "name")!;

                if (nameAttribute == null)
                    continue;

                string paramName = nameAttribute.Identifier.ToString().Trim();

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
            if (baseList_ is null || baseList_.Span.IsEmpty)
            {
                return [];
            }
            
            return baseList_.Types.Select(type => type.Type.ToString());
        }

        /// <summary>
        /// Extracts constraints for generic methods/types (e.g., 'where T : class, new()').
        /// </summary>
        internal static List<string> ExtractGenericConstraints(MemberDeclarationSyntax mds_MemberNode_)
        {
            List<string> listedConstraints = [];

            if (mds_MemberNode_ is MethodDeclarationSyntax methodNode)
            {
                if (methodNode.ConstraintClauses.Any())
                {
                    foreach (TypeParameterConstraintClauseSyntax tpcc_Clause in methodNode.ConstraintClauses)
                    {
                        // ToFullString(), um die vollständige Klausel zu kriegen (z.B. "where T : struct")
                        listedConstraints.Add(tpcc_Clause.ToFullString().Trim());
                    }
                }
            }
           
            return listedConstraints;
        }

        /// <summary>
        /// Extracts detailed parameter documentation by combining Roslyn's parameter structure 
        /// with the extracted XML documentation summaries.
        /// </summary>
        internal static IList<ParameterDoc> ExtractParameterDocs(MemberDeclarationSyntax mds_MemberNode_)
        {
            List<ParameterDoc> paramDocs = [];

            IDictionary<string, string> xmlSummaries = ExtractXmlParamSummaries(mds_MemberNode_);

            ParameterListSyntax? parameterList = null;

            if (mds_MemberNode_ is MethodDeclarationSyntax m)
                parameterList = m.ParameterList;
            else if (mds_MemberNode_ is ConstructorDeclarationSyntax c)
                parameterList = c.ParameterList;
            else if (mds_MemberNode_ is DelegateDeclarationSyntax d)
                parameterList = d.ParameterList;

            if (parameterList == null)
                return paramDocs;

            foreach (var roslynParam in parameterList.Parameters)
            {
                string paramName = roslynParam.Identifier.Text;
                string paramType = roslynParam.Type?.ToString() ?? "var"; 

                xmlSummaries.TryGetValue(paramName, out string? paramSummary);

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
                _ => string.Empty // Keine Rückgabetypen für Konstruktoren, Klassen etc!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            };
        }

        /// <summary>
        /// Check the DeclarationSyntax and return the corresponding signature as a readable string
        /// </summary>
        /// <param name="mds_Member_"></param>
        /// <returns>string s_Signature = "depends on the type of member declaration"</returns>
        internal  static string ExtractSignature(MemberDeclarationSyntax mds_Member_)
        {

            string modifiers = string.Join(" ", mds_Member_.Modifiers.Select(m => m.Text));

            modifiers = string.IsNullOrWhiteSpace(modifiers) ? "" : $"{modifiers} ";

            switch (mds_Member_)
            {
                case MethodDeclarationSyntax m:
                    {
                        string s_MethodString = $"{modifiers}{m.ReturnType} {m.Identifier}{m.TypeParameterList}{m.ParameterList}";
                        return s_MethodString;
                    }
                case PropertyDeclarationSyntax p:
                    {
                        string s_PropertyString = $"{modifiers}{p.Type} {p.Identifier} {{ ... }}";
                        return s_PropertyString;
                    }
                case FieldDeclarationSyntax f:
                    {
                        string typeName = f.Declaration.Type.ToString();
                        string variableNames = string.Join(", ", f.Declaration.Variables.Select(v => v.Identifier.Text));
                        return $"{modifiers}{typeName} {variableNames}";

                    }
                case EventDeclarationSyntax e:
                    {                        
                        string s_EventString = $"{modifiers}event {e.Type} {e.Identifier}";
                        return s_EventString;
                    }
                case EventFieldDeclarationSyntax ef:
                    {
                        return $"{modifiers}event {ef.Declaration.Type} {string.Join(", ", ef.Declaration.Variables.Select(v => v.Identifier.Text))}";
                    }
                case ConstructorDeclarationSyntax c:
                    {
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
            var xmlComment = node.GetLeadingTrivia().Select(t => t.GetStructure()).OfType<DocumentationCommentTriviaSyntax>().FirstOrDefault();

            if (xmlComment == null) return string.Empty;

            var tagElement = xmlComment.Content.OfType<XmlElementSyntax>().FirstOrDefault(e => e.StartTag.Name.ToString() == tagName);

            if (tagElement == null)
            {
                var emptyTag = xmlComment.Content.OfType<XmlEmptyElementSyntax>().FirstOrDefault(e => e.Name.ToString() == tagName);

                if (emptyTag != null)   return string.Empty; 

                return string.Empty;
            }

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

            string text = string.Concat(content.Select(n => n.ToFullString()));

            string cleanedText = string.Join(" ", text.Split('\n').Select(line => line.TrimStart()).Select(line => line.TrimStart('/', '*')).Select(line => line.Trim())).Trim();

            cleanedText = CleanRegex().Replace(cleanedText, " ");

            return cleanedText;
        }



        ///<summary> Extracts documentation for generic type parameters from the XML &lt;typeparam&gt; tags </summary>
        internal static Dictionary<string, string> ExtractXmlTypeParameterSummaries(MemberDeclarationSyntax parentNode)
        {
            Dictionary<string, string>  summaries = new ();

            DocumentationCommentTriviaSyntax xmlComment = parentNode.GetLeadingTrivia().Select(t => t.GetStructure()).OfType<DocumentationCommentTriviaSyntax>().FirstOrDefault()!;

            if (xmlComment == null)
                return summaries;

            IEnumerable<XmlEmptyElementSyntax> typeparamElements = xmlComment.Content.OfType<XmlEmptyElementSyntax>().Where(e => e.Name.ToString() == "typeparam");

           IEnumerable<XmlElementSyntax> typeparamContentElements = xmlComment.Content.OfType<XmlElementSyntax>().Where(e => e.StartTag.Name.ToString() == "typeparam");

            foreach (XmlEmptyElementSyntax typeparamElement in typeparamElements)
            {
                XmlNameAttributeSyntax nameAttribute = typeparamElement.Attributes.OfType<XmlNameAttributeSyntax>().FirstOrDefault(a => a.Name.LocalName.Text == "name")!;

                if (nameAttribute != null)
                {
                    string typeParamName = nameAttribute.Identifier.ToString().Trim();
                  
                    summaries[typeParamName] = string.Empty;
                }
            }

            foreach (XmlElementSyntax typeparamElement in typeparamContentElements)
            {
                XmlNameAttributeSyntax nameAttribute = typeparamElement.StartTag.Attributes.OfType<XmlNameAttributeSyntax>().FirstOrDefault(a => a.Name.LocalName.Text == "name")!;

                if (nameAttribute == null)
                    continue;

                string typeParamName = nameAttribute.Identifier.ToString().Trim();

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
