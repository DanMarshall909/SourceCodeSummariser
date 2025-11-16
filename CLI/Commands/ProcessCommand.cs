using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using SourceCodeSummariser.Providers;

namespace SourceCodeSummariser.CLI.Commands;

/// <summary>
/// Command to process/analyze code files
/// </summary>
public class ProcessCommand : AsyncCommand<ProcessCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [Description("Path to analyze (file or folder)")]
        [CommandArgument(0, "<path>")]
        public string Path { get; set; } = string.Empty;

        [Description("Watch mode - continuously monitor for changes")]
        [CommandOption("-w|--watch")]
        public bool WatchMode { get; set; }

        [Description("Use remote service instead of local processing")]
        [CommandOption("--remote")]
        public bool UseRemote { get; set; }

        [Description("Service URL (when using --remote)")]
        [CommandOption("--service-url")]
        [DefaultValue("http://localhost:5000")]
        public string ServiceUrl { get; set; } = "http://localhost:5000";

        [Description("Configuration file path")]
        [CommandOption("-c|--config")]
        public string? ConfigPath { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        AnsiConsole.MarkupLine("[bold blue]Source Code Analyzer[/]");
        AnsiConsole.WriteLine();

        // Validate path
        if (!File.Exists(settings.Path) && !Directory.Exists(settings.Path))
        {
            AnsiConsole.MarkupLine($"[red]Error: Path does not exist: {settings.Path}[/]");
            return 1;
        }

        if (settings.UseRemote)
        {
            return await ProcessRemoteAsync(settings);
        }
        else
        {
            return await ProcessLocalAsync(settings);
        }
    }

    private async Task<int> ProcessRemoteAsync(Settings settings)
    {
        using var client = new ApiClient(settings.ServiceUrl);

        // Check service availability
        var isRunning = await client.IsServiceRunningAsync();
        if (!isRunning)
        {
            AnsiConsole.MarkupLine("[red]Error: Analyzer service is not running[/]");
            AnsiConsole.MarkupLine("[yellow]Start the service with: analyze serve[/]");
            return 1;
        }

        try
        {
            if (File.Exists(settings.Path))
            {
                // Process single file
                var response = await AnsiConsole.Status()
                    .StartAsync($"Processing {Path.GetFileName(settings.Path)}...", async ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        ctx.SpinnerStyle(Style.Parse("blue"));
                        return await client.ProcessFileAsync(settings.Path);
                    });

                if (response?.Success == true)
                {
                    AnsiConsole.MarkupLine($"[green]✓[/] {response.Message}");
                    AnsiConsole.MarkupLine($"   Processed members: {response.ProcessedMembers}");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]⚠[/] {response?.Message ?? "Unknown error"}");
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Note: Directory processing via remote service is not yet supported.[/]");
                AnsiConsole.MarkupLine("[yellow]Use local processing (remove --remote flag) or process files individually.[/]");
                return 1;
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }

    private async Task<int> ProcessLocalAsync(Settings settings)
    {
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
                .AddJsonFile(configPath, optional: true)
                .AddEnvironmentVariables()
                .Build();

            var appSettings = new AppSettings();
            configuration.Bind(appSettings);

            // Initialize LLM provider
            var llmProvider = await InitializeLlmProviderAsync(appSettings);
            if (llmProvider == null)
            {
                return 1;
            }

            AnsiConsole.MarkupLine($"[green]✓[/] Initialized {llmProvider.ProviderName}");
            AnsiConsole.WriteLine();

            // Process based on path type
            var folderPath = Directory.Exists(settings.Path) ? settings.Path : Path.GetDirectoryName(settings.Path) ?? Directory.GetCurrentDirectory();
            var dbPath = Path.Combine(folderPath, "summaries.db");

            var dbContext = new SummaryContext(dbPath);
            var summarizerService = new SummarizerService(llmProvider, appSettings.LlmProvider.MaxTokens);
            var fileProcessorService = new FileProcessorService(dbContext, summarizerService, llmProvider, appSettings.LlmProvider);

            if (settings.WatchMode)
            {
                AnsiConsole.MarkupLine($"[blue]Watching for changes in: {folderPath}[/]");
                AnsiConsole.MarkupLine("[dim]Press Ctrl+C to stop[/]");
                AnsiConsole.WriteLine();

                return await WatchDirectoryAsync(folderPath, fileProcessorService, appSettings);
            }
            else
            {
                if (File.Exists(settings.Path))
                {
                    // Process single file
                    return await ProcessFileAsync(settings.Path, fileProcessorService);
                }
                else
                {
                    // Process directory
                    return await ProcessDirectoryAsync(settings.Path, fileProcessorService, appSettings);
                }
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private async Task<int> ProcessFileAsync(string filePath, FileProcessorService fileProcessor)
    {
        try
        {
            await AnsiConsole.Progress()
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask($"[blue]Processing {Path.GetFileName(filePath)}[/]");
                    task.IsIndeterminate = true;

                    await fileProcessor.ProcessFileAsync(filePath);

                    task.Value = 100;
                });

            AnsiConsole.MarkupLine($"[green]✓[/] Successfully processed {filePath}");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Error processing file: {ex.Message}");
            return 1;
        }
    }

    private async Task<int> ProcessDirectoryAsync(string folderPath, FileProcessorService fileProcessor, AppSettings settings)
    {
        var pattern = settings.Processing?.FilePattern ?? "*.cs";
        var excludedFolders = settings.Processing?.ExcludedFolders ?? new[] { "bin", "obj", "node_modules" };

        var files = Directory.EnumerateFiles(folderPath, pattern, SearchOption.AllDirectories)
            .Where(f => !excludedFolders.Any(excluded => f.Contains(Path.DirectorySeparatorChar + excluded + Path.DirectorySeparatorChar)))
            .ToList();

        AnsiConsole.MarkupLine($"[blue]Found {files.Count} files to process[/]");
        AnsiConsole.WriteLine();

        var processed = 0;
        var errors = 0;

        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[blue]Processing files[/]", maxValue: files.Count);

                foreach (var file in files)
                {
                    try
                    {
                        task.Description = $"[blue]Processing {Path.GetFileName(file)}[/]";
                        await fileProcessor.ProcessFileAsync(file);
                        processed++;
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]✗[/] {Path.GetFileName(file)}: {ex.Message}");
                        errors++;
                    }
                    finally
                    {
                        task.Increment(1);
                    }
                }
            });

        AnsiConsole.WriteLine();

        // Display summary
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Metric[/]")
            .AddColumn("[bold]Count[/]");

        table.AddRow("Total Files", files.Count.ToString());
        table.AddRow("[green]Processed[/]", processed.ToString());
        table.AddRow("[red]Errors[/]", errors.ToString());

        AnsiConsole.Write(table);

        return errors > 0 ? 1 : 0;
    }

    private async Task<int> WatchDirectoryAsync(string folderPath, FileProcessorService fileProcessor, AppSettings settings)
    {
        var pattern = settings.Processing?.FilePattern ?? "*.cs";

        using var watcher = new FileSystemWatcher(folderPath)
        {
            Filter = pattern,
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
        };

        var debounceTimer = new Dictionary<string, System.Timers.Timer>();

        watcher.Changed += async (sender, e) => await OnFileChanged(e.FullPath, fileProcessor, debounceTimer);
        watcher.Created += async (sender, e) => await OnFileChanged(e.FullPath, fileProcessor, debounceTimer);

        watcher.EnableRaisingEvents = true;

        // Keep running until Ctrl+C
        var tcs = new TaskCompletionSource<int>();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Stopping watch mode...[/]");
            tcs.SetResult(0);
        };

        return await tcs.Task;
    }

    private async Task OnFileChanged(string filePath, FileProcessorService fileProcessor, Dictionary<string, System.Timers.Timer> debounceTimer)
    {
        // Debounce rapid file changes
        if (debounceTimer.ContainsKey(filePath))
        {
            debounceTimer[filePath].Stop();
            debounceTimer[filePath].Start();
        }
        else
        {
            var timer = new System.Timers.Timer(500);
            timer.Elapsed += async (sender, e) =>
            {
                timer.Stop();
                debounceTimer.Remove(filePath);

                try
                {
                    AnsiConsole.MarkupLine($"[blue]↻[/] Processing {Path.GetFileName(filePath)}...");
                    await fileProcessor.ProcessFileAsync(filePath);
                    AnsiConsole.MarkupLine($"[green]✓[/] Updated {Path.GetFileName(filePath)}");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] Error: {ex.Message}");
                }
            };

            debounceTimer[filePath] = timer;
            timer.Start();
        }
    }

    private async Task<ILlmProvider?> InitializeLlmProviderAsync(AppSettings settings)
    {
        var providerName = settings.LlmProvider?.Provider?.ToLowerInvariant() ?? "openai";

        var apiKey = !string.IsNullOrEmpty(settings.LlmProvider?.ApiKey)
            ? settings.LlmProvider.ApiKey
            : settings.OpenAI?.ApiKey ?? string.Empty;

        var model = !string.IsNullOrEmpty(settings.LlmProvider?.Model)
            ? settings.LlmProvider.Model
            : settings.OpenAI?.Model ?? "gpt-3.5-turbo";

        return await AnsiConsole.Status()
            .StartAsync($"Initializing {providerName} provider...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                ctx.SpinnerStyle(Style.Parse("blue"));

                ILlmProvider? provider = providerName switch
                {
                    "openai" => InitializeOpenAI(apiKey, model, settings),
                    "local" => new LocalProvider(settings.LlmProvider?.LocalEndpoint ?? "http://localhost:11434", model),
                    "langchain" => InitializeLangChain(apiKey, model, settings),
                    _ => null
                };

                return await Task.FromResult(provider);
            });
    }

    private ILlmProvider? InitializeOpenAI(string apiKey, string model, AppSettings settings)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            AnsiConsole.MarkupLine("[red]Error: OpenAI API key not configured[/]");
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

    private ILlmProvider? InitializeLangChain(string apiKey, string model, AppSettings settings)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            AnsiConsole.MarkupLine("[red]Error: API key not configured[/]");
            return null;
        }

        var langChainProvider = settings.LlmProvider?.LangChainProvider?.ToLowerInvariant() ?? "openai";
        return langChainProvider == "anthropic"
            ? new LangChainProvider(apiKey, model, useAnthropic: true)
            : new LangChainProvider(apiKey, model);
    }
}
