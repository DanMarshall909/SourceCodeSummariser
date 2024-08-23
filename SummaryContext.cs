using Microsoft.EntityFrameworkCore;

namespace SourceCodeSummariser
{
    public class SummaryContext : DbContext
    {
        public DbSet<FileEntity> Files { get; set; } = null!;
        public DbSet<MemberEntity> Members { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=summaries.db");
        }
    }
}