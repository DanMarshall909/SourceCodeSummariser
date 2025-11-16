using System.Net.Http.Json;
using System.Text.Json;

namespace SourceCodeSummariser.CLI;

/// <summary>
/// HTTP client for communicating with the SourceCode Analyzer service
/// </summary>
public class ApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiClient(string baseUrl = "http://localhost:5000")
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// Check if the analyzer service is running
    /// </summary>
    public async Task<bool> IsServiceRunningAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Perform semantic code search
    /// </summary>
    public async Task<SearchResponse?> SearchAsync(string query, int topK = 10, double minSimilarity = 0.0)
    {
        var response = await _httpClient.GetAsync($"/api/search?query={Uri.EscapeDataString(query)}&topK={topK}");
        response.EnsureSuccessStatusCode();

        var results = await response.Content.ReadFromJsonAsync<List<SearchResultItem>>(JsonOptions);
        return new SearchResponse
        {
            Results = results?.Where(r => r.Similarity >= minSimilarity).ToList() ?? new List<SearchResultItem>()
        };
    }

    /// <summary>
    /// Search with tag filtering
    /// </summary>
    public async Task<SearchResponse?> SearchWithTagsAsync(string query, string[] tags, int topK = 10)
    {
        var tagsParam = string.Join(",", tags);
        var response = await _httpClient.GetAsync(
            $"/api/search/tags?query={Uri.EscapeDataString(query)}&tags={Uri.EscapeDataString(tagsParam)}&topK={topK}");
        response.EnsureSuccessStatusCode();

        var results = await response.Content.ReadFromJsonAsync<List<SearchResultItem>>(JsonOptions);
        return new SearchResponse { Results = results ?? new List<SearchResultItem>() };
    }

    /// <summary>
    /// Find similar code members
    /// </summary>
    public async Task<SearchResponse?> FindSimilarAsync(int memberId, int topK = 10)
    {
        var response = await _httpClient.GetAsync($"/api/search/similar?memberId={memberId}&topK={topK}");
        response.EnsureSuccessStatusCode();

        var results = await response.Content.ReadFromJsonAsync<List<SearchResultItem>>(JsonOptions);
        return new SearchResponse { Results = results ?? new List<SearchResultItem>() };
    }

    /// <summary>
    /// Get member details by ID
    /// </summary>
    public async Task<MemberDetails?> GetMemberAsync(int id)
    {
        var response = await _httpClient.GetAsync($"/api/members/{id}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MemberDetails>(JsonOptions);
    }

    /// <summary>
    /// List all tags with usage counts
    /// </summary>
    public async Task<List<TagInfo>?> GetTagsAsync()
    {
        var response = await _httpClient.GetAsync("/api/tags");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<TagInfo>>(JsonOptions);
    }

    /// <summary>
    /// Get file summary
    /// </summary>
    public async Task<FileSummary?> GetFileSummaryAsync(string filePath)
    {
        var response = await _httpClient.GetAsync($"/api/files/summary?filePath={Uri.EscapeDataString(filePath)}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FileSummary>(JsonOptions);
    }

    /// <summary>
    /// Process or update a file
    /// </summary>
    public async Task<ProcessFileResponse?> ProcessFileAsync(string filePath)
    {
        var response = await _httpClient.GetAsync($"/api/files/process?filePath={Uri.EscapeDataString(filePath)}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProcessFileResponse>(JsonOptions);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

// Response models
public class SearchResponse
{
    public List<SearchResultItem> Results { get; set; } = new();
}

public class SearchResultItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public double Similarity { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class MemberDetails
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}

public class TagInfo
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class FileSummary
{
    public string FileName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public List<string> MemberTypes { get; set; } = new();
}

public class ProcessFileResponse
{
    public string Message { get; set; } = string.Empty;
    public int ProcessedMembers { get; set; }
    public bool Success { get; set; }
}
