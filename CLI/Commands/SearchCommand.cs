using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SourceCodeSummariser.CLI.Commands;

/// <summary>
/// Command to perform semantic code search
/// </summary>
public class SearchCommand : AsyncCommand<SearchCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [Description("Search query")]
        [CommandArgument(0, "<query>")]
        public string Query { get; set; } = string.Empty;

        [Description("Number of results to return")]
        [CommandOption("-k|--top")]
        [DefaultValue(10)]
        public int TopK { get; set; }

        [Description("Minimum similarity threshold (0.0-1.0)")]
        [CommandOption("-s|--min-similarity")]
        [DefaultValue(0.0)]
        public double MinSimilarity { get; set; }

        [Description("Filter by tags (comma-separated)")]
        [CommandOption("-t|--tags")]
        public string? Tags { get; set; }

        [Description("Find similar to member ID")]
        [CommandOption("-m|--similar-to")]
        public int? SimilarToMemberId { get; set; }

        [Description("Output format: table, json, markdown")]
        [CommandOption("-f|--format")]
        [DefaultValue("table")]
        public string Format { get; set; } = "table";

        [Description("Service URL")]
        [CommandOption("--service-url")]
        [DefaultValue("http://localhost:5000")]
        public string ServiceUrl { get; set; } = "http://localhost:5000";

        [Description("Show full summaries")]
        [CommandOption("--full")]
        public bool ShowFull { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        using var client = new ApiClient(settings.ServiceUrl);

        // Check service availability
        if (!await EnsureServiceRunningAsync(client))
        {
            return 1;
        }

        try
        {
            SearchResponse? response;

            // Perform search based on mode
            if (settings.SimilarToMemberId.HasValue)
            {
                response = await AnsiConsole.Status()
                    .StartAsync($"Finding similar code to member {settings.SimilarToMemberId}...", async ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        ctx.SpinnerStyle(Style.Parse("blue"));
                        return await client.FindSimilarAsync(settings.SimilarToMemberId.Value, settings.TopK);
                    });

                AnsiConsole.MarkupLine($"[green]✓[/] Found {response?.Results.Count ?? 0} similar members");
            }
            else if (!string.IsNullOrEmpty(settings.Tags))
            {
                var tags = settings.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                response = await AnsiConsole.Status()
                    .StartAsync($"Searching with tags: {string.Join(", ", tags)}...", async ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        ctx.SpinnerStyle(Style.Parse("blue"));
                        return await client.SearchWithTagsAsync(settings.Query, tags, settings.TopK);
                    });

                AnsiConsole.MarkupLine($"[green]✓[/] Found {response?.Results.Count ?? 0} results");
            }
            else
            {
                response = await AnsiConsole.Status()
                    .StartAsync($"Searching for: [blue]{settings.Query}[/]...", async ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        ctx.SpinnerStyle(Style.Parse("blue"));
                        return await client.SearchAsync(settings.Query, settings.TopK, settings.MinSimilarity);
                    });

                AnsiConsole.MarkupLine($"[green]✓[/] Found {response?.Results.Count ?? 0} results");
            }

            AnsiConsole.WriteLine();

            if (response?.Results == null || response.Results.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No results found[/]");
                return 0;
            }

            // Display results in requested format
            switch (settings.Format.ToLowerInvariant())
            {
                case "json":
                    DisplayJson(response.Results);
                    break;
                case "markdown":
                    DisplayMarkdown(response.Results, settings.ShowFull);
                    break;
                default:
                    DisplayTable(response.Results, settings.ShowFull);
                    break;
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error performing search: {ex.Message}[/]");
            return 1;
        }
    }

    private async Task<bool> EnsureServiceRunningAsync(ApiClient client)
    {
        var isRunning = await client.IsServiceRunningAsync();
        if (!isRunning)
        {
            AnsiConsole.MarkupLine("[red]Error: Analyzer service is not running[/]");
            AnsiConsole.MarkupLine("[yellow]Start the service with: analyze serve[/]");
            return false;
        }
        return true;
    }

    private void DisplayTable(List<SearchResultItem> results, bool showFull)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]ID[/]")
            .AddColumn("[bold]Name[/]")
            .AddColumn("[bold]Type[/]")
            .AddColumn("[bold]File[/]")
            .AddColumn("[bold]Similarity[/]");

        if (showFull)
        {
            table.AddColumn("[bold]Summary[/]");
        }

        foreach (var result in results)
        {
            var similarity = result.Similarity.ToString("P1");
            var color = result.Similarity switch
            {
                >= 0.9 => "green",
                >= 0.7 => "yellow",
                _ => "white"
            };

            var row = new List<string>
            {
                result.Id.ToString(),
                $"[blue]{result.Name}[/]",
                result.Type,
                Path.GetFileName(result.FileName),
                $"[{color}]{similarity}[/]"
            };

            if (showFull)
            {
                row.Add(result.Summary);
            }

            table.AddRow(row.ToArray());
        }

        AnsiConsole.Write(table);

        // Show tags if available
        if (!showFull && results.Any(r => r.Tags.Any()))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Use --full to see summaries and tags[/]");
        }
    }

    private void DisplayMarkdown(List<SearchResultItem> results, bool showFull)
    {
        foreach (var result in results)
        {
            AnsiConsole.MarkupLine($"## {result.Name} ({result.Type})");
            AnsiConsole.MarkupLine($"**ID:** {result.Id}");
            AnsiConsole.MarkupLine($"**File:** {result.FileName}");
            AnsiConsole.MarkupLine($"**Similarity:** {result.Similarity:P1}");

            if (result.Tags.Any())
            {
                AnsiConsole.MarkupLine($"**Tags:** {string.Join(", ", result.Tags)}");
            }

            if (showFull)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine(result.Summary);
            }

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule().RuleStyle("dim"));
        }
    }

    private void DisplayJson(List<SearchResultItem> results)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(results, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        AnsiConsole.WriteLine(json);
    }
}
