using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OSSDependencyAnalyzer.API.Data;
using OSSDependencyAnalyzer.API.DTOs;
using OSSDependencyAnalyzer.API.Infrastructure;
using OSSDependencyAnalyzer.API.Models;
using OSSDependencyAnalyzer.API.Services;

namespace OSSDependencyAnalyzer.API.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(AppDbContext db, TimeProvider clock) : ControllerBase
{
    internal const int TrendDays = 30;

    /// <summary>Portfolio-wide security posture for the signed-in user.</summary>
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var repositories = await db.Repositories.AsNoTracking()
            .Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);
        var repositoryIds = repositories.Select(r => r.Id).ToList();

        var dependenciesByType = await db.Dependencies.AsNoTracking()
            .Where(d => repositoryIds.Contains(d.RepositoryId))
            .GroupBy(d => d.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var findings = await db.Vulnerabilities.AsNoTracking()
            .Where(v => repositoryIds.Contains(v.Dependency.RepositoryId))
            .Select(v => new
            {
                v.CveId, v.Severity, v.IsExploitable, v.RemediationVersion,
                v.Dependency.PackageName, v.Dependency.Type, v.Dependency.RepositoryId
            })
            .ToListAsync(cancellationToken);

        var completed = repositories.Where(r => r.Status == Repository.AnalysisStatus.Completed).ToList();

        var ecosystems = dependenciesByType
            .Select(d => new EcosystemBreakdownDto(d.Type.ToString(), d.Count, findings.Count(f => f.Type == d.Type)))
            .OrderByDescending(e => e.Dependencies)
            .ToList();

        var riskiest = completed
            .Where(r => r.RiskScore > 0)
            .OrderByDescending(r => r.RiskScore)
            .ThenByDescending(r => r.CriticalVulnerabilities)
            .Take(5)
            .Select(r => new RiskiestRepositoryDto(
                r.Id, $"{r.RepositoryOwner}/{r.RepositoryName}", r.RiskScore, RiskScoringService.Grade(r.RiskScore),
                r.CriticalVulnerabilities, r.HighVulnerabilities, r.MediumVulnerabilities, r.LowVulnerabilities))
            .ToList();

        var topPackages = findings
            .GroupBy(f => (f.PackageName, f.Type))
            .Select(g => new VulnerablePackageDto(
                g.Key.PackageName,
                g.Key.Type.ToString(),
                g.Select(f => f.CveId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                g.Select(f => f.RepositoryId).Distinct().Count(),
                g.Max(f => f.Severity).ToString()))
            .OrderByDescending(p => Enum.Parse<Severity>(p.HighestSeverity))
            .ThenByDescending(p => p.Vulnerabilities)
            .Take(8)
            .ToList();

        return Ok(new DashboardDto
        {
            TotalRepositories = repositories.Count,
            CompletedRepositories = completed.Count,
            InProgressRepositories = repositories.Count(r => r.IsInProgress),
            FailedRepositories = repositories.Count(r => r.Status == Repository.AnalysisStatus.Failed),
            TotalDependencies = dependenciesByType.Sum(d => d.Count),
            TotalVulnerabilities = findings.Count,
            TotalCritical = findings.Count(f => f.Severity == Severity.Critical),
            TotalHigh = findings.Count(f => f.Severity == Severity.High),
            TotalMedium = findings.Count(f => f.Severity == Severity.Medium),
            TotalLow = findings.Count(f => f.Severity == Severity.Low),
            ExploitedVulnerabilities = findings.Count(f => f.IsExploitable),
            FixableVulnerabilities = findings.Count(f => f.RemediationVersion != null),
            AverageRiskScore = completed.Count == 0 ? 0 : Math.Round(completed.Average(r => r.RiskScore), 1),
            Ecosystems = ecosystems,
            RiskiestRepositories = riskiest,
            TopVulnerablePackages = topPackages,
            Trend = await BuildTrendAsync(repositoryIds, cancellationToken)
        });
    }

    /// <summary>
    /// Daily portfolio totals for the last <see cref="TrendDays"/> days. Each repository contributes
    /// its most recent snapshot as of that day (carried forward between scans).
    /// </summary>
    private async Task<IReadOnlyList<TrendPointDto>> BuildTrendAsync(List<Guid> repositoryIds, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var firstDay = today.AddDays(-(TrendDays - 1));
        var windowStart = firstDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var snapshots = await db.VulnerabilitySnapshots.AsNoTracking()
            .Where(s => repositoryIds.Contains(s.RepositoryId))
            .Select(s => new { s.RepositoryId, s.SnapshotDate, s.CriticalCount, s.HighCount, s.MediumCount, s.LowCount })
            .ToListAsync(cancellationToken);

        if (snapshots.Count == 0)
            return [];

        var byRepository = snapshots
            .GroupBy(s => s.RepositoryId)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.SnapshotDate).ToList());

        var points = new List<TrendPointDto>(TrendDays);
        for (var day = firstDay; day <= today; day = day.AddDays(1))
        {
            var endOfDay = day.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            int critical = 0, high = 0, medium = 0, low = 0;

            foreach (var history in byRepository.Values)
            {
                var latest = history.LastOrDefault(s => s.SnapshotDate <= endOfDay);
                if (latest is null) continue;
                critical += latest.CriticalCount;
                high += latest.HighCount;
                medium += latest.MediumCount;
                low += latest.LowCount;
            }

            points.Add(new TrendPointDto(day, critical, high, medium, low, critical + high + medium + low));
        }

        // Trim leading days before the first ever scan so the chart starts where data starts.
        var firstData = snapshots.Min(s => s.SnapshotDate);
        return firstData > windowStart
            ? points.Where(p => p.Date >= DateOnly.FromDateTime(firstData)).ToList()
            : points;
    }
}
