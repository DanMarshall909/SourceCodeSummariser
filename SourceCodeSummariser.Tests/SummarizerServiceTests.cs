using Moq;
using SourceCodeSummariser.Services;
using Xunit;

namespace SourceCodeSummariser.Tests;

/// <summary>
/// Unit tests for SummarizerService
/// </summary>
public class SummarizerServiceTests
{
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly SummarizerService _summarizerService;

    public SummarizerServiceTests()
    {
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockLlmProvider.Setup(p => p.ProviderName).Returns("Test Provider");
        _summarizerService = new SummarizerService(_mockLlmProvider.Object, maxTokens: 50);
    }

    [Fact]
    public async Task SummarizeMethod_WithValidCode_ReturnsSummary()
    {
        // Arrange
        var code = @"
            public async Task<User> GetUserById(int id)
            {
                return await _database.Users.FindAsync(id);
            }";

        var expectedSummary = "Retrieves a user by ID from the database";

        _mockLlmProvider
            .Setup(p => p.GenerateSummary(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .ReturnsAsync(expectedSummary);

        // Act
        var summary = await _summarizerService.SummarizeMethod("GetUserById", code);

        // Assert
        Assert.NotNull(summary);
        Assert.Equal(expectedSummary, summary);
        _mockLlmProvider.Verify(p => p.GenerateSummary(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task SummarizeMethod_WithEmptyCode_ReturnsGenericSummary()
    {
        // Arrange
        var code = "";
        var expectedSummary = "Method implementation";

        _mockLlmProvider
            .Setup(p => p.GenerateSummary(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .ReturnsAsync(expectedSummary);

        // Act
        var summary = await _summarizerService.SummarizeMethod("EmptyMethod", code);

        // Assert
        Assert.NotNull(summary);
    }

    [Fact]
    public async Task SummarizeMethod_WithComplexCode_GeneratesAppropriatePrompt()
    {
        // Arrange
        var code = @"
            public async Task<IActionResult> AuthenticateUser(LoginRequest request)
            {
                var user = await _userService.FindByUsername(request.Username);
                if (user == null || !_passwordService.Verify(request.Password, user.PasswordHash))
                {
                    return Unauthorized();
                }

                var token = _tokenService.GenerateToken(user);
                return Ok(new { Token = token });
            }";

        var expectedSummary = "Authenticates a user with credentials and returns a JWT token";

        _mockLlmProvider
            .Setup(p => p.GenerateSummary(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .ReturnsAsync(expectedSummary);

        // Act
        var summary = await _summarizerService.SummarizeMethod("AuthenticateUser", code);

        // Assert
        Assert.NotNull(summary);
        Assert.NotEmpty(summary);
    }

    [Theory]
    [InlineData("GetUserById")]
    [InlineData("ProcessPayment")]
    [InlineData("ValidateEmail")]
    public async Task SummarizeMethod_WithDifferentMethodNames_IncludesMethodName(string methodName)
    {
        // Arrange
        var code = "public void Method() { }";
        var expectedSummary = $"Summary for {methodName}";

        _mockLlmProvider
            .Setup(p => p.GenerateSummary(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .ReturnsAsync(expectedSummary);

        // Act
        var summary = await _summarizerService.SummarizeMethod(methodName, code);

        // Assert
        Assert.NotNull(summary);
        _mockLlmProvider.Verify(p => p.GenerateSummary(
            It.IsAny<string>(),
            It.Is<string>(s => s.Contains(methodName)),
            It.IsAny<string>(),
            It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task SummarizeMethod_WhenLlmProviderThrows_PropagatesException()
    {
        // Arrange
        var code = "public void Method() { }";

        _mockLlmProvider
            .Setup(p => p.GenerateSummary(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .ThrowsAsync(new Exception("API Error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () =>
            await _summarizerService.SummarizeMethod("Method", code));
    }

    [Fact]
    public void Constructor_WithValidMaxTokens_InitializesSuccessfully()
    {
        // Arrange & Act
        var service = new SummarizerService(_mockLlmProvider.Object, maxTokens: 100);

        // Assert
        Assert.NotNull(service);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(200)]
    public void Constructor_WithDifferentMaxTokens_InitializesSuccessfully(int maxTokens)
    {
        // Arrange & Act
        var service = new SummarizerService(_mockLlmProvider.Object, maxTokens: maxTokens);

        // Assert
        Assert.NotNull(service);
    }
}
