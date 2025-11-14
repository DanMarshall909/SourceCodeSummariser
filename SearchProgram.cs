using Microsoft.Extensions.Configuration;

namespace SourceCodeSummariser;

/// <summary>
/// Simple CLI tool for semantic code search.
/// Usage: dotnet run --project Search.csproj -- "your search query"
/// </summary>
public class SearchProgram
{
    public static async Task Main(string[] args)
    {
        var logger = new ConsoleLogger();

        // Display banner
        logger.WriteLine("===========================================");
        logger.WriteLine("  Semantic Code Search");
        logger.WriteLine("  AI-powered code discovery");
        logger.WriteLine("===========================================\n");

        // Parse command-line arguments
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            ShowHelp(logger);
            return;
        }

        // Load configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var settings = new AppSettings();
        configuration.Bind(settings);

        // Check if embeddings are enabled
        if (!settings.LlmProvider.EnableEmbeddings)
        {
            logger.WriteLine("ERROR: Embeddings are not enabled.");
            logger.WriteLine("Please set 'LlmProvider:EnableEmbeddings' to true in appsettings.json");
            return;
        }

        // Parse search parameters
        string query = "";
        int topK = 10;
        float minSimilarity = 0.0f;
        var tags = new List<string>();
        int? similarTo = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--top":
                case "-k":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out var k))
                    {
                        topK = k;
                        i++;
                    }
                    break;
                case "--min-similarity":
                case "-s":
                    if (i + 1 < args.Length && float.TryParse(args[i + 1], out var s))
                    {
                        minSimilarity = s;
                        i++;
                    }
                    break;
                case "--tags":
                case "-t":
                    if (i + 1 < args.Length)
                    {
                        tags = args[i + 1].Split(',').Select(t => t.Trim()).ToList();
                        i++;
                    }
                    break;
                case "--similar-to":
                case "-m":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out var memberId))
                    {
                        similarTo = memberId;
                        i++;
                    }
                    break;
                default:
                    if (!args[i].StartsWith("-"))
                    {
                        query = args[i];
                    }
                    break;
            }
        }

        // Validate input
        if (string.IsNullOrEmpty(query) && !similarTo.HasValue)
        {
            logger.WriteLine("ERROR: No search query or --similar-to parameter specified.");
            ShowHelp(logger);
            return;
        }

        try
        {
            // Create LLM provider
            ILlmProvider llmProvider = CreateLlmProvider(settings, logger);

            // Create database context and search service
            var dbContext = new SummaryContext(settings.Database.ConnectionString);
            var searchService = new SemanticSearchService(dbContext, llmProvider, settings.LlmProvider);

            // Perform search
            List<SemanticSearchResult> results;

            if (similarTo.HasValue)
            {
                logger.WriteLine($"Finding members similar to ID {similarTo.Value}...\n");
                results = await searchService.FindSimilarMembersAsync(similarTo.Value, topK, minSimilarity);
            }
            else if (tags.Any())
            {
                logger.WriteLine($"Searching for: \"{query}\" with tags: [{string.Join(", ", tags)}]");
                logger.WriteLine($"Top {topK} results (min similarity: {minSimilarity:F2})\n");
                results = await searchService.SearchWithTagsAsync(query, tags, topK, minSimilarity);
            }
            else
            {
                logger.WriteLine($"Searching for: \"{query}\"");
                logger.WriteLine($"Top {topK} results (min similarity: {minSimilarity:F2})\n");
                results = await searchService.SearchAsync(query, topK, minSimilarity);
            }

            // Display results
            if (results.Any())
            {
                logger.WriteLine($"Found {results.Count} result(s):\n");
                logger.WriteLine("===========================================\n");

                for (int i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    logger.WriteLine($"{i + 1}. Similarity: {result.Similarity:F4} | ID: {result.Member.Id}");
                    logger.WriteLine($"   {result.Member.Type}: {result.Member.Name}");
                    logger.WriteLine($"   File: {result.FilePath}");
                    if (result.Tags.Any())
                    {
                        logger.WriteLine($"   Tags: {string.Join(", ", result.Tags)}");
                    }
                    logger.WriteLine($"   Summary: {result.Member.Summary}");
                    logger.WriteLine();
                }
            }
            else
            {
                logger.WriteLine("No results found. Try:");
                logger.WriteLine("  - Using a broader query");
                logger.WriteLine("  - Lowering the --min-similarity threshold");
                logger.WriteLine("  - Removing tag filters");
            }
        }
        catch (Exception ex)
        {
            logger.WriteLine($"ERROR: {ex.Message}");
            if (ex.InnerException != null)
            {
                logger.WriteLine($"  Inner: {ex.InnerException.Message}");
            }
        }
    }

    private static ILlmProvider CreateLlmProvider(AppSettings settings, ILogger logger)
    {
        var apiKey = !string.IsNullOrEmpty(settings.LlmProvider.ApiKey)
            ? settings.LlmProvider.ApiKey
            : settings.OpenAI.ApiKey;

        var model = !string.IsNullOrEmpty(settings.LlmProvider.Model)
            ? settings.LlmProvider.Model
            : settings.OpenAI.Model;

        var provider = settings.LlmProvider.Provider.ToLower();

        return provider switch
        {
            "openai" => new OpenAIProvider(new HttpClient(), new OpenAISettings
            {
                ApiKey = apiKey,
                Model = model,
                MaxTokens = settings.LlmProvider.MaxTokens,
                TimeoutSeconds = settings.LlmProvider.TimeoutSeconds
            }),

            "langchain" => CreateLangChainProvider(settings.LlmProvider, apiKey, model),

            "local" => new LocalProvider(settings.LlmProvider.LocalEndpoint, model),

            _ => throw new InvalidOperationException($"Unknown provider: {provider}")
        };
    }

    private static ILlmProvider CreateLangChainProvider(LlmProviderSettings settings, string apiKey, string model)
    {
        var subProvider = settings.LangChainProvider.ToLower();
        return subProvider switch
        {
            "openai" => new LangChainProvider(apiKey, model),
            "anthropic" => new LangChainProvider(apiKey, model, useAnthropic: true),
            _ => throw new InvalidOperationException($"Unknown LangChain provider: {subProvider}")
        };
    }

    private static void ShowHelp(ILogger logger)
    {
        logger.WriteLine("USAGE:");
        logger.WriteLine("  SemanticSearch \"<query>\" [options]");
        logger.WriteLine();
        logger.WriteLine("DESCRIPTION:");
        logger.WriteLine("  Performs semantic search on your codebase using AI embeddings.");
        logger.WriteLine("  Find code members by meaning, not just keywords.");
        logger.WriteLine();
        logger.WriteLine("OPTIONS:");
        logger.WriteLine("  -k, --top <number>           Number of results to return (default: 10)");
        logger.WriteLine("  -s, --min-similarity <float> Minimum similarity score 0.0-1.0 (default: 0.0)");
        logger.WriteLine("  -t, --tags <tag1,tag2>       Filter by tags (comma-separated)");
        logger.WriteLine("  -m, --similar-to <id>        Find members similar to the given member ID");
        logger.WriteLine("  -h, --help                   Show this help message");
        logger.WriteLine();
        logger.WriteLine("EXAMPLES:");
        logger.WriteLine("  # Find async methods that handle files");
        logger.WriteLine("  SemanticSearch \"async file operations\" -k 5");
        logger.WriteLine();
        logger.WriteLine("  # Find public methods with high similarity threshold");
        logger.WriteLine("  SemanticSearch \"API endpoints\" -t public -s 0.7");
        logger.WriteLine();
        logger.WriteLine("  # Find code similar to member ID 42");
        logger.WriteLine("  SemanticSearch -m 42 -k 10");
        logger.WriteLine();
    }
}
