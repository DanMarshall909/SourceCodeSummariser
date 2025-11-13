namespace SourceCodeSummariser
{
    /// <summary>
    /// Interface for Large Language Model providers.
    /// Abstracts AI provider implementation to support multiple backends (OpenAI, Anthropic, local models, etc.).
    /// </summary>
    public interface ILlmProvider
    {
        /// <summary>
        /// Generates a summary for the provided code snippet.
        /// </summary>
        /// <param name="code">The code to summarize</param>
        /// <param name="systemPrompt">The system prompt to guide the AI</param>
        /// <param name="userPrompt">The user prompt template (use {code} placeholder)</param>
        /// <param name="maxTokens">Maximum tokens for the response</param>
        /// <returns>The AI-generated summary</returns>
        Task<string> GenerateSummary(string code, string systemPrompt, string userPrompt, int maxTokens);

        /// <summary>
        /// Gets the provider name (e.g., "OpenAI", "Anthropic", "LangChain").
        /// </summary>
        string ProviderName { get; }
    }
}
