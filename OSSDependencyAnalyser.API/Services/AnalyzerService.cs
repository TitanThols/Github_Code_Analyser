using OSSDependencyAnalyzer.API.Models;
using OSSDependencyAnalyzer.API.Data;
using OSSDependencyAnalyzer.API.Integrations.Github;
using OSSDependencyAnalyzer.API.Integrations.DependencyParsing;
using OSSDependencyAnalyzer.API.Integrations.CVE;
using OSSDependencyAnalyzer.API.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace OSSDependencyAnalyzer.API.Services;

public interface IAnalyzerService
{
    Task AnalyzeRepositoryAsync(Guid repositoryId, CancellationToken cancellationToken);
}

public class AnalyzerService : IAnalyzerService
{
    private readonly AppDbContext _db;
    private readonly IGithubApiService _githubApiService;
    private readonly IDependencyParserFactory _parserFactory;
    private readonly ICveService _cveService;
    private readonly IRiskScoringService _riskScoringService;
    private readonly ILogger<AnalyzerService> _logger;

    private static readonly List<string> DependencyFiles = new()
    {
        "package.json",
        "requirements.txt",
        "*.csproj",
        "pom.xml",
        "Gemfile",
        "pyproject.toml"
    };

    public AnalyzerService(
        AppDbContext db,
        IGithubApiService githubApiService,
        IDependencyParserFactory parserFactory,
        ICveService cveService,
        IRiskScoringService riskScoringService,
        ILogger<AnalyzerService> logger)
    {
        _db = db;
        _githubApiService = githubApiService;
        _parserFactory = parserFactory;
        _cveService = cveService;
        _riskScoringService = riskScoringService;
        _logger = logger;
    }

    public async Task AnalyzeRepositoryAsync(Guid repositoryId, CancellationToken cancellationToken)
    {
        try
        {
            var repository = await _db.Repositories.FirstOrDefaultAsync(r => r.Id == repositoryId, cancellationToken);
            if (repository == null)
            {
                _logger.LogError("Repository {RepositoryId} not found", repositoryId);
                return;
            }

            repository.Status = Repository.AnalysisStatus.Parsing;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Starting analysis for {Url}", repository.GithubUrl);

            var repoMetadata = await _githubApiService.GetRepositoryAsync(
                repository.RepositoryOwner,
                repository.RepositoryName
            );

            _logger.LogInformation("Fetched metadata for {Owner}/{Repo}",
                repository.RepositoryOwner,
                repository.RepositoryName);

            var parsedDependencies = new List<ParsedDependency>();

            foreach (var fileName in new[] { "package.json", "requirements.txt", "pom.xml", "Gemfile", "pyproject.toml" })
            {
                try
                {
                    var fileContent = await _githubApiService.GetFileContentAsync(
                        repository.RepositoryOwner,
                        repository.RepositoryName,
                        fileName
                    );

                    if (fileContent == null)
                    {
                        _logger.LogInformation("File {FileName} not found in repo", fileName);
                        continue;
                    }

                    var parser = _parserFactory.TryGetParserByFileName(fileName);
                    if (parser == null)
                    {
                        _logger.LogWarning("No parser found for {FileName}", fileName);
                        continue;
                    }

                    var dependencies = await parser.ParseAsync(fileContent);
                    parsedDependencies.AddRange(dependencies);

                    _logger.LogInformation("Parsed {Count} dependencies from {FileName}",
                        dependencies.Count,
                        fileName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse {FileName}", fileName);
                }
            }

            repository.Status = Repository.AnalysisStatus.FetchingVulnerabilities;
            await _db.SaveChangesAsync(cancellationToken);

            var allVulnerabilities = new List<Vulnerability>();
            var criticalCount = 0;
            var highCount = 0;
            var mediumCount = 0;
            var lowCount = 0;

            foreach (var parsedDep in parsedDependencies)
            {
                var dependency = new Dependency
                {
                    Id = Guid.NewGuid(),
                    RepositoryId = repositoryId,
                    PackageName = parsedDep.PackageName,
                    CurrentVersion = parsedDep.Version,
                    Type = parsedDep.Type,
                    DiscoveredAt = DateTime.UtcNow
                };

                _db.Dependencies.Add(dependency);
                await _db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Added dependency {PackageName}/{Version}",
                    parsedDep.PackageName,
                    parsedDep.Version);

                try
                {
                    var vulnerabilities = await _cveService.GetVulnerabilitiesForDependencyAsync(
                        dependency.Id,
                        parsedDep.PackageName,
                        parsedDep.Version,
                        parsedDep.Type
                    );

                    if (vulnerabilities.Any())
                    {
                        _logger.LogInformation("Found {Count} vulnerabilities for {PackageName}",
                            vulnerabilities.Count,
                            parsedDep.PackageName);

                        foreach (var vuln in vulnerabilities)
                        {
                            vuln.Id = Guid.NewGuid();
                            vuln.DependencyId = dependency.Id;

                            var riskScore = _riskScoringService.CalculateVulnerabilityRiskScore(vuln);
                            vuln.RiskScore = riskScore;

                            allVulnerabilities.Add(vuln);

                            switch (vuln.Severity)
                            {
                                case Severity.Critical:
                                    criticalCount++;
                                    break;
                                case Severity.High:
                                    highCount++;
                                    break;
                                case Severity.Medium:
                                    mediumCount++;
                                    break;
                                case Severity.Low:
                                    lowCount++;
                                    break;
                            }

                            _logger.LogInformation(
                                "CVE {CveId} found in {PackageName}: Severity={Severity}, RiskScore={RiskScore}",
                                vuln.CveId,
                                parsedDep.PackageName,
                                vuln.Severity,
                                riskScore
                            );
                        }

                        _db.Vulnerabilities.AddRange(vulnerabilities);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch vulnerabilities for {PackageName}",
                        parsedDep.PackageName);
                }
            }

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Saved {Count} vulnerabilities: Critical={Critical}, High={High}, Medium={Medium}, Low={Low}",
                allVulnerabilities.Count,
                criticalCount,
                highCount,
                mediumCount,
                lowCount
            );

            repository.Status = Repository.AnalysisStatus.Completed;
            repository.CompletedAt = DateTime.UtcNow;
            repository.LastAnalysedAt = DateTime.UtcNow;
            repository.TotalDependencies = parsedDependencies.Count;
            repository.CriticalVulnerabilities = criticalCount;
            repository.HighVulnerabilities = highCount;
            repository.MediumVulnerabilities = mediumCount;

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Analysis completed for {Url}. Found {Dependencies} dependencies, {Vulns} vulnerabilities",
                repository.GithubUrl,
                parsedDependencies.Count,
                allVulnerabilities.Count
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analysis failed for repository {RepositoryId}", repositoryId);

            var repository = await _db.Repositories.FirstOrDefaultAsync(r => r.Id == repositoryId);
            if (repository != null)
            {
                repository.Status = Repository.AnalysisStatus.Failed;
                repository.ErrorMessage = ex.Message;
                await _db.SaveChangesAsync();
            }

            throw;
        }
    }
}