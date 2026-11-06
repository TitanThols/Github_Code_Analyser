namespace OSSDependencyAnalyzer.API.Models;

public enum Severity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum AdvisorySource
{
    GitHubAdvisories = 0,
    NVD = 1,
    Snyk = 2,
    OSV = 3
}

public enum DependencyType
{
    NPM = 0,
    PyPI = 1,
    NuGet = 2,
    Maven = 3,
    Gem = 4,
    Gradle = 5,
    Pub = 6,
    Composer = 7,
    Go = 8,
    Cargo = 9
}

public class Dependency
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public string PackageName { get; set; } = null!;

    /// <summary>Version constraint exactly as written in the manifest (e.g. "^4.17.1").</summary>
    public string CurrentVersion { get; set; } = null!;

    /// <summary>Concrete version used for advisory matching, or null when the constraint can't be pinned.</summary>
    public string? ResolvedVersion { get; set; }
    public string? LatestVersion { get; set; }
    public DependencyType Type { get; set; }
    public string? ManifestPath { get; set; }
    public bool IsDevDependency { get; set; }
    public DateTimeOffset DiscoveredAt { get; set; } = DateTimeOffset.UtcNow;

    public Repository Repository { get; set; } = null!;
    public ICollection<Vulnerability> Vulnerabilities { get; set; } = [];
}
