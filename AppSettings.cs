namespace SourceCodeSummariser
{
    /// <summary>
    /// Root application settings.
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Gets or sets the OpenAI configuration settings.
        /// </summary>
        public OpenAISettings OpenAI { get; set; } = new();

        /// <summary>
        /// Gets or sets the database configuration settings.
        /// </summary>
        public DatabaseSettings Database { get; set; } = new();

        /// <summary>
        /// Gets or sets the file processing configuration settings.
        /// </summary>
        public ProcessingSettings Processing { get; set; } = new();
    }

    /// <summary>
    /// Configuration settings for OpenAI API integration.
    /// </summary>
    public class OpenAISettings
    {
        /// <summary>
        /// Gets or sets the OpenAI API key. Should be set via environment variable OpenAI__ApiKey.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the OpenAI model to use for summarization.
        /// </summary>
        public string Model { get; set; } = "gpt-3.5-turbo";

        /// <summary>
        /// Gets or sets the maximum number of tokens per summary.
        /// </summary>
        public int MaxTokens { get; set; } = 50;

        /// <summary>
        /// Gets or sets the HTTP timeout in seconds for API requests.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;
    }

    /// <summary>
    /// Configuration settings for database storage.
    /// </summary>
    public class DatabaseSettings
    {
        /// <summary>
        /// Gets or sets the SQLite database connection string.
        /// </summary>
        public string ConnectionString { get; set; } = "Data Source=summaries.db";
    }

    /// <summary>
    /// Configuration settings for file processing.
    /// </summary>
    public class ProcessingSettings
    {
        /// <summary>
        /// Gets or sets the list of folder names to exclude from processing.
        /// </summary>
        public List<string> ExcludedFolders { get; set; } = new() { "bin", "obj", ".git", ".vs", "node_modules" };

        /// <summary>
        /// Gets or sets the file pattern to match for processing.
        /// </summary>
        public string FilePattern { get; set; } = "*.cs";

        /// <summary>
        /// Gets or sets the maximum number of retry attempts for failed operations.
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Gets or sets the delay in milliseconds between retry attempts.
        /// </summary>
        public int RetryDelayMilliseconds { get; set; } = 1000;
    }
}
