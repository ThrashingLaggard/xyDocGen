using FluentAssertions.Types;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using xyDocumentor.Core.Docs;
using xyDocumentor.Core.Helpers;

namespace xyDocumentor.Core.Extractors
{
    /// <summary>
    /// Extracts types (classes, structs, interfaces, records, enums) and their members from C# syntax trees
    /// </summary>
    public class TypeExtractor
    {
        private readonly bool _includeNonPublic;

        public TypeExtractor(bool includeNonPublic)
        {
            _includeNonPublic = includeNonPublic;
        }

        /// <summary>
        /// Process all members in a namespace or global scope
        /// </summary>
        public List<TypeDoc> ProcessMembers(SyntaxList<MemberDeclarationSyntax> members, string? ns, string file)
        {
            var typesInFile = new List<TypeDoc>();
            foreach (var m in members)
            {
                switch (m)
                {
                    case ClassDeclarationSyntax cls:
                        typesInFile.Add(HandleType(cls, ns, file));
                        break;
                    case StructDeclarationSyntax st:
                        typesInFile.Add(HandleType(st, ns, file));
                        break;
                    case InterfaceDeclarationSyntax itf:
                        typesInFile.Add(HandleType(itf, ns, file));
                        break;
                    case RecordDeclarationSyntax rec:
                        typesInFile.Add(HandleType(rec, ns, file));
                        break;
                    case EnumDeclarationSyntax en:
                        typesInFile.Add(HandleEnum(en, ns, file));
                        break;
                }
            }
            return typesInFile;
        }

        /// <summary>
        /// Handles class/struct/interface/record extraction, including members and nested types
        /// </summary>
        public TypeDoc HandleType(TypeDeclarationSyntax type, string? ns, string file, TypeDoc? parentType = null)
        {
            var modifiers = type.Modifiers.ToString();
            bool isPublic = modifiers.Contains("public");
            if (!_includeNonPublic && !isPublic) return null!;

            var td = new TypeDoc
            {
                Kind = type.Keyword.ValueText,
                Name = type.Identifier.Text + (type.TypeParameterList?.ToString() ?? string.Empty),
                Namespace = ns ?? "<global>",
                Modifiers = modifiers.Trim(),
                Attributes = Utils.FlattenAttributes(type.AttributeLists),
                BaseTypes = Utils.ExtractBaseTypes(type.BaseList),
                Summary = Utils.ExtractXmlSummaryFromSyntaxNode(type),
                FilePath = file,
                Parent = parentType?.Name!
            };

            foreach (var mem in type.Members)
            {
                switch (mem)
                {
                    case ConstructorDeclarationSyntax ctor:
                        if (_includeNonPublic || Utils.HasPublicLike(ctor.Modifiers))
                            td.Constructors.Add(new MemberDoc
                            {
                                Kind = "ctor",
                                Signature = ctor.Identifier.Text + ctor.ParameterList.ToString(),
                                Modifiers = ctor.Modifiers.ToString().Trim(),
                                Summary = Utils.ExtractXmlSummaryFromSyntaxNode(ctor)
                            });
                        break;
                    case MethodDeclarationSyntax mth:
                        if (_includeNonPublic || Utils.HasPublicLike(mth.Modifiers))
                            td.Methods.Add(new MemberDoc
                            {
                                Kind = "method",
                                Signature = $"{mth.ReturnType} {mth.Identifier}{mth.TypeParameterList}{mth.ParameterList}",
                                Modifiers = mth.Modifiers.ToString().Trim(),
                                Summary = Utils.ExtractXmlSummaryFromSyntaxNode(mth)
                            });
                        break;
                    case PropertyDeclarationSyntax prop:
                        if (_includeNonPublic || Utils.HasPublicLike(prop.Modifiers))
                            td.Properties.Add(new MemberDoc
                            {
                                Kind = "property",
                                Signature = $"{prop.Type} {prop.Identifier}{prop.AccessorList}",
                                Modifiers = prop.Modifiers.ToString().Trim(),
                                Summary = Utils.ExtractXmlSummaryFromSyntaxNode(prop)
                            });
                        break;
                    case EventDeclarationSyntax evd:
                        if (_includeNonPublic || Utils.HasPublicLike(evd.Modifiers))
                            td.Events.Add(new MemberDoc
                            {
                                Kind = "event",
                                Signature = $"event {evd.Type} {evd.Identifier}",
                                Modifiers = evd.Modifiers.ToString().Trim(),
                                Summary = Utils.ExtractXmlSummaryFromSyntaxNode(evd)
                            });
                        break;
                    case EventFieldDeclarationSyntax evf:
                        if (_includeNonPublic || Utils.HasPublicLike(evf.Modifiers))
                            td.Events.Add(new MemberDoc
                            {
                                Kind = "event",
                                Signature = $"event {evf.Declaration.Type} {string.Join(", ", evf.Declaration.Variables.Select(v => v.Identifier.Text))}",
                                Modifiers = evf.Modifiers.ToString().Trim(),
                                Summary = Utils.ExtractXmlSummaryFromSyntaxNode(evf)
                            });
                        break;
                    case FieldDeclarationSyntax fld:
                        if (_includeNonPublic || Utils.HasPublicLike(fld.Modifiers))
                            td.Fields.Add(new MemberDoc
                            {
                                Kind = "field",
                                Signature = $"{fld.Declaration.Type} {string.Join(", ", fld.Declaration.Variables.Select(v => v.Identifier.Text))}",
                                Modifiers = fld.Modifiers.ToString().Trim(),
                                Summary = Utils.ExtractXmlSummaryFromSyntaxNode(fld)
                            });
                        break;
                    case ClassDeclarationSyntax ncls:
                    case StructDeclarationSyntax nst:
                    case InterfaceDeclarationSyntax nitf:
                    case RecordDeclarationSyntax nrec:
                        if (_includeNonPublic || Utils.HasPublicLike(((TypeDeclarationSyntax)mem).Modifiers))
                        {
                            td.NestedTypes.Add(HandleType((TypeDeclarationSyntax)mem, ns, file, parentType: td));
                        }
                        break;
                    case EnumDeclarationSyntax nen:
                        if (_includeNonPublic || Utils.HasPublicLike(nen.Modifiers))
                        {
                            td.NestedTypes.Add(HandleEnum(nen, ns, file));
                        }
                        break;
                }
            }
            return td;
        }

