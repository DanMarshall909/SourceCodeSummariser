using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceCodeSummariser.Summarisers;

public class PropertySummarizer : MemberSummarizer
{
    public override Task<IEnumerable<string>> Summarize(MemberDeclarationSyntax member)
    {
        var propertyDecl = (PropertyDeclarationSyntax)member;
        IEnumerable<string> result = new[] { $"Property: {propertyDecl.Identifier.Text} ({propertyDecl.Type})" };
        return Task.FromResult(result);
    }
}