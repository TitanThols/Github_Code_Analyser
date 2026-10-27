using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OSSDependencyAnalyzer.API.Data;
using OSSDependencyAnalyzer.API.Integrations.DependencyParsing;
using OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsers;
using OSSDependencyAnalyzer.API.Integrations.Github;
using OSSDependencyAnalyzer.API.Integrations.Osv;
using OSSDependencyAnalyzer.API.Models;
using OSSDependencyAnalyzer.API.Services;
using OSSDependencyAnalyzer.Tests.TestSupport;

namespace OSSDependencyAnalyzer.Tests.Services;

public class AnalyzerServiceTests : IDisposable
{
    private readonly AppDbContext _db = TestDb.Create();
    private readonly IGithubApiService _github = Substitute.For<IGithubApiService>();
    private readonly IVulnerabilityService _vulnerabilities = Substitute.For<IVulnerabilityService>();
    private readonly IAlertService _alerts = Substitute.For<IAlertService>();
    private readonly Dictionary<string, string> _files = new();
    private readonly AnalyzerService _analyzer;

    public AnalyzerServiceTests()
    {
        var parsers = new DependencyParserFactory(
            [new NpmPackageJsonParser(), new PythonRequirementsParser(), new CsProjParser(), new DirectoryPackagesPropsParser()]);

        _github.GetRepositoryAsync(default!, default!, default)
            .ReturnsForAnyArgs(new RepositoryMetadata("A demo repo", "TypeScript", 1200, "main"));
        _github.FindManifestsAsync(default!, default!, default!, default)
            .ReturnsForAnyArgs(_ => _files.Keys.Select(path => new ManifestFile(path, null)).ToList());
        _github.GetFileContentAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(call => _files.GetValueOrDefault(call.ArgAt<ManifestFile>(3).Path));
        _vulnerabilities.FindVulnerabilitiesAsync(default!, default)
            .ReturnsForAnyArgs(new Dictionary<PackageQuery, IReadOnlyList<Vulnerability>>());

        _analyzer = new AnalyzerService(_db, _github, parsers, _vulnerabilities, new RiskScoringService(),
            new SnapshotService(_db, NullLogger<SnapshotService>.Instance), _alerts, NullLogger<AnalyzerService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private void GivenFindings(params (string Package, string Version, DependencyType Type, Vulnerability[] Vulns)[] findings) =>
        _vulnerabilities.FindVulnerabilitiesAsync(default!, default).ReturnsForAnyArgs(_ =>
            findings.ToDictionary(
                f => new PackageQuery(f.Package, f.Version, f.Type),
                f => (IReadOnlyList<Vulnerability>)f.Vulns.Select(Clone).ToList()));

    private static Vulnerability Vuln(string cve, Severity severity, decimal? cvss = null, bool exploited = false) => new()
    {
        CveId = cve, Title = cve, Description = "d", Severity = severity, CvssScore = cvss, IsExploitable = exploited,
        Source = AdvisorySource.OSV, PublishedAt = DateTime.UtcNow
    };

    private static Vulnerability Clone(Vulnerability v) => Vuln(v.CveId, v.Severity, v.CvssScore, v.IsExploitable);

    [Fact]
    public async Task CompletedScan_PersistsDependenciesFindingsCountsAndRisk()
    {
        var repository = _db.AddRepository();
        _files["package.json"] = """{ "dependencies": { "lodash": "^4.17.4", "express": "4.18.2" }, "devDependencies": { "jest": "29.0.0" } }""";
        GivenFindings(("lodash", "4.17.4", DependencyType.NPM, [Vuln("CVE-2019-10744", Severity.Critical, 9.1m, exploited: true), Vuln("CVE-2020-8203", Severity.High, 7.4m)]));

        await _analyzer.AnalyzeRepositoryAsync(repository.Id, CancellationToken.None);

        var saved = await _db.Repositories.AsNoTracking().SingleAsync();
        Assert.Equal(Repository.AnalysisStatus.Completed, saved.Status);
        Assert.Equal(3, saved.TotalDependencies);
        Assert.Equal(1, saved.ManifestCount);
        Assert.Equal(1, saved.CriticalVulnerabilities);
        Assert.Equal(1, saved.HighVulnerabilities);
        Assert.Equal("TypeScript", saved.Language);
        Assert.Equal(1200, saved.Stars);
        Assert.NotNull(saved.CompletedAt);
        Assert.Null(saved.ErrorMessage);
        Assert.InRange(saved.RiskScore, 95.5, 100); // critical + exploited, plus a high

        var lodash = await _db.Dependencies.Include(d => d.Vulnerabilities).SingleAsync(d => d.PackageName == "lodash");
        Assert.Equal("^4.17.4", lodash.CurrentVersion);
        Assert.Equal("4.17.4", lodash.ResolvedVersion);
        Assert.Equal("package.json", lodash.ManifestPath);
        Assert.Equal(2, lodash.Vulnerabilities.Count);
        Assert.Equal(95.5, lodash.Vulnerabilities.Single(v => v.Severity == Severity.Critical).RiskScore);
        Assert.True((await _db.Dependencies.SingleAsync(d => d.PackageName == "jest")).IsDevDependency);

        var snapshot = await _db.VulnerabilitySnapshots.SingleAsync();
        Assert.Equal(2, snapshot.TotalVulnerabilities);
        Assert.Equal(saved.RiskScore, snapshot.RiskScore);
    }

    [Fact]
    public async Task Scan_QueriesAdvisoriesOnlyForPinnableVersions()
    {
        var repository = _db.AddRepository();
        _files["package.json"] = """{ "dependencies": { "a": "1.0.0", "b": "github:user/b", "c": "*" } }""";
        IReadOnlyCollection<PackageQuery>? queried = null;
        _vulnerabilities.FindVulnerabilitiesAsync(Arg.Do<IReadOnlyCollection<PackageQuery>>(q => queried = q), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<PackageQuery, IReadOnlyList<Vulnerability>>());

        await _analyzer.AnalyzeRepositoryAsync(repository.Id, CancellationToken.None);

        Assert.Equal([new PackageQuery("a", "1.0.0", DependencyType.NPM)], queried);
        Assert.Equal(3, await _db.Dependencies.CountAsync());
    }

    [Fact]
    public async Task Scan_ContinuesPastAMalformedManifestAndReportsIt()
    {
        var repository = _db.AddRepository();
        _files["package.json"] = "{ this is not json";
        _files["requirements.txt"] = "requests==2.31.0";

        await _analyzer.AnalyzeRepositoryAsync(repository.Id, CancellationToken.None);

        var saved = await _db.Repositories.AsNoTracking().SingleAsync();
        Assert.Equal(Repository.AnalysisStatus.Completed, saved.Status);
        Assert.Equal(1, saved.TotalDependencies);
        Assert.Contains("package.json", saved.ErrorMessage);
    }

    [Fact]
    public async Task Scan_FailsWithHelpfulMessageWhenNoManifestsExist()
    {
        var repository = _db.AddRepository();

        await _analyzer.AnalyzeRepositoryAsync(repository.Id, CancellationToken.None);

        var saved = await _db.Repositories.AsNoTracking().SingleAsync();
        Assert.Equal(Repository.AnalysisStatus.Failed, saved.Status);
        Assert.StartsWith("No supported dependency manifests", saved.ErrorMessage);
        await _alerts.Received().SendAnalysisStatusAsync(Arg.Is<Repository>(r => r.Status == Repository.AnalysisStatus.Failed), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Scan_FailsWhenEveryManifestIsMalformed()
    {
        var repository = _db.AddRepository();
        _files["package.json"] = "nope";

        await _analyzer.AnalyzeRepositoryAsync(repository.Id, CancellationToken.None);

        Assert.StartsWith("Every manifest failed to parse", (await _db.Repositories.SingleAsync()).ErrorMessage);
    }

    [Fact]
    public async Task Scan_ReportsMissingRepository()
    {
        var repository = _db.AddRepository(owner: "ghost", name: "nope");
        _github.GetRepositoryAsync("ghost", "nope", Arg.Any<CancellationToken>()).ThrowsAsync(new GitHubRepositoryNotFoundException("ghost/nope"));

        await _analyzer.AnalyzeRepositoryAsync(repository.Id, CancellationToken.None);

        var saved = await _db.Repositories.AsNoTracking().SingleAsync();
        Assert.Equal(Repository.AnalysisStatus.Failed, saved.Status);
        Assert.Contains("was not found on GitHub", saved.ErrorMessage);
    }

    [Fact]
    public async Task Scan_MapsUpstreamOutagesToRetryableMessage()
    {
        var repository = _db.AddRepository();
        _files["package.json"] = """{ "dependencies": { "a": "1.0.0" } }""";
        _vulnerabilities.FindVulnerabilitiesAsync(default!, default).ThrowsAsyncForAnyArgs(new HttpRequestException("503"));

        await _analyzer.AnalyzeRepositoryAsync(repository.Id, CancellationToken.None);

        Assert.Contains("unavailable", (await _db.Repositories.SingleAsync()).ErrorMessage);
    }

    [Fact]
    public async Task Scan_UnexpectedErrorsAreRecordedWithoutLeakingDetailsAndRethrown()
    {
        var repository = _db.AddRepository();
        _github.GetRepositoryAsync(default!, default!, default).ThrowsAsyncForAnyArgs(new InvalidOperationException("connection string secret=hunter2"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _analyzer.AnalyzeRepositoryAsync(repository.Id, CancellationToken.None));

        var saved = await _db.Repositories.AsNoTracking().SingleAsync();
        Assert.Equal(Repository.AnalysisStatus.Failed, saved.Status);
        Assert.DoesNotContain("hunter2", saved.ErrorMessage);
    }

    [Fact]
    public async Task Scan_CancelledByShutdownIsResetToPending()
    {
        var repository = _db.AddRepository();
        using var cts = new CancellationTokenSource();
        _github.GetRepositoryAsync(default!, default!, default).ReturnsForAnyArgs<Task<RepositoryMetadata>>(_ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _analyzer.AnalyzeRepositoryAsync(repository.Id, cts.Token));

        Assert.Equal(Repository.AnalysisStatus.Pending, (await _db.Repositories.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Rescan_ReplacesPreviousResultsAndAlertsOnlyOnNewFindings()
    {
        var repository = _db.AddRepository();
        _files["package.json"] = """{ "dependencies": { "lodash": "4.17.4" } }""";
        GivenFindings(("lodash", "4.17.4", DependencyType.NPM, [Vuln("CVE-OLD", Severity.High)]));
        await _analyzer.AnalyzeRepositoryAsync(repository.Id, CancellationToken.None);
        await _alerts.DidNotReceiveWithAnyArgs().SendNewVulnerabilitiesAsync(default!, default!); // first scan: summary only

        GivenFindings(("lodash", "4.17.4", DependencyType.NPM, [Vuln("CVE-OLD", Severity.High), Vuln("CVE-NEW", Severity.Critical)]));
        await _analyzer.AnalyzeRepositoryAsync(repository.Id, CancellationToken.None);

        Assert.Equal(1, await _db.Dependencies.CountAsync());
        Assert.Equal(2, await _db.Vulnerabilities.CountAsync());
        Assert.Equal(2, await _db.VulnerabilitySnapshots.CountAsync());
        await _alerts.Received(1).SendNewVulnerabilitiesAsync(
            Arg.Any<Repository>(),
            Arg.Is<IReadOnlyCollection<(Vulnerability Vulnerability, string PackageName)>>(c => c.Count == 1 && c.Single().Vulnerability.CveId == "CVE-NEW"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Scan_ProgressesThroughStatusesAndNotifiesEachStep()
    {
        var repository = _db.AddRepository();
        _files["package.json"] = """{ "dependencies": { "a": "1.0.0" } }""";
        var statuses = new List<Repository.AnalysisStatus>();
        _alerts.SendAnalysisStatusAsync(Arg.Do<Repository>(r => statuses.Add(r.Status)), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await _analyzer.AnalyzeRepositoryAsync(repository.Id, CancellationToken.None);

        Assert.Equal(
            [Repository.AnalysisStatus.Parsing, Repository.AnalysisStatus.FetchingVulnerabilities, Repository.AnalysisStatus.Completed],
            statuses);
    }

    [Fact]
    public async Task Scan_OfDeletedRepositoryIsANoOp()
    {
        await _analyzer.AnalyzeRepositoryAsync(Guid.NewGuid(), CancellationToken.None);
        await _github.DidNotReceiveWithAnyArgs().GetRepositoryAsync(default!, default!, default);
    }

    [Fact]
    public void BuildDependencies_CollapsesDuplicatesAcrossManifests()
    {
        var parsed = new List<(ParsedDependency, string)>
        {
            (new ParsedDependency("react", "^18.2.0", DependencyType.NPM), "apps/web/package.json"),
            (new ParsedDependency("React", "18.2.0", DependencyType.NPM, IsDev: true), "packages/ui/package.json"),
            (new ParsedDependency("react", "^17.0.0", DependencyType.NPM), "legacy/package.json"),
            (new ParsedDependency("Serilog", "unknown", DependencyType.NuGet), "src/Api/Api.csproj"),
            (new ParsedDependency("Serilog", "3.1.1", DependencyType.NuGet), "Directory.Packages.props"),
            (new ParsedDependency("vitest", "^1.0.0", DependencyType.NPM, IsDev: true), "package.json"),
            (new ParsedDependency("local-lib", "file:../lib", DependencyType.NPM), "package.json"),
            (new ParsedDependency("local-lib", "workspace:*", DependencyType.NPM), "apps/web/package.json")
        };

        var result = AnalyzerService.BuildDependencies(Guid.NewGuid(), parsed);

        Assert.Equal(2, result.Count(d => d.PackageName.Equals("react", StringComparison.OrdinalIgnoreCase)));
        var react18 = result.Single(d => d.ResolvedVersion == "18.2.0");
        Assert.False(react18.IsDevDependency);                  // used at runtime by apps/web
        Assert.Equal("apps/web/package.json", react18.ManifestPath);

        var serilog = Assert.Single(result, d => d.PackageName == "Serilog");
        Assert.Equal("3.1.1", serilog.ResolvedVersion);         // CPM version wins over the unversioned reference

        Assert.True(result.Single(d => d.PackageName == "vitest").IsDevDependency);
        var local = Assert.Single(result, d => d.PackageName == "local-lib");
        Assert.Null(local.ResolvedVersion);
    }
}
