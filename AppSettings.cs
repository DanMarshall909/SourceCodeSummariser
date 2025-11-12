namespace SourceCodeSummariser
{
    public class AppSettings
    {
        public OpenAISettings OpenAI { get; set; } = new();
        public DatabaseSettings Database { get; set; } = new();
        public ProcessingSettings Processing { get; set; } = new();
    }

    public class OpenAISettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gpt-3.5-turbo";
        public int MaxTokens { get; set; } = 50;
        public int TimeoutSeconds { get; set; } = 30;
    }

    public class DatabaseSettings
    {
        public string ConnectionString { get; set; } = "Data Source=summaries.db";
    }

    public class ProcessingSettings
    {
        public List<string> ExcludedFolders { get; set; } = new() { "bin", "obj", ".git", ".vs", "node_modules" };
        public string FilePattern { get; set; } = "*.cs";
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMilliseconds { get; set; } = 1000;
    }
}