        /// <summary>
        /// Handles enums and their members
        /// </summary>
        private TypeDoc HandleEnum(EnumDeclarationSyntax en, string? ns, string file)
        {
            var modifiers = en.Modifiers.ToString();
            bool isPublic = modifiers.Contains("public");
            if (!_includeNonPublic && !isPublic)
                return null!;

            var td = new TypeDoc
            {
                Kind = "enum",
                Name = en.Identifier.Text,
                Namespace = ns ?? "<global>",
                Modifiers = modifiers.Trim(),
                Attributes = Utils.FlattenAttributes(en.AttributeLists),
                BaseTypes = new List<string>(),
                Summary = Utils.ExtractXmlSummaryFromSyntaxNode(en),
                FilePath = file
            };

            foreach (var m in en.Members)
            {
                td.Fields.Add(new MemberDoc
                {
                    Kind = "enum-member",
                    Signature = m.Identifier.Text + (m.EqualsValue != null ? $" = {m.EqualsValue.Value}" : string.Empty),
                    Summary = Utils.ExtractXmlSummaryFromSyntaxNode(m)
                });
            }

            return td;
        }

        /// <summary>
        /// Checks if a member is public/protected/internal
        /// </summary>
        private static bool HasPublicLike(SyntaxTokenList mods)
        {
            return mods.Any(t =>
                t.IsKind(SyntaxKind.PublicKeyword) ||
                t.IsKind(SyntaxKind.ProtectedKeyword) ||
                t.IsKind(SyntaxKind.InternalKeyword));
        }
    }
}