using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using xyDocumentor.Core.Docs;
using xyDocumentor.Core.Helpers;

namespace xyDocumentor.Core.Parser
{
    /// <summary>
    /// Parses C# files in a directory and returns all TypeDocs
    /// </summary>
    public class ProjectParser
    {
        /// <summary>
        /// Indicates whether non-public members should be included in the operation.
        /// </summary>
        /// <remarks>This field determines if non-public members, such as private or internal members, 
        /// are considered during the operation. It is a read-only field and cannot be modified  after
        /// initialization.</remarks>
        private readonly bool _includeNonPublic;
        private readonly HashSet<string> _excludeParts;

        /// <summary>
        /// Constructs the ProjectParser based on the parameters
        /// </summary>
        /// <param name="includesNonPublic_"></param>
        /// <param name="excludeTheseParts_"></param>
        public ProjectParser(bool includesNonPublic_, IEnumerable<string> excludeTheseParts_ = null)
        {
            _includeNonPublic = includesNonPublic_;
            _excludeParts = excludeTheseParts_ is null?      new HashSet<string>(StringComparer.OrdinalIgnoreCase) : 
                                                                                        new HashSet<string>(excludeTheseParts_, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Scan a root folder recursively and return all TypeDocs
        /// </summary>
        /// <param name="rootPath"></param>
        /// <returns></returns>
        public List<TypeDoc> ParseProject(string rootPath)
        {
            // 
            List<string> listedCsFiles = Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories).Where(p => !IsExcluded(p)).ToList();

            var allTypes = new List<TypeDoc>();

            foreach (var file in listedCsFiles)
            {
                var text = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Preview));
                var root = tree.GetCompilationUnitRoot();

                // Find namespaces (both block and file-scoped)
                var nsDecls = new List<BaseNamespaceDeclarationSyntax>();
                nsDecls.AddRange(root.Members.OfType<NamespaceDeclarationSyntax>());
                nsDecls.AddRange(root.Members.OfType<FileScopedNamespaceDeclarationSyntax>());

                if (nsDecls.Count == 0)
                {
                    // global namespace
                    allTypes.AddRange(ProcessMembers(root.Members, null, file));
                }
                else
                {
                    foreach (var ns in nsDecls)
                        allTypes.AddRange(ProcessMembers(ns.Members, ns.Name.ToString(), file));
                }
            }

            return allTypes;
        }

        /// <summary>
        /// Process members in a namespace or global scope
        /// </summary>
        private List<TypeDoc> ProcessMembers(SyntaxList<MemberDeclarationSyntax> members, string ns, string file)
        {
            var allTypes = new List<TypeDoc>();

            foreach (var m in members)
            {
                switch (m)
                {
                    case ClassDeclarationSyntax cls:
                        allTypes.Add(HandleType(cls, ns, file));
                        break;
                    case StructDeclarationSyntax st:
                        allTypes.Add(HandleType(st, ns, file));
                        break;
                    case InterfaceDeclarationSyntax itf:
                        allTypes.Add(HandleType(itf, ns, file));
                        break;
                    case RecordDeclarationSyntax rec:
                        allTypes.Add(HandleType(rec, ns, file));
                        break;
                    case EnumDeclarationSyntax en:
                        allTypes.Add(HandleEnum(en, ns, file));
                        break;
                }
            }

            return allTypes;
        }

        private TypeDoc HandleType(TypeDeclarationSyntax tds_Type_, string namespace_, string filePath_, string parentName_ = null)
        {
            var modifiers = tds_Type_.Modifiers.ToString();
            bool isPublic = modifiers.Contains("public");
            if (!_includeNonPublic && !isPublic) return null!;

            TypeDoc td = new()
            {
                Kind = tds_Type_.Keyword.ValueText,
                Name = tds_Type_.Identifier.Text + (tds_Type_.TypeParameterList?.ToString() ?? string.Empty),
                Namespace = namespace_ ?? "<global>",
                Modifiers = modifiers.Trim(),
                Attributes = (List<string>)Utils.FlattenAttributes(tds_Type_.AttributeLists),
                BaseTypes = (List<string>)Utils.ExtractBaseTypes(tds_Type_.BaseList),
                Summary = Utils.ExtractXmlSummaryFromSyntaxNode(tds_Type_),
                FilePath = filePath_,
                Parent = parentName_
            };

            foreach (MemberDeclarationSyntax mem in tds_Type_.Members)
            {
                if (!_includeNonPublic && !Utils.HasPublicLike(mem.GetModifiers()))
                    continue;

                switch (mem)
                {
                    case ClassDeclarationSyntax cls:
                    case StructDeclarationSyntax st:
                    case InterfaceDeclarationSyntax itf:
                    case RecordDeclarationSyntax rec:
                        // Nested types
                        td.NestedTypes().Add(HandleType((TypeDeclarationSyntax)mem, namespace_, filePath_, parentName_: td.Name));
                        break;
                    default:
                        td.AddMember(Utils.CreateMemberDoc(mem));
                        break;
                }
            }

            return td;
        }

        private TypeDoc HandleEnum(EnumDeclarationSyntax en, string ns, string file)
        {
            var modifiers = en.Modifiers.ToString();
            bool isPublic = modifiers.Contains("public");
            if (!_includeNonPublic && !isPublic) return null!;

            var td = new TypeDoc
            {
                Kind = "enum",
                Name = en.Identifier.Text,
                Namespace = ns ?? "<global>",
                Modifiers = modifiers.Trim(),
                Attributes = (List<string>) Utils.FlattenAttributes(en.AttributeLists),
                BaseTypes = new List<string>(),
                Summary = Utils.ExtractXmlSummaryFromSyntaxNode(en),
                FilePath = file
            };

            foreach (var m in en.Members)
            {
                td.Fields.Add(new MemberDoc
                {
                    Kind = "enum-member",
                    Signature = m.Identifier.Text + (m.EqualsValue != null ? $" = {m.EqualsValue.Value}" : string.Empty)
                });
            }

            return td;
        }

        private bool IsExcluded(string path)
        {
            var parts = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            return parts.Any(p => _excludeParts.Contains(p));
        }
    }

    internal static class SyntaxNodeExtensions
    {
        public static SyntaxTokenList GetModifiers(this MemberDeclarationSyntax member) =>
            member switch
            {
                BaseTypeDeclarationSyntax t => t.Modifiers,
                FieldDeclarationSyntax f => f.Modifiers,
                EventDeclarationSyntax e => e.Modifiers,
                EventFieldDeclarationSyntax ef => ef.Modifiers,
                MethodDeclarationSyntax m => m.Modifiers,
                ConstructorDeclarationSyntax c => c.Modifiers,
                PropertyDeclarationSyntax p => p.Modifiers,
                _ => new SyntaxTokenList()
            };
    }

}
