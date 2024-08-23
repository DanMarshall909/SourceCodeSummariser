using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceCodeSummariser.Summarisers;

public class ClassSummarizer : MemberSummarizer
{
    private readonly SummarizerService _service;

    public ClassSummarizer(SummarizerService service) => _service = service;

    public override IEnumerable<string> Summarize(MemberDeclarationSyntax member)
    {
        var classDecl = (ClassDeclarationSyntax)member;
        return new[] { $"Class: {classDecl.Identifier.Text}" }
            .Concat(classDecl.Members.SelectMany(m => _service.SummarizeMember(m).Result));
    }
}