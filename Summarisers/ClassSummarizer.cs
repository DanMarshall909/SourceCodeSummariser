using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceCodeSummariser.Summarisers;

public class ClassSummarizer : MemberSummarizer
{
    private readonly SummarizerService _service;

    public ClassSummarizer(SummarizerService service) => _service = service;

    public override async Task<IEnumerable<string>> Summarize(MemberDeclarationSyntax member)
    {
        var classDecl = (ClassDeclarationSyntax)member;
        var result = new List<string> { $"Class: {classDecl.Identifier.Text}" };

        foreach (var classMember in classDecl.Members)
        {
            var memberSummaries = await _service.SummarizeMember(classMember);
            result.AddRange(memberSummaries);
        }

        return result;
    }
}