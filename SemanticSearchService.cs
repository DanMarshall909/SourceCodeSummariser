using Microsoft.EntityFrameworkCore;

namespace SourceCodeSummariser;

/// <summary>
/// Service for performing semantic search on code members using vector embeddings.
/// </summary>
public class SemanticSearchService
{
    private readonly SummaryContext _dbContext;
    private readonly ILlmProvider _llmProvider;
    private readonly LlmProviderSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="SemanticSearchService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context for accessing code members.</param>
    /// <param name="llmProvider">The LLM provider for generating query embeddings.</param>
    /// <param name="settings">The LLM provider settings.</param>
    public SemanticSearchService(
        SummaryContext dbContext,
        ILlmProvider llmProvider,
        LlmProviderSettings settings)
    {
        _dbContext = dbContext;
        _llmProvider = llmProvider;
        _settings = settings;
    }

    /// <summary>
    /// Performs semantic search to find code members similar to the query text.
    /// </summary>
    /// <param name="query">The search query (e.g., "async methods that handle files")</param>
    /// <param name="topK">Number of results to return (default: 10)</param>
    /// <param name="minSimilarity">Minimum similarity threshold (0.0 to 1.0, default: 0.0)</param>
    /// <returns>List of search results ordered by relevance</returns>
    public async Task<List<SemanticSearchResult>> SearchAsync(string query, int topK = 10, float minSimilarity = 0.0f)
    {
        if (!_settings.EnableEmbeddings)
        {
            throw new InvalidOperationException(
                "Embeddings are not enabled. Set LlmProvider:EnableEmbeddings to true in appsettings.json");
        }

        // Generate embedding for the query
        var queryEmbedding = await _llmProvider.GenerateEmbedding(query, _settings.EmbeddingModel);

        // Get all members with embeddings from database
        var members = await _dbContext.Members
            .Include(m => m.File)
            .Where(m => m.Embedding != null)
            .ToListAsync();

        // Calculate cosine similarity for each member
        var results = new List<SemanticSearchResult>();
        foreach (var member in members)
        {
            if (member.Embedding == null)
                continue;

            var memberEmbedding = FileProcessorService.BytesToFloatArray(member.Embedding);
            var similarity = CosineSimilarity(queryEmbedding, memberEmbedding);

            if (similarity >= minSimilarity)
            {
                results.Add(new SemanticSearchResult
                {
                    Member = member,
                    Similarity = similarity,
                    FilePath = member.File.FileName
                });
            }
        }

        // Sort by similarity (descending) and take top K
        return results
            .OrderByDescending(r => r.Similarity)
            .Take(topK)
            .ToList();
    }

    /// <summary>
    /// Performs semantic search with optional tag filtering.
    /// </summary>
    /// <param name="query">The search query</param>
    /// <param name="requiredTags">Tags that members must have (e.g., ["public", "async"])</param>
    /// <param name="topK">Number of results to return (default: 10)</param>
    /// <param name="minSimilarity">Minimum similarity threshold (0.0 to 1.0, default: 0.0)</param>
    /// <returns>List of search results ordered by relevance</returns>
    public async Task<List<SemanticSearchResult>> SearchWithTagsAsync(
        string query,
        List<string> requiredTags,
        int topK = 10,
        float minSimilarity = 0.0f)
    {
        if (!_settings.EnableEmbeddings)
        {
            throw new InvalidOperationException(
                "Embeddings are not enabled. Set LlmProvider:EnableEmbeddings to true in appsettings.json");
        }

        // Generate embedding for the query
        var queryEmbedding = await _llmProvider.GenerateEmbedding(query, _settings.EmbeddingModel);

        // Get members with embeddings and load their tags
        var members = await _dbContext.Members
            .Include(m => m.File)
            .Include(m => m.MemberTags)
            .ThenInclude(mt => mt.Tag)
            .Where(m => m.Embedding != null)
            .ToListAsync();

        // Filter by required tags if specified
        if (requiredTags.Any())
        {
            members = members.Where(m =>
            {
                var memberTagNames = m.MemberTags.Select(mt => mt.Tag.Name.ToLower()).ToHashSet();
                return requiredTags.All(tag => memberTagNames.Contains(tag.ToLower()));
            }).ToList();
        }

        // Calculate cosine similarity for filtered members
        var results = new List<SemanticSearchResult>();
        foreach (var member in members)
        {
            if (member.Embedding == null)
                continue;

            var memberEmbedding = FileProcessorService.BytesToFloatArray(member.Embedding);
            var similarity = CosineSimilarity(queryEmbedding, memberEmbedding);

            if (similarity >= minSimilarity)
            {
                results.Add(new SemanticSearchResult
                {
                    Member = member,
                    Similarity = similarity,
                    FilePath = member.File.FileName,
                    Tags = member.MemberTags.Select(mt => $"{mt.Tag.Category}:{mt.Tag.Name}").ToList()
                });
            }
        }

        // Sort by similarity (descending) and take top K
        return results
            .OrderByDescending(r => r.Similarity)
            .Take(topK)
            .ToList();
    }

