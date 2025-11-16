using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SourceCodeSummariser.CLI.Commands;

/// <summary>
/// Command to list and manage tags
/// </summary>
public class TagsCommand : AsyncCommand<TagsCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [Description("Service URL")]
        [CommandOption("--service-url")]
        [DefaultValue("http://localhost:5000")]
        public string ServiceUrl { get; set; } = "http://localhost:5000";

        [Description("Filter by category")]
        [CommandOption("-c|--category")]
        public string? Category { get; set; }

        [Description("Minimum usage count")]
        [CommandOption("-m|--min-count")]
        [DefaultValue(1)]
        public int MinCount { get; set; }

        [Description("Output format: table, json, list")]
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
            var tags = await AnsiConsole.Status()
                .StartAsync("Retrieving tags...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    ctx.SpinnerStyle(Style.Parse("blue"));
                    return await client.GetTagsAsync();
                });

            if (tags == null || tags.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No tags found[/]");
                return 0;
            }

            // Apply filters
            var filteredTags = tags.Where(t => t.Count >= settings.MinCount);

            if (!string.IsNullOrEmpty(settings.Category))
            {
                filteredTags = filteredTags.Where(t => t.Category.Equals(settings.Category, StringComparison.OrdinalIgnoreCase));
            }

            var tagList = filteredTags.ToList();

            AnsiConsole.MarkupLine($"[green]✓[/] Found {tagList.Count} tags");
            AnsiConsole.WriteLine();

            // Display in requested format
            switch (settings.Format.ToLowerInvariant())
            {
                case "json":
                    DisplayJson(tagList);
                    break;
                case "list":
                    DisplayList(tagList);
                    break;
                default:
                    DisplayTable(tagList);
                    break;
            }

            // Show summary
            DisplaySummary(tags);

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error retrieving tags: {ex.Message}[/]");
            return 1;
        }
    }

    private void DisplayTable(List<TagInfo> tags)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Tag[/]")
            .AddColumn("[bold]Category[/]")
            .AddColumn("[bold]Count[/]");

        foreach (var tag in tags.OrderByDescending(t => t.Count))
        {
            var color = tag.Count switch
            {
                >= 100 => "green",
                >= 50 => "yellow",
                >= 10 => "blue",
                _ => "white"
            };

            table.AddRow(
                $"[cyan]{tag.Name}[/]",
                tag.Category,
                $"[{color}]{tag.Count}[/]"
            );
        }

        AnsiConsole.Write(table);
    }

    private void DisplayList(List<TagInfo> tags)
    {
        var grouped = tags.GroupBy(t => t.Category);

        foreach (var group in grouped.OrderBy(g => g.Key))
        {
            AnsiConsole.MarkupLine($"[bold blue]{group.Key}:[/]");

            var tree = new Tree($"[dim]{group.Count()} tags[/]");

            foreach (var tag in group.OrderByDescending(t => t.Count))
            {
                tree.AddNode($"[cyan]{tag.Name}[/] ({tag.Count})");
            }

            AnsiConsole.Write(tree);
            AnsiConsole.WriteLine();
        }
    }

    private void DisplayJson(List<TagInfo> tags)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(tags, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        AnsiConsole.WriteLine(json);
    }

    private void DisplaySummary(List<TagInfo> allTags)
    {
        AnsiConsole.WriteLine();

        var summary = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Metric[/]")
            .AddColumn("[bold]Value[/]");

        summary.AddRow("Total Tags", allTags.Count.ToString());
        summary.AddRow("Total Usages", allTags.Sum(t => t.Count).ToString());
        summary.AddRow("Categories", allTags.Select(t => t.Category).Distinct().Count().ToString());

        var mostUsed = allTags.OrderByDescending(t => t.Count).FirstOrDefault();
        if (mostUsed != null)
        {
            summary.AddRow("Most Used", $"{mostUsed.Name} ({mostUsed.Count})");
        }

        AnsiConsole.Write(summary);
    }
}
