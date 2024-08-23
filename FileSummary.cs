namespace SourceCodeSummariser
{
    public class FileSummary
    {
        public string FileName { get; set; } = null!;
        public List<string> Members { get; set; } = new List<string>();
    }
}