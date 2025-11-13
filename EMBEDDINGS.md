# Embedding-Based Semantic Search

This document explains how to use the vector embedding features for semantic code search.

## Overview

The embedding system generates AI-powered vector representations of your code summaries, enabling semantic search capabilities. Instead of keyword matching, you can search by meaning and intent.

## Features

- **Semantic Search**: Find code by meaning, not just keywords
- **Multi-Provider Support**: Works with OpenAI, LangChain, and local models (Ollama)
- **Tag Filtering**: Combine semantic search with structured tag filters
- **Similarity Detection**: Find similar code members automatically
- **Cosine Similarity**: Industry-standard vector similarity scoring

## Configuration

### Enable Embeddings

Add to `appsettings.json`:

```json
{
  "LlmProvider": {
    "Provider": "OpenAI",
    "ApiKey": "your-api-key",
    "Model": "gpt-3.5-turbo",
    "MaxTokens": 50,
    "EnableEmbeddings": true,
    "EmbeddingModel": "text-embedding-ada-002"
  }
}
```

### Configuration Options

| Setting | Default | Description |
|---------|---------|-------------|
| `EnableEmbeddings` | `true` | Enable/disable embedding generation |
| `EmbeddingModel` | `text-embedding-ada-002` | Model for generating embeddings |

### Supported Embedding Models

**OpenAI:**
- `text-embedding-ada-002` (1536 dimensions, $0.0001/1K tokens)
- `text-embedding-3-small` (512-1536 dimensions, $0.00002/1K tokens)
- `text-embedding-3-large` (256-3072 dimensions, $0.00013/1K tokens)

**Ollama (Local):**
- `mxbai-embed-large` (1024 dimensions, free)
- `nomic-embed-text` (768 dimensions, free)
- `all-minilm` (384 dimensions, free)

## Usage

### 1. Generate Embeddings

Run the summarizer with embeddings enabled:

```bash
# One-time processing
dotnet run /path/to/code

# Watch mode (continuous)
dotnet run /path/to/code --watch
```

Embeddings are generated automatically for each code member during summarization.

### 2. Semantic Search

Use the search tool to query your codebase:

```bash
# Basic search
dotnet run --project SearchProgram.csproj -- "async file operations"

# Limit results
dotnet run --project SearchProgram.csproj -- "database queries" -k 5

# Set similarity threshold (0.0-1.0)
dotnet run --project SearchProgram.csproj -- "error handling" -s 0.7

# Filter by tags
dotnet run --project SearchProgram.csproj -- "API endpoints" -t public,async

# Find similar code
dotnet run --project SearchProgram.csproj -- --similar-to 42
```

### Command-Line Options

| Option | Short | Description |
|--------|-------|-------------|
| `--top <n>` | `-k` | Number of results (default: 10) |
| `--min-similarity <f>` | `-s` | Minimum similarity 0.0-1.0 (default: 0.0) |
| `--tags <list>` | `-t` | Comma-separated tag filter |
| `--similar-to <id>` | `-m` | Find code similar to member ID |
| `--help` | `-h` | Show help |

## Programmatic Usage

### Search Service

```csharp
using SourceCodeSummariser;

// Initialize
var dbContext = new SummaryContext("Data Source=summaries.db");
var llmProvider = new OpenAIProvider(httpClient, settings);
var searchService = new SemanticSearchService(
    dbContext,
    llmProvider,
    settings.LlmProvider
);

// Basic search
var results = await searchService.SearchAsync(
    query: "async methods that handle files",
    topK: 10,
    minSimilarity: 0.5f
);

// Search with tag filtering
var filteredResults = await searchService.SearchWithTagsAsync(
    query: "database operations",
    requiredTags: new List<string> { "public", "async" },
    topK: 10,
    minSimilarity: 0.6f
);

// Find similar members
var similarMembers = await searchService.FindSimilarMembersAsync(
    memberId: 42,
    topK: 10,
    minSimilarity: 0.7f
);

// Process results
foreach (var result in results)
{
    Console.WriteLine($"Similarity: {result.Similarity:F4}");
    Console.WriteLine($"Member: {result.Member.Type} {result.Member.Name}");
    Console.WriteLine($"File: {result.FilePath}");
    Console.WriteLine($"Summary: {result.Member.Summary}");
    Console.WriteLine();
}
```

## How It Works

### 1. Embedding Generation

When you process code files:
1. Code is analyzed and summarized using an LLM
2. Summary text is converted to a vector embedding (e.g., 1536 floats for OpenAI)
3. Embedding is stored in the database as a BLOB

### 2. Semantic Search

When you search:
1. Your query is converted to an embedding using the same model
2. Cosine similarity is calculated between query and all member embeddings
3. Results are ranked by similarity score (0.0 = unrelated, 1.0 = identical)
4. Top K most similar results are returned

### 3. Cosine Similarity

```
similarity = (A · B) / (||A|| * ||B||)
```

Where:
- `A · B` = dot product of vectors
- `||A||` = magnitude of vector A
- Result ranges from 0.0 (orthogonal) to 1.0 (identical)

## Performance Considerations

### Database Size

