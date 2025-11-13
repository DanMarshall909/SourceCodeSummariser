using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceCodeSummariser.Summarisers;

namespace SourceCodeSummariser
{
    /// <summary>
    /// Service for analyzing C# code and generating AI-powered summaries using configurable LLM providers.
    /// </summary>
    public class SummarizerService
    {
        private readonly ILlmProvider _llmProvider;
        private readonly int _maxTokens;

        /// <summary>
        /// Initializes a new instance of the <see cref="SummarizerService"/> class.
        /// </summary>
        /// <param name="llmProvider">The LLM provider for generating summaries.</param>
        /// <param name="maxTokens">Maximum tokens for summary generation.</param>
        public SummarizerService(ILlmProvider llmProvider, int maxTokens = 50)
        {
            _llmProvider = llmProvider;
            _maxTokens = maxTokens;
        }

        /// <summary>
        /// Generates a summary for a C# source file by analyzing all its members.
        /// </summary>
        /// <param name="filePath">The path to the source file.</param>
        /// <param name="content">The content of the source file.</param>
        /// <returns>A <see cref="FileSummary"/> containing summaries of all code members.</returns>
        public async Task<FileSummary> GenerateFileSummary(string filePath, string content)
        {
            string fileName = Path.GetFileName(filePath);
            var memberSummaries = new List<string>();

            SyntaxTree tree = CSharpSyntaxTree.ParseText(content);
            var root = tree.GetRoot() as CompilationUnitSyntax;

            if (root == null)
            {
                Console.WriteLine($"No valid C# syntax found in {fileName}. Skipping file.");
            }
            else
            {
                Console.WriteLine($"Parsing members in {fileName}...");
                foreach (var member in root.Members)
                {
                    var memberSummary = await SummarizeMember(member);
                    if (memberSummary.Any())
                    {
                        memberSummaries.AddRange(memberSummary);
                    }
                }
            }

            return new FileSummary { FileName = fileName, Members = memberSummaries };
        }

        /// <summary>
        /// Summarizes a single code member (class, method, property, etc.).
        /// </summary>
        /// <param name="member">The member declaration syntax node to summarize.</param>
        /// <returns>A collection of summary strings for the member and its children.</returns>
        public async Task<IEnumerable<string>> SummarizeMember(MemberDeclarationSyntax member)
        {
            var summarizer = GetSummarizer(member);
            return await summarizer.Summarize(member);
        }

        /// <summary>
        /// Gets the appropriate summarizer for a given member type.
        /// </summary>
        /// <param name="member">The member declaration syntax node.</param>
        /// <returns>A <see cref="MemberSummarizer"/> instance for the member type.</returns>
        private MemberSummarizer GetSummarizer(MemberDeclarationSyntax member)
        {
            return member switch
            {
                NamespaceDeclarationSyntax => new NamespaceSummarizer(this),
                ClassDeclarationSyntax => new ClassSummarizer(this),
                MethodDeclarationSyntax => new MethodSummarizer(this),
                PropertyDeclarationSyntax => new PropertySummarizer(),
                FieldDeclarationSyntax => new FieldSummarizer(),
                InterfaceDeclarationSyntax => new InterfaceSummarizer(this),
                StructDeclarationSyntax => new StructSummarizer(this),
                _ => new UnhandledSummarizer()
            };
        }

        /// <summary>
        /// Generates an AI-powered summary for a method using the configured LLM provider.
        /// </summary>
        /// <param name="methodDecl">The method declaration syntax node.</param>
        /// <returns>A formatted string containing the method signature and AI-generated summary.</returns>
        public async Task<string> SummarizeMethod(MethodDeclarationSyntax methodDecl)
        {
            var methodSignature = $"{methodDecl.Modifiers} {methodDecl.ReturnType} {methodDecl.Identifier}({string.Join(", ", methodDecl.ParameterList.Parameters)})";

            try
            {
                var methodCode = methodDecl.ToString();
                var systemPrompt = "You are a code summarizer. The less tokens you can use the better, but accuracy is far more important than brevity.";
                var userPrompt = $"Summarize the following C# method optimizing for the smallest number of tokens possible and clarity.:\n\n{{code}}\n\nSummary:";

                var summary = await _llmProvider.GenerateSummary(methodCode, systemPrompt, userPrompt, _maxTokens);

                if (string.IsNullOrEmpty(summary))
                {
                    return $"Method: {methodSignature} - [AI summary unavailable]";
                }

                return $"Method: {methodSignature} - {PostProcessSummary(summary, _maxTokens)}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ERROR calling {_llmProvider.ProviderName} for method {methodDecl.Identifier}: {ex.Message}");
                return $"Method: {methodSignature} - [Error: {ex.Message}]";
            }
        }

        /// <summary>
        /// Post-processes an AI-generated summary by cleaning up trailing punctuation.
        /// </summary>
        /// <param name="summary">The raw summary from the AI.</param>
        /// <param name="tokenLimit">The token limit used for generation.</param>
        /// <returns>A cleaned and properly formatted summary string.</returns>
        public static string PostProcessSummary(string summary, int tokenLimit)
        {
            if (!summary.EndsWith(",") && !summary.EndsWith("and") && summary.Length >= tokenLimit) return summary;

            summary = summary.TrimEnd(',', ' ');

            if (summary.EndsWith("and"))
            {
                summary = summary.Substring(0, summary.Length - 3).TrimEnd(); // remove "and" and any trailing space
            }

            summary += ".";

            return summary;
        }

        /// <summary>
        /// Computes a SHA256 hash of the input string for change detection.
        /// </summary>
        /// <param name="input">The string to hash.</param>
        /// <returns>A lowercase hexadecimal string representation of the hash.</returns>
        public string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }
    }

}
