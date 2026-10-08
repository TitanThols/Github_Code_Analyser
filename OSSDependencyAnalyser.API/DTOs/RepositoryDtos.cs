using System.ComponentModel.DataAnnotations;

namespace OSSDependencyAnalyzer.API.DTOs;

public sealed class AnalyzeRequestDto
{
    /// <summary>Repository reference: a GitHub URL, an SSH remote or plain "owner/repo".</summary>
    [Required, MaxLength(500)]
    public string RepositoryUrl { get; set; } = string.Empty;
}

public sealed class RepositoryResponseDto
{
    public Guid Id { get; init; }
    public string GitHubUrl { get; init; } = null!;
    public string RepositoryOwner { get; init; } = null!;
    public string RepositoryName { get; init; } = null!;
    public string? Description { get; init; }
    public string? Language { get; init; }
    public int? Stars { get; init; }
    public string Status { get; init; } = null!;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? LastAnalysedAt { get; init; }
    public int TotalDependencies { get; init; }
    public int ManifestCount { get; init; }
    public int CriticalVulnerabilities { get; init; }
    public int HighVulnerabilities { get; init; }
    public int MediumVulnerabilities { get; init; }
    public int LowVulnerabilities { get; init; }
    public int TotalVulnerabilities => CriticalVulnerabilities + HighVulnerabilities + MediumVulnerabilities + LowVulnerabilities;
    public double RiskScore { get; init; }
    public string RiskGrade { get; init; } = "A";
    public string? ErrorMessage { get; init; }
}

public sealed class DependencyResponseDto
{
    public Guid Id { get; init; }
    public string PackageName { get; init; } = null!;
    public string CurrentVersion { get; init; } = null!;
    public string? ResolvedVersion { get; init; }
    public string Type { get; init; } = null!;
    public string? ManifestPath { get; init; }
    public bool IsDevDependency { get; init; }
    public int VulnerabilityCount { get; init; }
    public string? HighestSeverity { get; init; }
}

public sealed class VulnerabilityResponseDto
{
    public Guid Id { get; init; }
    public Guid DependencyId { get; init; }
    public string CveId { get; init; } = null!;
    public string? AdvisoryId { get; init; }
    public string? Aliases { get; init; }
    public string Title { get; init; } = null!;
    public string Description { get; init; } = null!;
    public string Severity { get; init; } = null!;
    public decimal? CvssScore { get; init; }
    public string? CvssVector { get; init; }
    public double RiskScore { get; init; }
    public bool IsExploitable { get; init; }
    public string? ExploitUrl { get; init; }
    public string? AffectedVersionRange { get; init; }
    public string? RemediationVersion { get; init; }
    public string? ReferenceUrl { get; init; }
    public DateTime PublishedAt { get; init; }
    public string PackageName { get; init; } = null!;
    public string PackageVersion { get; init; } = null!;
    public string Ecosystem { get; init; } = null!;
    public bool IsDevDependency { get; init; }
}

public sealed class SnapshotResponseDto
{
    public DateTime SnapshotDate { get; init; }
    public int CriticalCount { get; init; }
    public int HighCount { get; init; }
    public int MediumCount { get; init; }
    public int LowCount { get; init; }
    public int TotalVulnerabilities { get; init; }
    public decimal AverageRiskScore { get; init; }
    public double RiskScore { get; init; }
}
