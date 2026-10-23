using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OSSDependencyAnalyzer.API.Data;
using OSSDependencyAnalyzer.API.Domain;
using OSSDependencyAnalyzer.API.DTOs;
using OSSDependencyAnalyzer.API.Infrastructure;
using OSSDependencyAnalyzer.API.Models;
using OSSDependencyAnalyzer.API.Services;

namespace OSSDependencyAnalyzer.API.Controllers;

[ApiController]
[Authorize]
[Route("api/repositories")]
public sealed class RepositoriesController(AppDbContext db, IAnalysisQueue queue, ILogger<RepositoriesController> logger) : ControllerBase
{
    private IQueryable<Repository> OwnedRepositories
    {
        get
        {
            var userId = User.GetUserId();
            return db.Repositories.Where(r => r.UserId == userId);
        }
    }

    /// <summary>Lists the caller's repositories, newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RepositoryResponseDto>>> List(
        [FromQuery] string? search,
        [FromQuery] Repository.AnalysisStatus? status,
        CancellationToken cancellationToken)
    {
        var query = OwnedRepositories.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(r => r.GithubUrl.Contains(term));
        }
        if (status is not null)
            query = query.Where(r => r.Status == status);

        var repositories = await query.OrderByDescending(r => r.CreatedAt).ToListAsync(cancellationToken);
        return Ok(repositories.Select(ToDto).ToList());
    }

    /// <summary>Queues a repository for analysis. Returns the existing record if the caller already added it.</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Scan)]
    [ProducesResponseType<RepositoryResponseDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<RepositoryResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RepositoryResponseDto>> Create([FromBody] AnalyzeRequestDto request, CancellationToken cancellationToken)
    {
        if (!GitHubRepositoryUrl.TryParse(request.RepositoryUrl, out var reference))
        {
            ModelState.AddModelError(nameof(request.RepositoryUrl),
                "Enter a public GitHub repository, e.g. https://github.com/expressjs/express or expressjs/express.");
            return ValidationProblem(ModelState);
        }

        var userId = User.GetUserId();
        var existing = await db.Repositories.AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.GithubUrl == reference.CanonicalUrl, cancellationToken);
        if (existing is not null)
            return Ok(ToDto(existing));

        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GithubUrl = reference.CanonicalUrl,
            RepositoryOwner = reference.Owner,
            RepositoryName = reference.Name,
            Status = Repository.AnalysisStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Repositories.Add(repository);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost a race with a concurrent request for the same URL; the unique index kept us consistent.
            db.ChangeTracker.Clear();
            var winner = await db.Repositories.AsNoTracking()
                .FirstAsync(r => r.UserId == userId && r.GithubUrl == reference.CanonicalUrl, cancellationToken);
            return Ok(ToDto(winner));
        }

        queue.Enqueue(repository.Id);
        logger.LogInformation("Queued analysis of {Repository} ({RepositoryId})", reference.FullName, repository.Id);
        return AcceptedAtAction(nameof(Get), new { id = repository.Id }, ToDto(repository));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RepositoryResponseDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var repository = await OwnedRepositories.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        return repository is null ? NotFound() : Ok(ToDto(repository));
    }

    /// <summary>Re-runs the analysis against the repository's current default branch.</summary>
    [HttpPost("{id:guid}/rescan")]
    [EnableRateLimiting(RateLimitPolicies.Scan)]
    [ProducesResponseType<RepositoryResponseDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RepositoryResponseDto>> Rescan(Guid id, CancellationToken cancellationToken)
    {
        var repository = await OwnedRepositories.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (repository is null)
            return NotFound();
        if (repository.IsInProgress)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "An analysis is already running for this repository.");

        repository.Status = Repository.AnalysisStatus.Pending;
        repository.ErrorMessage = null;
        await db.SaveChangesAsync(cancellationToken);

        queue.Enqueue(repository.Id);
        return AcceptedAtAction(nameof(Get), new { id }, ToDto(repository));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var repository = await OwnedRepositories
            .Include(r => r.Dependencies).ThenInclude(d => d.Vulnerabilities)
            .Include(r => r.VulnerabilitySnapshots)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (repository is null)
            return NotFound();

        db.Repositories.Remove(repository);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/dependencies")]
    public async Task<ActionResult<IReadOnlyList<DependencyResponseDto>>> GetDependencies(Guid id, CancellationToken cancellationToken)
    {
        if (!await OwnedRepositories.AnyAsync(r => r.Id == id, cancellationToken))
            return NotFound();

        var rows = await db.Dependencies.AsNoTracking()
            .Where(d => d.RepositoryId == id)
            .Select(d => new
            {
                d.Id, d.PackageName, d.CurrentVersion, d.ResolvedVersion, d.Type, d.ManifestPath, d.IsDevDependency,
                VulnerabilityCount = d.Vulnerabilities.Count,
                Highest = d.Vulnerabilities.Max(v => (Severity?)v.Severity)
            })
            .ToListAsync(cancellationToken);

        return Ok(rows
            .OrderByDescending(d => d.Highest ?? (Severity)(-1))
            .ThenByDescending(d => d.VulnerabilityCount)
            .ThenBy(d => d.PackageName, StringComparer.OrdinalIgnoreCase)
            .Select(d => new DependencyResponseDto
            {
                Id = d.Id,
                PackageName = d.PackageName,
                CurrentVersion = d.CurrentVersion,
                ResolvedVersion = d.ResolvedVersion,
                Type = d.Type.ToString(),
                ManifestPath = d.ManifestPath,
                IsDevDependency = d.IsDevDependency,
                VulnerabilityCount = d.VulnerabilityCount,
                HighestSeverity = d.Highest?.ToString()
            })
            .ToList());
    }

    [HttpGet("{id:guid}/vulnerabilities")]
    public async Task<ActionResult<IReadOnlyList<VulnerabilityResponseDto>>> GetVulnerabilities(Guid id, CancellationToken cancellationToken)
    {
        if (!await OwnedRepositories.AnyAsync(r => r.Id == id, cancellationToken))
            return NotFound();

        var vulnerabilities = await db.Vulnerabilities.AsNoTracking()
            .Where(v => v.Dependency.RepositoryId == id)
            .OrderByDescending(v => v.RiskScore)
            .Select(v => new VulnerabilityResponseDto
            {
                Id = v.Id,
                DependencyId = v.DependencyId,
                CveId = v.CveId,
                AdvisoryId = v.AdvisoryId,
                Aliases = v.Aliases,
                Title = v.Title,
                Description = v.Description,
                Severity = v.Severity.ToString(),
                CvssScore = v.CvssScore,
                CvssVector = v.CvssVector,
                RiskScore = v.RiskScore,
                IsExploitable = v.IsExploitable,
                ExploitUrl = v.ExploitUrl,
                AffectedVersionRange = v.AffectedVersionRange,
                RemediationVersion = v.RemediationVersion,
                ReferenceUrl = v.ReferenceUrl,
                PublishedAt = v.PublishedAt,
                PackageName = v.Dependency.PackageName,
                PackageVersion = v.Dependency.ResolvedVersion ?? v.Dependency.CurrentVersion,
                Ecosystem = v.Dependency.Type.ToString(),
                IsDevDependency = v.Dependency.IsDevDependency
            })
            .ToListAsync(cancellationToken);

        return Ok(vulnerabilities);
    }

    [HttpGet("{id:guid}/snapshots")]
    public async Task<ActionResult<IReadOnlyList<SnapshotResponseDto>>> GetSnapshots(Guid id, CancellationToken cancellationToken)
    {
        if (!await OwnedRepositories.AnyAsync(r => r.Id == id, cancellationToken))
            return NotFound();

        var snapshots = await db.VulnerabilitySnapshots.AsNoTracking()
            .Where(s => s.RepositoryId == id)
            .OrderBy(s => s.SnapshotDate)
            .Select(s => new SnapshotResponseDto
            {
                SnapshotDate = s.SnapshotDate,
                CriticalCount = s.CriticalCount,
                HighCount = s.HighCount,
                MediumCount = s.MediumCount,
                LowCount = s.LowCount,
                TotalVulnerabilities = s.TotalVulnerabilities,
                AverageRiskScore = s.AverageRiskScore,
                RiskScore = s.RiskScore
            })
            .ToListAsync(cancellationToken);

        return Ok(snapshots);
    }

    /// <summary>Downloads a CycloneDX 1.5 SBOM including known vulnerabilities.</summary>
    [HttpGet("{id:guid}/sbom")]
    [Produces("application/vnd.cyclonedx+json")]
    public async Task<IActionResult> GetSbom(Guid id, CancellationToken cancellationToken)
    {
        var repository = await OwnedRepositories.AsNoTracking()
            .Include(r => r.Dependencies).ThenInclude(d => d.Vulnerabilities)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (repository is null)
            return NotFound();
        if (repository.Status != Repository.AnalysisStatus.Completed)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "The SBOM is available once the analysis has completed.");

        var bom = SbomBuilder.Build(repository, repository.Dependencies.ToList(), DateTimeOffset.UtcNow);
        var fileName = $"{repository.RepositoryOwner}-{repository.RepositoryName}.cdx.json";
        return File(System.Text.Encoding.UTF8.GetBytes(bom.ToJsonString()), "application/vnd.cyclonedx+json", fileName);
    }

    internal static RepositoryResponseDto ToDto(Repository r) => new()
    {
        Id = r.Id,
        GitHubUrl = r.GithubUrl,
        RepositoryOwner = r.RepositoryOwner,
        RepositoryName = r.RepositoryName,
        Description = r.Description,
        Language = r.Language,
        Stars = r.Stars,
        Status = r.Status.ToString(),
        CreatedAt = r.CreatedAt,
        CompletedAt = r.CompletedAt,
        LastAnalysedAt = r.LastAnalysedAt == default ? null : r.LastAnalysedAt,
        TotalDependencies = r.TotalDependencies,
        ManifestCount = r.ManifestCount,
        CriticalVulnerabilities = r.CriticalVulnerabilities,
        HighVulnerabilities = r.HighVulnerabilities,
        MediumVulnerabilities = r.MediumVulnerabilities,
        LowVulnerabilities = r.LowVulnerabilities,
        RiskScore = r.RiskScore,
        RiskGrade = RiskScoringService.Grade(r.RiskScore),
        ErrorMessage = r.ErrorMessage
    };
}
