namespace OSSDependencyAnalyzer.API.Models;

public class Repository
{
    public Guid Id { get; set; }

    /// <summary>Owning user. Nullable only for rows created before accounts existed.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Canonical, lower-cased https://github.com/{owner}/{repo} URL.</summary>
    public string GithubUrl { get; set; } = null!;
    public string RepositoryOwner { get; set; } = null!;
    public string RepositoryName { get; set; } = null!;
    public string? Description { get; set; }
    public string? Language { get; set; }
    public int? Stars { get; set; }

    public AnalysisStatus Status { get; set; } = AnalysisStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset LastAnalysedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public int TotalDependencies { get; set; }
    public int ManifestCount { get; set; }
    public int CriticalVulnerabilities { get; set; }
    public int HighVulnerabilities { get; set; }
    public int MediumVulnerabilities { get; set; }
    public int LowVulnerabilities { get; set; }

    /// <summary>Aggregate 0–100 risk score, see <c>RiskScoringService.CalculateRepositoryRiskScore</c>.</summary>
    public double RiskScore { get; set; }

    public ICollection<Dependency> Dependencies { get; set; } = new List<Dependency>();
    public ICollection<VulnerabilitySnapshot> VulnerabilitySnapshots { get; set; } = new List<VulnerabilitySnapshot>();

    public bool IsInProgress => Status is AnalysisStatus.Pending or AnalysisStatus.Parsing or AnalysisStatus.FetchingVulnerabilities;

    public enum AnalysisStatus
    {
        Pending = 0,
        Parsing = 1,
        FetchingVulnerabilities = 2,
        Completed = 3,
        Failed = 4
    }
}
