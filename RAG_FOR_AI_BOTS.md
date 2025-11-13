# RAG for AI Coding Bots

This document explains how to use the embedding system as a **Retrieval-Augmented Generation (RAG)** context provider for AI coding assistants and bots.

## Overview

The embedding system provides **semantic code search** that enables AI bots to find the most relevant code examples and context before generating code or answering questions. This dramatically improves code generation quality and consistency.

## Why RAG for Code?

### The Problem

AI coding bots face several challenges:

1. **Limited Context Window**: Can't fit entire codebase in prompt
2. **Irrelevant Context**: Including random code wastes tokens
3. **Pattern Inconsistency**: Generated code doesn't match existing patterns
4. **Knowledge Gaps**: Bot doesn't know about project-specific conventions

### The Solution: RAG with Embeddings

```
User Request → Semantic Search → Relevant Code Examples → LLM → Generated Code
```

**Benefits:**
- ✅ Find conceptually related code, not just keyword matches
- ✅ Respect existing architecture and patterns
- ✅ Stay within context limits with only relevant examples
- ✅ 250x more cost-efficient than including full codebase

## Use Cases

### 1. Code Generation with Context

**Scenario:** Generate new code that follows existing patterns

```csharp
// User: "Add authentication to the UserController"

// Step 1: Find relevant authentication examples
var authExamples = await searchService.SearchAsync(
    "authentication authorization security JWT validation",
    topK: 5,
    minSimilarity: 0.65f
);

// Step 2: Build prompt with relevant context
var prompt = BuildPromptWithContext(
    task: "Add authentication to UserController",
    examples: authExamples
);

// Step 3: Generate code with LLM
var generatedCode = await llm.Generate(prompt);
```

**Result:** Generated code follows existing auth patterns in the codebase.

### 2. Architectural Consistency

**Scenario:** Ensure new code matches existing architecture

```csharp
// User: "Create a new ProductRepository"

// Find existing repository patterns
var repoPatterns = await searchService.SearchWithTagsAsync(
    "repository pattern CRUD database operations",
    requiredTags: new List<string> { "public" },
    topK: 3,
    minSimilarity: 0.75f
);

// Generate following existing patterns
```

**Result:** New repository follows exact same structure as existing ones.

### 3. Similar Code Detection

**Scenario:** Find and suggest similar existing implementations

```csharp
// User starts typing a new error handler

// Find similar error handling code
var similarCode = await searchService.SearchAsync(
    "error handling exception logging retry",
    topK: 5
);

// Suggest: "Similar code exists in ErrorHandler.cs, consider reusing"
```

**Result:** Reduces code duplication, suggests refactoring opportunities.

### 4. Question Answering

**Scenario:** Answer questions about the codebase

```csharp
// User: "How do we handle database transactions?"

var examples = await searchService.SearchAsync(
    "database transaction commit rollback",
    topK: 5,
    minSimilarity: 0.6f
);

var answer = GenerateAnswer(question, examples);
```

**Result:** Accurate answers based on actual codebase, not hallucinations.

### 5. Code Review Assistant

**Scenario:** Check if PR follows existing patterns

```csharp
// Analyzing PR with new error handling code

var similarMembers = await searchService.FindSimilarMembersAsync(
    newMethodId,
    topK: 5,
    minSimilarity: 0.7f
);

if (similarMembers.Any())
{
    Console.WriteLine("✓ Follows existing error handling patterns");
}
else
{
    Console.WriteLine("⚠️ Deviates from existing patterns");
    ShowPreferredPatterns(similarMembers);
}
```

**Result:** Automated pattern compliance checking.

### 6. Documentation Generation

**Scenario:** Generate docs based on actual implementations

```csharp
// Generate "How to add a new service" guide

var serviceExamples = await searchService.SearchWithTagsAsync(
    "service dependency injection constructor",
    requiredTags: new List<string> { "public" },
    topK: 10
);

var guide = GenerateDocumentation(serviceExamples);
```

