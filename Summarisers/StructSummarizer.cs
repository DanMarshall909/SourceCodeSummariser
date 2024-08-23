using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceCodeSummariser.Summarisers;

public class StructSummarizer : MemberSummarizer
{
    private readonly SummarizerService _service;

    public StructSummarizer(SummarizerService service) => _service = service;

    public override IEnumerable<string> Summarize(MemberDeclarationSyntax member)
    {
        var structDecl = (StructDeclarationSyntax)member;
        return new[] { $"Struct: {structDecl.Identifier.Text}" }
            .Concat(structDecl.Members.SelectMany(m => _service.SummarizeMember(m).Result));
    }
}