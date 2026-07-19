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
    [HttpGet("{id}")]
    public async Task<ActionResult<RepositoryResponseDto>> GetRepository(Guid id)
    {
        var repository = await _db.Repositories
            .Include(r => r.Dependencies)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (repository == null)
            return NotFound();

        return Ok(new RepositoryResponseDto
        {
            Id = repository.Id,
            GitHubUrl = repository.GithubUrl,
            Status = repository.Status.ToString(),
            CreatedAt = repository.CreatedAt,
            CompletedAt = repository.CompletedAt,
            TotalDependencies = repository.TotalDependencies,
            DependenciesCount = repository.Dependencies.Count
        });
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
    public string Status { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int TotalDependencies { get; set; }
    public int DependenciesCount { get; set; }
}