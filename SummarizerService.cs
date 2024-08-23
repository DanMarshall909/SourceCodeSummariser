using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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

            return new FileSummary
            {
                FileName = fileName,
                Members = memberSummaries
            };
        }

        public async Task<IEnumerable<string>> SummarizeMember(MemberDeclarationSyntax member)
        {
            return member switch
            {
                NamespaceDeclarationSyntax namespaceDecl => await Summarize(namespaceDecl),
                ClassDeclarationSyntax classDecl => await Summarize(classDecl),
                MethodDeclarationSyntax methodDecl => await Summarize(methodDecl),
                PropertyDeclarationSyntax propertyDecl => Summarize(propertyDecl),
                FieldDeclarationSyntax fieldDecl => Summarize(fieldDecl),
                InterfaceDeclarationSyntax interfaceDecl => await Summarize(interfaceDecl),
                StructDeclarationSyntax structDecl => await Summarize(structDecl),
                _ => new[] { $"Unhandled member type: {member.Kind()}" }
            };
        }

        public Task<IEnumerable<string>> Summarize(NamespaceDeclarationSyntax namespaceDecl) =>
            Task.FromResult(new[] { $"Namespace: {namespaceDecl.Name}" }
                .Concat(namespaceDecl.Members.SelectMany(m => SummarizeMember(m).Result)));

        public Task<IEnumerable<string>> Summarize(ClassDeclarationSyntax classDecl) =>
            Task.FromResult(new[] { $"Class: {classDecl.Identifier.Text}" }
                .Concat(classDecl.Members.SelectMany(m => SummarizeMember(m).Result)));

        public async Task<IEnumerable<string>> Summarize(MethodDeclarationSyntax methodDecl)
        {
            string summary = await SummarizeMethod(methodDecl);
            return new[] { summary };
        }

        public IEnumerable<string> Summarize(PropertyDeclarationSyntax propertyDecl) =>
            new[] { $"Property: {propertyDecl.Identifier.Text} ({propertyDecl.Type})" };

        public IEnumerable<string> Summarize(FieldDeclarationSyntax fieldDecl) =>
            fieldDecl.Declaration.Variables.Select(variable =>
                $"Field: {variable.Identifier.Text} ({fieldDecl.Declaration.Type})");

        public Task<IEnumerable<string>> Summarize(InterfaceDeclarationSyntax interfaceDecl) =>
            Task.FromResult(new[] { $"Interface: {interfaceDecl.Identifier.Text}" }
                .Concat(interfaceDecl.Members.SelectMany(m => SummarizeMember(m).Result)));

        public Task<IEnumerable<string>> Summarize(StructDeclarationSyntax structDecl) =>
            Task.FromResult(new[] { $"Struct: {structDecl.Identifier.Text}" }
                .Concat(structDecl.Members.SelectMany(m => SummarizeMember(m).Result)));

        public async Task<string> SummarizeMethod(MethodDeclarationSyntax methodDecl)
        {
            var methodCode = methodDecl.ToString();
            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    (role: "system", content: "You are a code summarizer. The less tokens you can use the better, but accuracy is far more important than brevity."),
                    (role: "user", content: $"Summarize the following C# method optimizing for the smallest number of tokens possible and clarity.:\n\n{methodCode}\n\nSummary:")
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

        public string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
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
    }
}