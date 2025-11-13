using LangChain.Providers;
using LangChain.Providers.Ollama;

namespace SourceCodeSummariser
{
    /// <summary>
    /// Local inference implementation of ILlmProvider using Ollama.
    /// Supports running LLMs locally without API keys or internet connection.
    /// </summary>
    public class LocalProvider : ILlmProvider
    {
        private readonly IChatModel _chatModel;
        private readonly string _model;

        /// <summary>
        /// Gets the provider name.
        /// </summary>
        public string ProviderName => $"Local (Ollama - {_model})";

        /// <summary>
        /// Initializes a new instance of the LocalProvider with Ollama.
        /// </summary>
        /// <param name="endpoint">Ollama endpoint URL (default: http://localhost:11434)</param>
        /// <param name="model">Model name (e.g., llama2, codellama, mistral)</param>
        public LocalProvider(string endpoint, string model)
        {
            _model = model;
            var provider = new OllamaProvider(
                options: new OllamaConfiguration
                {
                    Host = endpoint
                });
            _chatModel = new OllamaChatModel(provider, id: model);
        }

        /// <summary>
        /// Generates a summary using a local LLM through Ollama.
        /// </summary>
        /// <param name="code">The code to summarize</param>
        /// <param name="systemPrompt">The system prompt</param>
        /// <param name="userPrompt">The user prompt (use {code} placeholder)</param>
        /// <param name="maxTokens">Maximum tokens for response</param>
        /// <returns>AI-generated summary</returns>
        public async Task<string> GenerateSummary(string code, string systemPrompt, string userPrompt, int maxTokens)
        {
            try
            {
                // Replace {code} placeholder in user prompt
                var formattedUserPrompt = userPrompt.Replace("{code}", code);

                // Create messages for the chat
                var messages = new[]
                {
                    Message.System(systemPrompt),
                    Message.User(formattedUserPrompt)
                };

                // Generate response using Ollama
                var response = await _chatModel.GenerateAsync(
                    messages,
                    new ChatSettings
                    {
                        MaxTokens = maxTokens
                    });

                var summary = response.Messages.LastOrDefault()?.Content?.Trim();

                if (string.IsNullOrEmpty(summary))
                {
                    throw new InvalidOperationException("Local LLM returned an empty summary");
                }

                return summary;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Local LLM error: {ex.Message}", ex);
            }
        }
    }
}
