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
    /// Service for analyzing C# code and generating AI-powered summaries using OpenAI.
    /// </summary>
    public class SummarizerService
    {
        private readonly HttpClient _httpClient;
        private readonly OpenAISettings _openAISettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="SummarizerService"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client for making API requests.</param>
        /// <param name="openAISettings">The OpenAI configuration settings.</param>
        public SummarizerService(HttpClient httpClient, OpenAISettings openAISettings)
        {
            _httpClient = httpClient;
            _openAISettings = openAISettings;

            // Configure HttpClient timeout
            _httpClient.Timeout = TimeSpan.FromSeconds(openAISettings.TimeoutSeconds);

            // Set OpenAI API key header
            if (!string.IsNullOrEmpty(openAISettings.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAISettings.ApiKey}");
            }
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
        /// Generates an AI-powered summary for a method using OpenAI's API.
        /// </summary>
        /// <param name="methodDecl">The method declaration syntax node.</param>
        /// <returns>A formatted string containing the method signature and AI-generated summary.</returns>
        public async Task<string> SummarizeMethod(MethodDeclarationSyntax methodDecl)
        {
            var methodSignature = $"{methodDecl.Modifiers} {methodDecl.ReturnType} {methodDecl.Identifier}({string.Join(", ", methodDecl.ParameterList.Parameters)})";

            try
            {
                var methodCode = methodDecl.ToString();
                var requestBody = new
                {
                    model = _openAISettings.Model,
                    messages = new[]
                    {
                        new { role = "system", content = "You are a code summarizer. The less tokens you can use the better, but accuracy is far more important than brevity." },
                        new { role = "user", content = $"Summarize the following C# method optimizing for the smallest number of tokens possible and clarity.:\n\n{methodCode}\n\nSummary:" }
                    },
                    max_tokens = _openAISettings.MaxTokens
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"OpenAI API request failed with status {response.StatusCode}: {errorContent}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(responseContent);

                var summary = jsonDoc.RootElement.GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString()
                    ?.Trim();

                if (string.IsNullOrEmpty(summary))
                {
                    return $"Method: {methodSignature} - [AI summary unavailable]";
                }

                return $"Method: {methodSignature} - {PostProcessSummary(summary, _openAISettings.MaxTokens)}";
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"    ERROR calling OpenAI API for method {methodDecl.Identifier}: {ex.Message}");
                return $"Method: {methodSignature} - [Error: {ex.Message}]";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ERROR processing method {methodDecl.Identifier}: {ex.Message}");
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
