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

        // Determine API key: use LlmProvider.ApiKey if set, otherwise fall back to OpenAI.ApiKey for backward compatibility
        var apiKey = !string.IsNullOrEmpty(settings.LlmProvider?.ApiKey)
            ? settings.LlmProvider.ApiKey
            : settings.OpenAI?.ApiKey ?? string.Empty;

        // Determine model: use LlmProvider.Model if set, otherwise fall back to OpenAI.Model
        var model = !string.IsNullOrEmpty(settings.LlmProvider?.Model)
            ? settings.LlmProvider.Model
            : settings.OpenAI?.Model ?? "gpt-3.5-turbo";

        switch (providerName)
        {
            case "openai":
                if (string.IsNullOrEmpty(apiKey))
                {
                    Console.WriteLine("Error: OpenAI API key not configured");
                    Console.WriteLine("Set the OPENAI_API_KEY environment variable or LlmProvider:ApiKey in appsettings.json");
                    return;
                }

                var openAiSettings = new OpenAISettings
                {
                    ApiKey = apiKey,
                    Model = model,
                    MaxTokens = settings.LlmProvider?.MaxTokens ?? 50,
                    TimeoutSeconds = settings.LlmProvider?.TimeoutSeconds ?? 30
                };

                llmProvider = new OpenAIProvider(new HttpClient(), openAiSettings);
                break;

            case "local":
                var endpoint = settings.LlmProvider?.LocalEndpoint ?? "http://localhost:11434";
                llmProvider = new LocalProvider(endpoint, model);
                break;

            case "langchain":
                if (string.IsNullOrEmpty(apiKey))
                {
                    Console.WriteLine("Error: LangChain API key not configured");
                    Console.WriteLine("Set the API key in LlmProvider:ApiKey");
                    return;
                }

                var langChainProvider = settings.LlmProvider?.LangChainProvider?.ToLowerInvariant() ?? "openai";
                if (langChainProvider == "anthropic")
                {
                    llmProvider = new LangChainProvider(apiKey, model, useAnthropic: true);
                }
                else
                {
                    llmProvider = new LangChainProvider(apiKey, model);
                }
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
            Console.WriteLine("  dotnet run <path-to-codebase>");
            Console.WriteLine();
        }

        // Start API server
        await ApiServer.RunAsync(args, dbPath, llmProvider, settings);
    }
}
