using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceCodeSummariser.Summarisers;

public abstract class MemberSummarizer
{
    public abstract IEnumerable<string> Summarize(MemberDeclarationSyntax member);
}