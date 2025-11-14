using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using SourceCodeSummariser.Data;
using SourceCodeSummariser.Services;
using System.Text.Json;

namespace SourceCodeSummariser;

/// <summary>
/// Minimal HTTP API server for MCP integration
/// Exposes semantic search and file processing capabilities via REST endpoints
/// </summary>
public class ApiServer
{
    public static async Task RunAsync(string[] args, string dbPath, ILlmProvider llmProvider, AppSettings settings)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure services
        builder.Services.AddDbContext<SummaryContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        builder.Services.AddSingleton(llmProvider);
        builder.Services.AddSingleton(settings);
        builder.Services.AddScoped<SemanticSearchService>();
        builder.Services.AddScoped<FileProcessorService>();
        builder.Services.AddScoped<SummarizerService>();

        // Add CORS for local development
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

        // API Endpoints

        // GET /api/search - Semantic code search
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

        // GET /api/search/tags - Search with tag filtering
        app.MapGet("/api/search/tags", async (
            string query,
            string tags,
            int topK,
            double minSimilarity,
            SemanticSearchService searchService) =>
        {
            var tagList = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var results = await searchService.SearchWithTagsAsync(query, tagList, topK, minSimilarity);
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

        // GET /api/search/similar - Find similar members
        app.MapGet("/api/search/similar", async (
            int memberId,
            int topK,
            double minSimilarity,
            SemanticSearchService searchService) =>
        {
            var results = await searchService.FindSimilarMembersAsync(memberId, topK, minSimilarity);
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

        // GET /api/members/{id} - Get member details
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

        // GET /api/tags - List all tags
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

        // GET /api/files/summary - Get file summary
        app.MapGet("/api/files/summary", async (
            string filePath,
            SummaryContext db) =>
        {
            var file = await db.Files
                .Include(f => f.Members)
                .ThenInclude(m => m.MemberTags)
                .ThenInclude(mt => mt.Tag)
                .FirstOrDefaultAsync(f => f.FileName == filePath);

            if (file == null)
                return Results.NotFound(new { error = $"File '{filePath}' not found in database" });

            return Results.Ok(new
            {
                fileName = file.FileName,
                members = file.Members.Select(m => new
                {
                    name = m.Name,
                    type = m.Type,
                    summary = m.Summary,
                    tags = m.MemberTags.Select(mt => mt.Tag.Name).ToList()
                }).ToList()
            });
        });

        // GET /api/files/process - Process a file
        app.MapGet("/api/files/process", async (
            string filePath,
            FileProcessorService fileProcessor) =>
        {
            try
            {
                if (!File.Exists(filePath))
                    return Results.BadRequest(new { success = false, message = $"File not found: {filePath}" });

                await fileProcessor.ProcessFile(filePath);
                return Results.Ok(new { success = true, message = $"Successfully processed {filePath}" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { success = false, message = $"Error processing file: {ex.Message}" });
            }
        });

        // GET /api/health - Health check
        app.MapGet("/api/health", () => Results.Ok(new
        {
            status = "healthy",
            service = "SourceCodeSummariser API",
            version = "1.0.0"
        }));

        Console.WriteLine("Starting API server on http://localhost:5000");
        Console.WriteLine("Available endpoints:");
        Console.WriteLine("  GET /api/search?query=...&topK=10&minSimilarity=0.7");
        Console.WriteLine("  GET /api/search/tags?query=...&tags=tag1,tag2&topK=10&minSimilarity=0.7");
        Console.WriteLine("  GET /api/search/similar?memberId=1&topK=10&minSimilarity=0.7");
        Console.WriteLine("  GET /api/members/{id}");
        Console.WriteLine("  GET /api/tags");
        Console.WriteLine("  GET /api/files/summary?filePath=...");
        Console.WriteLine("  GET /api/files/process?filePath=...");
        Console.WriteLine("  GET /api/health");
        Console.WriteLine();

        await app.RunAsync("http://localhost:5000");
    }
}
