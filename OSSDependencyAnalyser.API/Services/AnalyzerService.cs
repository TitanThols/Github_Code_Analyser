using Hangfire;
using Microsoft.EntityFrameworkCore;
using OSSDependencyAnalyzer.API.Data;
using OSSDependencyAnalyzer.API.Domain;
using OSSDependencyAnalyzer.API.Integrations.DependencyParsing;
using OSSDependencyAnalyzer.API.Integrations.Github;
using OSSDependencyAnalyzer.API.Integrations.Osv;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Services;

public interface IAnalyzerService
{
    [AutomaticRetry(Attempts = 0)]
    Task AnalyzeRepositoryAsync(Guid repositoryId, CancellationToken cancellationToken);
}

/// <summary>An expected analysis failure whose message is safe to show to the user.</summary>
public sealed class AnalysisException(string message) : Exception(message);

/// <summary>
/// Pipeline: GitHub metadata → manifest discovery → parse → normalise/dedupe → OSV batch lookup
/// (+ CISA KEV) → risk scoring → persist → snapshot → notify. Idempotent: a re-scan replaces the
/// previous dependency set and only alerts on vulnerabilities that weren't present before.
/// </summary>
public sealed class AnalyzerService(
    AppDbContext db,
    IGithubApiService github,
    IDependencyParserFactory parserFactory,
    IVulnerabilityService vulnerabilityService,
    IRiskScoringService riskScoring,
    ISnapshotService snapshots,
    IAlertService alerts,
    ILogger<AnalyzerService> logger) : IAnalyzerService
{
    public async Task AnalyzeRepositoryAsync(Guid repositoryId, CancellationToken cancellationToken)
    {
        var repository = await db.Repositories.FirstOrDefaultAsync(r => r.Id == repositoryId, cancellationToken);
        if (repository is null)
        {
            logger.LogWarning("Repository {RepositoryId} no longer exists; skipping analysis", repositoryId);
            return;
        }

        using var scope = logger.BeginScope(new Dictionary<string, object> { ["RepositoryId"] = repositoryId });
        var started = DateTimeOffset.UtcNow;

        try
        {
            var previousFindings = (await db.Vulnerabilities
                    .Where(v => v.Dependency.RepositoryId == repositoryId)
                    .Select(v => new { v.Dependency.PackageName, v.CveId })
                    .ToListAsync(cancellationToken))
                .Select(f => FindingKey(f.PackageName, f.CveId))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var isRescan = repository.LastAnalysedAt != default;

            await UpdateStatusAsync(repository, Repository.AnalysisStatus.Parsing, cancellationToken);

            var metadata = await github.GetRepositoryAsync(repository.RepositoryOwner, repository.RepositoryName, cancellationToken);
            repository.Description = Truncate(metadata.Description, 1000);
            repository.Language = metadata.Language;
            repository.Stars = metadata.Stars;

            var (parsed, manifestCount, warnings) = await CollectDependenciesAsync(repository, metadata.Ref, cancellationToken);
            var dependencies = BuildDependencies(repositoryId, parsed);

            await UpdateStatusAsync(repository, Repository.AnalysisStatus.FetchingVulnerabilities, cancellationToken);

            var queries = dependencies
                .Where(d => d.ResolvedVersion is not null)
                .Select(d => new PackageQuery(d.PackageName, d.ResolvedVersion!, d.Type))
                .Distinct()
                .ToList();
            var findings = await vulnerabilityService.FindVulnerabilitiesAsync(queries, cancellationToken);

            var allVulnerabilities = new List<(Vulnerability Vulnerability, string PackageName)>();
            foreach (var dependency in dependencies)
            {
                if (dependency.ResolvedVersion is null
                    || !findings.TryGetValue(new PackageQuery(dependency.PackageName, dependency.ResolvedVersion, dependency.Type), out var matches))
                    continue;

                foreach (var vulnerability in matches)
                {
                    vulnerability.Id = Guid.NewGuid();
                    vulnerability.DependencyId = dependency.Id;
                    vulnerability.RiskScore = riskScoring.CalculateVulnerabilityRiskScore(vulnerability);
                    dependency.Vulnerabilities.Add(vulnerability);
                    allVulnerabilities.Add((vulnerability, dependency.PackageName));
                }
            }

            // Replace the previous scan's results in the same unit of work as the new ones.
            var stale = await db.Dependencies
                .Include(d => d.Vulnerabilities)
                .Where(d => d.RepositoryId == repositoryId)
                .ToListAsync(cancellationToken);
            db.Dependencies.RemoveRange(stale);
            db.Dependencies.AddRange(dependencies);

            var vulnerabilities = allVulnerabilities.Select(v => v.Vulnerability).ToList();
            repository.TotalDependencies = dependencies.Count;
            repository.ManifestCount = manifestCount;
            repository.CriticalVulnerabilities = vulnerabilities.Count(v => v.Severity == Severity.Critical);
            repository.HighVulnerabilities = vulnerabilities.Count(v => v.Severity == Severity.High);
            repository.MediumVulnerabilities = vulnerabilities.Count(v => v.Severity == Severity.Medium);
            repository.LowVulnerabilities = vulnerabilities.Count(v => v.Severity == Severity.Low);
            repository.RiskScore = riskScoring.CalculateRepositoryRiskScore(vulnerabilities.Select(v => v.RiskScore));
            repository.Status = Repository.AnalysisStatus.Completed;
            repository.CompletedAt = DateTimeOffset.UtcNow;
            repository.LastAnalysedAt = DateTimeOffset.UtcNow;
            repository.ErrorMessage = warnings.Count == 0
                ? null
                : Truncate($"Skipped {warnings.Count} unreadable manifest(s): {string.Join("; ", warnings)}", 1000);

            await db.SaveChangesAsync(cancellationToken);
            await snapshots.CreateSnapshotAsync(repositoryId, cancellationToken);

            logger.LogInformation(
                "Analysed {Owner}/{Repo} in {ElapsedMs} ms: {Manifests} manifests, {Dependencies} dependencies, {Vulnerabilities} vulnerabilities, risk {Risk}",
                repository.RepositoryOwner, repository.RepositoryName, (DateTimeOffset.UtcNow - started).TotalMilliseconds,
                manifestCount, dependencies.Count, vulnerabilities.Count, repository.RiskScore);

            await alerts.SendAnalysisStatusAsync(repository, cancellationToken);
            if (isRescan)
            {
                var introduced = allVulnerabilities
                    .Where(v => !previousFindings.Contains(FindingKey(v.PackageName, v.Vulnerability.CveId)))
                    .ToList();
                await alerts.SendNewVulnerabilitiesAsync(repository, introduced, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Server shutdown: Hangfire re-queues the job, so reset to Pending rather than Failed.
            await MarkAsync(repositoryId, Repository.AnalysisStatus.Pending, null);
            throw;
        }
        catch (Exception ex)
        {
            var (message, expected) = ex switch
            {
                AnalysisException or GitHubRepositoryNotFoundException => (ex.Message, true),
                HttpRequestException => ("An upstream service (GitHub or OSV.dev) is unavailable. Please try again in a few minutes.", true),
                _ => ("Unexpected error while analysing the repository.", false)
            };

            if (expected)
                logger.LogWarning(ex, "Analysis of {RepositoryId} failed: {Reason}", repositoryId, message);
            else
                logger.LogError(ex, "Analysis of {RepositoryId} failed unexpectedly", repositoryId);

            var failed = await MarkAsync(repositoryId, Repository.AnalysisStatus.Failed, message);
            if (failed is not null)
                await alerts.SendAnalysisStatusAsync(failed, CancellationToken.None);

            if (!expected)
                throw;
        }
    }

    private async Task<(List<(ParsedDependency Dependency, string ManifestPath)> Parsed, int ManifestCount, List<string> Warnings)>
        CollectDependenciesAsync(Repository repository, string gitRef, CancellationToken cancellationToken)
    {
        var manifests = await github.FindManifestsAsync(repository.RepositoryOwner, repository.RepositoryName, gitRef, cancellationToken);
        var parsed = new List<(ParsedDependency, string)>();
        var warnings = new List<string>();
        var manifestCount = 0;

        foreach (var manifest in manifests)
        {
            var parser = parserFactory.TryGetParserByFileName(manifest.Path);
            if (parser is null)
                continue;

            var content = await github.GetFileContentAsync(repository.RepositoryOwner, repository.RepositoryName, gitRef, manifest, cancellationToken);
            if (content is null)
                continue;

            manifestCount++;
            try
            {
                var dependencies = parser.Parse(content);
                parsed.AddRange(dependencies.Select(d => (d, manifest.Path)));
                logger.LogDebug("Parsed {Count} dependencies from {Path}", dependencies.Count, manifest.Path);
            }
            catch (DependencyParseException ex)
            {
                warnings.Add($"{manifest.Path} ({ex.Message})");
            }
        }

        if (manifestCount == 0)
            throw new AnalysisException(
                "No supported dependency manifests were found (package.json, requirements.txt, pyproject.toml, pom.xml, " +
                "build.gradle, *.csproj, Gemfile, go.mod, composer.json or Cargo.toml).");

        if (parsed.Count == 0 && warnings.Count == manifestCount)
            throw new AnalysisException($"Every manifest failed to parse: {string.Join("; ", warnings)}");

        return (parsed, manifestCount, warnings);
    }

    /// <summary>
    /// Collapses duplicates across manifests (monorepos, Central Package Management) into one row per
    /// package version. Unpinnable declarations are dropped when a pinned one exists for the same package.
    /// </summary>
    internal static List<Dependency> BuildDependencies(Guid repositoryId, IEnumerable<(ParsedDependency Dependency, string ManifestPath)> parsed)
    {
        var result = new List<Dependency>();

        foreach (var package in parsed.GroupBy(p => (p.Dependency.Type, Name: p.Dependency.PackageName.ToLowerInvariant())))
        {
            var candidates = package
                .Select(p => (p.Dependency, p.ManifestPath, Resolved: VersionNormalizer.Normalize(p.Dependency.Version, p.Dependency.Type)))
                .ToList();

            var pinned = candidates.Where(c => c.Resolved is not null).ToList();
            var versions = pinned.Count > 0
                ? pinned.GroupBy(c => c.Resolved)
                : candidates.GroupBy(c => (string?)null).Take(1);

            foreach (var version in versions)
            {
                var first = version.First();
                result.Add(new Dependency
                {
                    Id = Guid.NewGuid(),
                    RepositoryId = repositoryId,
                    PackageName = first.Dependency.PackageName,
                    CurrentVersion = Truncate(first.Dependency.Version, 200)!,
                    ResolvedVersion = version.Key,
                    Type = first.Dependency.Type,
                    ManifestPath = first.ManifestPath,
                    IsDevDependency = version.All(c => c.Dependency.IsDev),
                    DiscoveredAt = DateTimeOffset.UtcNow
                });
            }
        }

        return result.OrderBy(d => d.Type).ThenBy(d => d.PackageName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task UpdateStatusAsync(Repository repository, Repository.AnalysisStatus status, CancellationToken cancellationToken)
    {
        repository.Status = status;
        repository.ErrorMessage = null;
        await db.SaveChangesAsync(cancellationToken);
        await alerts.SendAnalysisStatusAsync(repository, cancellationToken);
    }

    private async Task<Repository?> MarkAsync(Guid repositoryId, Repository.AnalysisStatus status, string? message)
    {
        db.ChangeTracker.Clear();
        var repository = await db.Repositories.FirstOrDefaultAsync(r => r.Id == repositoryId, CancellationToken.None);
        if (repository is null)
            return null;

        repository.Status = status;
        repository.ErrorMessage = message;
        if (status == Repository.AnalysisStatus.Failed)
            repository.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(CancellationToken.None);
        return repository;
    }

    private static string FindingKey(string packageName, string cveId) => $"{packageName}|{cveId}";

    private static string? Truncate(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
}
