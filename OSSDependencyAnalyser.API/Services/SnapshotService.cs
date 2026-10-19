using Microsoft.EntityFrameworkCore;
using OSSDependencyAnalyzer.API.Data;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Services;

public interface ISnapshotService
{
    Task CreateSnapshotAsync(Guid repositoryId, CancellationToken cancellationToken = default);
}

public sealed class SnapshotService(AppDbContext db, ILogger<SnapshotService> logger) : ISnapshotService
{
    public async Task CreateSnapshotAsync(Guid repositoryId, CancellationToken cancellationToken = default)
    {
        var repository = await db.Repositories.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == repositoryId, cancellationToken);
        if (repository is null)
        {
            logger.LogWarning("Repository {RepositoryId} not found for snapshot", repositoryId);
            return;
        }

        var riskScores = await db.Vulnerabilities
            .Where(v => v.Dependency.RepositoryId == repositoryId)
            .Select(v => v.RiskScore)
            .ToListAsync(cancellationToken);

        db.VulnerabilitySnapshots.Add(new VulnerabilitySnapshot
        {
            Id = Guid.NewGuid(),
            RepositoryId = repositoryId,
            SnapshotDate = DateTime.UtcNow,
            CriticalCount = repository.CriticalVulnerabilities,
            HighCount = repository.HighVulnerabilities,
            MediumCount = repository.MediumVulnerabilities,
            LowCount = repository.LowVulnerabilities,
            TotalVulnerabilities = riskScores.Count,
            AverageRiskScore = riskScores.Count == 0 ? 0 : Math.Round((decimal)riskScores.Average(), 2),
            RiskScore = repository.RiskScore
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
