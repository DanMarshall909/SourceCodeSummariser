using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceCodeSummariser.Summarisers;

public class InterfaceSummarizer : MemberSummarizer
{
    private readonly SummarizerService _service;

    public InterfaceSummarizer(SummarizerService service) => _service = service;

    public override async Task<IEnumerable<string>> Summarize(MemberDeclarationSyntax member)
    {
        var interfaceDecl = (InterfaceDeclarationSyntax)member;
        var result = new List<string> { $"Interface: {interfaceDecl.Identifier.Text}" };

        foreach (var interfaceMember in interfaceDecl.Members)
        {
            var memberSummaries = await _service.SummarizeMember(interfaceMember);
            result.AddRange(memberSummaries);
        }

        return result;
    }
}