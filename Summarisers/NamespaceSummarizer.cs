using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceCodeSummariser.Summarisers;

public class NamespaceSummarizer : MemberSummarizer
{
    private readonly SummarizerService _service;

    public NamespaceSummarizer(SummarizerService service) => _service = service;

    public override IEnumerable<string> Summarize(MemberDeclarationSyntax member)
    {
        var namespaceDecl = (NamespaceDeclarationSyntax)member;
        return new[] { $"Namespace: {namespaceDecl.Name}" }
            .Concat(namespaceDecl.Members.SelectMany(m => _service.SummarizeMember(m).Result));
    }
}