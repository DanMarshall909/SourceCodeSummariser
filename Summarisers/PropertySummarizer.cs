using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceCodeSummariser.Summarisers;

public class PropertySummarizer : MemberSummarizer
{
    public override IEnumerable<string> Summarize(MemberDeclarationSyntax member)
    {
        var propertyDecl = (PropertyDeclarationSyntax)member;
        return new[] { $"Property: {propertyDecl.Identifier.Text} ({propertyDecl.Type})" };
    }
}