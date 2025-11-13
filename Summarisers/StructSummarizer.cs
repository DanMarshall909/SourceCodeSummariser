using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceCodeSummariser.Summarisers;

public class StructSummarizer : MemberSummarizer
{
    private readonly SummarizerService _service;

    public StructSummarizer(SummarizerService service) => _service = service;

    public override async Task<IEnumerable<string>> Summarize(MemberDeclarationSyntax member)
    {
        var structDecl = (StructDeclarationSyntax)member;
        var result = new List<string> { $"Struct: {structDecl.Identifier.Text}" };

        foreach (var structMember in structDecl.Members)
        {
            var memberSummaries = await _service.SummarizeMember(structMember);
            result.AddRange(memberSummaries);
        }

        return result;
    }
}