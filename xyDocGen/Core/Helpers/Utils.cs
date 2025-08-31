using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using xyDocGen.Core.Docs;

namespace xyDocGen.Core.Helpers
{
    public static class Utils
    {
        // Flatten all attributes of a type/member into a simple list of names
        public static List<string> FlattenAttributes(SyntaxList<AttributeListSyntax> lists)
        {
            var result = new List<string>();
            foreach (var l in lists)
                foreach (var a in l.Attributes)
                    result.Add(a.Name.ToString());
            return result;
        }

        // Extract base types and interfaces
        public static List<string> ExtractBaseTypes(BaseListSyntax baseList)
        {
            var list = new List<string>();
            if (baseList == null) return list;

            foreach (var t in baseList.Types)
                list.Add(t.Type.ToString());

            return list;
        }

        /// <summary>
        /// Extracts the clean text from the XML summary (/// &lt;summary&gt;) for a given syntax node.
        /// </summary>
        public static string ExtractSummary(SyntaxNode node)
        {
            var trivia = node.GetLeadingTrivia()
                             .Select(t => t.GetStructure())
                             .OfType<DocumentationCommentTriviaSyntax>()
                             .FirstOrDefault();

            if (trivia != null)
            {
                var summary = trivia.Content
                                    .OfType<XmlElementSyntax>()
                                    .FirstOrDefault(x => x.StartTag.Name.LocalName.Text == "summary");

                if (summary != null)
                {
                    // Use a StringBuilder for efficient string concatenation.
                    var sb = new StringBuilder();
                    foreach (var content in summary.Content)
                    {
                        // Check if the content is an XML text token, which holds the actual text.
                        if (content is XmlTextSyntax textNode)
                        {
                            // Append the raw text from the token, which does not contain the "///" characters.
                            sb.Append(textNode.TextTokens.FirstOrDefault().ValueText);
                        }
                        else
                        {
                            // For other elements like <see>, just append the raw string and let CleanDoc handle it.
                            sb.Append(content.ToString());
                        }
                    }

                    var txt = CleanDoc(sb.ToString());
                    if (!string.IsNullOrWhiteSpace(txt))
                    {
                        return txt.Trim();
                    }
                }
            }

            return "(No XML-Summary)";
        }

        // Clean XML doc text to plain text (basic)
        public static string CleanDoc(string raw)
        {
            var s = raw.Replace("<para>", "\n").Replace("</para>", "\n");
            s = Regex.Replace(s, "<.*?>", string.Empty);    // remove any remaining tags
            s = WebUtility.HtmlDecode(s);                   // decode HTML entities
            return s.Trim();
        }

        // Check if a member has public-like visibility
        public static bool HasPublicLike(SyntaxTokenList modifiers)
        {
            // treat "public" and "protected" as "public-like"
            if (modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))) return true;
            if (modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword))) return true;

            // default is private, so false
            return false;
        }

        // create a MemberDoc from a Roslyn MemberDeclarationSyntax
        public static MemberDoc CreateMemberDoc(MemberDeclarationSyntax member)
        {
            var doc = new MemberDoc
            {
                Kind = member.Kind().ToString().Replace("Declaration", "").ToLower(),
                Signature = ExtractSignature(member),
                Summary = ExtractSummary(member)
            };
            return doc;
        }

        // helper: build readable signature string
        private static string ExtractSignature(MemberDeclarationSyntax member)
        {
            switch (member)
            {
                case MethodDeclarationSyntax m:
                    return $"{m.Identifier}{m.TypeParameterList}{m.ParameterList}";
                case PropertyDeclarationSyntax p:
                    return $"{p.Type} {p.Identifier} {{ ... }}";
                case FieldDeclarationSyntax f:
                    return string.Join(", ", f.Declaration.Variables.Select(v => $"{f.Declaration.Type} {v.Identifier}"));
                case EventDeclarationSyntax e:
                    return $"event {e.Type} {e.Identifier}";
                case EventFieldDeclarationSyntax ef:
                    return string.Join(", ", ef.Declaration.Variables.Select(v => $"event {ef.Declaration.Type} {v.Identifier}"));
                case ConstructorDeclarationSyntax c:
                    return $"{c.Identifier}{c.ParameterList}";
                default:
                    return member.ToString().Split('\n').FirstOrDefault()?.Trim() ?? "(unknown)";
            }
        }
    }
}