using Microsoft.Extensions.Configuration;
using SourceCodeSummariser.Providers;

namespace SourceCodeSummariser;

/// <summary>
/// Entry point for running the HTTP API server
/// Usage: dotnet run --project SourceCodeSummariser.csproj -- api
/// </summary>
public class ApiProgram
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Source Code Summariser - API Server Mode");
        Console.WriteLine("========================================");
        Console.WriteLine();

        // Load configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var settings = new AppSettings();
        configuration.Bind(settings);

        // Validate required configuration
        if (string.IsNullOrEmpty(settings.TargetDirectory))
        {
            Console.WriteLine("Error: TargetDirectory not configured in appsettings.json");
            return;
        }

        // Initialize LLM provider
        ILlmProvider llmProvider;
        var providerName = settings.LlmProvider?.Provider?.ToLowerInvariant() ?? "openai";

        switch (providerName)
        {
            case "openai":
                if (string.IsNullOrEmpty(settings.LlmProvider?.OpenAI?.ApiKey))
                {
                    Console.WriteLine("Error: OpenAI API key not configured");
                    Console.WriteLine("Set the OPENAI_API_KEY environment variable");
                    return;
                }
                llmProvider = new OpenAIProvider(settings.LlmProvider.OpenAI.ApiKey, settings.LlmProvider);
                break;

            case "local":
                llmProvider = new LocalProvider(settings.LlmProvider);
                break;

            case "langchain":
                if (string.IsNullOrEmpty(settings.LlmProvider?.LangChain?.ApiKey))
                {
                    Console.WriteLine("Error: LangChain API key not configured");
                    return;
                }
                llmProvider = new LangChainProvider(
                    settings.LlmProvider.LangChain.ApiKey,
                    settings.LlmProvider.LangChain.ModelName,
                    settings.LlmProvider
                );
                break;

            default:
                Console.WriteLine($"Error: Unknown LLM provider '{providerName}'");
                return;
        }

        Console.WriteLine($"LLM Provider: {llmProvider.ProviderName}");

        // Database path
        var dbPath = Path.Combine(settings.TargetDirectory, "summaries.db");
        Console.WriteLine($"Database: {dbPath}");
        Console.WriteLine();

        if (!File.Exists(dbPath))
        {
            Console.WriteLine("Warning: Database not found. Run the summarizer first to populate the database.");
            Console.WriteLine("  dotnet run");
            Console.WriteLine();
        }

        // Start API server
        await ApiServer.RunAsync(args, dbPath, llmProvider, settings);
    }
}
