using Microsoft.EntityFrameworkCore;
using OSSDependencyAnalyzer.API.Data;
using OSSDependencyAnalyzer.API.Models;
using OSSDependencyAnalyzer.API.Services;

namespace OSSDependencyAnalyzer.API.Jobs;

/// <summary>
/// Re-scans every completed repository once a day. New advisories are published constantly, so a
/// repository that was clean yesterday may not be today — this is what makes the trend charts and
/// "new vulnerability" alerts meaningful.
/// </summary>
public sealed class DailyRescanJob(AppDbContext db, IAnalysisQueue queue, ILogger<DailyRescanJob> logger)
{
    public const string RecurringJobId = "daily-rescan";

    public async Task ExecuteAsync()
    {
        var repositoryIds = await db.Repositories
            .Where(r => r.Status == Repository.AnalysisStatus.Completed && r.UserId != null)
            .Select(r => r.Id)
            .ToListAsync();

        foreach (var id in repositoryIds)
            queue.Enqueue(id);

        logger.LogInformation("Daily rescan queued {Count} repositories", repositoryIds.Count);
    }
}
