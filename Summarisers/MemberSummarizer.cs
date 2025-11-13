using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceCodeSummariser.Summarisers;

/// <summary>
/// Abstract base class for summarizing different types of code members.
/// Implements the Strategy pattern for member-specific summarization logic.
/// </summary>
public abstract class MemberSummarizer
{
    /// <summary>
    /// Generates a summary for the specified code member.
    /// </summary>
    /// <param name="member">The member declaration syntax node to summarize.</param>
    /// <returns>A collection of summary strings for the member and its children.</returns>
    public abstract Task<IEnumerable<string>> Summarize(MemberDeclarationSyntax member);
}