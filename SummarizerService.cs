using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceCodeSummariser.Summarisers;

namespace SourceCodeSummariser
{
    public class SummarizerService
    {
        private readonly HttpClient _httpClient;

        public SummarizerService(HttpClient httpClient) => _httpClient = httpClient;

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

        public async Task<IEnumerable<string>> SummarizeMember(MemberDeclarationSyntax member)
        {
            var summarizer = GetSummarizer(member);
            return summarizer.Summarize(member);
        }

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

        public async Task<string> SummarizeMethod(MethodDeclarationSyntax methodDecl)
        {
            var methodCode = methodDecl.ToString();
            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = "You are a code summarizer. The less tokens you can use the better, but accuracy is far more important than brevity." },
                    new { role = "user", content = $"Summarize the following C# method optimizing for the smallest number of tokens possible and clarity.:\n\n{methodCode}\n\nSummary:" }
                },
                max_tokens = 50
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            response.EnsureSuccessStatusCode();

            var summary = JsonDocument.Parse(await response.Content.ReadAsStringAsync())
                .RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
                ?.Trim();

            return PostProcessSummary(summary, 50);
        }

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

        public string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }
    }

}
