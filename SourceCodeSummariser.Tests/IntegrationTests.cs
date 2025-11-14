using Microsoft.EntityFrameworkCore;
using Moq;
using SourceCodeSummariser.Data;
using SourceCodeSummariser.Services;
using Xunit;

namespace SourceCodeSummariser.Tests;

/// <summary>
/// Integration tests for end-to-end workflows
/// </summary>
public class IntegrationTests : IDisposable
{
    private readonly SummaryContext _dbContext;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly SemanticSearchService _searchService;
    private readonly SummarizerService _summarizerService;
    private readonly FileProcessorService _fileProcessorService;

    public IntegrationTests()
    {
        var options = new DbContextOptionsBuilder<SummaryContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new SummaryContext(options);

        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockLlmProvider.Setup(p => p.ProviderName).Returns("Test Provider");

        // Setup default mock responses
        _mockLlmProvider
            .Setup(p => p.GenerateSummary(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .ReturnsAsync("Generated summary");

        var embedding = new float[1536];
        for (int i = 0; i < embedding.Length; i++)
            embedding[i] = 0.1f;

        _mockLlmProvider
            .Setup(p => p.GenerateEmbedding(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(embedding);

        var settings = new LlmProviderSettings
        {
            Provider = "test",
            EnableEmbeddings = true,
            MaxTokens = 50
        };

        _summarizerService = new SummarizerService(_mockLlmProvider.Object, maxTokens: 50);
        _fileProcessorService = new FileProcessorService(_dbContext, _summarizerService, _mockLlmProvider.Object, settings);
        _searchService = new SemanticSearchService(_dbContext, _mockLlmProvider.Object);
    }

    [Fact]
    public async Task EndToEnd_ProcessFile_ThenSearch_ReturnsResults()
    {
        // Arrange - Create a test C# file
        var testFilePath = Path.Combine(Path.GetTempPath(), "TestFile.cs");
        var testCode = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public async Task<User> AuthenticateUser(string username, string password)
        {
            // Authentication logic here
            return await _database.Users.FindAsync(username);
        }

        public void ValidatePassword(string password)
        {
            // Password validation logic
        }
    }
}";
        await File.WriteAllTextAsync(testFilePath, testCode);

        try
        {
            // Act - Process the file
            var changes = await _fileProcessorService.ProcessFile(testFilePath);

            // Assert - File was processed
            var file = await _dbContext.Files.FirstOrDefaultAsync(f => f.FileName == testFilePath);
            Assert.NotNull(file);

            var members = await _dbContext.Members
                .Where(m => m.File.FileName == testFilePath)
                .ToListAsync();
            Assert.NotEmpty(members);

            // Act - Search for authentication-related code
            var searchResults = await _searchService.SearchAsync("authentication", topK: 10, minSimilarity: 0.0);

            // Assert - Found relevant results
            Assert.NotEmpty(searchResults);
            Assert.All(searchResults, r => Assert.NotNull(r.Member));
            Assert.All(searchResults, r => Assert.NotNull(r.Member.Summary));
        }
        finally
        {
            // Cleanup
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task EndToEnd_ProcessFile_GeneratesEmbeddings()
    {
        // Arrange
        var testFilePath = Path.Combine(Path.GetTempPath(), "TestFileWithEmbeddings.cs");
        var testCode = @"
public class TestClass
{
    public void TestMethod()
    {
        Console.WriteLine(""Test"");
    }
}";
        await File.WriteAllTextAsync(testFilePath, testCode);

        try
        {
            // Act
            await _fileProcessorService.ProcessFile(testFilePath);

            // Assert
            var members = await _dbContext.Members
                .Where(m => m.File.FileName == testFilePath)
                .ToListAsync();

            Assert.NotEmpty(members);
            Assert.All(members, m =>
            {
                if (m.Type == "Method") // Only methods have embeddings
                {
                    Assert.NotNull(m.Embedding);
                    Assert.NotEmpty(m.Embedding);
                }
            });
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task EndToEnd_ProcessFile_ExtractsTags()
    {
        // Arrange
        var testFilePath = Path.Combine(Path.GetTempPath(), "TestFileWithTags.cs");
        var testCode = @"
public class TestClass
{
    public async Task<string> PublicMethod()
    {
        return ""test"";
    }

    private void PrivateMethod()
    {
    }
}";
        await File.WriteAllTextAsync(testFilePath, testCode);

        try
        {
            // Act
            await _fileProcessorService.ProcessFile(testFilePath);

            // Assert
            var tags = await _dbContext.Tags.ToListAsync();
            Assert.NotEmpty(tags);
            Assert.Contains(tags, t => t.Name == "public");
            Assert.Contains(tags, t => t.Name == "private");
            Assert.Contains(tags, t => t.Name == "async");
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task EndToEnd_ProcessSameFileTwice_DetectsNoChanges()
    {
        // Arrange
        var testFilePath = Path.Combine(Path.GetTempPath(), "TestFileNoChanges.cs");
        var testCode = @"
public class TestClass
{
    public void TestMethod() { }
}";
        await File.WriteAllTextAsync(testFilePath, testCode);

        try
        {
            // Act - Process file twice
            var changes1 = await _fileProcessorService.ProcessFile(testFilePath);
            var changes2 = await _fileProcessorService.ProcessFile(testFilePath);

            // Assert
            Assert.NotEmpty(changes1); // First time should detect changes
            Assert.Empty(changes2);     // Second time should detect no changes
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task EndToEnd_UpdateFile_DetectsChanges()
    {
        // Arrange
        var testFilePath = Path.Combine(Path.GetTempPath(), "TestFileChanges.cs");
        var originalCode = @"
public class TestClass
{
    public void OriginalMethod() { }
}";
        var updatedCode = @"
public class TestClass
{
    public void UpdatedMethod() { }
}";

        await File.WriteAllTextAsync(testFilePath, originalCode);

        try
        {
            // Act - Process original file
            await _fileProcessorService.ProcessFile(testFilePath);

            // Update file content
            await File.WriteAllTextAsync(testFilePath, updatedCode);

            // Process updated file
            var changes = await _fileProcessorService.ProcessFile(testFilePath);

            // Assert
            Assert.NotEmpty(changes);

            var members = await _dbContext.Members
                .Where(m => m.File.FileName == testFilePath)
                .ToListAsync();

            Assert.Contains(members, m => m.Name == "UpdatedMethod");
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task EndToEnd_SearchWithTags_FiltersCorrectly()
    {
        // Arrange
        var testFilePath = Path.Combine(Path.GetTempPath(), "TestFileTagFiltering.cs");
        var testCode = @"
public class TestClass
{
    public async Task<string> PublicAsyncMethod()
    {
        return ""test"";
    }

    private void PrivateMethod()
    {
    }

    public void PublicMethod()
    {
    }
}";
        await File.WriteAllTextAsync(testFilePath, testCode);

        try
        {
            // Act
            await _fileProcessorService.ProcessFile(testFilePath);

            var allResults = await _searchService.SearchAsync("method", topK: 10, minSimilarity: 0.0);
            var publicResults = await _searchService.SearchWithTagsAsync("method", new[] { "public" }, topK: 10, minSimilarity: 0.0);
            var asyncResults = await _searchService.SearchWithTagsAsync("method", new[] { "async" }, topK: 10, minSimilarity: 0.0);

            // Assert
            Assert.NotEmpty(allResults);
            Assert.NotEmpty(publicResults);
            Assert.NotEmpty(asyncResults);

            Assert.True(publicResults.Count() <= allResults.Count());
            Assert.True(asyncResults.Count() <= publicResults.Count());

            Assert.All(publicResults, r => Assert.Contains(r.Tags, t => t.Name == "public"));
            Assert.All(asyncResults, r => Assert.Contains(r.Tags, t => t.Name == "async"));
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
