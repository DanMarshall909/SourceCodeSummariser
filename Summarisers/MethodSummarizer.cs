using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceCodeSummariser.Summarisers;

public class MethodSummarizer : MemberSummarizer
{
    private readonly SummarizerService _service;

    public MethodSummarizer(SummarizerService service) => _service = service;

    public override async Task<IEnumerable<string>> Summarize(MemberDeclarationSyntax member)
    {
        var methodDecl = (MethodDeclarationSyntax)member;
        var summary = await _service.SummarizeMethod(methodDecl);
        return new[] { summary };
    }
}