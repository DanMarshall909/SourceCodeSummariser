using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SourceCodeSummariser.CLI.Commands;

/// <summary>
/// Command to work with file summaries
/// </summary>
public class FilesCommand : AsyncCommand<FilesCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [Description("File path to get summary for")]
        [CommandArgument(0, "<file-path>")]
        public string FilePath { get; set; } = string.Empty;

        [Description("Service URL")]
        [CommandOption("--service-url")]
        [DefaultValue("http://localhost:5000")]
        public string ServiceUrl { get; set; } = "http://localhost:5000";

        [Description("Output format: table, json, markdown")]
        [CommandOption("-f|--format")]
        [DefaultValue("table")]
        public string Format { get; set; } = "table";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
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
            var summary = await AnsiConsole.Status()
                .StartAsync($"Retrieving summary for {Path.GetFileName(settings.FilePath)}...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    ctx.SpinnerStyle(Style.Parse("blue"));
                    return await client.GetFileSummaryAsync(settings.FilePath);
                });

            if (summary == null)
            {
                AnsiConsole.MarkupLine($"[yellow]File not found in database: {settings.FilePath}[/]");
                AnsiConsole.MarkupLine("[dim]Process the file first with: analyze process <path>[/]");
                return 1;
            }

            AnsiConsole.MarkupLine("[green]✓[/] File summary retrieved");
            AnsiConsole.WriteLine();

            // Display in requested format
            switch (settings.Format.ToLowerInvariant())
            {
                case "json":
                    DisplayJson(summary);
                    break;
                case "markdown":
                    DisplayMarkdown(summary);
                    break;
                default:
                    DisplayTable(summary);
                    break;
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error retrieving file summary: {ex.Message}[/]");
            return 1;
        }
    }

    private void DisplayTable(FileSummary summary)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Property[/]")
            .AddColumn("[bold]Value[/]");

        table.AddRow("File", $"[blue]{summary.FileName}[/]");
        table.AddRow("Member Count", summary.MemberCount.ToString());

        if (summary.MemberTypes.Any())
        {
            table.AddRow("Member Types", string.Join(", ", summary.MemberTypes.Select(t => $"[cyan]{t}[/]")));
        }

        AnsiConsole.Write(table);

        // Display summary in a panel
        if (!string.IsNullOrEmpty(summary.Summary))
        {
            AnsiConsole.WriteLine();
            var panel = new Panel(summary.Summary)
            {
                Header = new PanelHeader("[bold]File Summary[/]"),
                Border = BoxBorder.Rounded,
                Padding = new Padding(2, 1)
            };
            AnsiConsole.Write(panel);
        }
    }

    private void DisplayMarkdown(FileSummary summary)
    {
        AnsiConsole.MarkupLine($"# {summary.FileName}");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"**Member Count:** {summary.MemberCount}");

        if (summary.MemberTypes.Any())
        {
            AnsiConsole.MarkupLine($"**Member Types:** {string.Join(", ", summary.MemberTypes)}");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("## Summary");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(summary.Summary);
    }

    private void DisplayJson(FileSummary summary)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(summary, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        AnsiConsole.WriteLine(json);
    }
}