    /// <summary>
    /// Finds code members similar to a given member.
    /// </summary>
    /// <param name="memberId">The ID of the member to find similar members for</param>
    /// <param name="topK">Number of results to return (default: 10)</param>
    /// <param name="minSimilarity">Minimum similarity threshold (0.0 to 1.0, default: 0.5)</param>
    /// <returns>List of similar members ordered by relevance</returns>
    public async Task<List<SemanticSearchResult>> FindSimilarMembersAsync(int memberId, int topK = 10, float minSimilarity = 0.5f)
    {
        if (!_settings.EnableEmbeddings)
        {
            throw new InvalidOperationException(
                "Embeddings are not enabled. Set LlmProvider:EnableEmbeddings to true in appsettings.json");
        }

        // Get the source member
        var sourceMember = await _dbContext.Members
            .Include(m => m.File)
            .FirstOrDefaultAsync(m => m.Id == memberId);

        if (sourceMember?.Embedding == null)
        {
            throw new InvalidOperationException($"Member with ID {memberId} not found or has no embedding");
        }

        var sourceEmbedding = FileProcessorService.BytesToFloatArray(sourceMember.Embedding);

        // Get all other members with embeddings
        var members = await _dbContext.Members
            .Include(m => m.File)
            .Where(m => m.Embedding != null && m.Id != memberId)
            .ToListAsync();

        // Calculate cosine similarity
        var results = new List<SemanticSearchResult>();
        foreach (var member in members)
        {
            if (member.Embedding == null)
                continue;

            var memberEmbedding = FileProcessorService.BytesToFloatArray(member.Embedding);
            var similarity = CosineSimilarity(sourceEmbedding, memberEmbedding);

            if (similarity >= minSimilarity)
            {
                results.Add(new SemanticSearchResult
                {
                    Member = member,
                    Similarity = similarity,
                    FilePath = member.File.FileName
                });
            }
        }

        // Sort by similarity (descending) and take top K
        return results
            .OrderByDescending(r => r.Similarity)
            .Take(topK)
            .ToList();
    }

    /// <summary>
    /// Calculates the cosine similarity between two embedding vectors.
    /// </summary>
    /// <param name="a">First embedding vector</param>
    /// <param name="b">Second embedding vector</param>
    /// <returns>Similarity score between 0 and 1 (1 = identical, 0 = orthogonal)</returns>
    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("Embedding vectors must have the same length");
        }

        float dotProduct = 0;
        float magnitudeA = 0;
        float magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = (float)Math.Sqrt(magnitudeA);
        magnitudeB = (float)Math.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
        {
            return 0;
        }

        return dotProduct / (magnitudeA * magnitudeB);
    }
}

/// <summary>
/// Represents a semantic search result with similarity score.
/// </summary>
public class SemanticSearchResult
{
    /// <summary>
    /// Gets or sets the code member.
    /// </summary>
    public MemberEntity Member { get; set; } = null!;

    /// <summary>
    /// Gets or sets the cosine similarity score (0.0 to 1.0).
    /// </summary>
    public float Similarity { get; set; }

    /// <summary>
    /// Gets or sets the file path of the member.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tags associated with the member (optional).
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Returns a formatted string representation of the search result.
    /// </summary>
    public override string ToString()
    {
        var tagString = Tags.Any() ? $" [{string.Join(", ", Tags)}]" : "";
        return $"[{Similarity:F4}] {Member.Type}: {Member.Name} in {FilePath}{tagString}\n  {Member.Summary}";
    }
}
