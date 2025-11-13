namespace SourceCodeSummariser
{
    /// <summary>
    /// Interface for logging output messages.
    /// Abstracts the output mechanism to improve testability and allow different implementations.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Writes a message to the output.
        /// </summary>
        /// <param name="message">The message to write</param>
        void Write(string message);

        /// <summary>
        /// Writes a message followed by a line terminator to the output.
        /// </summary>
        /// <param name="message">The message to write</param>
        void WriteLine(string message);

        /// <summary>
        /// Writes an empty line to the output.
        /// </summary>
        void WriteLine();
    }
}
