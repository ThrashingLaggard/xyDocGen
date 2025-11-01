using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using xyDocumentor.Docs;
using xyDocumentor.Extractors;
using xyDocumentor.Renderer;
using xyToolz.Filesystem;
using xyToolz.Helper.Logging;
using xyToolz.Logging.Helper;

namespace xyDocumentor.Helpers
{
#nullable enable
    /// <summary>
    /// Little helpers in the fight for better oversight
    /// </summary>
    internal static partial class Utils
    {
        public static string? Description { get; set; }

        /// <summary>
        /// Computes a reasonable default source root:
        /// - In DEBUG builds, walk up from /bin/Debug/... to approximate the repo root.
        /// - In RELEASE builds, use the current working directory.
        /// </summary>
        internal  static string GetDefaultRoot()
        {
#if DEBUG
            // project directory: /bin/Debug/... → step up to repo root-ish
            var cwd = Directory.GetCurrentDirectory();
            var d = Directory.GetParent(cwd);
            if (d?.Parent?.Parent != null)
                return d.Parent.Parent.FullName;
            return cwd;
#else
            return Environment.CurrentDirectory;
#endif
        }






        // Cached dominant root namespace (auto-detected once per run)
        private static string? _cachedDominantRoot = null;

        /// <summary>
        /// Does almost the same as the normal version but with more variables and two  foreaches instead of selects: 
        /// Flattens all attributes of a type/member into a simple list of names
        /// </summary>
        /// <param name="listedAttributesFromMember_"></param>
        /// <returns></returns>
        public static List<string> FlattenAttributes(SyntaxList<AttributeListSyntax> listedAttributesFromMember_)
        {
            // Store the results to return them for later use
            List<string> listedResults = [];

            // For every SyntaxNode (here: List of Attributes) in the List
            foreach (AttributeListSyntax als_ListOfAttributes in listedAttributesFromMember_)
            {
                // For every attribute
                foreach (AttributeSyntax as_Attribute in als_ListOfAttributes.Attributes)
                {
                    // Read the name and add its string representation to the list
                    NameSyntax ns_AttributeName = as_Attribute.Name;
                    string attributeString = ns_AttributeName.ToString();
                    listedResults.Add(attributeString);
                }
            }
            return listedResults;
        }

      
        /// <summary>
        /// Remove XML Tags from the target and decode it into a String
        /// </summary>
        /// <param name="rawXmlString_"></param>
        /// <returns></returns>
        public static string CleanDoc(string rawXmlString_)
        {
            // Remove html elements
            string cleanedResult = rawXmlString_.Replace("<para>", "\n").Replace("</para>", "\n");

            // Remove any remaining tags
            cleanedResult = Extractor.TagRemovalRegex.Replace(cleanedResult, string.Empty);

            // Decode HTML entities
            cleanedResult = WebUtility.HtmlDecode(cleanedResult);

            // Remove leading and tailing whitespaces
            cleanedResult = cleanedResult.Trim();

            return cleanedResult;
        }

        /// <summary>
        /// Check if the given SyntaxToken is either: treat "public" and "protected" as "public-like"
        /// </summary>
        /// <param name="listedModifiers_"></param>
        /// <returns></returns>
        public static bool HasPublicLike(SyntaxTokenList listedModifiers_) =>(listedModifiers_.Any(m => m.IsKind(SyntaxKind.PublicKeyword) || m.IsKind(SyntaxKind.ProtectedKeyword)));
        

        /// <summary>
        /// Creates a MemberDoc from a Roslyn MemberDeclarationSyntax instance
        /// </summary>
        /// <param name="mds_Member_"></param>
        /// <returns> A MemberDoc with the combined data of a single member of [...]</returns>
        public static MemberDoc CreateMemberDoc(MemberDeclarationSyntax mds_Member_)
        {
            string modifiers = string.Join(" ", mds_Member_.Modifiers.Select(m => m.Text));

            // Fill MemberDoc with the data of the given member
            MemberDoc md_Member = new()
            {
                // What kind of member is this?
                Kind = mds_Member_.Kind().ToString().Replace("Declaration", "").ToLower(),

                // Get the signature for the target
                Signature = Extractor.ExtractSignature(mds_Member_),

                // Read the summary from the xml comment
                Summary = Extractor.ExtractXmlSummaryFromSyntaxNode(mds_Member_),

                Modifiers = modifiers,
                Attributes = [.. Utils.FlattenAttributes(mds_Member_.AttributeLists)],

                Remarks = Extractor.ExtractXmlRemarksFromSyntaxNode(mds_Member_),

                ReturnSummary = Extractor.ExtractXmlReturnSummary(mds_Member_), // Nutzt den existierenden Helper

                ReturnType = Extractor.ExtractReturnType(mds_Member_),
                Parameters = (IList<ParameterDoc>)Extractor.ExtractXmlParamSummaries(mds_Member_).ToList(),

                GenericConstraints = Extractor.ExtractGenericConstraints(mds_Member_),
                TypeParameterSummaries = Extractor.ExtractXmlTypeParameterSummaries(mds_Member_)
            };
            return md_Member;
        }


