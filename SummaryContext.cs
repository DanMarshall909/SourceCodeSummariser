using Microsoft.EntityFrameworkCore;

namespace SourceCodeSummariser
{
    /// <summary>
    /// Entity Framework database context for storing code summaries.
    /// </summary>
    public class SummaryContext : DbContext
    {
        private readonly string _connectionString;

        /// <summary>
        /// Initializes a new instance of the <see cref="SummaryContext"/> class with default connection string.
        /// </summary>
        public SummaryContext() : this("Data Source=summaries.db")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SummaryContext"/> class with a specified connection string.
        /// </summary>
        /// <param name="connectionString">The SQLite database connection string.</param>
        public SummaryContext(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Gets or sets the collection of source files.
        /// </summary>
        public DbSet<FileEntity> Files { get; set; } = null!;

        /// <summary>
        /// Gets or sets the collection of code members and their summaries.
        /// </summary>
        public DbSet<MemberEntity> Members { get; set; } = null!;

        /// <summary>
        /// Configures the database context to use SQLite.
        /// </summary>
        /// <param name="optionsBuilder">The options builder for configuring the context.</param>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(_connectionString);
        }
    }
}