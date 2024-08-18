using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Net.Http.Headers;

namespace SourceCodeSummarizer
{
    public class SummaryContext : DbContext
    {
        public DbSet<FileEntity> Files { get; set; } = null!;
        public DbSet<MemberEntity> Members { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=summaries.db");
        }

        public void TruncateTables()
        {
            Files.RemoveRange(Files);
            Members.RemoveRange(Members);
            SaveChanges();
        }
    }

    public class FileEntity
    {
        public int Id { get; set; }
        public string FileName { get; set; } = null!;
        public List<MemberEntity> Members { get; set; } = new List<MemberEntity>();
    }

    public class MemberEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string Summary { get; set; } = null!;
        public FileEntity File { get; set; } = null!;
        public int FileEntityId { get; set; }
    }

    class Program
    {
        private static readonly HttpClient client = new HttpClient();
        private static string apiKey = null!;

        static Program()
        {
            // Retrieve the API key from the environment variable
            apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY_TestChat") ?? throw new InvalidOperationException("API key not found in environment variables.");
            
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var dbContext = new SummaryContext();
            dbContext.Database.EnsureCreated();
            dbContext.TruncateTables(); // Truncate the database on restart
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

            using var dbContext = new SummaryContext();

            // Traverse the directory and process each file
            foreach (var file in Directory.EnumerateFiles(folderPath, "*.cs", SearchOption.AllDirectories))
            {
                if (IsInExcludedFolder(file, folderPath))
                {
                    Console.WriteLine($"Skipping file in excluded folder: {file}");
                    continue;
                }

                Console.WriteLine($"Processing file: {file}");
                await ProcessFile(file, dbContext);
            }

            Console.WriteLine("Processing completed. Summaries saved to the database.");
        }

        static bool IsInExcludedFolder(string filePath, string rootPath)
        {
            // Normalize paths for consistent comparison
            string normalizedPath = Path.GetFullPath(filePath).ToLower();
            string normalizedRootPath = Path.GetFullPath(rootPath).ToLower();

            return normalizedPath.Contains(Path.Combine(normalizedRootPath, "bin").ToLower()) ||
                   normalizedPath.Contains(Path.Combine(normalizedRootPath, "obj").ToLower());
        }

        static async Task ProcessFile(string filePath, SummaryContext dbContext)
        {
            string content = await File.ReadAllTextAsync(filePath);
            var fileSummary = await GenerateFileSummary(filePath, content);

            var fileEntity = new FileEntity
            {
                FileName = fileSummary.FileName,
                Members = fileSummary.Members.Select(m => new MemberEntity
                {
                    Name = m.Split(':')[1].Trim(),
                    Type = m.Split(':')[0].Trim(),
                    Summary = m,
                }).ToList()
            };

            dbContext.Files.Add(fileEntity);
            await dbContext.SaveChangesAsync();
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
                    summaries.AddRange((await Task.WhenAll(namespaceDecl.Members.Select(SummarizeMember))).SelectMany(s => s));
                    break;

                case ClassDeclarationSyntax classDecl:
                    summaries.Add($"Class: {classDecl.Identifier.Text}");
                    summaries.AddRange((await Task.WhenAll(classDecl.Members.Select(SummarizeMember))).SelectMany(s => s));
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
                    summaries.AddRange((await Task.WhenAll(interfaceDecl.Members.Select(SummarizeMember))).SelectMany(s => s));
                    break;

                case StructDeclarationSyntax structDecl:
                    summaries.Add($"Struct: {structDecl.Identifier.Text}");
                    summaries.AddRange((await Task.WhenAll(structDecl.Members.Select(SummarizeMember))).SelectMany(s => s));
                    break;

                default:
                    summaries.Add($"Unhandled member type: {member.Kind()}");
                    break;
            }

            return summaries;
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
    }

    public class FileSummary
    {
        public string FileName { get; set; } = null!;
        public List<string> Members { get; set; } = new List<string>();
    }
}
