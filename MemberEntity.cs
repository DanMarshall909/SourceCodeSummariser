namespace SourceCodeSummariser;

public class MemberEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Summary { get; set; } = null!;
    public string Hash { get; set; } = null!;
    public FileEntity File { get; set; } = null!;
    public int FileEntityId { get; set; }
}