**Result:** Up-to-date documentation reflecting actual code patterns.

## API Reference

### Basic Search for Context

```csharp
// Find relevant code members by semantic meaning
var results = await searchService.SearchAsync(
    query: "async methods that handle file operations",
    topK: 10,              // Number of results
    minSimilarity: 0.6f    // Similarity threshold (0.0-1.0)
);

foreach (var result in results)
{
    Console.WriteLine($"Similarity: {result.Similarity:F4}");
    Console.WriteLine($"Member: {result.Member.Name}");
    Console.WriteLine($"Summary: {result.Member.Summary}");
    Console.WriteLine($"File: {result.FilePath}");
}
```

### Search with Tag Filtering

```csharp
// Find public async methods related to authentication
var results = await searchService.SearchWithTagsAsync(
    query: "authentication validation",
    requiredTags: new List<string> { "public", "async" },
    topK: 5,
    minSimilarity: 0.65f
);
```

### Find Similar Code

```csharp
// Find code similar to a specific member
var similarCode = await searchService.FindSimilarMembersAsync(
    memberId: 42,          // ID of the reference member
    topK: 10,
    minSimilarity: 0.7f    // Higher threshold for similar code
);
```

## Prompt Engineering Patterns

### Pattern 1: Examples-First Prompting

```csharp
public string BuildExamplesFirstPrompt(string task, List<SemanticSearchResult> examples)
{
    var prompt = "Here are examples of similar code in our codebase:\n\n";

    foreach (var example in examples)
    {
        prompt += $"## Example from {example.FilePath}\n";
        prompt += $"```csharp\n// {example.Member.Type}: {example.Member.Name}\n";
        prompt += $"// {example.Member.Summary}\n```\n\n";
    }

    prompt += $"Now, following these patterns, {task}\n";

    return prompt;
}
```

**When to use:** Code generation that should match existing patterns.

### Pattern 2: Architecture-Guided Prompting

```csharp
public string BuildArchitecturePrompt(string task, List<SemanticSearchResult> examples)
{
    var patterns = examples
        .GroupBy(e => ExtractPattern(e.Member.Summary))
        .Select(g => g.Key);

    var prompt = "Our codebase follows these patterns:\n";
    foreach (var pattern in patterns)
    {
        prompt += $"- {pattern}\n";
    }

    prompt += $"\nImplement: {task}\n";
    prompt += "Follow the patterns listed above.\n";

    return prompt;
}
```

**When to use:** Ensuring architectural consistency.

### Pattern 3: Context-Aware Completion

```csharp
public string BuildCompletionPrompt(
    string currentFile,
    string currentCode,
    List<SemanticSearchResult> relatedCode)
{
    var prompt = $"Current file: {currentFile}\n\n";
    prompt += $"Current code:\n```csharp\n{currentCode}\n```\n\n";
    prompt += "Related code in the codebase:\n";

    foreach (var related in relatedCode)
    {
        prompt += $"- {related.Member.Name} in {related.FilePath}\n";
        prompt += $"  {related.Member.Summary}\n\n";
    }

    prompt += "Complete the current code following the patterns from related code.\n";

    return prompt;
}
```

**When to use:** IDE autocomplete, code completion tools.

### Pattern 4: Anti-Pattern Detection

```csharp
public async Task<string> DetectAntiPatterns(int memberId)
{
    // Find similar code
    var similar = await searchService.FindSimilarMembersAsync(memberId, topK: 10);

    // Find different implementations
    var different = similar.Where(s => s.Similarity < 0.5f);

    var prompt = "This code is implemented differently from most of the codebase.\n\n";
    prompt += "Preferred patterns:\n";
    foreach (var pref in similar.Take(3))
    {
        prompt += $"- {pref.Member.Name}: {pref.Member.Summary}\n";
    }

    prompt += "\nSuggest refactoring to match preferred patterns.\n";

    return prompt;
}
```

**When to use:** Code review, refactoring suggestions.

## Integration Examples

### Example 1: GitHub Copilot-Style Context

```csharp
public class ContextAwareCompletionService
{
    private readonly SemanticSearchService _searchService;
    private readonly ILlmProvider _llm;

