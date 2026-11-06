using System.Text.Json.Nodes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OSSDependencyAnalyzer.API.Hubs;
using OSSDependencyAnalyzer.API.Jobs;
using OSSDependencyAnalyzer.API.Models;
using OSSDependencyAnalyzer.API.Services;
using OSSDependencyAnalyzer.Tests.TestSupport;

namespace OSSDependencyAnalyzer.Tests.Services;

public class SbomBuilderTests
{
    private static (Repository, List<Dependency>) Sample()
    {
        var repository = new Repository { Id = Guid.NewGuid(), GithubUrl = "https://github.com/acme/app", RepositoryOwner = "acme", RepositoryName = "app" };
        var lodash = new Dependency
        {
            Id = Guid.NewGuid(), PackageName = "lodash", CurrentVersion = "^4.17.4", ResolvedVersion = "4.17.4", Type = DependencyType.NPM,
            ManifestPath = "package.json",
            Vulnerabilities =
            [
                new Vulnerability
                {
                    CveId = "CVE-2019-10744", Title = "Prototype Pollution", Description = "details", Severity = Severity.Critical,
                    CvssScore = 9.1m, CvssVector = "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:N/I:H/A:H", RemediationVersion = "4.17.12",
                    ReferenceUrl = "https://osv.dev/vulnerability/GHSA-jf85-cpcp-j695", IsExploitable = true, PublishedAt = new DateTime(2019, 7, 10)
                }
            ]
        };
        var jest = new Dependency { Id = Guid.NewGuid(), PackageName = "jest", CurrentVersion = "github:x/y", Type = DependencyType.NPM, IsDevDependency = true };
        return (repository, [lodash, jest]);
    }

    [Fact]
    public void Build_ProducesCycloneDx15Document()
    {
        var (repository, dependencies) = Sample();
        var bom = SbomBuilder.Build(repository, dependencies, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal("CycloneDX", (string?)bom["bomFormat"]);
        Assert.Equal("1.5", (string?)bom["specVersion"]);
        Assert.StartsWith("urn:uuid:", (string?)bom["serialNumber"]);
        Assert.Equal("acme/app", (string?)bom["metadata"]!["component"]!["name"]);

        var components = bom["components"]!.AsArray();
        Assert.Equal(2, components.Count);
        Assert.Equal("pkg:npm/lodash@4.17.4", (string?)components[0]!["purl"]);
        Assert.Equal("required", (string?)components[0]!["scope"]);
        Assert.Equal("optional", (string?)components[1]!["scope"]);
        Assert.Equal("pkg:npm/jest", (string?)components[1]!["purl"]); // unpinned → versionless purl
    }

    [Fact]
    public void Build_IncludesVulnerabilitiesWithRatingsAndRemediation()
    {
        var (repository, dependencies) = Sample();
        var vulnerability = SbomBuilder.Build(repository, dependencies, DateTimeOffset.UtcNow)["vulnerabilities"]!.AsArray().Single()!;

        Assert.Equal("CVE-2019-10744", (string?)vulnerability["id"]);
        Assert.Equal("pkg:npm/lodash@4.17.4", (string?)vulnerability["affects"]![0]!["ref"]);
        Assert.Equal("critical", (string?)vulnerability["ratings"]![0]!["severity"]);
        Assert.Equal("CVSSv31", (string?)vulnerability["ratings"]![0]!["method"]);
        Assert.Equal(9.1m, vulnerability["ratings"]![0]!["score"]!.GetValue<decimal>());
        Assert.Contains("4.17.12", (string?)vulnerability["recommendation"]);
        Assert.Equal("cisa:kev", (string?)vulnerability["properties"]![0]!["name"]);
    }

    [Fact]
    public void Build_SerializesToValidJson()
    {
        var (repository, dependencies) = Sample();
        var json = SbomBuilder.Build(repository, dependencies, DateTimeOffset.UtcNow).ToJsonString();
        Assert.NotNull(JsonNode.Parse(json));
    }
}

public class SnapshotServiceTests
{
    [Fact]
    public async Task CreateSnapshot_CapturesCountsAndAverages()
    {
        using var db = TestDb.Create();
        var repository = db.AddRepository(status: Repository.AnalysisStatus.Completed);
        repository.CriticalVulnerabilities = 1;
        repository.HighVulnerabilities = 1;
        repository.RiskScore = 91.2;
        var dependency = new Dependency { Id = Guid.NewGuid(), RepositoryId = repository.Id, PackageName = "p", CurrentVersion = "1" };
        dependency.Vulnerabilities.Add(new Vulnerability { Id = Guid.NewGuid(), CveId = "A", Title = "a", Description = "a", RiskScore = 90 });
        dependency.Vulnerabilities.Add(new Vulnerability { Id = Guid.NewGuid(), CveId = "B", Title = "b", Description = "b", RiskScore = 60 });
        db.Dependencies.Add(dependency);
        await db.SaveChangesAsync();

        await new SnapshotService(db, NullLogger<SnapshotService>.Instance).CreateSnapshotAsync(repository.Id);

        var snapshot = Assert.Single(db.VulnerabilitySnapshots);
        Assert.Equal(2, snapshot.TotalVulnerabilities);
        Assert.Equal(75m, snapshot.AverageRiskScore);
        Assert.Equal(91.2, snapshot.RiskScore);
        Assert.Equal(1, snapshot.CriticalCount);
    }

