using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using xyDocumentor.Core.Docs;
using xyDocumentor.Core.Renderer;
using xyToolz.Filesystem;

namespace xyDocumentor.Core.Helpers
{
    public static class Utils
    {
        /// <summary>
        /// Flattens all attributes of a type/member into a simple list of names
        /// </summary>
        /// <param name="listedAttributesFromMember_"> List of SyntaxNodes</param>
        /// <returns></returns>
        public static IEnumerable<string> FlattenAttributes(SyntaxList<AttributeListSyntax> listedAttributesFromMember_)
        {
            // Store the results to return them for later use
            List<string> lst_Results = new();

            // For every SyntaxNode (here: List of Attributes) in the List
            foreach (AttributeListSyntax als_ListOfAttributes in listedAttributesFromMember_)
            {
                // For every attribute
                foreach (AttributeSyntax as_Attribute in als_ListOfAttributes.Attributes)
                {
                    // Read the name and add its string representation to the list
                    NameSyntax ns_AttributeName = as_Attribute.Name;
                    string s_AttributeString = ns_AttributeName.ToString();
                    lst_Results.Add(s_AttributeString);
                }
            }
            return lst_Results;
        }

        /// <summary>
        /// Extracts base types and interfaces
        /// </summary>
        /// <param name="baseList_"></param>
        /// <returns></returns>
        public static List<string> ExtractBaseTypes(BaseListSyntax baseList_)
        {
            // Storage for returning the results
            List<string> lst_BaseTypes = [];

            // Checks for an invalid parameter
            if (baseList_ is null || baseList_.Span.IsEmpty)
            {
                // log invalid parameter here
                return lst_BaseTypes;
            }

            // For every type in the externally provided list
            foreach (BaseTypeSyntax type in baseList_.Types)
            {
                // Add its String representation
                lst_BaseTypes.Add(type.Type.ToString());
            }
            return lst_BaseTypes;
        }

        /// <summary>
        /// Extracts the clean text from the XML summary for a given syntax node.
        /// </summary>
        public static string ExtractXmlSummaryFromSyntaxNode(SyntaxNode syn_Node_)
        {
            // Read the trivia syntax element from the target node
            DocumentationCommentTriviaSyntax trivia = syn_Node_.GetLeadingTrivia().Select(t => t.GetStructure()).OfType<DocumentationCommentTriviaSyntax>().FirstOrDefault();

            if (trivia != null)
            {
                // Read the content from the XmlSummary section
                XmlElementSyntax xesXmlSummary = trivia.Content.OfType<XmlElementSyntax>().FirstOrDefault(x => x.StartTag.Name.LocalName.Text == "summary");

                if (xesXmlSummary != null)
                {
                    // For consistent String handling
                    StringBuilder sb_SummaryBuilder = new();

                    // For every item in the content list
                    foreach (XmlNodeSyntax content in xesXmlSummary.Content)
                    {
                        // Check if the content is an XML text token, which holds the actual text.
                        if (content is XmlTextSyntax textNode)
                        {
                            // Append the raw text from the token, which does not contain the "///" characters.
                            sb_SummaryBuilder.Append(textNode.TextTokens.FirstOrDefault().ValueText);
                        }
                        else
                        {
                            // For other elements like <see>, just append the raw string and let CleanDoc handle it.
                            sb_SummaryBuilder.Append(content.ToString());
                        }
                    }

                    // If theres something in the buffer
                    if (sb_SummaryBuilder.Length > 0)
                    {

                        // Write the string from the builder
                        string summary = sb_SummaryBuilder.ToString();

                        // Remove xml tags and other unwanted bullshit
                        if (CleanDoc(summary) is string txt)
                        {
                            // Remove leading and trailing whitespaces
                            string s_TrimmedTxt = txt.Trim();
                        }
                    }
                }
            }
            // else
            {
                return "(No XML-Summary)";
            }
        }

        /// <summary>
        /// Remove XML Tags from the target and decode it into a String
        /// </summary>
        /// <param name="raw_"></param>
        /// <returns></returns>
        public static string CleanDoc(string raw_)
        {
            // Remove html elements
            string s_CleanedResult = raw_.Replace("<para>", "\n").Replace("</para>", "\n");

            // Remove any remaining tags
            s_CleanedResult = Regex.Replace(s_CleanedResult, "<.*?>", string.Empty);

            // Decode HTML entities
            s_CleanedResult = WebUtility.HtmlDecode(s_CleanedResult);

            // Remove leading and tailing whitespaces
            s_CleanedResult.Trim();

            return s_CleanedResult;
        }

