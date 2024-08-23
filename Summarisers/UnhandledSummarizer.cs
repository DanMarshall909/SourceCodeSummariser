using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceCodeSummariser.Summarisers;

public class UnhandledSummarizer : MemberSummarizer
{
    public override IEnumerable<string> Summarize(MemberDeclarationSyntax member)
    {
        return new[] { $"Unhandled member type: {member.Kind()}" };
    }
}