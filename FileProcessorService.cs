using Microsoft.EntityFrameworkCore;

namespace SourceCodeSummariser;

public class FileProcessorService
{
    private readonly SummaryContext _dbContext;
    private readonly SummarizerService _summarizerService;

    public FileProcessorService(SummaryContext dbContext, SummarizerService summarizerService)
    {
        _dbContext = dbContext;
        _summarizerService = summarizerService;
    }

    public async Task<List<(string MethodSignature, string OldSummary, string NewSummary)>> ProcessFile(string filePath)
    {
        string content = await System.IO.File.ReadAllTextAsync(filePath);
        var fileSummary = await _summarizerService.GenerateFileSummary(filePath, content);

        var changes = new List<(string MethodSignature, string OldSummary, string NewSummary)>();

        var fileEntity = _dbContext.Files.Include(f => f.Members)
            .FirstOrDefault(f => f.FileName == fileSummary.FileName);
        if (fileEntity == null)
        {
            fileEntity = new FileEntity { FileName = fileSummary.FileName };
            _dbContext.Files.Add(fileEntity);
            await _dbContext.SaveChangesAsync();
        }

        foreach (var memberSummary in fileSummary.Members)
        {
            string memberHash = _summarizerService.ComputeHash(memberSummary);
            var existingMember = _dbContext.Members
                .FirstOrDefault(m => m.Hash == memberHash && m.FileEntityId == fileEntity.Id);

            string methodSignature = memberSummary.Split('-')[0].Trim();
            string newSummary = memberSummary;

            if (existingMember == null)
            {
                var newMember = new MemberEntity
                {
                    Name = memberSummary.Split(':')[1].Trim(),
                    Type = memberSummary.Split(':')[0].Trim(),
                    Summary = memberSummary,
                    Hash = memberHash,
                    File = fileEntity
                };
                _dbContext.Members.Add(newMember);
                await _dbContext.SaveChangesAsync();
                changes.Add((methodSignature, string.Empty, newSummary));
            }
            else if (existingMember.Summary != newSummary)
            {
                changes.Add((methodSignature, existingMember.Summary, newSummary));
                existingMember.Summary = newSummary;
                existingMember.Hash = memberHash;
                await _dbContext.SaveChangesAsync();
            }
        }

        return changes;
    }
}