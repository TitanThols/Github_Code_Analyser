namespace OSSDependencyAnalyzer.API.DTOs;

public sealed class DashboardDto
{
    public int TotalRepositories { get; init; }
    public int CompletedRepositories { get; init; }
    public int InProgressRepositories { get; init; }
    public int FailedRepositories { get; init; }
    public int TotalDependencies { get; init; }
    public int TotalVulnerabilities { get; init; }
    public int TotalCritical { get; init; }
    public int TotalHigh { get; init; }
    public int TotalMedium { get; init; }
    public int TotalLow { get; init; }
    public int ExploitedVulnerabilities { get; init; }
    public int FixableVulnerabilities { get; init; }
    public double AverageRiskScore { get; init; }
    public IReadOnlyList<EcosystemBreakdownDto> Ecosystems { get; init; } = [];
    public IReadOnlyList<RiskiestRepositoryDto> RiskiestRepositories { get; init; } = [];
    public IReadOnlyList<VulnerablePackageDto> TopVulnerablePackages { get; init; } = [];
    public IReadOnlyList<TrendPointDto> Trend { get; init; } = [];
}

public sealed record EcosystemBreakdownDto(string Ecosystem, int Dependencies, int Vulnerabilities);

public sealed record RiskiestRepositoryDto(Guid Id, string FullName, double RiskScore, string RiskGrade, int Critical, int High, int Medium, int Low);

public sealed record VulnerablePackageDto(string PackageName, string Ecosystem, int Vulnerabilities, int Repositories, string HighestSeverity);

public sealed record TrendPointDto(DateOnly Date, int Critical, int High, int Medium, int Low, int Total);
