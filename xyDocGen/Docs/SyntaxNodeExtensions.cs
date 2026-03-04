using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace xyDocumentor.Docs
{
    /// <summary>
    /// Helper class (wrapper) for "SyntaxNode" operations
    /// </summary>
    internal static class SyntaxNodeExtensions
    {
        /// <summary>
        /// Read the modifiers associated with the target
        /// </summary>
        /// <param name="member"></param>
        /// <returns></returns>
        public static SyntaxTokenList GetModifiers(this MemberDeclarationSyntax member) =>  member switch{BaseTypeDeclarationSyntax t => t.Modifiers,FieldDeclarationSyntax f => f.Modifiers,EventDeclarationSyntax e => e.Modifiers,EventFieldDeclarationSyntax ef => ef.Modifiers,MethodDeclarationSyntax m => m.Modifiers,ConstructorDeclarationSyntax c => c.Modifiers,PropertyDeclarationSyntax p => p.Modifiers,_ => new SyntaxTokenList()}; // lol
    }

}
