using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyModel;

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
    Snyk = 2
}

public class Dependency
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public string PackageName { get; set; } = null!;
    public string CurrentVersion { get; set; } = null!;
    public string? LatestVersion { get; set; }
    public DependencyType Type { get; set; }
    public DateTimeOffset DiscoveredAt { get; set; } = DateTime.UtcNow;

    public Repository Repository { get; set; } = null!;
    public ICollection<Vulnerability> Vulnerabilities { get; set; } = [];
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
    Composer = 7
}