Embeddings increase database size:
- OpenAI ada-002: ~6KB per member (1536 floats × 4 bytes)
- 1000 members: ~6MB
- 10,000 members: ~60MB

### Search Performance

Current implementation:
- **Algorithm**: Brute-force cosine similarity
- **Complexity**: O(n) where n = number of members
- **Typical performance**:
  - <1,000 members: <100ms
  - 1,000-10,000 members: 100ms-1s
  - >10,000 members: >1s

### Future Optimizations

For large codebases (>10,000 members), consider:
1. **Vector databases**: Qdrant, Pinecone, Weaviate
2. **ANN algorithms**: FAISS, HNSW, ScaNN
3. **Indexing**: Reduce search space with tags first

## Cost Optimization

### OpenAI Costs

Embedding generation costs (OpenAI):
- `text-embedding-ada-002`: $0.0001 per 1K tokens
- Average summary: ~50 tokens
- Cost per member: ~$0.000005 (0.0005 cents)
- 10,000 members: ~$0.05

### Cost-Saving Strategies

1. **Use Ollama for development**:
```json
{
  "LlmProvider": {
    "Provider": "Local",
    "Model": "llama2",
    "EmbeddingModel": "mxbai-embed-large",
    "LocalEndpoint": "http://localhost:11434"
  }
}
```

2. **Disable embeddings for testing**:
```json
{
  "LlmProvider": {
    "EnableEmbeddings": false
  }
}
```

3. **Use smaller embedding models**:
```json
{
  "LlmProvider": {
    "EmbeddingModel": "text-embedding-3-small"
  }
}
```

## Examples

### Example 1: Find Async File Operations

```bash
$ dotnet run --project SearchProgram.csproj -- "async methods that read or write files" -k 5 -s 0.6

Found 3 result(s):

1. Similarity: 0.8234 | ID: 142
   Method: ProcessFileAsync
   File: FileProcessorService.cs
   Summary: Asynchronously processes a source file and saves summaries to database

2. Similarity: 0.7891 | ID: 89
   Method: ReadAllTextAsync
   File: FileReader.cs
   Summary: Reads entire file content asynchronously with error handling

3. Similarity: 0.7456 | ID: 203
   Method: WriteEmbeddingAsync
   File: EmbeddingService.cs
   Summary: Writes embedding data to file asynchronously
```

### Example 2: Find Public API Methods

```bash
$ dotnet run --project SearchProgram.csproj -- "user authentication" -t public,async -k 3

Found 2 result(s):

1. Similarity: 0.8567 | ID: 56
   Method: AuthenticateUserAsync
   File: AuthService.cs
   Tags: modifier:public, modifier:async, return-type:Task<bool>
   Summary: Validates user credentials against database and returns authentication result

2. Similarity: 0.7234 | ID: 57
   Method: RegisterUserAsync
   File: AuthService.cs
   Tags: modifier:public, modifier:async, return-type:Task
   Summary: Creates new user account with validation and password hashing
```

### Example 3: Find Similar Code

```bash
$ dotnet run --project SearchProgram.csproj -- --similar-to 142 -k 5

Finding members similar to ID 142...

Found 4 result(s):

1. Similarity: 0.9123 | ID: 143
   Method: ProcessDirectoryAsync
   File: FileProcessorService.cs
   Summary: Recursively processes all files in a directory

2. Similarity: 0.8756 | ID: 89
   Method: ReadAllTextAsync
   File: FileReader.cs
   Summary: Reads entire file content asynchronously with error handling
```

## Troubleshooting

### No Results Found

1. Check if embeddings are enabled: `LlmProvider:EnableEmbeddings = true`
2. Verify embeddings were generated: Check `Members.Embedding` in database
3. Lower similarity threshold: Try `-s 0.0`
4. Use broader search terms

### Slow Search Performance

1. Reduce search space with tag filters: `-t public,async`
2. Increase similarity threshold: `-s 0.7`
3. Reduce result count: `-k 5`

### API Errors

1. Check API key is set: `LlmProvider__ApiKey` environment variable
2. Verify network connectivity
3. Check API rate limits
4. Try local provider: `"Provider": "Local"`

## Advanced Topics

### Custom Embedding Models

Implement `ILlmProvider.GenerateEmbedding()` for custom models:

```csharp
public async Task<float[]> GenerateEmbedding(string text, string? model = null)
{
    // Your custom embedding logic
    return embeddingVector;
}
```

### Vector Storage Optimization

For production, consider external vector databases:

```csharp
// Pseudo-code for vector DB integration
public async Task<List<SearchResult>> SearchAsync(string query)
{
    var queryEmbedding = await _llmProvider.GenerateEmbedding(query);
    var results = await _vectorDb.SearchAsync(queryEmbedding, topK: 10);
    return results;
}
```

## References

- [OpenAI Embeddings Guide](https://platform.openai.com/docs/guides/embeddings)
- [Ollama Embedding Models](https://ollama.ai/library)
- [Cosine Similarity](https://en.wikipedia.org/wiki/Cosine_similarity)
- [Vector Databases Comparison](https://weaviate.io/blog/vector-database-comparison)
