using Microsoft.EntityFrameworkCore;
using Moq;
using SourceCodeSummariser.Data;
using SourceCodeSummariser.Services;
using Xunit;

namespace SourceCodeSummariser.Tests;

/// <summary>
/// Unit tests for SemanticSearchService
/// </summary>
public class SemanticSearchServiceTests : IDisposable
{
    private readonly SummaryContext _dbContext;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly SemanticSearchService _searchService;

    public SemanticSearchServiceTests()
    {
        var options = new DbContextOptionsBuilder<SummaryContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new SummaryContext(options);

        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockLlmProvider.Setup(p => p.ProviderName).Returns("Test Provider");

        _searchService = new SemanticSearchService(_dbContext, _mockLlmProvider.Object);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var file1 = new FileEntity { FileName = "AuthService.cs" };
        var file2 = new FileEntity { FileName = "UserService.cs" };
        _dbContext.Files.AddRange(file1, file2);
        _dbContext.SaveChanges();

        var tagPublic = new TagEntity { Name = "public", Category = "visibility" };
        var tagPrivate = new TagEntity { Name = "private", Category = "visibility" };
        var tagAsync = new TagEntity { Name = "async", Category = "modifier" };
        _dbContext.Tags.AddRange(tagPublic, tagPrivate, tagAsync);
        _dbContext.SaveChanges();

        // Create test embeddings (simplified for testing)
        var embedding1 = CreateEmbedding(0.5f);
        var embedding2 = CreateEmbedding(0.3f);
        var embedding3 = CreateEmbedding(0.8f);

        var member1 = new MemberEntity
        {
            Name = "AuthenticateUser",
            Type = "Method",
            Summary = "Authenticates a user with username and password",
            Hash = "hash1",
            Embedding = embedding1,
            File = file1
        };

        var member2 = new MemberEntity
        {
            Name = "GetUserById",
            Type = "Method",
            Summary = "Retrieves a user by their ID",
            Hash = "hash2",
            Embedding = embedding2,
            File = file2
        };

        var member3 = new MemberEntity
        {
            Name = "ValidatePassword",
            Type = "Method",
            Summary = "Validates password complexity requirements",
            Hash = "hash3",
            Embedding = embedding3,
            File = file1
        };

        _dbContext.Members.AddRange(member1, member2, member3);
        _dbContext.SaveChanges();

        // Add tags
        _dbContext.MemberTags.AddRange(
            new MemberTagEntity { Member = member1, Tag = tagPublic },
            new MemberTagEntity { Member = member1, Tag = tagAsync },
            new MemberTagEntity { Member = member2, Tag = tagPublic },
            new MemberTagEntity { Member = member3, Tag = tagPrivate }
        );
        _dbContext.SaveChanges();
    }

    private byte[] CreateEmbedding(float baseValue)
    {
        var embedding = new float[1536];
        for (int i = 0; i < embedding.Length; i++)
            embedding[i] = baseValue + (i * 0.0001f);

        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    [Fact]
    public async Task SearchAsync_WithValidQuery_ReturnsResults()
    {
        // Arrange
        var queryEmbedding = new float[1536];
        for (int i = 0; i < queryEmbedding.Length; i++)
            queryEmbedding[i] = 0.5f;

        _mockLlmProvider
            .Setup(p => p.GenerateEmbedding(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(queryEmbedding);

        // Act
        var results = await _searchService.SearchAsync("authentication", topK: 10, minSimilarity: 0.0);

        // Assert
        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.NotNull(r.Member));
        Assert.All(results, r => Assert.InRange(r.Similarity, 0.0, 1.0));
    }

    [Fact]
    public async Task SearchAsync_WithHighSimilarityThreshold_FiltersResults()
    {
        // Arrange
        var queryEmbedding = new float[1536];
        for (int i = 0; i < queryEmbedding.Length; i++)
            queryEmbedding[i] = 0.5f;

        _mockLlmProvider
            .Setup(p => p.GenerateEmbedding(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(queryEmbedding);

        // Act
        var resultsLowThreshold = await _searchService.SearchAsync("authentication", topK: 10, minSimilarity: 0.0);
        var resultsHighThreshold = await _searchService.SearchAsync("authentication", topK: 10, minSimilarity: 0.99);

        // Assert
        Assert.NotEmpty(resultsLowThreshold);
        Assert.True(resultsHighThreshold.Count() <= resultsLowThreshold.Count());
    }

    [Fact]
    public async Task SearchWithTagsAsync_FiltersResultsByTags()
    {
        // Arrange
        var queryEmbedding = new float[1536];
        for (int i = 0; i < queryEmbedding.Length; i++)
            queryEmbedding[i] = 0.5f;

        _mockLlmProvider
            .Setup(p => p.GenerateEmbedding(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(queryEmbedding);

        // Act
        var allResults = await _searchService.SearchAsync("user", topK: 10, minSimilarity: 0.0);
        var publicResults = await _searchService.SearchWithTagsAsync("user", new[] { "public" }, topK: 10, minSimilarity: 0.0);

        // Assert
        Assert.NotEmpty(allResults);
        Assert.NotEmpty(publicResults);
        Assert.True(publicResults.Count() <= allResults.Count());
        Assert.All(publicResults, r => Assert.Contains(r.Tags, t => t.Name == "public"));
    }

    [Fact]
    public async Task FindSimilarMembersAsync_WithValidMemberId_ReturnsSimilarMembers()
    {
        // Arrange
        var memberId = 1; // AuthenticateUser

        // Act
        var results = await _searchService.FindSimilarMembersAsync(memberId, topK: 10, minSimilarity: 0.0);

        // Assert
        Assert.NotNull(results);
        // Should not include the source member itself
        Assert.All(results, r => Assert.NotEqual(memberId, r.Member.Id));
    }

    [Fact]
    public async Task FindSimilarMembersAsync_WithInvalidMemberId_ReturnsEmpty()
    {
        // Arrange
        var memberId = 9999;

        // Act
        var results = await _searchService.FindSimilarMembersAsync(memberId, topK: 10, minSimilarity: 0.0);

        // Assert
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_OrdersResultsBySimilarity()
    {
        // Arrange
        var queryEmbedding = new float[1536];
        for (int i = 0; i < queryEmbedding.Length; i++)
            queryEmbedding[i] = 0.5f;

        _mockLlmProvider
            .Setup(p => p.GenerateEmbedding(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(queryEmbedding);

        // Act
        var results = await _searchService.SearchAsync("test", topK: 10, minSimilarity: 0.0);

        // Assert
        var resultsList = results.ToList();
        for (int i = 1; i < resultsList.Count; i++)
        {
            Assert.True(resultsList[i - 1].Similarity >= resultsList[i].Similarity,
                "Results should be ordered by similarity in descending order");
        }
    }

    [Fact]
    public async Task SearchAsync_RespectsTopKLimit()
    {
        // Arrange
        var queryEmbedding = new float[1536];
        for (int i = 0; i < queryEmbedding.Length; i++)
            queryEmbedding[i] = 0.5f;

        _mockLlmProvider
            .Setup(p => p.GenerateEmbedding(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(queryEmbedding);

        // Act
        var results = await _searchService.SearchAsync("test", topK: 2, minSimilarity: 0.0);

        // Assert
        Assert.True(results.Count() <= 2, "Should respect topK limit");
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
