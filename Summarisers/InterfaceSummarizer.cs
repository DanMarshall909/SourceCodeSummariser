using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceCodeSummariser.Summarisers;

public class InterfaceSummarizer : MemberSummarizer
{
    private readonly SummarizerService _service;

    public InterfaceSummarizer(SummarizerService service) => _service = service;

    public override IEnumerable<string> Summarize(MemberDeclarationSyntax member)
    {
        var interfaceDecl = (InterfaceDeclarationSyntax)member;
        return new[] { $"Interface: {interfaceDecl.Identifier.Text}" }
            .Concat(interfaceDecl.Members.SelectMany(m => _service.SummarizeMember(m).Result));
    }
}