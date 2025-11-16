using Spectre.Console.Cli;
using SourceCodeSummariser.CLI.Commands;

namespace SourceCodeSummariser;

/// <summary>
/// Main entry point for the SourceCode Analyzer CLI
/// </summary>
internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        var app = new CommandApp();

        app.Configure(config =>
        {
            config.SetApplicationName("analyze");

            config.AddCommand<InitCommand>("init")
                .WithDescription("Initialize the analyzer (create config and database)")
                .WithExample(new[] { "init" })
                .WithExample(new[] { "init", "--directory", "/path/to/code" });

            config.AddCommand<ServeCommand>("serve")
                .WithDescription("Start the analyzer service")
                .WithExample(new[] { "serve" })
                .WithExample(new[] { "serve", "--port", "5000" });

            config.AddCommand<ProcessCommand>("process")
                .WithDescription("Process/analyze code files or directories")
                .WithExample(new[] { "process", "/path/to/code" })
                .WithExample(new[] { "process", "MyFile.cs" })
                .WithExample(new[] { "process", "/path/to/code", "--watch" })
                .WithExample(new[] { "process", "MyFile.cs", "--remote" });

            config.AddCommand<SearchCommand>("search")
                .WithDescription("Perform semantic code search")
                .WithExample(new[] { "search", "\"authentication logic\"" })
                .WithExample(new[] { "search", "\"error handling\"", "--top", "5" })
                .WithExample(new[] { "search", "\"async methods\"", "--tags", "public,async" })
                .WithExample(new[] { "search", "--similar-to", "123" })
                .WithExample(new[] { "search", "\"database queries\"", "--format", "json" });

            config.AddCommand<MemberCommand>("member")
                .WithDescription("Get details about a specific code member")
                .WithExample(new[] { "member", "123" })
                .WithExample(new[] { "member", "456", "--format", "json" });

            config.AddCommand<TagsCommand>("tags")
                .WithDescription("List all tags and their usage")
                .WithExample(new[] { "tags" })
                .WithExample(new[] { "tags", "--category", "visibility" })
                .WithExample(new[] { "tags", "--min-count", "10" })
                .WithExample(new[] { "tags", "--format", "list" });

            config.AddCommand<FilesCommand>("files")
                .WithDescription("Get file summary and information")
                .WithExample(new[] { "files", "Program.cs" })
                .WithExample(new[] { "files", "/path/to/MyClass.cs", "--format", "markdown" });

            config.AddCommand<StatsCommand>("stats")
                .WithDescription("Show database statistics")
                .WithExample(new[] { "stats" })
                .WithExample(new[] { "stats", "--database", "/path/to/summaries.db" })
                .WithExample(new[] { "stats", "--format", "json" });

            // Validation and error handling
            config.PropagateExceptions();
            config.ValidateExamples();
        });

        try
        {
            return await app.RunAsync(args);
        }
        catch (Exception ex)
        {
            Spectre.Console.AnsiConsole.WriteException(ex, Spectre.Console.ExceptionFormats.ShortenEverything);
            return 1;
        }
    }
}