    public async Task<string> GetCompletion(
        string filePath,
        string currentCode,
        int cursorPosition)
    {
        // Extract intent from current code
        var intent = ExtractIntent(currentCode, cursorPosition);

        // Find relevant context
        var context = await _searchService.SearchAsync(
            intent,
            topK: 5,
            minSimilarity: 0.65f
        );

        // Build prompt
        var prompt = BuildCompletionPrompt(filePath, currentCode, context);

        // Generate completion
        var completion = await _llm.GenerateSummary(
            code: currentCode,
            systemPrompt: "You are a code completion assistant.",
            userPrompt: prompt,
            maxTokens: 200
        );

        return completion;
    }
}
```

### Example 2: Code Review Bot

```csharp
public class CodeReviewBot
{
    private readonly SemanticSearchService _searchService;

    public async Task<ReviewResult> ReviewCode(
        string fileName,
        string newCode,
        string changeDescription)
    {
        // Find similar implementations
        var similar = await _searchService.SearchAsync(
            changeDescription,
            topK: 10,
            minSimilarity: 0.6f
        );

        var review = new ReviewResult();

        // Check consistency
        if (similar.Any(s => s.Similarity > 0.8f))
        {
            review.AddComment(
                "✓ Implementation follows existing patterns",
                Severity.Info
            );
        }
        else
        {
            review.AddComment(
                "⚠️ Consider following existing patterns:\n" +
                string.Join("\n", similar.Take(3)
                    .Select(s => $"  - {s.Member.Name} in {s.FilePath}")),
                Severity.Warning
            );
        }

        // Check for duplication
        var duplicates = similar.Where(s => s.Similarity > 0.95f);
        if (duplicates.Any())
        {
            review.AddComment(
                "⚠️ Very similar code already exists - consider refactoring",
                Severity.Warning
            );
        }

        return review;
    }
}
```

### Example 3: Chatbot Q&A

```csharp
public class CodebaseChatbot
{
    private readonly SemanticSearchService _searchService;
    private readonly ILlmProvider _llm;

    public async Task<string> Answer(string question)
    {
        // Find relevant code
        var relevantCode = await _searchService.SearchAsync(
            question,
            topK: 5,
            minSimilarity: 0.5f
        );

        if (!relevantCode.Any())
        {
            return "I couldn't find relevant code for that question.";
        }

        // Build context-aware prompt
        var context = string.Join("\n\n", relevantCode.Select(r =>
            $"File: {r.FilePath}\n" +
            $"Code: {r.Member.Type} {r.Member.Name}\n" +
            $"Description: {r.Member.Summary}"
        ));

        var prompt = $@"
Based on this code from the codebase:

{context}

Answer this question: {question}

Provide a clear, accurate answer based on the actual code shown above.
";

        var answer = await _llm.GenerateSummary(
            code: "",
            systemPrompt: "You are a codebase assistant. Answer based only on the provided code.",
            userPrompt: prompt,
            maxTokens: 300
        );

        return answer;
    }
}
```

### Example 4: Documentation Generator

```csharp
public class DocumentationGenerator
{
    private readonly SemanticSearchService _searchService;
    private readonly ILlmProvider _llm;

    public async Task<string> GenerateGuide(string topic)
    {
        // Find relevant examples
        var examples = await _searchService.SearchAsync(
            topic,
            topK: 10,
            minSimilarity: 0.6f
        );

        // Group by file/module
        var byFile = examples.GroupBy(e => e.FilePath);

        var guide = $"# {topic} - Implementation Guide\n\n";
        guide += "This guide is generated from actual code in the repository.\n\n";

        foreach (var fileGroup in byFile)
        {
            guide += $"## {fileGroup.Key}\n\n";
            foreach (var example in fileGroup)
            {
                guide += $"### {example.Member.Name}\n";
                guide += $"{example.Member.Summary}\n\n";
            }
        }

        // Use LLM to add explanations
        var enhancedGuide = await _llm.GenerateSummary(
            code: guide,
            systemPrompt: "You are a technical writer.",
            userPrompt: "Add explanations and usage examples to this guide:",
            maxTokens: 500
        );

        return enhancedGuide;
    }
}
```

## Performance Optimization

### Context Window Management

```csharp
public class ContextWindowManager
{
    private const int MAX_TOKENS = 4000; // Example limit

