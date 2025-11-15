using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SourceCodeSummariser.Data;
using SourceCodeSummariser.Services;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SourceCodeSummariser.Tests;

/// <summary>
/// Integration tests for API endpoints
/// </summary>
public class ApiEndpointsTests : IDisposable
{
    private readonly TestServer _server;
    private readonly HttpClient _client;
    private readonly SummaryContext _dbContext;
    private readonly Mock<ILlmProvider> _mockLlmProvider;

    public ApiEndpointsTests()
    {
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockLlmProvider.Setup(p => p.ProviderName).Returns("Test Provider");
        _mockLlmProvider.Setup(p => p.GenerateEmbedding(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new float[1536]); // Mock embedding vector

        var builder = WebApplication.CreateBuilder();

        // Configure in-memory database
        builder.Services.AddDbContext<SummaryContext>(options =>
            options.UseInMemoryDatabase("TestDb"));

        builder.Services.AddSingleton(_mockLlmProvider.Object);
        builder.Services.AddSingleton(new AppSettings
        {
            TargetDirectory = "./TestData",
            LlmProvider = new LlmProviderSettings
            {
                Provider = "test",
                EnableEmbeddings = true,
                MaxTokens = 50
            }
        });
        builder.Services.AddScoped<SemanticSearchService>();
        builder.Services.AddScoped<FileProcessorService>();
        builder.Services.AddScoped<SummarizerService>();

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        var app = builder.Build();
        app.UseCors();

        // Register the same endpoints as ApiServer
        RegisterEndpoints(app);

        _server = new TestServer(builder.WebHost);
        _client = _server.CreateClient();

        // Initialize database with test data
        _dbContext = _server.Services.GetRequiredService<SummaryContext>();
        SeedTestData();
    }

    private void RegisterEndpoints(WebApplication app)
    {
        // GET /api/health
        app.MapGet("/api/health", () => Results.Ok(new
        {
            status = "healthy",
            service = "SourceCodeSummariser API",
            version = "1.0.0"
        }));

        // GET /api/search
        app.MapGet("/api/search", async (
            string query,
            int topK,
            double minSimilarity,
            SemanticSearchService searchService) =>
        {
            var results = await searchService.SearchAsync(query, topK, minSimilarity);
            return Results.Ok(results.Select(r => new
            {
                memberName = r.Member.Name,
                memberType = r.Member.Type,
                summary = r.Member.Summary,
                similarity = r.Similarity,
                filePath = r.FilePath,
                tags = r.Tags.Select(t => t.Name).ToList()
            }));
        });

        // GET /api/tags
        app.MapGet("/api/tags", async (SummaryContext db) =>
        {
            var tags = await db.Tags
                .Select(t => new
                {
                    name = t.Name,
                    category = t.Category,
                    count = t.MemberTags.Count
                })
                .OrderByDescending(t => t.count)
                .ToListAsync();

            return Results.Ok(tags);
        });

        // GET /api/members/{id}
        app.MapGet("/api/members/{id:int}", async (
            int id,
            SummaryContext db) =>
        {
            var member = await db.Members
                .Include(m => m.File)
                .Include(m => m.MemberTags)
                .ThenInclude(mt => mt.Tag)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (member == null)
                return Results.NotFound(new { error = $"Member with ID {id} not found" });

            return Results.Ok(new
            {
                id = member.Id,
                name = member.Name,
                type = member.Type,
                summary = member.Summary,
                filePath = member.File.FileName,
                tags = member.MemberTags.Select(mt => mt.Tag.Name).ToList()
            });
        });
    }

    private void SeedTestData()
    {
        var file = new FileEntity { FileName = "TestFile.cs" };
        _dbContext.Files.Add(file);
        _dbContext.SaveChanges();

        var tag1 = new TagEntity { Name = "public", Category = "visibility" };
        var tag2 = new TagEntity { Name = "async", Category = "modifier" };
        _dbContext.Tags.AddRange(tag1, tag2);
        _dbContext.SaveChanges();

        var embedding = new float[1536];
        for (int i = 0; i < embedding.Length; i++)
            embedding[i] = 0.1f;

        var member = new MemberEntity
        {
            Name = "TestMethod",
            Type = "Method",
            Summary = "This is a test method",
            Hash = "test-hash",
            Embedding = SerializeEmbedding(embedding),
            File = file
        };

        _dbContext.Members.Add(member);
        _dbContext.SaveChanges();

        var memberTag1 = new MemberTagEntity { Member = member, Tag = tag1 };
        var memberTag2 = new MemberTagEntity { Member = member, Tag = tag2 };
        _dbContext.MemberTags.AddRange(memberTag1, memberTag2);
        _dbContext.SaveChanges();
    }

    private byte[] SerializeEmbedding(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var healthData = JsonSerializer.Deserialize<JsonElement>(content);

        Assert.Equal("healthy", healthData.GetProperty("status").GetString());
        Assert.Equal("SourceCodeSummariser API", healthData.GetProperty("service").GetString());
    }

    [Fact]
    public async Task SearchEndpoint_WithValidQuery_ReturnsResults()
    {
        // Arrange
        var query = "test method";
        var topK = 10;
        var minSimilarity = 0.0;

        // Act
        var response = await _client.GetAsync($"/api/search?query={query}&topK={topK}&minSimilarity={minSimilarity}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var results = JsonSerializer.Deserialize<JsonElement>(content);

        Assert.True(results.ValueKind == JsonValueKind.Array);
    }

    [Fact]
    public async Task TagsEndpoint_ReturnsAllTags()
    {
        // Act
        var response = await _client.GetAsync("/api/tags");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var tags = JsonSerializer.Deserialize<JsonElement>(content);

        Assert.True(tags.ValueKind == JsonValueKind.Array);
        Assert.True(tags.GetArrayLength() >= 2); // We seeded 2 tags
    }

    [Fact]
    public async Task MemberEndpoint_WithValidId_ReturnsMember()
    {
        // Arrange
        var memberId = 1;

        // Act
        var response = await _client.GetAsync($"/api/members/{memberId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var member = JsonSerializer.Deserialize<JsonElement>(content);

        Assert.Equal(memberId, member.GetProperty("id").GetInt32());
        Assert.Equal("TestMethod", member.GetProperty("name").GetString());
        Assert.Equal("Method", member.GetProperty("type").GetString());
    }

    [Fact]
    public async Task MemberEndpoint_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var memberId = 9999;

        // Act
        var response = await _client.GetAsync($"/api/members/{memberId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        _client?.Dispose();
        _server?.Dispose();
    }
}
