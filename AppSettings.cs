namespace SourceCodeSummariser
{
    /// <summary>
    /// Root application settings.
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Gets or sets the target directory containing the codebase to analyze.
        /// </summary>
        public string TargetDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the LLM provider configuration settings.
        /// </summary>
        public LlmProviderSettings LlmProvider { get; set; } = new();

        /// <summary>
        /// Gets or sets the OpenAI configuration settings (deprecated - use LlmProvider instead).
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

    /// <summary>
    /// Configuration settings for LLM provider selection and configuration.
    /// Supports multiple providers: OpenAI, LangChain (with OpenAI/Anthropic), and Local (Ollama).
    /// </summary>
    public class LlmProviderSettings
    {
        /// <summary>
        /// Gets or sets the provider type: "OpenAI", "LangChain", or "Local".
        /// Default: "OpenAI" for backward compatibility.
        /// </summary>
        public string Provider { get; set; } = "OpenAI";

        /// <summary>
        /// Gets or sets the API key. Required for OpenAI and Anthropic.
        /// Should be set via environment variable (LlmProvider__ApiKey or OpenAI__ApiKey).
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the model name.
        /// Examples: "gpt-3.5-turbo", "gpt-4", "claude-3-sonnet-20240229", "llama2", "codellama".
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

        /// <summary>
        /// Gets or sets the LangChain sub-provider: "OpenAI", "Anthropic", etc.
        /// Only used when Provider is "LangChain".
        /// </summary>
        public string LangChainProvider { get; set; } = "OpenAI";

        /// <summary>
        /// Gets or sets the local inference endpoint URL (e.g., Ollama).
        /// Only used when Provider is "Local".
        /// Default: http://localhost:11434 (Ollama default).
        /// </summary>
        public string LocalEndpoint { get; set; } = "http://localhost:11434";

        /// <summary>
        /// Gets or sets the embedding model name.
        /// Examples: "text-embedding-ada-002" (OpenAI), "text-embedding-3-small" (OpenAI), "mxbai-embed-large" (Ollama).
        /// </summary>
        public string EmbeddingModel { get; set; } = "text-embedding-ada-002";

        /// <summary>
        /// Gets or sets whether to generate embeddings during summarization.
        /// Default: true.
        /// </summary>
        public bool EnableEmbeddings { get; set; } = true;
    }
}
