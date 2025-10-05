using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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


        /// <summary>
        /// Contruct the Extractor and decide whether to include non public members in the process
        /// </summary>
        /// <param name="includeNonPublic_"></param>
        public TypeExtractor(bool includeNonPublic_)
        {
            _includeNonPublic = includeNonPublic_;
        }

        /// <summary>
        /// Process all members in a namespace or global scope
        /// </summary>
        /// <param name="listedMembers_"></param>
        /// <param name="namespace_"></param>
        /// <param name="file_"></param>
        /// <returns></returns>
        public List<TypeDoc> ProcessMembers(SyntaxList<MemberDeclarationSyntax> listedMembers_, string? namespace_, string file_)
        {
            // Used to store the values for return
            List<TypeDoc> ListedTypesInFile = [];

            // For every member declaration:  Call the HandleType() Method with the according parameter
            foreach (MemberDeclarationSyntax memberDeclaration in listedMembers_)
            {
                switch (memberDeclaration)
                {
                    case ClassDeclarationSyntax __Class:
                        ListedTypesInFile.Add(HandleType(__Class, namespace_, file_));
                        break;
                    case StructDeclarationSyntax __Struct:
                        ListedTypesInFile.Add(HandleType(__Struct, namespace_, file_));
                        break;
                    case InterfaceDeclarationSyntax __Interface:
                        ListedTypesInFile.Add(HandleType(__Interface, namespace_, file_));
                        break;
                    case RecordDeclarationSyntax __Record:
                        ListedTypesInFile.Add(HandleType(__Record, namespace_, file_));
                        break;
                    case EnumDeclarationSyntax __Enum:
                        ListedTypesInFile.Add(HandleEnum(__Enum, namespace_, file_));
                        break;
                }
            }
            return ListedTypesInFile;
        }

        /// <summary>
        /// Handles class/struct/interface/record extraction, including members and nested types
        /// </summary>
        /// <param name="type_"></param>
        /// <param name="namespace_"></param>
        /// <param name="filePath_"></param>
        /// <param name="parentType_"></param>
        /// <returns></returns>
        public TypeDoc HandleType(TypeDeclarationSyntax type_, string? namespace_, string filePath_, TypeDoc? parentType_ = null)
        {
            // Store in here for better readability
            string modifiers = type_.Modifiers.ToString();

            // Is the type public?
            bool isPublic = modifiers.Contains("public");

            // If not and therefore not to be processed return null
            if (!_includeNonPublic && !isPublic) return null!;

            // Fill in all the needed data
            TypeDoc td_Result = new()
            {
                Kind = type_.Keyword.ValueText,
                Name = type_.Identifier.Text + (type_.TypeParameterList?.ToString() ?? string.Empty),
                Namespace = namespace_ ?? "<global>",
                Modifiers = modifiers.Trim(),
                Attributes = (List<string>)Utils.FlattenAttributes(type_.AttributeLists),
                BaseTypes = Utils.ExtractBaseTypes(type_.BaseList),
                Summary = Utils.ExtractXmlSummaryFromSyntaxNode(type_),
                FilePath = filePath_,
                Parent = parentType_?.Name!
            };

            // For every member in the type: Create a typedoc and add them to the corresponding list
            foreach (MemberDeclarationSyntax member in type_.Members)
            {
                switch (member)
                {
                    case ConstructorDeclarationSyntax __Constructor:
                        if (_includeNonPublic || Utils.HasPublicLike(__Constructor.Modifiers))
                            td_Result.Constructors.Add(new MemberDoc
                            {
                                Kind = "ctor",
                                Signature = __Constructor.Identifier.Text + __Constructor.ParameterList.ToString(),
                                Modifiers = __Constructor.Modifiers.ToString().Trim(),
                                Summary = Utils.ExtractXmlSummaryFromSyntaxNode(__Constructor)
                            });
                        break;
                    case MethodDeclarationSyntax __Mehtod:
                        if (_includeNonPublic || Utils.HasPublicLike(__Mehtod.Modifiers))
                            td_Result.Methods.Add(new MemberDoc
                            {
                                Kind = "method",
                                Signature = $"{__Mehtod.ReturnType} {__Mehtod.Identifier}{__Mehtod.TypeParameterList}{__Mehtod.ParameterList}",
                                Modifiers = __Mehtod.Modifiers.ToString().Trim(),
                                Summary = Utils.ExtractXmlSummaryFromSyntaxNode(__Mehtod)
                            });
                        break;
                    case PropertyDeclarationSyntax __Property:
                        if (_includeNonPublic || Utils.HasPublicLike(__Property.Modifiers))
                            td_Result.Properties.Add(new MemberDoc
                            {
                                Kind = "property",
                                Signature = $"{__Property.Type} {__Property.Identifier}{__Property.AccessorList}",
                                Modifiers = __Property.Modifiers.ToString().Trim(),
                                Summary = Utils.ExtractXmlSummaryFromSyntaxNode(__Property)
                            });
                        break;
                    case EventDeclarationSyntax __Event:
                        if (_includeNonPublic || Utils.HasPublicLike(__Event.Modifiers))
                            td_Result.Events.Add(new MemberDoc
                            {
                                Kind = "event",
                                Signature = $"event {__Event.Type} {__Event.Identifier}",
                                Modifiers = __Event.Modifiers.ToString().Trim(),
                                Summary = Utils.ExtractXmlSummaryFromSyntaxNode(__Event)
                            });
                        break;
                    case EventFieldDeclarationSyntax __EventField:
                        if (_includeNonPublic || Utils.HasPublicLike(__EventField.Modifiers))
                            td_Result.Events.Add(new MemberDoc
                            {
                                Kind = "event",
                                Signature = $"event {__EventField.Declaration.Type} {string.Join(", ", __EventField.Declaration.Variables.Select(v => v.Identifier.Text))}",
                                Modifiers = __EventField.Modifiers.ToString().Trim(),
                                Summary = Utils.ExtractXmlSummaryFromSyntaxNode(__EventField)
                            });
                        break;
                    case FieldDeclarationSyntax __Field:
                        if (_includeNonPublic || Utils.HasPublicLike(__Field.Modifiers))
                            td_Result.Fields.Add(new MemberDoc
                            {
                                Kind = "field",
                                Signature = $"{__Field.Declaration.Type} {string.Join(", ", __Field.Declaration.Variables.Select(v => v.Identifier.Text))}",
                                Modifiers = __Field.Modifiers.ToString().Trim(),
                                Summary = Utils.ExtractXmlSummaryFromSyntaxNode(__Field)
                            });
                        break;
                    case ClassDeclarationSyntax __Class:
                    case StructDeclarationSyntax __Struct:
                    case InterfaceDeclarationSyntax __Interface:
                    case RecordDeclarationSyntax __Record:
                        if (_includeNonPublic || Utils.HasPublicLike(((TypeDeclarationSyntax)member).Modifiers))
                        {
                            td_Result.NestedTypes.Add(HandleType((TypeDeclarationSyntax)member, namespace_, filePath_, parentType_: td_Result));
                        }
                        break;
                    case EnumDeclarationSyntax nen:
                        if (_includeNonPublic || Utils.HasPublicLike(nen.Modifiers))
                        {
                            td_Result.NestedTypes.Add(HandleEnum(nen, namespace_, filePath_));
                        }
                        break;
                }
            }
            return td_Result;
        }

        /// <summary>
        /// Handles enums and their members
        /// </summary>
        /// <param name="enumDeclaration_"></param>
        /// <param name="namespace_"></param>
        /// <param name="filePath_"></param>
        /// <returns></returns>
        private TypeDoc HandleEnum(EnumDeclarationSyntax enumDeclaration_, string? namespace_, string filePath_)
        {
            // Store the declaration modifiers for better readability
            string modifiers = enumDeclaration_.Modifiers.ToString();

            // Is the Enum public?
            bool isPublic = modifiers.Contains("public");

            // If its not public and thus not to be included return NULL
            if (!_includeNonPublic && !isPublic)
                return null!;
            //Else

            // Fill in all the needed data
            TypeDoc td_Result = new()
            {
                Kind = "enum",
                Name = enumDeclaration_.Identifier.Text,
                Namespace = namespace_ ?? "<global>",
                Modifiers = modifiers.Trim(),
                Attributes = (List<string>)Utils.FlattenAttributes(enumDeclaration_.AttributeLists),
                BaseTypes = new List<string>(),
                Summary = Utils.ExtractXmlSummaryFromSyntaxNode(enumDeclaration_),
                FilePath = filePath_
            };

            // For every member of the Enum: add the corresponding MemberDoc
            foreach (EnumMemberDeclarationSyntax member in enumDeclaration_.Members)
            {
                td_Result.Fields.Add(new MemberDoc
                {
                    Kind = "enum-member",
                    Signature = member.Identifier.Text + (member.EqualsValue != null ? $" = {member.EqualsValue.Value}" : string.Empty),
                    Summary = Utils.ExtractXmlSummaryFromSyntaxNode(member)
                });
            }

            // Return the filled result
            return td_Result;
        }

        /// <summary>
        /// Checks if a member is public/protected/internal
        /// 
        /// by checklng the listed modifiers  
        /// </summary>
        /// <param name="listedModifiers_"></param>
        /// <returns></returns>
        private static bool HasPublicLike(SyntaxTokenList listedModifiers_)
        {
            // For every modifier in the list, check what kind it is
            return listedModifiers_.Any(t =>
                t.IsKind(SyntaxKind.PublicKeyword) ||
                t.IsKind(SyntaxKind.ProtectedKeyword) ||
                t.IsKind(SyntaxKind.InternalKeyword));
        }
    }
}