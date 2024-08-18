using System.Text;
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

            var summaries = new List<string>();

            // Traverse the directory and process each file
            foreach (var file in Directory.EnumerateFiles(folderPath, "*.cs", SearchOption.AllDirectories))
            {
                string content = await File.ReadAllTextAsync(file);
                var summary = await GenerateFileSummary(file, content);
                summaries.Add(summary);

                // Save individual file summary to disk
                string summaryFileName = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(file) + "_summary.txt");
                await File.WriteAllTextAsync(summaryFileName, summary);
            }

            // Chunk the summaries to fit within token limits
            const int tokenLimit = 4000; // Assuming each token is roughly 4 characters
            var chunks = ChunkSummaries(summaries, tokenLimit);

            // Write the chunks to disk
            for (int i = 0; i < chunks.Count; i++)
            {
                string chunkFileName = Path.Combine(outputDirectory, $"Chunk_{i + 1}.txt");
                await File.WriteAllTextAsync(chunkFileName, chunks[i]);
                Console.WriteLine($"Chunk {i + 1} written to {chunkFileName}");
            }
        }

        static async Task<string> GenerateFileSummary(string filePath, string content)
        {
            string fileName = Path.GetFileName(filePath);
            var memberSummaries = new List<string>();

            // Load existing summary if it exists
            string existingSummaryFilePath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(filePath) + "_summary.txt");
            if (File.Exists(existingSummaryFilePath))
            {
                memberSummaries.Add($"Existing summary found for {fileName}:");
                memberSummaries.AddRange(await File.ReadAllLinesAsync(existingSummaryFilePath));
                memberSummaries.Add("\n---\n");
            }

            SyntaxTree tree = CSharpSyntaxTree.ParseText(content);
            var root = tree.GetRoot() as CompilationUnitSyntax;

            if (root != null)
            {
                foreach (var member in root.Members)
                {
                    if (member is NamespaceDeclarationSyntax namespaceDecl)
                    {
                        foreach (var nsMember in namespaceDecl.Members)
                        {
                            memberSummaries.AddRange(await SummarizeMember(nsMember));
                        }
                    }
                    else
                    {
                        memberSummaries.AddRange(await SummarizeMember(member));
                    }
                }
            }

            return $"File: {fileName}\n" + string.Join("\n", memberSummaries);
        }

        static async Task<IEnumerable<string>> SummarizeMember(MemberDeclarationSyntax member)
        {
            var summaries = new List<string>();

            switch (member)
            {
                case ClassDeclarationSyntax classDecl:
                    summaries.Add($"Class: {classDecl.Identifier.Text}");
                    summaries.AddRange(await classDecl.Members.SelectManyAsync(SummarizeMember));
                    break;

                case MethodDeclarationSyntax methodDecl:
                    string methodDescription = await GetMethodDescription(methodDecl);
                    summaries.Add($"Method: {methodDecl.Identifier.Text} - {methodDescription}");
                    break;

                case PropertyDeclarationSyntax propertyDecl:
                    summaries.Add($"Property: {propertyDecl.Identifier.Text} ({propertyDecl.Type})");
                    break;

                case FieldDeclarationSyntax fieldDecl:
                    foreach (var variable in fieldDecl.Declaration.Variables)
                    {
                        summaries.Add($"Field: {variable.Identifier.Text} ({fieldDecl.Declaration.Type})");
                    }
                    break;

                case InterfaceDeclarationSyntax interfaceDecl:
                    summaries.Add($"Interface: {interfaceDecl.Identifier.Text}");
                    summaries.AddRange(await interfaceDecl.Members.SelectManyAsync(SummarizeMember));
                    break;

                case StructDeclarationSyntax structDecl:
                    summaries.Add($"Struct: {structDecl.Identifier.Text}");
                    summaries.AddRange(await structDecl.Members.SelectManyAsync(SummarizeMember));
                    break;

                default:
                    break;
            }

            return summaries;
        }

        static async Task<string> GetMethodDescription(MethodDeclarationSyntax methodDecl)
        {
            string methodCode = methodDecl.ToString();
            string prompt = $"Summarize the following C# method optimising for the smallest number of tokens possible and clarity.:\n\n{methodCode}\n\nSummary:";

            var requestBody = new
            {
                model = "gpt-4",
                messages = new[]
                {
                    new { role = "system", content = "You are a code summarizer. The less tokens you can use the better, but accuracy is far more important than brevity." },
                    new { role = "user", content = prompt }
                },
                max_tokens = 20
            };

            string jsonRequestBody = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
            response.EnsureSuccessStatusCode();

            string responseContent = await response.Content.ReadAsStringAsync();
            var result = System.Text.Json.JsonDocument.Parse(responseContent);

            return result.RootElement
                         .GetProperty("choices")[0]
                         .GetProperty("message")
                         .GetProperty("content")
                         .GetString()
                         .Trim();
        }

        static List<string> ChunkSummaries(List<string> summaries, int tokenLimit)
        {
            var chunks = new List<string>();
            var currentChunk = new StringBuilder();

            foreach (var summary in summaries)
            {
                if (currentChunk.Length + summary.Length > tokenLimit)
                {
                    chunks.Add(currentChunk.ToString());
                    currentChunk.Clear();
                }

                currentChunk.AppendLine(summary);
            }

            if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString());
            }

            return chunks;
        }
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
