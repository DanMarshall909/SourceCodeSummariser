using Microsoft.Extensions.Configuration;

namespace SourceCodeSummariser
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            // Display banner
            Console.WriteLine("===========================================");
            Console.WriteLine("  Source Code Summariser");
            Console.WriteLine("  AI-powered C# code documentation tool");
            Console.WriteLine("===========================================\n");

            // Load configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var settings = new AppSettings();
            configuration.Bind(settings);

            // Validate configuration
            if (string.IsNullOrEmpty(settings.OpenAI.ApiKey) || settings.OpenAI.ApiKey == "YOUR_OPENAI_API_KEY_HERE")
            {
                Console.WriteLine("ERROR: OpenAI API key not configured.");
                Console.WriteLine("\nPlease set your API key in one of the following ways:");
                Console.WriteLine("  1. Edit appsettings.json and set OpenAI:ApiKey");
                Console.WriteLine("  2. Set environment variable: OpenAI__ApiKey=your-key-here");
                Console.WriteLine("\nYou can get an API key from: https://platform.openai.com/api-keys");
                return;
            }

            // Parse command-line arguments
            if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
            {
                ShowHelp();
                return;
            }

            string folderPath = args[0];

            if (!Directory.Exists(folderPath))
            {
                Console.WriteLine($"ERROR: The specified folder does not exist: {folderPath}");
                return;
            }

            Console.WriteLine($"Processing folder: {folderPath}");
            Console.WriteLine($"Using model: {settings.OpenAI.Model}");
            Console.WriteLine($"Database: {settings.Database.ConnectionString}\n");

            try
            {
                var httpClient = new HttpClient();
                var dbContext = new SummaryContext(settings.Database.ConnectionString);
                var summarizerService = new SummarizerService(httpClient, settings.OpenAI);
                var fileProcessorService = new FileProcessorService(dbContext, summarizerService);

                var changedFiles = new Dictionary<string, List<(string MethodSignature, string OldSummary, string NewSummary)>>();
                var processedFiles = 0;
                var skippedFiles = 0;

                var files = Directory.EnumerateFiles(folderPath, settings.Processing.FilePattern, SearchOption.AllDirectories).ToList();
                Console.WriteLine($"Found {files.Count} C# files to process.\n");

                foreach (var file in files)
                {
                    if (IsInExcludedFolder(file, folderPath, settings.Processing.ExcludedFolders))
                    {
                        Console.WriteLine($"  [SKIP] {Path.GetFileName(file)} (excluded folder)");
                        skippedFiles++;
                        continue;
                    }

                    try
                    {
                        Console.Write($"  [PROCESSING] {Path.GetFileName(file)}... ");
                        var changes = await fileProcessorService.ProcessFile(file);
                        if (changes.Any())
                        {
                            changedFiles[file] = changes;
                            Console.WriteLine($"✓ ({changes.Count} changes detected)");
                        }
                        else
                        {
                            Console.WriteLine("✓ (no changes)");
                        }
                        processedFiles++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ ERROR: {ex.Message}");
                        skippedFiles++;
                    }
                }

                Console.WriteLine($"\n===========================================");
                Console.WriteLine($"Processing Summary:");
                Console.WriteLine($"  Total files found: {files.Count}");
                Console.WriteLine($"  Successfully processed: {processedFiles}");
                Console.WriteLine($"  Skipped/Failed: {skippedFiles}");
                Console.WriteLine($"  Files with changes: {changedFiles.Count}");
                Console.WriteLine($"===========================================\n");

                // Display changed methods grouped by file
                if (changedFiles.Any())
                {
                    Console.WriteLine("CHANGES DETECTED:\n");
                    foreach (var (file, changes) in changedFiles)
                    {
                        Console.WriteLine($"File: {file}");
                        foreach (var (methodSignature, oldSummary, newSummary) in changes)
                        {
                            Console.WriteLine($"\n  ### {methodSignature}");
                            if (!string.IsNullOrEmpty(oldSummary))
                            {
                                Console.WriteLine($"      Old: {oldSummary}");
                            }
                            Console.WriteLine($"      New: {newSummary}");
                        }
                        Console.WriteLine();
                    }
                }

                Console.WriteLine("✓ Processing completed. Summaries saved to the database.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nFATAL ERROR: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }

        private static bool IsInExcludedFolder(string filePath, string rootPath, List<string> excludedFolders)
        {
            // Normalize paths for consistent comparison
            string normalizedPath = Path.GetFullPath(filePath).ToLower();
            string normalizedRootPath = Path.GetFullPath(rootPath).ToLower();

            foreach (var excludedFolder in excludedFolders)
            {
                string excludedPath = Path.Combine(normalizedRootPath, excludedFolder).ToLower();
                if (normalizedPath.Contains(excludedPath))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ShowHelp()
        {
            Console.WriteLine("USAGE:");
            Console.WriteLine("  SourceCodeSummariser <folder-path>");
            Console.WriteLine();
            Console.WriteLine("DESCRIPTION:");
            Console.WriteLine("  Analyzes C# source code files in the specified folder and generates");
            Console.WriteLine("  AI-powered summaries using OpenAI's GPT models. Summaries are stored");
            Console.WriteLine("  in a SQLite database and changes are tracked over time.");
            Console.WriteLine();
            Console.WriteLine("ARGUMENTS:");
            Console.WriteLine("  <folder-path>    Path to the folder containing C# source code");
            Console.WriteLine();
            Console.WriteLine("OPTIONS:");
            Console.WriteLine("  -h, --help       Show this help message");
            Console.WriteLine();
            Console.WriteLine("CONFIGURATION:");
            Console.WriteLine("  The tool reads configuration from appsettings.json:");
            Console.WriteLine("  - OpenAI:ApiKey          Your OpenAI API key (required)");
            Console.WriteLine("  - OpenAI:Model           Model to use (default: gpt-3.5-turbo)");
            Console.WriteLine("  - OpenAI:MaxTokens       Max tokens per summary (default: 50)");
            Console.WriteLine("  - Database:ConnectionString  SQLite database path");
            Console.WriteLine("  - Processing:ExcludedFolders  Folders to skip (default: bin, obj, .git)");
            Console.WriteLine();
            Console.WriteLine("  You can also set configuration via environment variables:");
            Console.WriteLine("    OpenAI__ApiKey=your-key-here");
            Console.WriteLine();
            Console.WriteLine("EXAMPLES:");
            Console.WriteLine("  SourceCodeSummariser ./MyProject");
            Console.WriteLine("  SourceCodeSummariser C:\\Projects\\MyApp\\src");
            Console.WriteLine();
        }
    }
}
