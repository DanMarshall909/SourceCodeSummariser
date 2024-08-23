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

        public SummarizerService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> SummarizeMethod(MethodDeclarationSyntax methodDecl)
        {
            var input = methodDecl.ToString();
            string methodDescription = await GetMethodDescription(methodDecl);
            return $"Method: {methodDecl.Identifier.Text} - {methodDescription}";
        }

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
            var summaries = new List<string>();

            switch (member)
            {
                case NamespaceDeclarationSyntax namespaceDecl:
                    summaries.Add($"Namespace: {namespaceDecl.Name}");
                    summaries.AddRange(
                        (await Task.WhenAll(namespaceDecl.Members.Select(m => SummarizeMember(m))))
                        .SelectMany(s => s));
                    break;

                case ClassDeclarationSyntax classDecl:
                    summaries.Add($"Class: {classDecl.Identifier.Text}");
                    summaries.AddRange(
                        (await Task.WhenAll(classDecl.Members.Select(m => SummarizeMember(m))))
                        .SelectMany(s => s));
                    break;

                case MethodDeclarationSyntax methodDecl:
                    summaries.Add(await SummarizeMethod(methodDecl));
                    break;

                case PropertyDeclarationSyntax propertyDecl:
                    summaries.Add($"Property: {propertyDecl.Identifier.Text} ({propertyDecl.Type})");
                    break;

                case FieldDeclarationSyntax fieldDecl:
                    summaries.AddRange(SummarizeFields(fieldDecl));
                    break;

                case InterfaceDeclarationSyntax interfaceDecl:
                    summaries.Add($"Interface: {interfaceDecl.Identifier.Text}");
                    summaries.AddRange(
                        (await Task.WhenAll(interfaceDecl.Members.Select(m => SummarizeMember(m))))
                        .SelectMany(s => s));
                    break;

                case StructDeclarationSyntax structDecl:
                    summaries.Add($"Struct: {structDecl.Identifier.Text}");
                    summaries.AddRange(
                        (await Task.WhenAll(structDecl.Members.Select(m => SummarizeMember(m))))
                        .SelectMany(s => s));
                    break;

                default:
                    summaries.Add($"Unhandled member type: {member.Kind()}");
                    break;
            }

            return summaries;
        }

        public IEnumerable<string> SummarizeFields(FieldDeclarationSyntax fieldDecl)
        {
            return fieldDecl.Declaration.Variables.Select(variable =>
                $"Field: {variable.Identifier.Text} ({fieldDecl.Declaration.Type})");
        }

        public string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

        public string PostProcessSummary(string summary, int tokenLimit)
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

        private async Task<string> GetMethodDescription(MethodDeclarationSyntax methodDecl)
        {
            string methodCode = methodDecl.ToString();
            string prompt =
                $"Summarize the following C# method optimizing for the smallest number of tokens possible and clarity.:\n\n{methodCode}\n\nSummary:";

            int tokenLimit = 50;

            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "You are a code summarizer. The less tokens you can use the better, but accuracy is far more important than brevity."
                    },
                    new { role = "user", content = prompt }
                },
                max_tokens = tokenLimit
            };

            string jsonRequestBody = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            response.EnsureSuccessStatusCode();

            string responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonDocument.Parse(responseContent);
            string summary = result.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content")
                .GetString().Trim();

            return PostProcessSummary(summary, tokenLimit);
        }
    }
}



