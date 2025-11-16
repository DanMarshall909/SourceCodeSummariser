using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SourceCodeSummariser.CLI.Commands;

/// <summary>
/// Command to get details about a code member
/// </summary>
public class MemberCommand : AsyncCommand<MemberCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [Description("Member ID to retrieve")]
        [CommandArgument(0, "<id>")]
        public int MemberId { get; set; }

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
            var member = await AnsiConsole.Status()
                .StartAsync($"Retrieving member {settings.MemberId}...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    ctx.SpinnerStyle(Style.Parse("blue"));
                    return await client.GetMemberAsync(settings.MemberId);
                });

            if (member == null)
            {
                AnsiConsole.MarkupLine($"[yellow]Member {settings.MemberId} not found[/]");
                return 1;
            }

            AnsiConsole.MarkupLine("[green]✓[/] Member found");
            AnsiConsole.WriteLine();

            // Display in requested format
            switch (settings.Format.ToLowerInvariant())
            {
                case "json":
                    DisplayJson(member);
                    break;
                case "markdown":
                    DisplayMarkdown(member);
                    break;
                default:
                    DisplayTable(member);
                    break;
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error retrieving member: {ex.Message}[/]");
            return 1;
        }
    }

    private void DisplayTable(MemberDetails member)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Property[/]")
            .AddColumn("[bold]Value[/]");

        table.AddRow("ID", member.Id.ToString());
        table.AddRow("Name", $"[blue]{member.Name}[/]");
        table.AddRow("Type", member.Type);
        table.AddRow("File", member.FileName);
        table.AddRow("Hash", member.Hash);

        if (member.Tags.Any())
        {
            table.AddRow("Tags", string.Join(", ", member.Tags.Select(t => $"[cyan]{t}[/]")));
        }

        AnsiConsole.Write(table);

        // Display summary in a panel
        if (!string.IsNullOrEmpty(member.Summary))
        {
            AnsiConsole.WriteLine();
            var panel = new Panel(member.Summary)
            {
                Header = new PanelHeader("[bold]Summary[/]"),
                Border = BoxBorder.Rounded,
                Padding = new Padding(2, 1)
            };
            AnsiConsole.Write(panel);
        }
    }

    private void DisplayMarkdown(MemberDetails member)
    {
        AnsiConsole.MarkupLine($"# {member.Name}");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"**ID:** {member.Id}");
        AnsiConsole.MarkupLine($"**Type:** {member.Type}");
        AnsiConsole.MarkupLine($"**File:** {member.FileName}");
        AnsiConsole.MarkupLine($"**Hash:** {member.Hash}");

        if (member.Tags.Any())
        {
            AnsiConsole.MarkupLine($"**Tags:** {string.Join(", ", member.Tags)}");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("## Summary");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(member.Summary);
    }

    private void DisplayJson(MemberDetails member)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(member, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        AnsiConsole.WriteLine(json);
    }
}
