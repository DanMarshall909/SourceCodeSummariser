namespace SourceCodeSummariser;

/// <summary>
/// Represents a unique tag that can be associated with code members.
/// Tags are stored without duplication to normalize the database.
/// </summary>
public class TagEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for the tag.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the tag name (e.g., "async", "public", "static", "void").
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the tag category (e.g., "modifier", "return-type", "keyword", "parameter-type").
    /// </summary>
    public string Category { get; set; } = null!;

    /// <summary>
    /// Gets or sets the collection of member-tag associations.
    /// </summary>
    public ICollection<MemberTagEntity> MemberTags { get; set; } = new List<MemberTagEntity>();
}
