using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SourceCodeSummariser.CLI.Commands;

/// <summary>
/// Command to initialize the tool and database
/// </summary>
public class InitCommand : AsyncCommand<InitCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [Description("Target directory for analysis")]
        [CommandOption("-d|--directory")]
        public string? Directory { get; set; }

        [Description("Force overwrite of existing configuration")]
        [CommandOption("-f|--force")]
        public bool Force { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        AnsiConsole.Write(new FigletText("Initialize")
            .Centered()
            .Color(Color.Blue));

        AnsiConsole.MarkupLine("[bold blue]Source Code Analyzer Initialization[/]");
        AnsiConsole.WriteLine();

        var hasErrors = false;

        // Step 1: Configuration Setup
        hasErrors |= !await SetupConfigurationAsync(settings.Force);

        AnsiConsole.WriteLine();

        // Step 2: Database Setup
        var targetDir = settings.Directory ?? Directory.GetCurrentDirectory();
        hasErrors |= !await SetupDatabaseAsync(targetDir);

        AnsiConsole.WriteLine();

        // Step 3: Final Instructions
        ShowFinalInstructions(targetDir);

        return hasErrors ? 1 : 0;
    }

    private async Task<bool> SetupConfigurationAsync(bool force)
    {
        var rule = new Rule("[blue]Step 1: Configuration Setup[/]")
        {
            Justification = Justify.Left
        };
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();

        var settingsPath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "appsettings.json");
        var examplePath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "appsettings.example.json");

        if (File.Exists(settingsPath) && !force)
        {
            AnsiConsole.MarkupLine("[green]✓[/] appsettings.json already exists");
            return true;
        }

        if (!File.Exists(examplePath))
        {
            // Create a default configuration
            return await CreateDefaultConfigurationAsync(settingsPath, force);
        }

        try
        {
            File.Copy(examplePath, settingsPath, force);
            AnsiConsole.MarkupLine("[green]✓[/] Created appsettings.json from example");
            AnsiConsole.MarkupLine("[yellow]⚠[/] Please edit appsettings.json and set your API key");
            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Failed to create configuration: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> CreateDefaultConfigurationAsync(string settingsPath, bool force)
    {
        if (File.Exists(settingsPath) && !force)
        {
            AnsiConsole.MarkupLine("[green]✓[/] appsettings.json already exists");
            return true;
        }

        var defaultConfig = @"{
  ""OpenAI"": {
    ""ApiKey"": ""your-api-key-here"",
    ""Model"": ""gpt-3.5-turbo"",
    ""MaxTokens"": 50,
    ""TimeoutSeconds"": 30
  },
  ""LlmProvider"": {
    ""Provider"": ""openai"",
    ""ApiKey"": ""your-api-key-here"",
    ""Model"": ""gpt-3.5-turbo"",
    ""MaxTokens"": 50,
    ""TimeoutSeconds"": 30,
    ""LocalEndpoint"": ""http://localhost:11434"",
    ""LangChainProvider"": ""openai"",
    ""EnableEmbeddings"": true,
    ""EmbeddingModel"": ""text-embedding-ada-002""
  },
  ""Database"": {
    ""ConnectionString"": ""Data Source=summaries.db""
  },
  ""Processing"": {
    ""FilePattern"": ""*.cs"",
    ""ExcludedFolders"": [""bin"", ""obj"", ""node_modules"", "".git""]
  },
  ""TargetDirectory"": """"
}";

        try
        {
            await File.WriteAllTextAsync(settingsPath, defaultConfig);
            AnsiConsole.MarkupLine("[green]✓[/] Created default appsettings.json");
            AnsiConsole.MarkupLine("[yellow]⚠[/] Please edit appsettings.json and set your API key");
            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Failed to create configuration: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> SetupDatabaseAsync(string targetDir)
    {
        var rule = new Rule("[blue]Step 2: Database Setup[/]")
        {
            Justification = Justify.Left
        };
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();

        var dbPath = Path.Combine(targetDir, "summaries.db");

        try
        {
            if (File.Exists(dbPath))
            {
                AnsiConsole.MarkupLine($"[green]✓[/] Database already exists at {dbPath}");
                return true;
            }

            await AnsiConsole.Status()
                .StartAsync("Creating database...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    ctx.SpinnerStyle(Style.Parse("blue"));

                    using var context = new SummaryContext(dbPath);
                    await context.Database.MigrateAsync();
                });

            AnsiConsole.MarkupLine($"[green]✓[/] Database created at {dbPath}");
            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Failed to create database: {ex.Message}");
            return false;
        }
    }

    private void ShowFinalInstructions(string targetDir)
    {
        var rule = new Rule("[blue]Step 3: Next Steps[/]")
        {
            Justification = Justify.Left
        };
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();

        var panel = new Panel(
            new Markup(
                "[bold]Setup Complete![/]\n\n" +
                "To get started:\n\n" +
                "1. Edit [cyan]appsettings.json[/] and set your API key\n" +
                "2. Process your code: [cyan]analyze process <path>[/]\n" +
                "3. Start the service: [cyan]analyze serve[/]\n" +
                "4. Search your code: [cyan]analyze search \"your query\"[/]\n\n" +
                "For more help: [cyan]analyze --help[/]"))
        {
            Header = new PanelHeader("[bold green]✓ Initialization Complete[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 1)
        };

        AnsiConsole.Write(panel);
    }
}
