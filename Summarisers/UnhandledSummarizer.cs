using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceCodeSummariser.Summarisers;

public class UnhandledSummarizer : MemberSummarizer
{
    public override Task<IEnumerable<string>> Summarize(MemberDeclarationSyntax member)
    {
        IEnumerable<string> result = new[] { $"Unhandled member type: {member.Kind()}" };
        return Task.FromResult(result);
    }
}