        internal static void PrimeDominantRoot(string? root)
        {
            if (!string.IsNullOrWhiteSpace(_cachedDominantRoot)) return;
            _cachedDominantRoot = root ?? string.Empty;
        }


        /// <summary>
        /// Detects the dominant root namespace (e.g., "xyDocumentor") only once per run.
        /// Subsequent calls reuse the cached value.
        /// </summary>
        internal static string GetDominantRoot(IEnumerable<TypeDoc> types)
        {
            if (!string.IsNullOrEmpty(_cachedDominantRoot))
                return _cachedDominantRoot!;

            string? root = types
                .Select(t => t.Namespace)
                .Where(ns => !string.IsNullOrWhiteSpace(ns))
                .Select(ns => ns!.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
                .Where(first => !string.IsNullOrEmpty(first))
                .GroupBy(first => first)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key;

            _cachedDominantRoot = root ?? string.Empty;
            return _cachedDominantRoot!;
        }


        /// <summary>
        /// Writes each TypeDoc to a file, grouped by namespace, in the requested format.
        /// Supports JSON, HTML, PDF, Markdown.
        /// </summary>
        public static async Task<bool> WriteDataToFilesOrderedByNamespace(IEnumerable<TypeDoc> listedAllTypes_, string outPath_, string format_)
        {
            string content;
            bool isWrittenCurrent;
            bool isWrittenAll = true;
    
            string lowerFormat = format_.ToLowerInvariant();

            var dominantRoot = GetDominantRoot(listedAllTypes_);

            // Iterating through the list 
            foreach (TypeDoc td_TypeInList in listedAllTypes_)
            {
                var ns = td_TypeInList.Namespace ?? string.Empty;
                ns = ns.Replace('<', '_').Replace('>', '_');

                // in Segmente teilen
                var parts = ns.Split('.', StringSplitOptions.RemoveEmptyEntries);

                if (!string.IsNullOrEmpty(dominantRoot) &&parts.Length > 1 &&string.Equals(parts[0], dominantRoot, StringComparison.Ordinal))
                {
                    parts = [.. parts.Skip(1)];
                }
                // Relativer Namespace-Pfad (z. B. "Core/Helpers")
                var relNsPath = parts.Length > 0 ? Path.Combine(parts) : "_";

                // Zielordner bauen
                var namespaceFolder = Path.Combine(outPath_, relNsPath);
                Directory.CreateDirectory(namespaceFolder);

                string cleanedDisplayName = td_TypeInList.DisplayName.Replace(' ', '_').Replace('<', '_').Replace('>', '_');

                string fileName = Path.Combine(namespaceFolder, cleanedDisplayName);

                // Choosing the format and saving converted data to the target file
                switch (lowerFormat)
                {
                    case "json":
                        var filePath = EnsureUniquePath(fileName, ".json");
                        content = JsonRenderer.Render(td_TypeInList);
                        isWrittenCurrent = await xyFiles.SaveToFile(content, filePath);
                        break;

                    case "html":
                        filePath = EnsureUniquePath(fileName, ".html");
                        content = HtmlRenderer.Render(td_TypeInList, cssPath: null);
                        isWrittenCurrent = await xyFiles.SaveToFile(content, filePath);
                        break;

                    case "pdf":
                        filePath = EnsureUniquePath(fileName, ".pdf");
                        PdfRenderer.RenderToFile(td_TypeInList, filePath);
                        isWrittenCurrent = true;
                        break;

                    default: // Markdown
                        filePath = EnsureUniquePath(fileName, ".md");
                        content = MarkdownRenderer.Render(td_TypeInList);
                        isWrittenCurrent = await xyFiles.SaveToFile(content, filePath);
                        break;
                }
                if(isWrittenCurrent is false)
                {
                    isWrittenAll = isWrittenCurrent;
                    xyLog.Log($"Wrote the current type {isWrittenCurrent}");
                }
            }
            return isWrittenAll;
        }

        private static string EnsureUniquePath(string baseWithoutExt, string ext)
        {
            var path = baseWithoutExt + ext;
            int i = 1;
            while (File.Exists(path))
                path = $"{baseWithoutExt}~{i++}{ext}";
            return path;
        }


        /// <summary>
        /// Checks if a given file path contains any excluded folder parts.
        /// </summary>
        public static bool IsExcluded(string path, HashSet<string> excludeParts)
        {
            var parts = path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
            return parts.Any(p => excludeParts.Contains(p));
        }

   
    }
}


