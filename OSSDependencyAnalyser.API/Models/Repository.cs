using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyModel;

namespace OSSDependencyAnalyzer.API.Models;

public class Repository
{
    public Guid Id { get; set; }
    public string GithubUrl { get; set; } = null!;
    public string RepositoryOwner { get; set; } = null!;
    public string RepositoryName { get; set; } = null!;
    public AnalysisStatus Status { get; set; } = AnalysisStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset LastAnalysedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int TotalDependencies { get; set; }
    public int HighVulnerabilities { get; set; }
    public int CriticalVulnerabilities { get; set; }
    public int MediumVulnerabilities { get; set; }

    public ICollection<Dependency> Dependencies { get; set; } = new List<Dependency>();
    public ICollection<VulnerabilitySnapshot> VulnerabilitySnapshots { get; set; } = new List<VulnerabilitySnapshot>();

    public enum AnalysisStatus
    {
        Pending = 0,
        Parsing = 1,
        FetchingVulnerabilities = 2,
        Completed = 3,
        Failed = 4
    }

}