        /// <summary>
        /// Check if a member has public-like visibility
        /// </summary>
        /// <param name="stl_Modifiers_"></param>
        /// <returns></returns>
        public static bool HasPublicLike(SyntaxTokenList stl_Modifiers_)
        {
            // Check if the given SyntaxToken is either: treat "public" and "protected" as "public-like"
            if (stl_Modifiers_.Any(m => m.IsKind(SyntaxKind.PublicKeyword))) return true;
            if (stl_Modifiers_.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword))) return true;

            // default is private => false
            return false;
        }

        /// <summary>
        /// Creates a MemberDoc from a Roslyn MemberDeclarationSyntax instance
        /// </summary>
        /// <param name="mds_Member_"></param>
        /// <returns> A MemberDoc with the combined data of a single member of [...]</returns>
        public static MemberDoc CreateMemberDoc(MemberDeclarationSyntax mds_Member_)
        {
            // Fill MemberDoc with the data of the given member
            MemberDoc doc = new()
            {
                // What kind of member is this?
                Kind = mds_Member_.Kind().ToString().Replace("Declaration", "").ToLower(),

                // Get the signature for the target
                Signature = ExtractSignature(mds_Member_),

                // Read the summary from the xml comment
                Summary = ExtractXmlSummaryFromSyntaxNode(mds_Member_)
            };
            return doc;
        }

        /// <summary>
        /// Check the DeclarationSyntax and return the corresponding signature as a readable string
        /// </summary>
        /// <param name="mds_Member_"></param>
        /// <returns>string s_Signature = "depends on the type of member declaration"</returns>
        private static string ExtractSignature(MemberDeclarationSyntax mds_Member_)
        {
            switch (mds_Member_)
            {
                case MethodDeclarationSyntax m:
                    {
                        string s_MethodString = $"{m.Identifier}{m.TypeParameterList}{m.ParameterList}";
                        return s_MethodString;
                    }
                case PropertyDeclarationSyntax p:
                    {
                        string s_PropertyString = $"{p.Type} {p.Identifier} {{ ... }}";
                        return s_PropertyString;
                    }
                case FieldDeclarationSyntax f:
                    {
                        string s_FieldString = string.Join(", ", f.Declaration.Variables.Select(v => $"{f.Declaration.Type} {v.Identifier}"));
                        return s_FieldString;
                    }
                case EventDeclarationSyntax e:
                    {
                        string s_EventString = $"event {e.Type} {e.Identifier}";
                        return s_EventString;
                    }
                case EventFieldDeclarationSyntax ef:
                    {
                        string s_EventFieldString = string.Join(", ", ef.Declaration.Variables.Select(v => $"event {ef.Declaration.Type} {v.Identifier}"));
                        return s_EventFieldString;
                    }
                case ConstructorDeclarationSyntax c:
                    {
                        string s_ConstructorString = $"{c.Identifier}{c.ParameterList}";
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
        /// Writes each TypeDoc to a file, grouped by namespace, in the requested format.
        /// Supports JSON, HTML, PDF, Markdown.
        /// </summary>
        public static async Task<bool> WriteDataToFilesOrderedByNamespace(IEnumerable<TypeDoc> alltypes, string outpath, string format)
        {
            bool isWritten = false;

            // Iterating through the list 
            foreach (TypeDoc tD in alltypes)
            {
                // Creating a folder for each namespace
                string namespaceFolder = Path.Combine(outpath, tD.Namespace.Replace('<', '_').Replace('>', '_'));
                Directory.CreateDirectory(namespaceFolder);

                string fileName = Path.Combine(namespaceFolder, tD.DisplayName.Replace(' ', '_'));
                string content;

                // Choosing the format and saving converted data to the target file
                switch (format)
                {
                    case "json":
                        fileName += ".json";
                        content = JsonRenderer.Render(tD);
                        isWritten = await xyFiles.SaveToFile(content, fileName);
                        break;

                    case "html":
                        fileName += ".html";
                        content = HtmlRenderer.Render(tD, cssPath: null);
                        isWritten = await xyFiles.SaveToFile(content, fileName);
                        break;

                    case "pdf":
                        fileName += ".pdf";
                        PdfRenderer.RenderToFile(tD, fileName);
                        isWritten = true;
                        break;

                    default: // Markdown
                        fileName += ".md";
                        content = MarkdownRenderer.Render(tD);
                        isWritten = await xyFiles.SaveToFile(content, fileName);
                        break;
                }
            }
            return isWritten;
        }


        /// <summary>
        /// Checks if a given file path contains any excluded folder parts.
        /// </summary>
        public static bool IsExcluded(string path, HashSet<string> excludeParts)
        {
            var parts = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            return parts.Any(p => excludeParts.Contains(p));
        }


    }
}
