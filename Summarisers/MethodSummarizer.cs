using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceCodeSummariser.Summarisers;

public class MethodSummarizer : MemberSummarizer
{
    private readonly SummarizerService _service;

    public MethodSummarizer(SummarizerService service) => _service = service;

    public override IEnumerable<string> Summarize(MemberDeclarationSyntax member)
    {
        var methodDecl = (MethodDeclarationSyntax)member;
        return new[] { _service.SummarizeMethod(methodDecl).Result };
    }
}