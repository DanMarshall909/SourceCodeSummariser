using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceCodeSummariser.Summarisers;

public class FieldSummarizer : MemberSummarizer
{
    public override IEnumerable<string> Summarize(MemberDeclarationSyntax member)
    {
        var fieldDecl = (FieldDeclarationSyntax)member;
        return fieldDecl.Declaration.Variables.Select(variable =>
            $"Field: {variable.Identifier.Text} ({fieldDecl.Declaration.Type})");
    }
}