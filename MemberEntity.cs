namespace SourceCodeSummariser;

/// <summary>
/// Represents a code member (class, method, property, etc.) in the database.
/// </summary>
public class MemberEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for the member.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the code member.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the type of the code member (e.g., "Method", "Class", "Property").
    /// </summary>
    public string Type { get; set; } = null!;

    /// <summary>
    /// Gets or sets the AI-generated summary of the code member.
    /// </summary>
    public string Summary { get; set; } = null!;

    /// <summary>
    /// Gets or sets the SHA256 hash of the summary for change detection.
    /// </summary>
    public string Hash { get; set; } = null!;

    /// <summary>
    /// Gets or sets the parent file that contains this member.
    /// </summary>
    public FileEntity File { get; set; } = null!;

    /// <summary>
    /// Gets or sets the foreign key to the parent file.
    /// </summary>
    public int FileEntityId { get; set; }

    /// <summary>
    /// Gets or sets the collection of tags associated with this member.
    /// </summary>
    public ICollection<MemberTagEntity> MemberTags { get; set; } = new List<MemberTagEntity>();
}