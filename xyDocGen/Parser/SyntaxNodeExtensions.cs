using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace xyDocumentor.Parser
{
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
