using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using SourceCodeSummariser.Providers;

namespace SourceCodeSummariser.CLI.Commands;

/// <summary>
/// Command to start the analyzer service
/// </summary>
public class ServeCommand : AsyncCommand<ServeCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [Description("Port to run the service on")]
        [CommandOption("-p|--port")]
        [DefaultValue(5000)]
        public int Port { get; set; }

        [Description("Configuration file path")]
        [CommandOption("-c|--config")]
        public string? ConfigPath { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        AnsiConsole.Write(new FigletText("Code Analyzer")
            .Centered()
            .Color(Color.Blue));

        AnsiConsole.MarkupLine("[bold blue]Starting Analyzer Service...[/]");
        AnsiConsole.WriteLine();

        try
        {
            // Load configuration
            var configPath = settings.ConfigPath ?? "appsettings.json";

            if (!File.Exists(configPath))
            {
                AnsiConsole.MarkupLine($"[red]Error: Configuration file not found: {configPath}[/]");
                AnsiConsole.MarkupLine("[yellow]Run 'analyze init' to create a default configuration.[/]");
                return 1;
            }

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(configPath, optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var appSettings = new AppSettings();
            configuration.Bind(appSettings);

            // Validate required configuration
            if (string.IsNullOrEmpty(appSettings.TargetDirectory))
            {
                AnsiConsole.MarkupLine("[red]Error: TargetDirectory not configured in appsettings.json[/]");
                return 1;
            }

            // Initialize LLM provider
            var llmProvider = await InitializeLlmProviderAsync(appSettings);
            if (llmProvider == null)
            {
                return 1;
            }

            // Display configuration
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[bold]Setting[/]")
                .AddColumn("[bold]Value[/]");

            table.AddRow("LLM Provider", llmProvider.ProviderName);
            table.AddRow("Target Directory", appSettings.TargetDirectory);

            var dbPath = Path.Combine(appSettings.TargetDirectory, "summaries.db");
            table.AddRow("Database", dbPath);
            table.AddRow("Port", settings.Port.ToString());

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            if (!File.Exists(dbPath))
            {
                AnsiConsole.MarkupLine("[yellow]Warning: Database not found.[/]");
                AnsiConsole.MarkupLine("[yellow]Run 'analyze process <path>' to populate the database.[/]");
                AnsiConsole.WriteLine();
            }

            // Start the API server
            AnsiConsole.Status()
                .Start($"[green]Service running on http://localhost:{settings.Port}[/]", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    ctx.SpinnerStyle(Style.Parse("green"));

                    AnsiConsole.MarkupLine("[green]Press Ctrl+C to stop the service[/]");
                    AnsiConsole.WriteLine();

                    // Start API server (this blocks)
                    var args = new[] { "--urls", $"http://localhost:{settings.Port}" };
                    ApiServer.RunAsync(args, dbPath, llmProvider, appSettings).Wait();
                });

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error starting service: {ex.Message}[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private async Task<ILlmProvider?> InitializeLlmProviderAsync(AppSettings settings)
    {
        var providerName = settings.LlmProvider?.Provider?.ToLowerInvariant() ?? "openai";

        // Determine API key
        var apiKey = !string.IsNullOrEmpty(settings.LlmProvider?.ApiKey)
            ? settings.LlmProvider.ApiKey
            : settings.OpenAI?.ApiKey ?? string.Empty;

        // Determine model
        var model = !string.IsNullOrEmpty(settings.LlmProvider?.Model)
            ? settings.LlmProvider.Model
            : settings.OpenAI?.Model ?? "gpt-3.5-turbo";

        return await AnsiConsole.Status()
            .StartAsync($"Initializing {providerName} provider...", async ctx =>
            {
                ILlmProvider? provider = providerName switch
                {
                    "openai" => InitializeOpenAI(apiKey, model, settings),
                    "local" => InitializeLocal(model, settings),
                    "langchain" => InitializeLangChain(apiKey, model, settings),
                    _ => null
                };

                if (provider != null)
                {
                    AnsiConsole.MarkupLine($"[green]✓[/] Initialized {provider.ProviderName}");
                }

                return await Task.FromResult(provider);
            });
    }

    private ILlmProvider? InitializeOpenAI(string apiKey, string model, AppSettings settings)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            AnsiConsole.MarkupLine("[red]Error: OpenAI API key not configured[/]");
            AnsiConsole.MarkupLine("[yellow]Set the OPENAI_API_KEY environment variable or LlmProvider:ApiKey in appsettings.json[/]");
            return null;
        }

        var openAiSettings = new OpenAISettings
        {
            ApiKey = apiKey,
            Model = model,
            MaxTokens = settings.LlmProvider?.MaxTokens ?? 50,
            TimeoutSeconds = settings.LlmProvider?.TimeoutSeconds ?? 30
        };

        return new OpenAIProvider(new HttpClient(), openAiSettings);
    }

    private ILlmProvider InitializeLocal(string model, AppSettings settings)
    {
        var endpoint = settings.LlmProvider?.LocalEndpoint ?? "http://localhost:11434";
        return new LocalProvider(endpoint, model);
    }

    private ILlmProvider? InitializeLangChain(string apiKey, string model, AppSettings settings)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            AnsiConsole.MarkupLine("[red]Error: LangChain API key not configured[/]");
            AnsiConsole.MarkupLine("[yellow]Set the API key in LlmProvider:ApiKey[/]");
            return null;
        }

        var langChainProvider = settings.LlmProvider?.LangChainProvider?.ToLowerInvariant() ?? "openai";
        return langChainProvider == "anthropic"
            ? new LangChainProvider(apiKey, model, useAnthropic: true)
            : new LangChainProvider(apiKey, model);
    }
}
