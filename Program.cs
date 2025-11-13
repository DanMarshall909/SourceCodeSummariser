using Microsoft.Extensions.Configuration;

namespace SourceCodeSummariser
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            var logger = new ConsoleLogger();

            // Display banner
            logger.WriteLine("===========================================");
            logger.WriteLine("  Source Code Summariser");
            logger.WriteLine("  AI-powered C# code documentation tool");
            logger.WriteLine("===========================================\n");

            // Load configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var settings = new AppSettings();
            configuration.Bind(settings);

            // Validate configuration
            if (string.IsNullOrEmpty(settings.OpenAI.ApiKey))
            {
                logger.WriteLine("ERROR: OpenAI API key not configured.");
                logger.WriteLine("\nPlease set your API key as an environment variable:");
                logger.WriteLine("  Linux/macOS:  export OpenAI__ApiKey=your-key-here");
                logger.WriteLine("  Windows:      set OpenAI__ApiKey=your-key-here");
                logger.WriteLine("\nYou can get an API key from: https://platform.openai.com/api-keys");
                return;
            }

            // Parse command-line arguments
            if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
            {
                ShowHelp(logger);
                return;
            }

            // Check for watch mode flag
            bool watchMode = args.Contains("--watch") || args.Contains("-w");
            string folderPath = args.FirstOrDefault(arg => !arg.StartsWith("-")) ?? string.Empty;

            if (string.IsNullOrEmpty(folderPath))
            {
                logger.WriteLine("ERROR: No folder path specified.");
                ShowHelp(logger);
                return;
            }

            if (!Directory.Exists(folderPath))
            {
                logger.WriteLine($"ERROR: The specified folder does not exist: {folderPath}");
                return;
            }

            logger.WriteLine($"Processing folder: {folderPath}");
            logger.WriteLine($"Using model: {settings.OpenAI.Model}");
            logger.WriteLine($"Database: {settings.Database.ConnectionString}");
            logger.WriteLine($"Mode: {(watchMode ? "Watch (continuous monitoring)" : "One-time processing")}\n");

            try
            {
                var httpClient = new HttpClient();
                var dbContext = new SummaryContext(settings.Database.ConnectionString);
                var summarizerService = new SummarizerService(httpClient, settings.OpenAI);
                var fileProcessorService = new FileProcessorService(dbContext, summarizerService);

                if (watchMode)
                {
                    // Run initial scan before starting watch mode
                    logger.WriteLine("Running initial scan...\n");
                    await ProcessAllFilesOnce(folderPath, settings, fileProcessorService, logger);

                    // Start file watching
                    using var fileWatcher = new FileWatcherService(folderPath, fileProcessorService, settings, logger);
                    fileWatcher.Start();

                    // Keep the application running until Ctrl+C is pressed
                    var cancellationTokenSource = new CancellationTokenSource();
                    Console.CancelKeyPress += (sender, eventArgs) =>
                    {
                        eventArgs.Cancel = true;
                        cancellationTokenSource.Cancel();
                        logger.WriteLine("\n\nStopping file watcher...");
                    };

                    // Wait indefinitely until cancellation is requested
                    await Task.Delay(Timeout.Infinite, cancellationTokenSource.Token);
                }
                else
                {
                    // One-time processing mode
                    await ProcessAllFilesOnce(folderPath, settings, fileProcessorService, logger);
                    logger.WriteLine("✓ Processing completed. Summaries saved to the database.");
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown via Ctrl+C
                logger.WriteLine("✓ File watcher stopped successfully.");
            }
            catch (Exception ex)
            {
                logger.WriteLine($"\nFATAL ERROR: {ex.Message}");
                logger.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }

        private static async Task ProcessAllFilesOnce(string folderPath, AppSettings settings, FileProcessorService fileProcessorService, ILogger logger)
        {
            var changedFiles = new Dictionary<string, List<(string MethodSignature, string OldSummary, string NewSummary)>>();
            var processedFiles = 0;
            var skippedFiles = 0;

            var files = Directory.EnumerateFiles(folderPath, settings.Processing.FilePattern, SearchOption.AllDirectories).ToList();
            logger.WriteLine($"Found {files.Count} C# files to process.\n");

            foreach (var file in files)
            {
                if (IsInExcludedFolder(file, folderPath, settings.Processing.ExcludedFolders))
                {
                    logger.WriteLine($"  [SKIP] {Path.GetFileName(file)} (excluded folder)");
                    skippedFiles++;
                    continue;
                }

                try
                {
                    logger.Write($"  [PROCESSING] {Path.GetFileName(file)}... ");
                    var changes = await fileProcessorService.ProcessFile(file);
                    if (changes.Any())
                    {
                        changedFiles[file] = changes;
                        logger.WriteLine($"✓ ({changes.Count} changes detected)");
                    }
                    else
                    {
                        logger.WriteLine("✓ (no changes)");
                    }
                    processedFiles++;
                }
                catch (Exception ex)
                {
                    logger.WriteLine($"✗ ERROR: {ex.Message}");
                    skippedFiles++;
                }
            }

            logger.WriteLine($"\n===========================================");
            logger.WriteLine($"Processing Summary:");
            logger.WriteLine($"  Total files found: {files.Count}");
            logger.WriteLine($"  Successfully processed: {processedFiles}");
            logger.WriteLine($"  Skipped/Failed: {skippedFiles}");
            logger.WriteLine($"  Files with changes: {changedFiles.Count}");
            logger.WriteLine($"===========================================\n");

            // Display changed methods grouped by file
            if (changedFiles.Any())
            {
                logger.WriteLine("CHANGES DETECTED:\n");
                foreach (var (file, changes) in changedFiles)
                {
                    logger.WriteLine($"File: {file}");
                    foreach (var (methodSignature, oldSummary, newSummary) in changes)
                    {
                        logger.WriteLine($"\n  ### {methodSignature}");
                        if (!string.IsNullOrEmpty(oldSummary))
                        {
                            logger.WriteLine($"      Old: {oldSummary}");
                        }
                        logger.WriteLine($"      New: {newSummary}");
                    }
                    logger.WriteLine();
                }
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

        private static void ShowHelp(ILogger logger)
        {
            logger.WriteLine("USAGE:");
            logger.WriteLine("  SourceCodeSummariser <folder-path> [options]");
            logger.WriteLine();
            logger.WriteLine("DESCRIPTION:");
            logger.WriteLine("  Analyzes C# source code files in the specified folder and generates");
            logger.WriteLine("  AI-powered summaries using OpenAI's GPT models. Summaries are stored");
            logger.WriteLine("  in a SQLite database and changes are tracked over time.");
            logger.WriteLine();
            logger.WriteLine("ARGUMENTS:");
            logger.WriteLine("  <folder-path>    Path to the folder containing C# source code");
            logger.WriteLine();
            logger.WriteLine("OPTIONS:");
            logger.WriteLine("  -h, --help       Show this help message");
            logger.WriteLine("  -w, --watch      Enable watch mode (continuously monitor for file changes)");
            logger.WriteLine();
            logger.WriteLine("CONFIGURATION:");
            logger.WriteLine("  API Key (required) - Set via environment variable:");
            logger.WriteLine("    OpenAI__ApiKey=your-key-here");
            logger.WriteLine();
            logger.WriteLine("  Other settings can be customized in appsettings.json:");
            logger.WriteLine("  - OpenAI:Model           Model to use (default: gpt-3.5-turbo)");
            logger.WriteLine("  - OpenAI:MaxTokens       Max tokens per summary (default: 50)");
            logger.WriteLine("  - Database:ConnectionString  SQLite database path");
            logger.WriteLine("  - Processing:ExcludedFolders  Folders to skip (default: bin, obj, .git)");
            logger.WriteLine();
            logger.WriteLine("EXAMPLES:");
            logger.WriteLine("  # One-time processing");
            logger.WriteLine("  SourceCodeSummariser ./MyProject");
            logger.WriteLine("  SourceCodeSummariser C:\\Projects\\MyApp\\src");
            logger.WriteLine();
            logger.WriteLine("  # Watch mode (continuously monitor for changes)");
            logger.WriteLine("  SourceCodeSummariser ./MyProject --watch");
            logger.WriteLine("  SourceCodeSummariser ./MyProject -w");
            logger.WriteLine();
        }
    }
}
