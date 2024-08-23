namespace SourceCodeSummariser;

public class FileEntity
{
    public int Id { get; set; }
    public string FileName { get; set; } = null!;
    public List<MemberEntity> Members { get; set; } = new List<MemberEntity>();
}