using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Net.Http.Headers;

namespace SourceCodeSummarizer
{
    class Program
    {
        private static readonly HttpClient client = new HttpClient();
        private static string apiKey;
        private static readonly string outputDirectory = "SummariesOutput";
        private static readonly string outputFileName = "summaries.json";

        static Program()
        {
            // Retrieve the API key from the environment variable
            apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY_TestChat");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("API key not found in environment variables.");
                Environment.Exit(1); // Exit the program if the API key is missing
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // Ensure the output directory exists
            Directory.CreateDirectory(outputDirectory);
        }

        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Please provide a source code folder path.");
                return;
            }

            string folderPath = args[0];

            if (!Directory.Exists(folderPath))
            {
                Console.WriteLine("The specified folder does not exist.");
                return;
            }

            var summaries = new List<FileSummary>();

            // Traverse the directory and process each file
            foreach (var file in Directory.EnumerateFiles(folderPath, "*.cs", SearchOption.AllDirectories))
            {
                Console.WriteLine($"Processing file: {file}");
                var summary = await ProcessFile(file);
                summaries.Add(summary);
            }

            // Write the summaries to a single JSON file
            await WriteSummariesToJson(summaries);
        }

        static async Task<FileSummary> ProcessFile(string filePath)
        {
            string content = await File.ReadAllTextAsync(filePath);
            var summary = await GenerateFileSummary(filePath, content);

            return summary;
        }

        static async Task<FileSummary> GenerateFileSummary(string filePath, string content)
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

        static async Task<IEnumerable<string>> SummarizeMember(MemberDeclarationSyntax member)
        {
            var summaries = new List<string>();

            switch (member)
            {
                case NamespaceDeclarationSyntax namespaceDecl:
                    summaries.Add($"Namespace: {namespaceDecl.Name}");
                    summaries.AddRange(await namespaceDecl.Members.SelectManyAsync(SummarizeMember));
                    break;

                case ClassDeclarationSyntax classDecl:
                    summaries.Add($"Class: {classDecl.Identifier.Text}");
                    summaries.AddRange(await SummarizeClassMembers(classDecl));
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
                    summaries.AddRange(await SummarizeInterfaceMembers(interfaceDecl));
                    break;

                case StructDeclarationSyntax structDecl:
                    summaries.Add($"Struct: {structDecl.Identifier.Text}");
                    summaries.AddRange(await SummarizeStructMembers(structDecl));
                    break;

                default:
                    summaries.Add($"Unhandled member type: {member.Kind()}");
                    break;
            }

            return summaries;
        }

        static async Task<IEnumerable<string>> SummarizeClassMembers(ClassDeclarationSyntax classDecl)
        {
            return await classDecl.Members.SelectManyAsync(SummarizeMember);
        }

        static async Task<string> SummarizeMethod(MethodDeclarationSyntax methodDecl)
        {
            string methodDescription = await GetMethodDescription(methodDecl);
            return $"Method: {methodDecl.Identifier.Text} - {methodDescription}";
        }

        static IEnumerable<string> SummarizeFields(FieldDeclarationSyntax fieldDecl)
        {
            return fieldDecl.Declaration.Variables.Select(variable => $"Field: {variable.Identifier.Text} ({fieldDecl.Declaration.Type})");
        }

        static async Task<IEnumerable<string>> SummarizeInterfaceMembers(InterfaceDeclarationSyntax interfaceDecl)
        {
            return await interfaceDecl.Members.SelectManyAsync(SummarizeMember);
        }

        static async Task<IEnumerable<string>> SummarizeStructMembers(StructDeclarationSyntax structDecl)
        {
            return await structDecl.Members.SelectManyAsync(SummarizeMember);
        }

        static async Task<string> GetMethodDescription(MethodDeclarationSyntax methodDecl)
        {
            string methodCode = methodDecl.ToString();
            string prompt = $"Summarize the following C# method optimizing for the smallest number of tokens possible and clarity.:\n\n{methodCode}\n\nSummary:";

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

            string jsonRequestBody = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
            response.EnsureSuccessStatusCode();

            string responseContent = await response.Content.ReadAsStringAsync();
            var result = System.Text.Json.JsonDocument.Parse(responseContent);
            string summary = result.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString().Trim();

            return PostProcessSummary(summary, tokenLimit);
        }

        static string PostProcessSummary(string summary, int tokenLimit)
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

        static async Task WriteSummariesToJson(List<FileSummary> summaries)
        {
            string json = JsonSerializer.Serialize(summaries, new JsonSerializerOptions { WriteIndented = true });
            string outputPath = Path.Combine(outputDirectory, outputFileName);
            await File.WriteAllTextAsync(outputPath, json);
            Console.WriteLine($"Summaries written to {outputFileName}");
        }
    }

    public class FileSummary
    {
        public string FileName { get; set; }
        public List<string> Members { get; set; }
    }

    public static class EnumerableExtensions
    {
        public static async Task<IEnumerable<TResult>> SelectManyAsync<TSource, TResult>(
            this IEnumerable<TSource> source,
            Func<TSource, Task<IEnumerable<TResult>>> selector)
        {
            var results = new List<TResult>();
            foreach (var item in source)
            {
                results.AddRange(await selector(item));
            }

            return results;
        }
    }
}
