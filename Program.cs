using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Net.Http.Headers;
using System.Security.Cryptography;

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
        public string Hash { get; set; } = null!;
        public FileEntity File { get; set; } = null!;
        public int FileEntityId { get; set; }
    }

    class Program
    {
        private static readonly HttpClient client = new HttpClient();
        private static string apiKey = null!;

        static Program()
        {
            apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY_TestChat") ??
                     throw new InvalidOperationException("API key not found in environment variables.");

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var dbContext = new SummaryContext();
            dbContext.Database.EnsureCreated();
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
            var changedFiles = new Dictionary<string, List<(string MethodSignature, string OldSummary, string NewSummary)>>();

            // Traverse the directory and process each file
            foreach (var file in Directory.EnumerateFiles(folderPath, "*.cs", SearchOption.AllDirectories))
            {
                if (IsInExcludedFolder(file, folderPath))
                {
                    Console.WriteLine($"Skipping file in excluded folder: {file}");
                    continue;
                }

                var changes = await ProcessFile(file, dbContext);
                if (changes.Any())
                {
                    changedFiles[file] = changes;
                }
            }

            // Display changed methods grouped by file
            foreach (var (file, changes) in changedFiles)
            {
                Console.WriteLine($"\nFile: {file}");
                foreach (var (methodSignature, oldSummary, newSummary) in changes)
                {
                    Console.WriteLine($"\n### Method: {methodSignature}");
                    Console.WriteLine($"Old Summary: {oldSummary}");
                    Console.WriteLine($"New Summary: {newSummary}");
                }
            }

            Console.WriteLine("\nProcessing completed. Summaries saved to the database.");
        }

        static bool IsInExcludedFolder(string filePath, string rootPath)
        {
            // Normalize paths for consistent comparison
            string normalizedPath = Path.GetFullPath(filePath).ToLower();
            string normalizedRootPath = Path.GetFullPath(rootPath).ToLower();

            return normalizedPath.Contains(Path.Combine(normalizedRootPath, "bin").ToLower()) ||
                   normalizedPath.Contains(Path.Combine(normalizedRootPath, "obj").ToLower());
        }

        static async Task<List<(string MethodSignature, string OldSummary, string NewSummary)>> ProcessFile(string filePath, SummaryContext dbContext)
        {
            string content = await File.ReadAllTextAsync(filePath);
            var fileSummary = await GenerateFileSummary(filePath, content, dbContext);

            var changes = new List<(string MethodSignature, string OldSummary, string NewSummary)>();

            var fileEntity = dbContext.Files.Include(f => f.Members)
                .FirstOrDefault(f => f.FileName == fileSummary.FileName);
            if (fileEntity == null)
            {
                fileEntity = new FileEntity { FileName = fileSummary.FileName };
                dbContext.Files.Add(fileEntity);
                await dbContext.SaveChangesAsync();
            }

            foreach (var memberSummary in fileSummary.Members)
            {
                string memberHash = ComputeHash(memberSummary);
                var existingMember = dbContext.Members
                    .FirstOrDefault(m => m.Hash == memberHash && m.FileEntityId == fileEntity.Id);

                string methodSignature = memberSummary.Split('-')[0].Trim();
                string newSummary = memberSummary;

                if (existingMember == null)
                {
                    var newMember = new MemberEntity
                    {
                        Name = memberSummary.Split(':')[1].Trim(),
                        Type = memberSummary.Split(':')[0].Trim(),
                        Summary = memberSummary,
                        Hash = memberHash,
                        File = fileEntity
                    };
                    dbContext.Members.Add(newMember);
                    await dbContext.SaveChangesAsync();
                    changes.Add((methodSignature, string.Empty, newSummary));
                }
                else if (existingMember.Summary != newSummary)
                {
                    changes.Add((methodSignature, existingMember.Summary, newSummary));
                    existingMember.Summary = newSummary;
                    existingMember.Hash = memberHash;
                    await dbContext.SaveChangesAsync();
                }
            }

            return changes;
        }

        static async Task<FileSummary> GenerateFileSummary(string filePath, string content, SummaryContext dbContext)
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
                    var memberSummary = await SummarizeMember(member, dbContext);
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

        static async Task<IEnumerable<string>> SummarizeMember(MemberDeclarationSyntax member, SummaryContext dbContext)
        {
            var summaries = new List<string>();
            string memberHash = ComputeHash(member.ToString());

            var existingMember = dbContext.Members.FirstOrDefault(m => m.Hash == memberHash);
            if (existingMember != null)
            {
                summaries.Add(existingMember.Summary);
                return summaries;
            }

            switch (member)
            {
                case NamespaceDeclarationSyntax namespaceDecl:
                    summaries.Add($"Namespace: {namespaceDecl.Name}");
                    summaries.AddRange(
                        (await Task.WhenAll(namespaceDecl.Members.Select(m => SummarizeMember(m, dbContext))))
                        .SelectMany(s => s));
                    break;

                case ClassDeclarationSyntax classDecl:
                    summaries.Add($"Class: {classDecl.Identifier.Text}");
                    summaries.AddRange(
                        (await Task.WhenAll(classDecl.Members.Select(m => SummarizeMember(m, dbContext))))
                        .SelectMany(s => s));
                    break;

                case MethodDeclarationSyntax methodDecl:
                    summaries.Add(await SummarizeMethod(methodDecl, dbContext));
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
                        (await Task.WhenAll(interfaceDecl.Members.Select(m => SummarizeMember(m, dbContext))))
                        .SelectMany(s => s));
                    break;

                case StructDeclarationSyntax structDecl:
                    summaries.Add($"Struct: {structDecl.Identifier.Text}");
                    summaries.AddRange(
                        (await Task.WhenAll(structDecl.Members.Select(m => SummarizeMember(m, dbContext))))
                        .SelectMany(s => s));
                    break;

                default:
                    summaries.Add($"Unhandled member type: {member.Kind()}");
                    break;
            }

            return summaries;
        }

        static async Task<string> SummarizeMethod(MethodDeclarationSyntax methodDecl, SummaryContext dbContext)
        {
            var input = methodDecl.ToString();
            string methodHash = ComputeHash(input);
            var existingSummary = dbContext.Members.FirstOrDefault(m => m.Hash == methodHash);

            if (existingSummary != null)
            {
                return existingSummary.Summary;
            }

            string methodDescription = await GetMethodDescription(methodDecl);
            return $"Method: {methodDecl.Identifier.Text} - {methodDescription}";
        }

        static IEnumerable<string> SummarizeFields(FieldDeclarationSyntax fieldDecl)
        {
            return fieldDecl.Declaration.Variables.Select(variable =>
                $"Field: {variable.Identifier.Text} ({fieldDecl.Declaration.Type})");
        }

        static async Task<string> GetMethodDescription(MethodDeclarationSyntax methodDecl)
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
                        content =
                            "You are a code summarizer. The less tokens you can use the better, but accuracy is far more important than brevity."
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
            string summary = result.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content")
                .GetString().Trim();

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

        static string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }
    }

    public class FileSummary
    {
        public string FileName { get; set; } = null!;
        public List<string> Members { get; set; } = new List<string>();
    }
}
