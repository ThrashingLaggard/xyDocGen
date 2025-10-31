using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using xyDocumentor.Docs;
using xyDocumentor.Helpers;
using xyToolz.Filesystem;
using xyToolz.Helper.Logging;


namespace xyDocumentor.Extractors
{
#nullable enable
    /// <summary>
    /// Extracts types (classes, structs, interfaces, records, enums) and their members from C# syntax trees 
    /// </summary>
    public class TypeExtractor
    {

        /// <summary>
        /// Get or set usefull information
        /// </summary>
        public string? Description { get; set; }

        private readonly bool _includeNonPublic;


        /// <summary>
        /// Contruct the Extractor and decide whether to include non public members in the process
        /// </summary>
        /// <param name="includeNonPublic_"></param>
        public TypeExtractor(bool includeNonPublic_)
        {
            _includeNonPublic = includeNonPublic_;
            xyLog.Log($"Created instance (Include Non-Public = {_includeNonPublic})");
        }

        /// <summary>
        /// Extrahiert Parameterdetails und deren XML-Dokumentation.
        /// </summary>
        private static IList<ParameterDoc> ExtractParameters(ParameterListSyntax parameterList, MemberDeclarationSyntax parentNode)
        {
            IList<ParameterDoc> parameters = [];
            IDictionary<string, string> paramSummaries = Utils.ExtractXmlParamSummaries(parentNode);

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
        /// Handles class/struct/interface/record extraction, including members and nested types by creating the corresponding TypeDocs and MemberDocs
        /// </summary>
        /// <returns></returns>
        public TypeDoc? HandleType(TypeDeclarationSyntax typeNode, string? namespaceName, string filePath, TypeDoc? parentType = null)
        {
            // Prüfen, ob wir non-public Types ignorieren
            if (!_includeNonPublic && parentType == null && !HasPublicLike(typeNode.Modifiers))
                return null;

            TypeDoc typeDoc = new()
            {
                Kind = typeNode.Keyword.ValueText,
                Name = typeNode.Identifier.Text + (typeNode.TypeParameterList?.ToString() ?? string.Empty),
                Namespace = namespaceName ?? "Global (Default)",
                Modifiers = typeNode.Modifiers.ToString().Trim(),
                Attributes = Utils.FlattenAttributes(typeNode.AttributeLists),
                BaseTypes = [.. Utils.ExtractBaseTypes(typeNode.BaseList!)],
                Summary = Utils.ExtractXmlSummaryFromSyntaxNode(typeNode),
                FilePath = filePath,
                Parent = parentType?.Name ?? string.Empty
            };

            foreach (var member in typeNode.Members)
            {
                MemberDoc? memberDoc = null;
                TypeDoc? nestedTypeDoc = null;

                switch (member)
                {
                    case TypeDeclarationSyntax nestedType:
                        nestedTypeDoc = HandleType(nestedType, namespaceName, filePath, typeDoc);
                        if (nestedTypeDoc != null) typeDoc.NestedTypes.Add(nestedTypeDoc);
                        break;

                    case EnumDeclarationSyntax nestedEnum:
                        nestedTypeDoc = HandleEnum(nestedEnum, namespaceName, filePath, typeDoc);
                        if (nestedTypeDoc != null) typeDoc.NestedTypes.Add(nestedTypeDoc);
                        break;

                    case DelegateDeclarationSyntax nestedDelegate:
                        nestedTypeDoc = HandleDelegate(nestedDelegate, namespaceName, filePath, typeDoc);
                        if (nestedTypeDoc != null) typeDoc.NestedTypes.Add(nestedTypeDoc);
                        break;

                    case MethodDeclarationSyntax methodNode:
                        memberDoc = TryCreateMemberDoc(methodNode, "method",
                            $"{methodNode.ReturnType} {methodNode.Identifier}{methodNode.TypeParameterList}{methodNode.ParameterList}");
                        if (memberDoc != null)
                        {
                            memberDoc = memberDoc with
                            {
                                ReturnType = methodNode.ReturnType.ToString(),
                                Parameters = ExtractParameters(methodNode.ParameterList, methodNode),
                                ReturnSummary = Utils.ExtractXmlReturnSummary(methodNode)
                            };
                            typeDoc.Methods.Add(memberDoc);
                        }
                        break;

                    case ConstructorDeclarationSyntax ctorNode:
                        memberDoc = TryCreateMemberDoc(ctorNode, "ctor", ctorNode.Identifier.Text + ctorNode.ParameterList);
                        if (memberDoc != null)
                        {
                            memberDoc = memberDoc with
                            {
                                Parameters = ExtractParameters(ctorNode.ParameterList, ctorNode)
                            };
                            typeDoc.Constructors.Add(memberDoc);
                        }
                        break;

                    case PropertyDeclarationSyntax propertyNode:
                        memberDoc = TryCreateMemberDoc(propertyNode, "property",
                            $"{propertyNode.Type} {propertyNode.Identifier}{propertyNode.AccessorList}");
                        if (memberDoc != null)
                        {
                            memberDoc = memberDoc with
                            {
                                ReturnType = propertyNode.Type.ToString(),
                                ReturnSummary = Utils.ExtractXmlReturnSummary(propertyNode)
                            };
                            typeDoc.Properties.Add(memberDoc);
                        }
                        break;

                    case FieldDeclarationSyntax fieldNode:
                        string fieldSig = $"{fieldNode.Declaration.Type} {string.Join(", ", fieldNode.Declaration.Variables.Select(v => v.Identifier.Text))}";
                        memberDoc = TryCreateMemberDoc(fieldNode, "field", fieldSig);
                        if (memberDoc != null)
                        {
                            memberDoc = memberDoc with { ReturnType = fieldNode.Declaration.Type.ToString() };
                            typeDoc.Fields.Add(memberDoc);
                        }
                        break;

                    case EventDeclarationSyntax eventNode:
                        memberDoc = TryCreateMemberDoc(eventNode, "event", $"event {eventNode.Type} {eventNode.Identifier}");
                        if (memberDoc != null) typeDoc.Events.Add(memberDoc);
                        break;

                    case EventFieldDeclarationSyntax eventFieldNode:
                        string eventSig = $"event {eventFieldNode.Declaration.Type} {string.Join(", ", eventFieldNode.Declaration.Variables.Select(v => v.Identifier.Text))}";
                        memberDoc = TryCreateMemberDoc(eventFieldNode, "event", eventSig);
                        if (memberDoc != null) typeDoc.Events.Add(memberDoc);
                        break;
                }
            }

            return typeDoc;
        }


        /// <summary>
        /// Handles enums and their members by creating corresponding TypeDocs and MemberDocs
        /// </summary>
        /// <param name="enumDeclaration_"></param>
        /// <param name="namespace_"></param> 
        /// <param name="filePath_"></param>
        /// <param name="parentType_"></param>
        /// <returns></returns>
        private TypeDoc? HandleEnum(EnumDeclarationSyntax enumDeclaration_, string? namespace_, string filePath_, TypeDoc? parentType_ = null)
        {
            // If its not public and thus not to be included return NULL
            if (!_includeNonPublic)
            {
                if (parentType_ == null && !HasPublicLike(enumDeclaration_.Modifiers))
                {
                    xyLog.Log($"[TypeExtractor] Skipped non-public top-level enum: {enumDeclaration_.Identifier.Text}");
                    return null;
                }
            }

            // Store the declaration modifiers for better readability
            string modifiers = enumDeclaration_.Modifiers.ToString();

            // Fill in all the needed data
            TypeDoc td_Result = new()
            {
                Kind = "enum",
                Name = enumDeclaration_.Identifier.Text,
                Namespace = namespace_ ?? "Global   (Default)",
                Modifiers = modifiers.Trim(),
                Attributes = Utils.FlattenAttributes(enumDeclaration_.AttributeLists),
                BaseTypes = new List<string>(),
                Summary = Utils.ExtractXmlSummaryFromSyntaxNode(enumDeclaration_),
                FilePath = filePath_,
                Parent = parentType_?.Name ?? string.Empty,
            };

            // For every member of the Enum: add the corresponding MemberDoc
            foreach (EnumMemberDeclarationSyntax member in enumDeclaration_.Members)
            {
                td_Result.Fields.Add(new MemberDoc
                {
                    Kind = "enum-member",
                    Signature = member.Identifier.Text + (member.EqualsValue != null ? $" = {member.EqualsValue.Value}" : string.Empty),
                    Summary = Utils.ExtractXmlSummaryFromSyntaxNode(member),
                    Remarks = Utils.ExtractXmlRemarksFromSyntaxNode(member),
                    Attributes = Utils.FlattenAttributes(member.AttributeLists) 
                });
            }

            // Return the filled result
            return td_Result;
        }

        /// <summary>
        ///  Handles delegate declarations.
        /// </summary>
        /// <param name="delegateNode"></param>
        /// <param name="namespaceName"></param>
        /// <param name="filePath"></param>
        /// <param name="parentType_"></param>
        /// <returns></returns>
        private TypeDoc? HandleDelegate(DelegateDeclarationSyntax delegateNode, string? namespaceName, string filePath, TypeDoc? parentType_ = null)
        {
            if (!_includeNonPublic)
            {
                if (parentType_ == null && !HasPublicLike(delegateNode.Modifiers))
                {
                    xyLog.Log($"[TypeExtractor] Skipped non-public top-level delegate: {delegateNode.Identifier.Text}");
                    return null;
                }
            }


            string modifiers = delegateNode.Modifiers.ToString();

            // Die Signatur eines Delegates ist im Prinzip der Rückgabetyp, der Name und die Parameter.
            string signature = $"{delegateNode.ReturnType} {delegateNode.Identifier}{delegateNode.TypeParameterList}{delegateNode.ParameterList}";

            IList<ParameterDoc> delegateParameters = ExtractParameters(delegateNode.ParameterList, delegateNode);
            string returnType = delegateNode.ReturnType.ToString();
            string returnSummary = Utils.ExtractXmlReturnSummary(delegateNode);


            TypeDoc td_Delegate = new TypeDoc
            {
                Kind = "delegate",
                Name = delegateNode.Identifier.Text + (delegateNode.TypeParameterList?.ToString() ?? string.Empty),
                Namespace = namespaceName ?? "Global",
                Modifiers = modifiers.Trim(),
                Attributes = Utils.FlattenAttributes(delegateNode.AttributeLists),
                Summary = Utils.ExtractXmlSummaryFromSyntaxNode(delegateNode),
                FilePath = filePath,
                BaseTypes = new List<string> { signature }
               
            };

            // Hinzufügen des "Invoke"-Members für die Dokumentation
            td_Delegate.Methods.Add(new MemberDoc
            {
                Kind = "invoke", // Spezial-Kind für Delegate-Aufruf
                Signature = signature,
                Modifiers = modifiers.Trim(),
                Summary = td_Delegate.Summary, // Wiederverwendung der Summary
                Remarks = Utils.ExtractXmlRemarksFromSyntaxNode(delegateNode),
                Parameters = delegateParameters,
                ReturnType = returnType,
                ReturnSummary = returnSummary
            });

            return td_Delegate;
        }

        /// <summary>
        /// Process all members in a namespace or global scope 
        /// </summary>
        /// <param name="listedMembers_"></param>
        /// <param name="namespace_"></param>
        /// <param name="file_"></param>
        /// <returns> A List of TypeDocs filled with all members in the chosen scope</returns>
        public IList<TypeDoc> ProcessMembers(SyntaxList<MemberDeclarationSyntax> listedMembers_, string? namespace_, string file_)
        {
            // Used to store the values for return
            IList<TypeDoc> listedMembers = [];


            // For every member declaration:  Call the HandleType() Method with the according parameter
            foreach (MemberDeclarationSyntax memberDeclaration in listedMembers_)
            {
                if (!_includeNonPublic && !HasPublicLike(memberDeclaration.Modifiers))
                {
                    xyLog.Log($" Skipped non-public top-level member: {memberDeclaration.Kind()}");
                    continue;
                }

                TypeDoc? extractedType = memberDeclaration switch
                {
                    TypeDeclarationSyntax tds_TypeNode => HandleType(tds_TypeNode, namespace_, file_),
                    EnumDeclarationSyntax eds_EnumNode => HandleEnum(eds_EnumNode, namespace_, file_),
                    DelegateDeclarationSyntax dds_DelegateNode => HandleDelegate(dds_DelegateNode, namespace_, file_),
                    _ =>null // Discards every other kind for now
                };
                if (extractedType is not null) 
                {
                    listedMembers.Add(extractedType);
                }
            }
            return listedMembers;
        }
        
        private MemberDoc? TryCreateMemberDoc(MemberDeclarationSyntax mds_Member_, string kind_, string signature_)
        {
            if (!_includeNonPublic && !HasPublicLike(mds_Member_.Modifiers)) 
            {
                return null;
            }
            else
            {
                MemberDoc md_Member = new()
                {
                    Kind = kind_,
                    Signature = signature_,
                    Modifiers = mds_Member_.Modifiers.ToString().Trim(),
                    Summary = Utils.ExtractXmlSummaryFromSyntaxNode(mds_Member_),
                    Remarks = Utils.ExtractXmlRemarksFromSyntaxNode(mds_Member_),
                     Attributes = Utils.FlattenAttributes(mds_Member_.AttributeLists)
                };
                return md_Member;
            }
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

        /// <summary>
        /// Parses all collected .cs files into <see cref="TypeDoc"/> objects.
        /// Uses Roslyn to analyze syntax trees and namespaces.
        /// </summary>
        /// <param name="relevantFiles"></param>
        /// <param name="includeNonPublic"></param>
        /// <returns></returns>
        public static async Task<List<TypeDoc>> TryParseDataFromFile( IEnumerable<string> relevantFiles, bool includeNonPublic)
        {
            TypeExtractor extractor = new(includeNonPublic);
            List<TypeDoc> allTypes = new();

            foreach (string file in relevantFiles)
            {
                string text = await xyFiles.LoadFileAsync(file)??"";

                if (string.IsNullOrEmpty(text))
                {
                    xyLog.Log($"No content found in {file}");
                    continue;
                }

                SyntaxTree tree = CSharpSyntaxTree.ParseText(text);
                CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

           
                foreach (MemberDeclarationSyntax member in root.Members)
                {
                    switch (member)
                    {
                        // Standard-Namespace-Deklaration (Block-Scoped)
                        case NamespaceDeclarationSyntax namespaceDecl:
                            // Rekursiver Aufruf für Mitglieder IM Namensraum
                            allTypes.AddRange(extractor.ProcessMembers(namespaceDecl.Members, namespaceDecl.Name.ToString(), file));
                            break;

                        // File-Scoped-Namespace-Deklaration (C# 10+)
                        case FileScopedNamespaceDeclarationSyntax fileNamespaceDecl:
                            // Rekursiver Aufruf für Mitglieder IM Namensraum
                            allTypes.AddRange(extractor.ProcessMembers(fileNamespaceDecl.Members, fileNamespaceDecl.Name.ToString(), file));
                            break;

                        // Alle anderen Mitglieder (Typen, Delegates, Enums im globalen Scope).
                        // Wir übergeben sie als Liste von 1 an ProcessMembers, um die Logik zu vereinheitlichen.
                        default:
                            allTypes.AddRange(extractor.ProcessMembers(SyntaxFactory.List(new[] { member }), null, file));
                            break;
                    }
                }
            }

            return allTypes;
        }
    }
}

