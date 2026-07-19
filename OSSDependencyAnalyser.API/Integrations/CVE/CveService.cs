using OSSDependencyAnalyzer.API.Models;
using System.Text;
using System.Text.Json;
using OSSDependencyAnalyzer.API.Data;
using Microsoft.Extensions.Caching.Distributed;
using Serilog;

namespace OSSDependencyAnalyzer.API.Integrations.CVE;

public interface ICveService
{
    Task<List<Vulnerability>> GetVulnerabilitiesForDependencyAsync(Guid dependencyId, string packageName, string version, DependencyType type);
}

public class CveService : ICveService
{
    private readonly INvdApiClient _nvdApiClient;
    private readonly IgithubAdvisoriesClient _githubAdvisoriesClient;
    private readonly IDistributedCache _cache;
    private readonly AppDbContext _db;
    private readonly ILogger<CveService> _logger;
    private const int CacheDurationHours = 24;

    public CveService(IgithubAdvisoriesClient githubAdvisoriesClient, INvdApiClient nvdApiClient, IDistributedCache cache, AppDbContext db, ILogger<CveService> logger)
    {
        _nvdApiClient = nvdApiClient;
        _githubAdvisoriesClient = githubAdvisoriesClient;
        _cache = cache;
        _db = db;
        _logger = logger;
    }

    public async Task<List<Vulnerability>> GetVulnerabilitiesForDependencyAsync(Guid dependencyId, string packageName, string version, DependencyType type)
    {
        var vulnerabilities = new List<Vulnerability>();

        try
        {
            var githubVulns = await GetGithubAdvisoriesAsync(packageName, type);
            if (githubVulns.Any())
            {
                foreach (var vuln in githubVulns)
                {
                    vulnerabilities.Add(new Vulnerability
                    {
                        Id = Guid.NewGuid(),
                        DependencyId = dependencyId,
                        CveId = vuln.Cve_Id ?? $"GHSA-{vuln.Ghsa_Id}",
                        Title = vuln.Summary,
                        Description = vuln.Description,
                        Severity = ParseSeverity(vuln.Severity),
                        CvssScore = (decimal?)vuln.Cvss_Score,
                        CvssVector = vuln.Cvss_Vector_String,
                        Source = AdvisorySource.GitHubAdvisories,
                        PublishedAt = DateTime.Parse(vuln.Published_At ?? DateTime.UtcNow.ToString()),
                        IsExploitable = false
                    });
                }
                _logger.LogInformation("Found {Count} vulnerabilities from GitHub Advisories for {Package}",
                    vulnerabilities.Count, packageName);
            }
            var nvdVulns = await GetNvdVulnerabilitiesAsync(packageName);
            if (nvdVulns.Any())
            {
                foreach (var vuln in nvdVulns)
                {

                    if (vulnerabilities.Any(v => v.CveId == vuln.Id))
                        continue;

                    var cvssScore = vuln.Metrics?.Cvss_V3_1?.Base_Score
                        ?? vuln.Metrics?.Cvss_V2_0?.Base_Score;

                    vulnerabilities.Add(new Vulnerability
                    {
                        Id = Guid.NewGuid(),
                        DependencyId = dependencyId,
                        CveId = vuln.Id,
                        Title = vuln.Id,
                        Description = vuln.Description ?? "No description available",
                        Severity = ParseNvdSeverity(cvssScore),
                        CvssScore = (decimal?)cvssScore,
                        CvssVector = vuln.Metrics?.Cvss_V3_1?.Vector_String,
                        Source = AdvisorySource.NVD,
                        PublishedAt = DateTime.Parse(vuln.Published ?? DateTime.UtcNow.ToString()),
                        IsExploitable = false
                    });
                }
                _logger.LogInformation("Found {Count} vulnerabilities from NVD for {Package}",
                    nvdVulns.Count, packageName);
            }
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch vulnerabilities for {package}/{Version}", packageName, version);
        }
        return vulnerabilities;
    }

