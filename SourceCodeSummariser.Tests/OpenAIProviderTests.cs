using Moq;
using Moq.Protected;
using SourceCodeSummariser.Providers;
using System.Net;
using System.Text.Json;
using Xunit;

namespace SourceCodeSummariser.Tests;

/// <summary>
/// Unit tests for OpenAIProvider
/// </summary>
public class OpenAIProviderTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;

    public OpenAIProviderTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
    }

    [Fact]
    public void Constructor_WithValidSettings_InitializesSuccessfully()
    {
        // Arrange
        var settings = new OpenAISettings
        {
            ApiKey = "test-key",
            Model = "gpt-3.5-turbo",
            MaxTokens = 50,
            TimeoutSeconds = 30
        };

        // Act
        var provider = new OpenAIProvider(_httpClient, settings);

        // Assert
        Assert.NotNull(provider);
        Assert.Equal("OpenAI", provider.ProviderName);
    }

    [Fact]
    public void Constructor_WithNullApiKey_ThrowsArgumentException()
    {
        // Arrange
        var settings = new OpenAISettings
        {
            ApiKey = null!,
            Model = "gpt-3.5-turbo",
            MaxTokens = 50
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new OpenAIProvider(_httpClient, settings));
    }

    [Fact]
    public void Constructor_WithEmptyApiKey_ThrowsArgumentException()
    {
        // Arrange
        var settings = new OpenAISettings
        {
            ApiKey = "",
            Model = "gpt-3.5-turbo",
            MaxTokens = 50
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new OpenAIProvider(_httpClient, settings));
    }

    [Fact]
    public async Task GenerateSummary_WithValidInput_ReturnsSummary()
    {
        // Arrange
        var settings = new OpenAISettings
        {
            ApiKey = "test-key",
            Model = "gpt-3.5-turbo",
            MaxTokens = 50
        };

        var mockResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = "This is a test summary"
                    }
                }
            }
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(mockResponse))
            });

        var provider = new OpenAIProvider(_httpClient, settings);

        // Act
        var summary = await provider.GenerateSummary(
            "public void Test() { }",
            "You are a code summarizer",
            "Summarize this method",
            50);

        // Assert
        Assert.NotNull(summary);
        Assert.Equal("This is a test summary", summary);
    }

    [Fact]
    public async Task GenerateEmbedding_WithValidInput_ReturnsEmbeddingVector()
    {
        // Arrange
        var settings = new OpenAISettings
        {
            ApiKey = "test-key",
            Model = "gpt-3.5-turbo",
            MaxTokens = 50
        };

        var mockEmbedding = new float[1536];
        for (int i = 0; i < mockEmbedding.Length; i++)
            mockEmbedding[i] = 0.1f;

        var mockResponse = new
        {
            data = new[]
            {
                new { embedding = mockEmbedding }
            }
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(mockResponse))
            });

        var provider = new OpenAIProvider(_httpClient, settings);

        // Act
        var embedding = await provider.GenerateEmbedding("test text", "text-embedding-ada-002");

        // Assert
        Assert.NotNull(embedding);
        Assert.Equal(1536, embedding.Length);
    }

    [Fact]
    public async Task GenerateSummary_WhenApiReturnsError_ThrowsException()
    {
        // Arrange
        var settings = new OpenAISettings
        {
            ApiKey = "test-key",
            Model = "gpt-3.5-turbo",
            MaxTokens = 50
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("API Error")
            });

        var provider = new OpenAIProvider(_httpClient, settings);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await provider.GenerateSummary("code", "system", "user", 50));
    }

    [Theory]
    [InlineData("gpt-3.5-turbo")]
    [InlineData("gpt-4")]
    [InlineData("gpt-4-turbo")]
    public void Constructor_WithDifferentModels_InitializesSuccessfully(string model)
    {
        // Arrange
        var settings = new OpenAISettings
        {
            ApiKey = "test-key",
            Model = model,
            MaxTokens = 50
        };

        // Act
        var provider = new OpenAIProvider(_httpClient, settings);

        // Assert
        Assert.NotNull(provider);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(500)]
    public void Constructor_WithDifferentMaxTokens_InitializesSuccessfully(int maxTokens)
    {
        // Arrange
        var settings = new OpenAISettings
        {
            ApiKey = "test-key",
            Model = "gpt-3.5-turbo",
            MaxTokens = maxTokens
        };

        // Act
        var provider = new OpenAIProvider(_httpClient, settings);

        // Assert
        Assert.NotNull(provider);
    }
}
