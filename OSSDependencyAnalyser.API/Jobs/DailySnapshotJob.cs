using OSSDependencyAnalyzer.API.Data;
using OSSDependencyAnalyzer.API.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Jobs;

public class DailySnapshotJob
{
    private readonly AppDbContext _db;
    private readonly ISnapshotService _snapshotService;
    private readonly ILogger<DailySnapshotJob> _logger;

    public DailySnapshotJob(AppDbContext db, ISnapshotService snapshotService, ILogger<DailySnapshotJob> logger)
    {
        _db = db;
        _snapshotService = snapshotService;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        try
        {
            _logger.LogInformation("Starting daily vulnerability snapshot job");

            var repositories = await _db.Repositories
                .Where(r => r.Status == Repository.AnalysisStatus.Completed)
                .Select(r => r.Id)
                .ToListAsync();

            foreach (var repositoryId in repositories)
            {
                await _snapshotService.CreateVulnerabilitysnapshotAsync(repositoryId);
            }

            _logger.LogInformation("Daily snapshot job completed for {Count} repositories", repositories.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Daily snapshot job failed");
        }
    }
}