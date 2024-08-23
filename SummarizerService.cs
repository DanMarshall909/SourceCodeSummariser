using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceCodeSummarizer
{
    public class SummarizerService
    {
        private readonly HttpClient _httpClient;

        public SummarizerService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<FileSummary> GenerateFileSummary(string filePath, string content)
        {
            string fileName = Path.GetFileName(filePath);
            var memberSummaries = new List<string>();

            var tree = CSharpSyntaxTree.ParseText(content);
            var root = tree.GetRoot() as CompilationUnitSyntax;

            if (root == null)
            {
                Console.WriteLine($"No valid C# syntax found in {fileName}. Skipping file.");
                return new FileSummary { FileName = fileName, Members = memberSummaries };
            }

            Console.WriteLine($"Parsing members in {fileName}...");
            foreach (var member in root.Members)
            {
                var memberSummary = await SummarizeMember(member);
                if (memberSummary.Any())
                {
                    memberSummaries.AddRange(memberSummary);
                }
            }

            return new FileSummary { FileName = fileName, Members = memberSummaries };
        }

        public async Task<IEnumerable<string>> SummarizeMember(MemberDeclarationSyntax member)
        {
            return member switch
            {
                NamespaceDeclarationSyntax ns => await Summarize(ns),
                ClassDeclarationSyntax cls => await Summarize(cls),
                MethodDeclarationSyntax mtd => await Summarize(mtd),
                PropertyDeclarationSyntax prop => Summarize(prop),
                FieldDeclarationSyntax fld => Summarize(fld),
                InterfaceDeclarationSyntax iface => await Summarize(iface),
                StructDeclarationSyntax strct => await Summarize(strct),
                _ => new[] { $"Unhandled member type: {member.Kind()}" }
            };
        }

        private Task<IEnumerable<string>> Summarize(NamespaceDeclarationSyntax ns)
        {
            var summaries = new List<string> { $"Namespace: {ns.Name}" };
            summaries.AddRange(ns.Members.SelectMany(m => SummarizeMember(m).Result));
            return Task.FromResult<IEnumerable<string>>(summaries);
        }

        private Task<IEnumerable<string>> Summarize(ClassDeclarationSyntax cls)
        {
            var summaries = new List<string> { $"Class: {cls.Identifier.Text}" };
            summaries.AddRange(cls.Members.SelectMany(m => SummarizeMember(m).Result));
            return Task.FromResult<IEnumerable<string>>(summaries);
        }

        private async Task<IEnumerable<string>> Summarize(MethodDeclarationSyntax mtd)
        {
            var summary = await SummarizeMethod(mtd);
            return new[] { summary };
        }

        private IEnumerable<string> Summarize(PropertyDeclarationSyntax prop) =>
            new[] { $"Property: {prop.Identifier.Text} ({prop.Type})" };

        private IEnumerable<string> Summarize(FieldDeclarationSyntax fld) =>
            fld.Declaration.Variables.Select(variable => $"Field: {variable.Identifier.Text} ({fld.Declaration.Type})");

        private Task<IEnumerable<string>> Summarize(InterfaceDeclarationSyntax iface)
        {
            var summaries = new List<string> { $"Interface: {iface.Identifier.Text}" };
            summaries.AddRange(iface.Members.SelectMany(m => SummarizeMember(m).Result));
            return Task.FromResult<IEnumerable<string>>(summaries);
        }

        private Task<IEnumerable<string>> Summarize(StructDeclarationSyntax strct)
        {
            var summaries = new List<string> { $"Struct: {strct.Identifier.Text}" };
            summaries.AddRange(strct.Members.SelectMany(m => SummarizeMember(m).Result));
            return Task.FromResult<IEnumerable<string>>(summaries);
        }

        public async Task<string> SummarizeMethod(MethodDeclarationSyntax mtd)
        {
            var methodCode = mtd.ToString();
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
                .RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content")
                .GetString()?.Trim();

            return PostProcessSummary(summary, 50);
        }

        public static string PostProcessSummary(string summary, int tokenLimit)
        {
            if (!summary.EndsWith(",") && !summary.EndsWith("and") && summary.Length >= tokenLimit) return summary;

            summary = summary.TrimEnd(',', ' ');

            if (summary.EndsWith("and"))
            {
                summary = summary.Substring(0, summary.Length - 3).TrimEnd();
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

    public class FileSummary
    {
        public string FileName { get; set; } = string.Empty;
        public List<string> Members { get; set; } = new List<string>();
    }
}
