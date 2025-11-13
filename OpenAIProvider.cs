using System.Text;
using System.Text.Json;

namespace SourceCodeSummariser
{
    /// <summary>
    /// OpenAI-based implementation of ILlmProvider.
    /// Uses OpenAI's Chat Completions API for generating code summaries.
    /// </summary>
    public class OpenAIProvider : ILlmProvider
    {
        private readonly HttpClient _httpClient;
        private readonly OpenAISettings _settings;

        /// <summary>
        /// Gets the provider name.
        /// </summary>
        public string ProviderName => "OpenAI";

        /// <summary>
        /// Initializes a new instance of the OpenAIProvider class.
        /// </summary>
        /// <param name="httpClient">HTTP client for API requests</param>
        /// <param name="settings">OpenAI configuration settings</param>
        public OpenAIProvider(HttpClient httpClient, OpenAISettings settings)
        {
            _httpClient = httpClient;
            _settings = settings;

            // Configure HttpClient
            _httpClient.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);

            // Set OpenAI API key header
            if (!string.IsNullOrEmpty(settings.ApiKey))
            {
                if (!_httpClient.DefaultRequestHeaders.Contains("Authorization"))
                {
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {settings.ApiKey}");
                }
            }
        }

        /// <summary>
        /// Generates a summary using OpenAI's Chat Completions API.
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

                var requestBody = new
                {
                    model = _settings.Model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = formattedUserPrompt }
                    },
                    max_tokens = maxTokens
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(
                    "https://api.openai.com/v1/chat/completions",
                    content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException(
                        $"OpenAI API request failed with status {response.StatusCode}: {errorContent}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(responseContent);

                var summary = jsonDoc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString()
                    ?.Trim();

                if (string.IsNullOrEmpty(summary))
                {
                    throw new InvalidOperationException("OpenAI returned an empty summary");
                }

                return summary;
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException($"OpenAI API error: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to parse OpenAI response: {ex.Message}", ex);
            }
        }
    }
}
