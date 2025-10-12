using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using xyDocumentor.Core.Docs;
using xyDocumentor.Core.Helpers;
using xyToolz.Filesystem;


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
        /// Extrahiert Parameterdetails und deren XML-Dokumentation.
        /// </summary>
        private static IList<ParameterDoc> ExtractParameters(ParameterListSyntax parameterList, MemberDeclarationSyntax parentNode)
        {
            IList<ParameterDoc> parameters = [];
            IDictionary<string, string> paramSummaries = Utils.ExtractXmlParamSummaries(parentNode);

            foreach (ParameterSyntax param in parameterList.Parameters)
            {
                string? defaultValueExpression = param.Default?.Value?.ToString();

                // ✅ Modifikatoren als Tokens prüfen
                bool isRef = param.Modifiers.Any(t => t.IsKind(SyntaxKind.RefKeyword));
                bool isOut = param.Modifiers.Any(t => t.IsKind(SyntaxKind.OutKeyword));
                bool isIn = param.Modifiers.Any(t => t.IsKind(SyntaxKind.InKeyword));
                bool isParams = param.Modifiers.Any(t => t.IsKind(SyntaxKind.ParamsKeyword));
                // Ref-Readonly muss separat geprüft werden, falls es in der Helpers-Klasse fehlt
                bool isRefReadonly = param.Modifiers.Any(t => t.IsKind(SyntaxKind.RefKeyword))
                                    && param.Modifiers.Any(t => t.IsKind(SyntaxKind.ReadOnlyKeyword));

                paramSummaries.TryGetValue(param.Identifier.Text, out string? summary);

                parameters.Add(new ParameterDoc
                {
                    Name = param.Identifier.Text,
                    TypeDisplayName = param.Type?.ToString() ?? "var",
                    Summary = summary ?? string.Empty,
                    DefaultValueExpression = defaultValueExpression,
                    IsOptional = defaultValueExpression is not null, // Ableitung aus DefaultValueExpression

                    // ✅ Zuordnung der Booleschen Flags
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
        /// <param name="tds_TypeNode_"></param>
        /// <param name="namespace_"></param>
        /// <param name="filePath_"></param>
        /// <param name="parentType_"></param>
        /// <returns></returns>
        public TypeDoc? HandleType(TypeDeclarationSyntax tds_TypeNode_, string? namespace_, string filePath_, TypeDoc? parentType_ = null)
        {
            // Store in here for better readability
            string modifiers = tds_TypeNode_.Modifiers.ToString();

            // Is the type public?
            bool isPublic = modifiers.Contains("public");

            // If not and therefore not to be processed return null
            if (!_includeNonPublic && !HasPublicLike(tds_TypeNode_.Modifiers))
            {
                return null;
            }

            // Fill in all the needed data
            TypeDoc td_Result = new()
            {
                Kind = tds_TypeNode_.Keyword.ValueText,
                Name = tds_TypeNode_.Identifier.Text + (tds_TypeNode_.TypeParameterList?.ToString() ?? string.Empty),
                Namespace = namespace_ ?? "Global   (Default)",
                Modifiers = modifiers.Trim(),
                Attributes = Utils.FlattenAttributes(tds_TypeNode_.AttributeLists).ToList(),
                BaseTypes = Utils.ExtractBaseTypes(tds_TypeNode_.BaseList).ToList(),
                Summary = Utils.ExtractXmlSummaryFromSyntaxNode(tds_TypeNode_),
                FilePath = filePath_,
                Parent = parentType_?.Name ?? string.Empty
            };

            // For every member in the type: Create a typedoc and add them to the corresponding list
            foreach (MemberDeclarationSyntax member in tds_TypeNode_.Members)
            {
                MemberDoc? md_Member = null;
                TypeDoc? td_NestedType = null;

                switch (member)
                {
                    case ConstructorDeclarationSyntax ctorNode:
                        md_Member = TryCreateMemberDoc(ctorNode, "ctor", ctorNode.Identifier.Text + ctorNode.ParameterList.ToString());
                        if (md_Member != null)
                        {
                            // PARAMETER EXTRACTION
                            md_Member = md_Member with 
                            { 
                                Parameters = ExtractParameters(ctorNode.ParameterList, ctorNode),
                            };
                            td_Result.Constructors.Add(md_Member);
                        }
                        break;
                    case MethodDeclarationSyntax methodNode:
                        md_Member = TryCreateMemberDoc(methodNode, "method", $"{methodNode.ReturnType} {methodNode.Identifier}{methodNode.TypeParameterList}{methodNode.ParameterList}");
                        if (md_Member != null)
                        {

                            md_Member = md_Member with
                            {
                                ReturnType = methodNode.ReturnType.ToString(),
                                Parameters = ExtractParameters(methodNode.ParameterList, methodNode),
                                ReturnSummary = Utils.ExtractXmlReturnSummary(methodNode),
                            };
                            td_Result.Methods.Add(md_Member);
                        }
                        break;
                    case PropertyDeclarationSyntax propertyNode:
                        md_Member = TryCreateMemberDoc(propertyNode, "property", $"{propertyNode.Type} {propertyNode.Identifier}{propertyNode.AccessorList}");
                        if (md_Member != null)
                        {
                            md_Member = md_Member with
                            {
                                ReturnType = propertyNode.Type.ToString(),
                                ReturnSummary = Utils.ExtractXmlReturnSummary(propertyNode)
                            };
                            td_Result.Properties.Add(md_Member);
                        }
                        break;
                    case EventDeclarationSyntax eventNode:
                        md_Member = TryCreateMemberDoc(eventNode, "event", $"event {eventNode.Type} {eventNode.Identifier}");
                        if (md_Member != null) 
                        {
                            //Remarks = Utils.ExtractXmlRemarksFromSyntaxNode(eventNode);
                            td_Result.Events.Add(md_Member);
                        }
                        break;
                    case EventFieldDeclarationSyntax eventFieldNode:
                        // Hinweis: Der Kind-Type sollte "event" sein, nicht "event-field"
                        string eventFieldSignature = $"event {eventFieldNode.Declaration.Type} {string.Join(", ", eventFieldNode.Declaration.Variables.Select(v => v.Identifier.Text))}";
                        md_Member = TryCreateMemberDoc(eventFieldNode, "event", eventFieldSignature);
                        if (md_Member != null) td_Result.Events.Add(md_Member);
                        break;
                    case FieldDeclarationSyntax fieldNode:
                        string fieldSignature = $"{fieldNode.Declaration.Type} {string.Join(", ", fieldNode.Declaration.Variables.Select(v => v.Identifier.Text))}";
                        md_Member = TryCreateMemberDoc(fieldNode, "field", fieldSignature);
                        // FELDTYP EXTRAKTION
                        if (md_Member != null)
                        {
                            md_Member = md_Member with
                            {
                                ReturnType = fieldNode.Declaration.Type.ToString() 
                            };
                            td_Result.Fields.Add(md_Member);
                        }
                        break;
                    case TypeDeclarationSyntax nestedTypeNode:
                        td_NestedType = HandleType(nestedTypeNode, namespace_, filePath_, parentType_: td_Result);
                        if (td_NestedType != null) td_Result.NestedTypes.Add(td_NestedType);
                        break;
                    case EnumDeclarationSyntax nestedEnumNode:
                        td_NestedType = HandleEnum(nestedEnumNode, namespace_, filePath_, parentType_: td_Result); // Parent-Übergabe hinzugefügt
                        if (td_NestedType != null) td_Result.NestedTypes.Add(td_NestedType);
                        break;
                }
            }
            return td_Result;
        }

        /// <summary>
        /// Handles enums and their members by creating corresponding TypeDocs and MemberDocs
        /// </summary>
        /// <param name="enumDeclaration_"></param>
        /// <param name="namespace_"></param> 
        /// <param name="filePath_"></param>
        /// <returns></returns>
        private TypeDoc? HandleEnum(EnumDeclarationSyntax enumDeclaration_, string? namespace_, string filePath_, TypeDoc? parentType_ = null)
        {
            // If its not public and thus not to be included return NULL
            if (!_includeNonPublic && !HasPublicLike(enumDeclaration_.Modifiers))
            {
                return null;
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
                Attributes = Utils.FlattenAttributes(enumDeclaration_.AttributeLists).ToList(),
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
                    Attributes = Utils.FlattenAttributes(member.AttributeLists).ToList() 
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
        /// <returns></returns>
        private TypeDoc? HandleDelegate(DelegateDeclarationSyntax delegateNode, string? namespaceName, string filePath)
        {
            // Konsistente Sichtbarkeitsprüfung
            if (!_includeNonPublic && !HasPublicLike(delegateNode.Modifiers))
            {
                return null;
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
                Namespace = namespaceName ?? "Global (Default)",
                Modifiers = modifiers.Trim(),
                Attributes = Utils.FlattenAttributes(delegateNode.AttributeLists).ToList(),
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
                     Attributes = Utils.FlattenAttributes(mds_Member_.AttributeLists).ToList()
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
        /// <param name="listedExternalArguments"></param>
        /// <param name="args"></param>
        /// <param name="relevantFiles"></param>
        /// <param name="includeNonPublic"></param>
        /// <returns></returns>
        public static async Task<List<TypeDoc>> TryParseDataFromFile(List<string> listedExternalArguments, string[] args, IEnumerable<string> relevantFiles, bool includeNonPublic)
        {
            TypeExtractor extractor = new(includeNonPublic);
            List<TypeDoc> allTypes = new();

            foreach (string file in relevantFiles)
            {
                string text = await xyFiles.LoadFileAsync(file);
                SyntaxTree tree = CSharpSyntaxTree.ParseText(text);
                CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

                // ✅ KORREKTUR: Alle Top-Level-Mitglieder auf einmal verarbeiten und Namensräume "flach" klopfen

                // Sammeln Sie alle Members, die direkt im globalen Scope liegen oder in einem Namensraum-Block.
                // Dadurch wird der globale Scope korrekt mit dem Namespace "null" behandelt.

                // Zuerst alle Mitglieder verarbeiten, die direkt unter dem Root liegen (Global oder Namespace-Deklaration).
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

