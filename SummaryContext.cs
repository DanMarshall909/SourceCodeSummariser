using Microsoft.EntityFrameworkCore;

namespace SourceCodeSummariser
{
    public class SummaryContext : DbContext
    {
        private readonly string _connectionString;

        public SummaryContext() : this("Data Source=summaries.db")
        {
        }

        public SummaryContext(string connectionString)
        {
            _connectionString = connectionString;
        }

        public DbSet<FileEntity> Files { get; set; } = null!;
        public DbSet<MemberEntity> Members { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(_connectionString);
        }
    }
}