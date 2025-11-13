namespace SourceCodeSummariser
{
    /// <summary>
    /// Data transfer object containing a file's summary information.
    /// </summary>
    public class FileSummary
    {
        /// <summary>
        /// Gets or sets the name of the file.
        /// </summary>
        public string FileName { get; set; } = null!;

        /// <summary>
        /// Gets or sets the list of member summaries contained in the file.
        /// </summary>
        public List<string> Members { get; set; } = new List<string>();
    }
}