using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceCodeSummariser.Summarisers;

public class FieldSummarizer : MemberSummarizer
{
    public override Task<IEnumerable<string>> Summarize(MemberDeclarationSyntax member)
    {
        var fieldDecl = (FieldDeclarationSyntax)member;
        var result = fieldDecl.Declaration.Variables.Select(variable =>
            $"Field: {variable.Identifier.Text} ({fieldDecl.Declaration.Type})");
        return Task.FromResult(result);
    }
}