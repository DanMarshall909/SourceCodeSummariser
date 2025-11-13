using LangChain.Providers;
using LangChain.Providers.Anthropic;
using LangChain.Providers.OpenAI;

namespace SourceCodeSummariser
{
    /// <summary>
    /// LangChain-based implementation of ILlmProvider.
    /// Supports multiple AI providers through LangChain abstraction (OpenAI, Anthropic, etc.).
    /// </summary>
    public class LangChainProvider : ILlmProvider
    {
        private readonly IChatModel _chatModel;
        private readonly string _providerType;

        /// <summary>
        /// Gets the provider name.
        /// </summary>
        public string ProviderName => $"LangChain ({_providerType})";

        /// <summary>
        /// Initializes a new instance of the LangChainProvider with OpenAI.
        /// </summary>
        /// <param name="apiKey">OpenAI API key</param>
        /// <param name="model">Model name (e.g., gpt-3.5-turbo, gpt-4)</param>
        public LangChainProvider(string apiKey, string model)
        {
            _chatModel = new OpenAiChatModel(new OpenAiProvider(apiKey), id: model);
            _providerType = "OpenAI";
        }

        /// <summary>
        /// Initializes a new instance of the LangChainProvider with Anthropic Claude.
        /// </summary>
        /// <param name="apiKey">Anthropic API key</param>
        /// <param name="model">Model name (e.g., claude-3-sonnet-20240229)</param>
        /// <param name="useAnthropic">Marker parameter to distinguish from OpenAI constructor</param>
        public LangChainProvider(string apiKey, string model, bool useAnthropic)
        {
            if (useAnthropic)
            {
                _chatModel = new AnthropicChatModel(new AnthropicProvider(apiKey), id: model);
                _providerType = "Anthropic";
            }
            else
            {
                _chatModel = new OpenAiChatModel(new OpenAiProvider(apiKey), id: model);
                _providerType = "OpenAI";
            }
        }

        /// <summary>
        /// Initializes a new instance of the LangChainProvider with a custom chat model.
        /// </summary>
        /// <param name="chatModel">The LangChain chat model to use</param>
        /// <param name="providerType">Provider type name for display</param>
        public LangChainProvider(IChatModel chatModel, string providerType)
        {
            _chatModel = chatModel;
            _providerType = providerType;
        }

        /// <summary>
        /// Generates a summary using LangChain's unified interface.
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

                // Generate response using LangChain
                var response = await _chatModel.GenerateAsync(
                    messages,
                    new ChatSettings
                    {
                        MaxTokens = maxTokens
                    });

                var summary = response.Messages.LastOrDefault()?.Content?.Trim();

                if (string.IsNullOrEmpty(summary))
                {
                    throw new InvalidOperationException($"{_providerType} returned an empty summary");
                }

                return summary;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"{_providerType} error: {ex.Message}", ex);
            }
        }
    }
}
