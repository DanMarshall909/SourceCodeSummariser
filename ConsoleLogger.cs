namespace SourceCodeSummariser
{
    /// <summary>
    /// Console-based implementation of ILogger.
    /// Writes log messages to the standard console output.
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        /// <summary>
        /// Writes a message to the console without a line terminator.
        /// </summary>
        /// <param name="message">The message to write</param>
        public void Write(string message)
        {
            Console.Write(message);
        }

        /// <summary>
        /// Writes a message followed by a line terminator to the console.
        /// </summary>
        /// <param name="message">The message to write</param>
        public void WriteLine(string message)
        {
            Console.WriteLine(message);
        }

        /// <summary>
        /// Writes an empty line to the console.
        /// </summary>
        public void WriteLine()
        {
            Console.WriteLine();
        }
    }
}