    public List<SemanticSearchResult> OptimizeContext(
        List<SemanticSearchResult> results,
        int maxTokens = MAX_TOKENS)
    {
        var optimized = new List<SemanticSearchResult>();
        int currentTokens = 0;

        // Sort by similarity (most relevant first)
        var sorted = results.OrderByDescending(r => r.Similarity);

        foreach (var result in sorted)
        {
            // Estimate tokens (rough: 1 token ≈ 4 characters)
            int estimatedTokens = result.Member.Summary.Length / 4;

            if (currentTokens + estimatedTokens > maxTokens)
            {
                break; // Stop when we hit the limit
            }

            optimized.Add(result);
            currentTokens += estimatedTokens;
        }

        return optimized;
    }
}
```

### Caching Strategy

```csharp
public class CachedSearchService
{
    private readonly SemanticSearchService _searchService;
    private readonly IMemoryCache _cache;

    public async Task<List<SemanticSearchResult>> SearchWithCache(
        string query,
        int topK = 10,
        float minSimilarity = 0.6f)
    {
        // Create cache key from query parameters
        var cacheKey = $"search:{query}:{topK}:{minSimilarity}";

        // Try to get from cache
        if (_cache.TryGetValue(cacheKey, out List<SemanticSearchResult> cached))
        {
            return cached;
        }

        // Search if not cached
        var results = await _searchService.SearchAsync(query, topK, minSimilarity);

        // Cache for 5 minutes
        _cache.Set(cacheKey, results, TimeSpan.FromMinutes(5));

        return results;
    }
}
```

### Hybrid Search (Embeddings + Keywords + Tags)

```csharp
public async Task<List<SemanticSearchResult>> HybridSearch(
    string query,
    List<string>? keywords = null,
    List<string>? tags = null)
{
    // Stage 1: Filter by tags (fast, structural)
    var candidates = await GetCandidatesByTags(tags);

    // Stage 2: Filter by keywords (fast, exact match)
    if (keywords != null)
    {
        candidates = candidates.Where(c =>
            keywords.Any(k => c.Member.Summary.Contains(k, StringComparison.OrdinalIgnoreCase))
        ).ToList();
    }

    // Stage 3: Rank by semantic similarity (slower, but on smaller set)
    var embeddings = await GenerateEmbeddings(query);
    var ranked = RankBySimilarity(candidates, embeddings);

    return ranked;
}
```

## Cost Analysis

### Token Efficiency Comparison

**Without RAG (Include entire codebase):**
```
Codebase: 100 files × 5,000 tokens = 500,000 tokens
Cost per request (GPT-4): $15.00
Total context used: 500,000 tokens
Relevant code included: ~1%
```

**With RAG (Your embedding system):**
```
Search: <1 second, negligible cost
Top 5 relevant members: ~2,000 tokens
Cost per request (GPT-4): $0.06
Total context used: 2,000 tokens
Relevant code included: ~100%

