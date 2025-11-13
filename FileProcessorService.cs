using Microsoft.EntityFrameworkCore;

namespace SourceCodeSummariser;

/// <summary>
/// Service for processing source files and managing summaries in the database.
/// </summary>
public class FileProcessorService
{
    private readonly SummaryContext _dbContext;
    private readonly SummarizerService _summarizerService;
    private readonly ILlmProvider _llmProvider;
    private readonly LlmProviderSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileProcessorService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context for storing summaries.</param>
    /// <param name="summarizerService">The summarizer service for generating summaries.</param>
    /// <param name="llmProvider">The LLM provider for generating embeddings.</param>
    /// <param name="settings">The LLM provider settings.</param>
    public FileProcessorService(
        SummaryContext dbContext,
        SummarizerService summarizerService,
        ILlmProvider llmProvider,
        LlmProviderSettings settings)
    {
        _dbContext = dbContext;
        _summarizerService = summarizerService;
        _llmProvider = llmProvider;
        _settings = settings;
    }

    /// <summary>
    /// Processes a source file, generates summaries, and tracks changes in the database.
    /// </summary>
    /// <param name="filePath">The path to the source file to process.</param>
    /// <returns>A list of tuples containing method signatures and their old/new summaries for changed members.</returns>
    public async Task<List<(string MethodSignature, string OldSummary, string NewSummary)>> ProcessFile(string filePath)
    {
        string content = await System.IO.File.ReadAllTextAsync(filePath);
        var fileSummary = await _summarizerService.GenerateFileSummary(filePath, content);

        var changes = new List<(string MethodSignature, string OldSummary, string NewSummary)>();

        var fileEntity = _dbContext.Files.Include(f => f.Members)
            .FirstOrDefault(f => f.FileName == fileSummary.FileName);
        if (fileEntity == null)
        {
            fileEntity = new FileEntity { FileName = fileSummary.FileName };
            _dbContext.Files.Add(fileEntity);
            await _dbContext.SaveChangesAsync();
        }

        foreach (var memberSummary in fileSummary.Members)
        {
            string memberHash = _summarizerService.ComputeHash(memberSummary);
            var existingMember = _dbContext.Members
                .FirstOrDefault(m => m.Hash == memberHash && m.FileEntityId == fileEntity.Id);

            string methodSignature = memberSummary.Split('-')[0].Trim();
            string newSummary = memberSummary;

            if (existingMember == null)
            {
                // Generate embedding for new member
                var embedding = await GenerateEmbeddingAsync(memberSummary);

                var newMember = new MemberEntity
                {
                    Name = memberSummary.Split(':')[1].Trim(),
                    Type = memberSummary.Split(':')[0].Trim(),
                    Summary = memberSummary,
                    Hash = memberHash,
                    Embedding = embedding,
                    File = fileEntity
                };
                _dbContext.Members.Add(newMember);
                await _dbContext.SaveChangesAsync();
                changes.Add((methodSignature, string.Empty, newSummary));
            }
            else if (existingMember.Summary != newSummary)
            {
                // Generate new embedding for updated member
                var embedding = await GenerateEmbeddingAsync(memberSummary);

                changes.Add((methodSignature, existingMember.Summary, newSummary));
                existingMember.Summary = newSummary;
                existingMember.Hash = memberHash;
                existingMember.Embedding = embedding;
                await _dbContext.SaveChangesAsync();
            }
        }

        return changes;
    }

    /// <summary>
    /// Generates an embedding vector for the given text if embeddings are enabled.
    /// </summary>
    /// <param name="text">The text to embed</param>
    /// <returns>The embedding as a byte array, or null if embeddings are disabled or generation fails</returns>
    private async Task<byte[]?> GenerateEmbeddingAsync(string text)
    {
        if (!_settings.EnableEmbeddings)
        {
            return null;
        }

        try
        {
            var embedding = await _llmProvider.GenerateEmbedding(text, _settings.EmbeddingModel);
            return FloatArrayToBytes(embedding);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    WARNING: Failed to generate embedding: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Converts a float array to a byte array for database storage.
    /// </summary>
    /// <param name="floats">The float array to convert</param>
    /// <returns>The byte array representation</returns>
    private static byte[] FloatArrayToBytes(float[] floats)
    {
        byte[] bytes = new byte[floats.Length * sizeof(float)];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>
    /// Converts a byte array back to a float array for similarity calculations.
    /// </summary>
    /// <param name="bytes">The byte array to convert</param>
    /// <returns>The float array representation</returns>
    public static float[] BytesToFloatArray(byte[] bytes)
    {
        float[] floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }
}