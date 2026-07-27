using OSSDependencyAnalyzer.API.Models;
using OSSDependencyAnalyzer.API.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace OSSDependencyAnalyzer.API.Services;

public interface ISnapshotService
{
    Task CreateVulnerabilitysnapshotAsync(Guid repositoryId);
}

public class SnapshotService : ISnapshotService
{
    private readonly AppDbContext _db;
    private readonly IRiskScoringService _riskScoringService;
    private readonly ILogger<SnapshotService> _logger;

    public SnapshotService(AppDbContext db, IRiskScoringService riskScoringService, ILogger<SnapshotService> logger)
    {
        _db = db;
        _riskScoringService = riskScoringService;
        _logger = logger;
    }

    public async Task CreateVulnerabilitysnapshotAsync(Guid repositoryId)
    {
        try
        {
            var repository = await _db.Repositories.Include(r => r.Dependencies).ThenInclude(d => d.Vulnerabilities).FirstOrDefaultAsync(r => r.Id == repositoryId);
            if(repository == null)
            {
                _logger.LogWarning("Repository {RepositoryId} not found for snapshot", repositoryId);
                return;
            }

            var allVulnerabilities = repository.Dependencies.SelectMany(d => d.Vulnerabilities).ToList();

            var criticalCount = allVulnerabilities.Count(v => v.Severity == Severity.Critical);
            var highCount = allVulnerabilities.Count(v => v.Severity == Severity.High);
            var mediumCount = allVulnerabilities.Count(v => v.Severity == Severity.Medium);
            var lowCount = allVulnerabilities.Count(v => v.Severity == Severity.Low);

            var averageRiskScore = allVulnerabilities.Any()
                ? allVulnerabilities.Average(v => v.RiskScore)
                : 0;
            var snapshot = new VulnerabilitySnapshot
            {
                Id = Guid.NewGuid(),
                RepositoryId = repositoryId,
                SnapshotDate = DateTime.UtcNow,
                CriticalCount = criticalCount,
                HighCount = highCount,
                MediumCount = mediumCount,
                LowCount = lowCount,
                TotalVulnerabilities = allVulnerabilities.Count,
                AverageRiskScore = (decimal)averageRiskScore
            };

            _db.VulnerabilitySnapshots.Add(snapshot);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Created vulnerability snapshot for repository {RepositoryId}: Critical={Critical}, High={High}, Medium={Medium}, Low={Low}, AvgRisk={AvgRisk}",
                repositoryId,
                criticalCount,
                highCount,
                mediumCount,
                lowCount,
                averageRiskScore
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create vulnerability snapshot for repository {RepositoryId}", repositoryId);
        }
    }
}
