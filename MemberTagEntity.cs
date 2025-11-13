namespace SourceCodeSummariser;

/// <summary>
/// Represents the many-to-many relationship between code members and tags.
/// This join table allows a member to have multiple tags and a tag to be associated with multiple members.
/// </summary>
public class MemberTagEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for this member-tag association.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the foreign key to the member.
    /// </summary>
    public int MemberId { get; set; }

    /// <summary>
    /// Gets or sets the member associated with this tag.
    /// </summary>
    public MemberEntity Member { get; set; } = null!;

    /// <summary>
    /// Gets or sets the foreign key to the tag.
    /// </summary>
    public int TagId { get; set; }

    /// <summary>
    /// Gets or sets the tag associated with this member.
    /// </summary>
    public TagEntity Tag { get; set; } = null!;
}
