namespace SourceCodeSummariser
{
    internal class Program
    {
        private static async Task Main(string[] args)
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

            var httpClient = new HttpClient();
            var dbContext = new SummaryContext();
            var summarizerService = new SummarizerService(httpClient);
            var fileProcessorService = new FileProcessorService(dbContext, summarizerService);

            var changedFiles = new Dictionary<string, List<(string MethodSignature, string OldSummary, string NewSummary)>>();

            foreach (var file in Directory.EnumerateFiles(folderPath, "*.cs", SearchOption.AllDirectories))
            {
                if (IsInExcludedFolder(file, folderPath))
                {
                    Console.WriteLine($"Skipping file in excluded folder: {file}");
                    continue;
                }

                var changes = await fileProcessorService.ProcessFile(file);
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

        private static bool IsInExcludedFolder(string filePath, string rootPath)
        {
            // Normalize paths for consistent comparison
            string normalizedPath = Path.GetFullPath(filePath).ToLower();
            string normalizedRootPath = Path.GetFullPath(rootPath).ToLower();

            return normalizedPath.Contains(Path.Combine(normalizedRootPath, "bin").ToLower()) ||
                   normalizedPath.Contains(Path.Combine(normalizedRootPath, "obj").ToLower());
        }
    }
}