Efficiency gain: 250x
Cost savings: 99.6%
```

### Embedding Costs

**OpenAI Embeddings:**
- `text-embedding-ada-002`: $0.0001 per 1K tokens
- Average summary: 50 tokens = $0.000005 per member
- 10,000 members: **$0.05 total**

**Ollama (Local):**
- `mxbai-embed-large`: **Free**
- No API costs
- Runs offline

## Best Practices

### 1. Similarity Threshold Guidelines

```
0.9-1.0  → Nearly identical (potential duplicates)
0.8-0.9  → Very similar (same pattern/purpose)
0.7-0.8  → Related (similar domain/functionality)
0.6-0.7  → Somewhat related
0.5-0.6  → Loosely related
< 0.5    → Different topics
```

**Recommendations:**
- **Code generation**: 0.65-0.75 (similar patterns)
- **Duplicate detection**: 0.85+ (very similar)
- **Related code discovery**: 0.60-0.70 (broader search)
- **Architecture consistency**: 0.75+ (strict matching)

### 2. Context Size Guidelines

```
Simple tasks:     3-5 examples
Complex tasks:    5-10 examples
Architectural:    10-15 examples
Documentation:    15-20+ examples
```

### 3. Tag Filtering Strategy

```csharp
// Use tags to pre-filter, embeddings to rank
var results = await searchService.SearchWithTagsAsync(
    "database operations",
    requiredTags: new List<string> { "public", "async" },  // Structural filter
    topK: 10,
    minSimilarity: 0.6f  // Semantic ranking
);
```

### 4. Multi-Stage Retrieval

```csharp
// Stage 1: Broad search for candidates
var candidates = await searchService.SearchAsync(
    query,
    topK: 20,
    minSimilarity: 0.5f
);

// Stage 2: Re-rank by specific criteria
var reranked = RerankByRecency(candidates);
var final = reranked.Take(5);
```

## Troubleshooting

### Issue: Generated code doesn't match patterns

**Solution:** Increase similarity threshold and reduce topK
```csharp
var context = await searchService.SearchAsync(
    query,
    topK: 3,              // Fewer, more relevant examples
    minSimilarity: 0.75f  // Higher threshold
);
```

### Issue: No relevant results found

**Solution:** Lower threshold and broaden search
```csharp
var context = await searchService.SearchAsync(
    query,
    topK: 10,
    minSimilarity: 0.5f  // Lower threshold
);
```

### Issue: Too much irrelevant context

**Solution:** Use tag filtering
```csharp
var context = await searchService.SearchWithTagsAsync(
    query,
    requiredTags: new List<string> { "public" },  // Filter first
    topK: 5,
    minSimilarity: 0.7f
);
```

### Issue: Slow search performance

**Solution:** Implement caching and hybrid search
```csharp
// Cache frequent queries
var cached = await cachedSearchService.SearchWithCache(query);

// Or use hybrid search (tags + embeddings)
var hybrid = await HybridSearch(query, tags: new[] { "public" });
```

## Future Enhancements

### 1. Vector Database Integration

For large codebases (>10,000 members):

```csharp
// Current: Brute-force O(n) similarity
// Future: ANN search with Qdrant/Pinecone O(log n)

public class VectorDbSearchService
{
    private readonly IVectorDatabase _vectorDb;

    public async Task<List<SearchResult>> SearchAsync(string query)
    {
        var queryEmbedding = await GenerateEmbedding(query);
        var results = await _vectorDb.SearchAsync(
            queryEmbedding,
            topK: 10,
            metric: "cosine"
        );
        return results;
    }
}
```

### 2. Code Graph Integration

Combine embeddings with code structure:

```csharp
// Find related code using both semantics and dependencies
var semanticallySimilar = await searchService.SearchAsync(query);
var dependencies = await codeGraph.GetDependencies(memberId);
var combined = Merge(semanticallySimilar, dependencies);
```

### 3. Usage Analytics

Track which context leads to best results:

```csharp
public class ContextAnalytics
{
    public async Task TrackUsage(
        string query,
        List<SemanticSearchResult> context,
        string generatedCode,
        bool wasAccepted)
    {
        // Track effectiveness of different similarity thresholds
        // Learn optimal topK for different types of queries
        // Improve search strategy over time
    }
}
```

## References

- [Semantic Search Service](SemanticSearchService.cs)
- [Embedding Documentation](EMBEDDINGS.md)
- [OpenAI Embeddings Guide](https://platform.openai.com/docs/guides/embeddings)
- [RAG Pattern Overview](https://arxiv.org/abs/2005.11401)
