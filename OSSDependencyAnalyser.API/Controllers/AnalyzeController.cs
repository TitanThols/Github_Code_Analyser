using OSSDependencyAnalyzer.API.Data;
using OSSDependencyAnalyzer.API.Models;
using OSSDependencyAnalyzer.API.Services;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace OSSDependencyAnalyzer.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyzeController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<AnalyzeController> _logger;

    public AnalyzeController(AppDbContext db, IBackgroundJobClient backgroundJobClient, ILogger<AnalyzeController> logger)
    {
        _db = db;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    private static bool TryParseGitHubUrl(string url, out string owner, out string repo)
    {
        owner = null!;
        repo = null!;
        var uri = new Uri(url, UriKind.RelativeOrAbsolute);
        var parts = uri.AbsolutePath.Trim('/').Split('/');

        if (parts.Length >= 2)
        {
            owner = parts[0];
            repo = parts[1].Replace(".git", "");
            return true;
        }

        return false;
    }

    [HttpPost]
    public async Task<ActionResult<RepositoryResponseDto>> Analyze([FromBody] AnalyzeRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            if (!TryParseGitHubUrl(request.RepositoryUrl, out var owner, out var repo))
            {
                return BadRequest(new { error = "Invalid GitHub URL format" });
            }

            _logger.LogInformation("Analysis requested for {Owner}/{Repo}", owner, repo);
    
            var repository = new Repository
            {
                Id = Guid.NewGuid(),
                GithubUrl = request.RepositoryUrl,
                RepositoryOwner = owner,
                RepositoryName = repo,
                Status = Repository.AnalysisStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _db.Repositories.Add(repository);
            await _db.SaveChangesAsync(cancellationToken);

            _backgroundJobClient.Enqueue<IAnalyzerService>(
                service => service.AnalyzeRepositoryAsync(repository.Id, cancellationToken)
            );

            _logger.LogInformation("Queued analysis job for repository {RepositoryId}", repository.Id);

            return Accepted(new RepositoryResponseDto
            {
                Id = repository.Id,
                GitHubUrl = repository.GithubUrl,
                Status = repository.Status.ToString(),
                CreatedAt = repository.CreatedAt
            });

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start analysis");
            return StatusCode(500, new { error = "Failed to start analysis" });
        }
    }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RepositoryResponseDto>> GetRepository(Guid id, CancellationToken cancellationToken)
    {
        var repository = await _db.Repositories
            .Include(r => r.Dependencies)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (repository == null)
            return NotFound();

        return Ok(new RepositoryResponseDto
        {
            Id = repository.Id,
            GitHubUrl = repository.GithubUrl,
            RepositoryOwner = repository.RepositoryOwner,
            RepositoryName = repository.RepositoryName,
            Status = repository.Status.ToString(),
            CreatedAt = repository.CreatedAt,
            CompletedAt = repository.CompletedAt,
            TotalDependencies = repository.TotalDependencies,
            DependenciesCount = repository.Dependencies.Count,
            CriticalVulnerabilities = repository.CriticalVulnerabilities,
            HighVulnerabilities = repository.HighVulnerabilities,
            MediumVulnerabilities = repository.MediumVulnerabilities,
            ErrorMessage = repository.ErrorMessage
        });
    }

    [HttpGet]
    public async Task<ActionResult<List<RepositoryResponseDto>>> GetAllRepositories(CancellationToken cancellationToken)
    {
        var repositories = await _db.Repositories
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RepositoryResponseDto
            {
                Id = r.Id,
                GitHubUrl = r.GithubUrl,
                RepositoryOwner = r.RepositoryOwner,
                RepositoryName = r.RepositoryName,
                Status = r.Status.ToString(),
                CreatedAt = r.CreatedAt,
                CompletedAt = r.CompletedAt,
                TotalDependencies = r.TotalDependencies,
                CriticalVulnerabilities = r.CriticalVulnerabilities,
                HighVulnerabilities = r.HighVulnerabilities,
                MediumVulnerabilities = r.MediumVulnerabilities
            })
            .ToListAsync(cancellationToken);

        return Ok(repositories);
    }
    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetStats(CancellationToken cancellationToken)
    {
        var stats = await _db.Repositories
            .GroupBy(r => 1)
            .Select(g => new DashboardStatsDto
            {
                TotalRepositories = g.Count(),
                TotalDependencies = g.Sum(r => r.TotalDependencies),
                TotalCritical = g.Sum(r => r.CriticalVulnerabilities),
                TotalHigh = g.Sum(r => r.HighVulnerabilities),
                TotalMedium = g.Sum(r => r.MediumVulnerabilities)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(stats ?? new DashboardStatsDto());
    }
    [HttpGet("{id:guid}/snapshots")]
    public async Task<ActionResult<List<SnapshotResponseDto>>> GetSnapshots(Guid id, CancellationToken cancellationToken)
    {
        var snapshots = await _db.VulnerabilitySnapshots
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
                AverageRiskScore = s.AverageRiskScore
            })
            .ToListAsync(cancellationToken);

        return Ok(snapshots);
    }
}


public class AnalyzeRequestDto
{
    public string RepositoryUrl { get; set; } = null!;
}

public class RepositoryResponseDto
{
    public Guid Id { get; set; }
    public string GitHubUrl { get; set; } = null!;
    public string RepositoryOwner { get; set; } = null!;
    public string RepositoryName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int TotalDependencies { get; set; }
    public int DependenciesCount { get; set; }
    public int CriticalVulnerabilities { get; set; }
    public int HighVulnerabilities { get; set; }
    public int MediumVulnerabilities { get; set; }
    public string? ErrorMessage { get; set; }
}