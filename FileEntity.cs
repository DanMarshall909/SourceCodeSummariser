namespace SourceCodeSummariser;

/// <summary>
/// Represents a source code file in the database.
/// </summary>
public class FileEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for the file.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the file.
    /// </summary>
    public string FileName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the collection of code members contained in this file.
    /// </summary>
    public List<MemberEntity> Members { get; set; } = new List<MemberEntity>();
}