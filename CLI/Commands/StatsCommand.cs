using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SourceCodeSummariser.CLI.Commands;

/// <summary>
/// Command to show database statistics
/// </summary>
public class StatsCommand : AsyncCommand<StatsCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [Description("Database path (default: ./summaries.db)")]
        [CommandOption("-d|--database")]
        public string? DatabasePath { get; set; }

        [Description("Output format: table, json")]
        [CommandOption("-f|--format")]
        [DefaultValue("table")]
        public string Format { get; set; } = "table";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var dbPath = settings.DatabasePath ?? Path.Combine(Directory.GetCurrentDirectory(), "summaries.db");

        if (!File.Exists(dbPath))
        {
            AnsiConsole.MarkupLine($"[red]Error: Database not found at {dbPath}[/]");
            AnsiConsole.MarkupLine("[yellow]Process some code first with: analyze process <path>[/]");
            return 1;
        }

        try
        {
            var stats = await AnsiConsole.Status()
                .StartAsync("Gathering statistics...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    ctx.SpinnerStyle(Style.Parse("blue"));
                    return await GetStatisticsAsync(dbPath);
                });

            AnsiConsole.MarkupLine("[green]✓[/] Statistics gathered");
            AnsiConsole.WriteLine();

            // Display in requested format
            switch (settings.Format.ToLowerInvariant())
            {
                case "json":
                    DisplayJson(stats);
                    break;
                default:
                    DisplayTable(stats);
                    break;
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error gathering statistics: {ex.Message}[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private async Task<DatabaseStats> GetStatisticsAsync(string dbPath)
    {
        using var context = new SummaryContext(dbPath);

        var stats = new DatabaseStats
        {
            TotalFiles = await context.Files.CountAsync(),
            TotalMembers = await context.Members.CountAsync(),
            TotalTags = await context.Tags.CountAsync(),
            MembersWithEmbeddings = await context.Members.CountAsync(m => m.Embedding != null),
            DatabaseSizeMB = new FileInfo(dbPath).Length / (1024.0 * 1024.0)
        };

        // Get member type breakdown
        stats.MembersByType = await context.Members
            .GroupBy(m => m.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToDictionaryAsync(x => x.Type, x => x.Count);

        // Get top files by member count
        stats.TopFilesByMembers = await context.Files
            .Include(f => f.Members)
            .OrderByDescending(f => f.Members.Count)
            .Take(5)
            .Select(f => new { f.FileName, Count = f.Members.Count })
            .ToDictionaryAsync(x => x.FileName, x => x.Count);

        return stats;
    }

    private void DisplayTable(DatabaseStats stats)
    {
        // Main statistics
        var mainTable = new Table()
            .Title("[bold blue]Database Statistics[/]")
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Metric[/]")
            .AddColumn("[bold]Value[/]");

        mainTable.AddRow("Total Files", stats.TotalFiles.ToString());
        mainTable.AddRow("Total Members", stats.TotalMembers.ToString());
        mainTable.AddRow("Total Tags", stats.TotalTags.ToString());
        mainTable.AddRow("Members with Embeddings", $"{stats.MembersWithEmbeddings} ({stats.EmbeddingPercentage:P1})");
        mainTable.AddRow("Database Size", $"{stats.DatabaseSizeMB:F2} MB");

        AnsiConsole.Write(mainTable);
        AnsiConsole.WriteLine();

        // Member type breakdown
        if (stats.MembersByType.Any())
        {
            var typeTable = new Table()
                .Title("[bold blue]Members by Type[/]")
                .Border(TableBorder.Rounded)
                .AddColumn("[bold]Type[/]")
                .AddColumn("[bold]Count[/]")
                .AddColumn("[bold]Percentage[/]");

            foreach (var kvp in stats.MembersByType)
            {
                var percentage = (double)kvp.Value / stats.TotalMembers;
                typeTable.AddRow(
                    $"[cyan]{kvp.Key}[/]",
                    kvp.Value.ToString(),
                    $"{percentage:P1}"
                );
            }

            AnsiConsole.Write(typeTable);
            AnsiConsole.WriteLine();
        }

        // Top files
        if (stats.TopFilesByMembers.Any())
        {
            var filesTable = new Table()
                .Title("[bold blue]Top Files by Member Count[/]")
                .Border(TableBorder.Rounded)
                .AddColumn("[bold]File[/]")
                .AddColumn("[bold]Members[/]");

            foreach (var kvp in stats.TopFilesByMembers)
            {
                filesTable.AddRow(
                    $"[blue]{Path.GetFileName(kvp.Key)}[/]",
                    kvp.Value.ToString()
                );
            }

            AnsiConsole.Write(filesTable);
        }
    }

    private void DisplayJson(DatabaseStats stats)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(stats, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        AnsiConsole.WriteLine(json);
    }
}

public class DatabaseStats
{
    public int TotalFiles { get; set; }
    public int TotalMembers { get; set; }
    public int TotalTags { get; set; }
    public int MembersWithEmbeddings { get; set; }
    public double DatabaseSizeMB { get; set; }
    public Dictionary<string, int> MembersByType { get; set; } = new();
    public Dictionary<string, int> TopFilesByMembers { get; set; } = new();

    public double EmbeddingPercentage => TotalMembers > 0 ? (double)MembersWithEmbeddings / TotalMembers : 0;
}
