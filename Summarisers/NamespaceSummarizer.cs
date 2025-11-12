using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceCodeSummariser.Summarisers;

public class NamespaceSummarizer : MemberSummarizer
{
    private readonly SummarizerService _service;

    public NamespaceSummarizer(SummarizerService service) => _service = service;

    public override async Task<IEnumerable<string>> Summarize(MemberDeclarationSyntax member)
    {
        var namespaceDecl = (NamespaceDeclarationSyntax)member;
        var result = new List<string> { $"Namespace: {namespaceDecl.Name}" };

        foreach (var namespaceMember in namespaceDecl.Members)
        {
            var memberSummaries = await _service.SummarizeMember(namespaceMember);
            result.AddRange(memberSummaries);
        }

        return result;
    }
}