    private async Task<List<GithubAdvisoryDto>> GetGithubAdvisoriesAsync(string packageName, DependencyType type)
    {
        var cacheKey = $"cve:github:{type}:{packageName}";

        var cachedData = await _cache.GetAsync(cacheKey);
        if(cachedData != null)
        {
            _logger.LogInformation("Cache hit for Github Advisories: {Package}", packageName);
            var json = Encoding.UTF8.GetString(cachedData);
            return JsonSerializer.Deserialize<List<GithubAdvisoryDto>>(json) ?? new();

        }

        try
        {
            var ecosystem = MapDependencyTypeToEcosystem(type);

            var query = new GithubGraphQLRequestDto
            {
                Query = @"
                    query($ecosystem: SecurityAdvisoryEcosystem!, $package: String!) {
                        securityAdvisories(first: 10, ecosystem: $ecosystem, package: $package) {
                            nodes {
                                ghsaId
                                cveIds(first: 1) {
                                    nodes {
                                        cveId
                                    }
                                }
                                summary
                                description
                                severity
                                cvssScore
                                publishedAt
                            }
                        }
                    }",
                Variables = new Dictionary<string, object>
                {
                    {"ecosystem", ecosystem},
                    {"package", packageName}
                }
            };
            _logger.LogInformation("Querying GitHub Advisories for {Package} in {Ecosystem}", packageName, ecosystem);
            var response = await _githubAdvisoriesClient.QueryAdvisoriesAsync(query);

            var advisories = new List<GithubAdvisoryDto>();
            if (response?.data?.securityAdvisories?.nodes != null)
            {
                advisories = ParseGitHubGraphQLResponse(response);
            }
            var json = JsonSerializer.Serialize(advisories);
            await _cache.SetAsync(cacheKey, Encoding.UTF8.GetBytes(json), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(CacheDurationHours)
            });

            return advisories;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch GitHub Advisories for {Package}", packageName);
            return [];
        }
    }
    private async Task<List<NvdCveDto>> GetNvdVulnerabilitiesAsync(string packageName)
    {
        var cacheKey = $"cve:nvd:{packageName}";

        var cachedData = await _cache.GetAsync(cacheKey);
        if (cachedData != null)
        {
            _logger.LogInformation("Cache hit for NVD: {Package}", packageName);
            var json = Encoding.UTF8.GetString(cachedData);
            return JsonSerializer.Deserialize<List<NvdCveDto>>(json) ?? new();
        }
        try
        {            
            _logger.LogInformation("NVD query attempted for {Package} (requires manual CVE lookup)", packageName);
            var nvdCves = new List<NvdCveDto>();

            var json = JsonSerializer.Serialize(nvdCves);
            await _cache.SetAsync(cacheKey, Encoding.UTF8.GetBytes(json), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(CacheDurationHours)
            });

            return nvdCves;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch NVD data for {Package}", packageName);
            return [];
        }
    }
    private string MapDependencyTypeToEcosystem(DependencyType type)
    {
        return type switch
        {
            DependencyType.NPM => "NPM",
            DependencyType.PyPI => "PIP",
            DependencyType.NuGet => "NUGET",
            DependencyType.Maven => "MAVEN",
            DependencyType.Gem => "RUBYGEMS",
            _ => "UNKNOWN"
        };
    }

    private Severity ParseSeverity(string githubSeverity)
    {
        return githubSeverity?.ToLower() switch
        {
            "critical" => Severity.Critical,
            "high" => Severity.High,
            "moderate" => Severity.Medium,
            "low" => Severity.Low,
            _ => Severity.Low
        };
    }

    private Severity ParseNvdSeverity(double? cvssScore)
    {
        return cvssScore switch
        {
            >= 9.0 => Severity.Critical,
            >= 7.0 => Severity.High,
            >= 4.0 => Severity.Medium,
            _ => Severity.Low
        };
    }
    private List<GithubAdvisoryDto> ParseGitHubGraphQLResponse(dynamic response)
    {
        var advisories = new List<GithubAdvisoryDto>();

        try
        {
            var nodes = response?.data?.securityAdvisories?.nodes;
            if (nodes == null)
                return advisories;

            foreach (var node in nodes)
            {
                var advisory = new GithubAdvisoryDto
                {
                    Ghsa_Id = node.ghsaId ?? string.Empty,
                    Cve_Id = node.cveIds?[0]?.cveId ?? string.Empty,
                    Summary = node.summary ?? string.Empty,
                    Description = node.description ?? string.Empty,
                    Severity = node.severity ?? "low",
                    Cvss_Score = node.cvssScore,
                    Published_At = node.publishedAt?.ToString()
                };
                advisories.Add(advisory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse GitHub GraphQL response");
        }

        return advisories;
    }
    
}