    [Fact]
    public async Task CreateSnapshot_IgnoresUnknownRepository()
    {
        using var db = TestDb.Create();
        await new SnapshotService(db, NullLogger<SnapshotService>.Instance).CreateSnapshotAsync(Guid.NewGuid());
        Assert.Empty(db.VulnerabilitySnapshots);
    }
}

public class AlertServiceTests
{
    private readonly IHubContext<AlertHub> _hub = Substitute.For<IHubContext<AlertHub>>();
    private readonly IClientProxy _client = Substitute.For<IClientProxy>();
    private readonly AlertService _alerts;

    public AlertServiceTests()
    {
        _hub.Clients.User(Arg.Any<string>()).Returns(_client);
        _alerts = new AlertService(_hub, NullLogger<AlertService>.Instance);
    }

    private static Repository Repo(Guid? userId) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, RepositoryOwner = "acme", RepositoryName = "app", GithubUrl = "u",
        Status = Repository.AnalysisStatus.Completed
    };

    [Fact]
    public async Task StatusAlertsGoOnlyToTheOwningUser()
    {
        var userId = Guid.NewGuid();
        await _alerts.SendAnalysisStatusAsync(Repo(userId));

        _hub.Clients.Received(1).User(userId.ToString());
        await _client.Received(1).SendCoreAsync(AlertHub.ReceiveAlert, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LegacyRepositoriesWithoutOwnerAreNotBroadcast()
    {
        await _alerts.SendAnalysisStatusAsync(Repo(null));
        _hub.Clients.DidNotReceiveWithAnyArgs().User(default!);
    }

    [Fact]
    public async Task NewVulnerabilityAlertsAreCappedAndOrderedByRisk()
    {
        var findings = Enumerable.Range(0, 25)
            .Select(i => (new Vulnerability { CveId = $"CVE-{i}", Title = "t", Description = "d", RiskScore = i }, "pkg"))
            .ToList();

        await _alerts.SendNewVulnerabilitiesAsync(Repo(Guid.NewGuid()), findings);

        await _client.Received(AlertService.MaxVulnerabilityAlertsPerScan)
            .SendCoreAsync(AlertHub.ReceiveAlert, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeliveryFailuresAreSwallowed()
    {
        _client.SendCoreAsync(default!, default!, default).ThrowsAsyncForAnyArgs(new IOException("socket closed"));
        await _alerts.SendAnalysisStatusAsync(Repo(Guid.NewGuid())); // does not throw
    }
}

public class DailyRescanJobTests
{
    [Fact]
    public async Task QueuesEveryCompletedOwnedRepository()
    {
        using var db = TestDb.Create();
        var completed = db.AddRepository(status: Repository.AnalysisStatus.Completed);
        db.AddRepository(name: "failed", status: Repository.AnalysisStatus.Failed);
        db.AddRepository(name: "running", status: Repository.AnalysisStatus.Parsing);
        var legacy = db.AddRepository(name: "legacy", status: Repository.AnalysisStatus.Completed);
        legacy.UserId = null;
        await db.SaveChangesAsync();
        var queue = new RecordingAnalysisQueue();

        await new DailyRescanJob(db, queue, NullLogger<DailyRescanJob>.Instance).ExecuteAsync();

        Assert.Equal([completed.Id], queue.Enqueued);
    }
}
