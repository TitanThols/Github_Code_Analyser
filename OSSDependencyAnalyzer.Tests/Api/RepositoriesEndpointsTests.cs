using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OSSDependencyAnalyzer.API.Models;
using OSSDependencyAnalyzer.Tests.TestSupport;

namespace OSSDependencyAnalyzer.Tests.Api;

public class RepositoriesEndpointsTests(DepScanApiFactory factory) : IClassFixture<DepScanApiFactory>
{
    private static async Task<Guid> UserIdOf(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("id").GetGuid();

    private Repository Seed(Guid userId, Repository.AnalysisStatus status, Action<Repository>? configure = null) =>
        factory.WithDb(db =>
        {
            var repository = db.AddRepository(userId, owner: "acme", name: $"repo-{Guid.NewGuid():N}"[..20], status: status);
            configure?.Invoke(repository);
            db.SaveChanges();
            return repository;
        });

    private Repository SeedScanned(Guid userId) => factory.WithDb(db =>
    {
        var repository = db.AddRepository(userId, owner: "acme", name: $"scanned-{Guid.NewGuid():N}"[..20], status: Repository.AnalysisStatus.Completed);
        repository.TotalDependencies = 2;
        repository.CriticalVulnerabilities = 1;
        repository.MediumVulnerabilities = 1;
        repository.RiskScore = 90;
        repository.LastAnalysedAt = DateTimeOffset.UtcNow;

        var lodash = new Dependency
        {
            Id = Guid.NewGuid(), RepositoryId = repository.Id, PackageName = "lodash", CurrentVersion = "^4.17.4",
            ResolvedVersion = "4.17.4", Type = DependencyType.NPM, ManifestPath = "package.json"
        };
        lodash.Vulnerabilities.Add(new Vulnerability
        {
            Id = Guid.NewGuid(), CveId = "CVE-2019-10744", Title = "Prototype Pollution", Description = "d",
            Severity = Severity.Critical, CvssScore = 9.1m, RiskScore = 85.5, RemediationVersion = "4.17.12", IsExploitable = true
        });
        lodash.Vulnerabilities.Add(new Vulnerability
        {
            Id = Guid.NewGuid(), CveId = "CVE-2020-0001", Title = "ReDoS", Description = "d", Severity = Severity.Medium, RiskScore = 40
        });
        var express = new Dependency
        {
            Id = Guid.NewGuid(), RepositoryId = repository.Id, PackageName = "express", CurrentVersion = "4.18.2",
            ResolvedVersion = "4.18.2", Type = DependencyType.NPM, IsDevDependency = true
        };
        db.Dependencies.AddRange(lodash, express);
        db.VulnerabilitySnapshots.Add(new VulnerabilitySnapshot
        {
            Id = Guid.NewGuid(), RepositoryId = repository.Id, SnapshotDate = DateTime.UtcNow.AddDays(-1), CriticalCount = 1, MediumCount = 1, TotalVulnerabilities = 2, RiskScore = 90
        });
        db.SaveChanges();
        return repository;
    });

    [Fact]
    public async Task Endpoints_RequireAuthentication()
    {
        var anonymous = factory.CreateBrowserClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/repositories")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/repositories", new { repositoryUrl = "a/b" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/dashboard")).StatusCode);
    }

    [Fact]
    public async Task Create_QueuesAnalysisAndDeduplicatesEquivalentUrls()
    {
        var client = await factory.CreateSignedInClientAsync();

        var created = await client.PostAsJsonAsync("/api/repositories", new { repositoryUrl = "https://github.com/ExpressJS/Express.git" });
        var again = await client.PostAsJsonAsync("/api/repositories", new { repositoryUrl = "expressjs/express" });

        Assert.Equal(HttpStatusCode.Accepted, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        var first = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", first.GetProperty("status").GetString());
        Assert.Equal("https://github.com/expressjs/express", first.GetProperty("gitHubUrl").GetString());
        Assert.Equal("ExpressJS", first.GetProperty("repositoryOwner").GetString());

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var id = first.GetProperty("id").GetGuid();
        Assert.Equal(id, (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        Assert.Single(factory.Queue.Enqueued, queued => queued == id);
    }

    [Fact]
    public async Task Create_SameRepositoryForDifferentUsersCreatesSeparateRecords()
    {
        var alice = await factory.CreateSignedInClientAsync();
        var bob = await factory.CreateSignedInClientAsync();

        var a = await (await alice.PostAsJsonAsync("/api/repositories", new { repositoryUrl = "pallets/flask" })).Content.ReadFromJsonAsync<JsonElement>();
        var b = await (await bob.PostAsJsonAsync("/api/repositories", new { repositoryUrl = "pallets/flask" })).Content.ReadFromJsonAsync<JsonElement>();

        Assert.NotEqual(a.GetProperty("id").GetGuid(), b.GetProperty("id").GetGuid());
    }

    [Theory]
    [InlineData("https://gitlab.com/a/b")]
    [InlineData("not-a-repo")]
    [InlineData("")]
    public async Task Create_RejectsInvalidReferencesWithValidationProblem(string url)
    {
        var client = await factory.CreateSignedInClientAsync();

        var response = await client.PostAsJsonAsync("/api/repositories", new { repositoryUrl = url });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("RepositoryUrl", out _));
    }

    [Fact]
    public async Task UsersCannotSeeOrModifyEachOthersRepositories()
    {
        var owner = await factory.CreateSignedInClientAsync();
        var intruder = await factory.CreateSignedInClientAsync();
        var repository = SeedScanned(await UserIdOf(owner));

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/repositories/{repository.Id}")).StatusCode);
        foreach (var path in new[] { "", "/dependencies", "/vulnerabilities", "/snapshots", "/sbom" })
            Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/api/repositories/{repository.Id}{path}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.DeleteAsync($"/api/repositories/{repository.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PostAsync($"/api/repositories/{repository.Id}/rescan", null)).StatusCode);
        Assert.Empty(await intruder.GetFromJsonAsync<JsonElement[]>("/api/repositories") ?? []);
    }

    [Fact]
    public async Task List_SupportsSearchAndStatusFilters()
    {
        var client = await factory.CreateSignedInClientAsync();
        var userId = await UserIdOf(client);
        var scanned = SeedScanned(userId);
        Seed(userId, Repository.AnalysisStatus.Failed);

        var all = await client.GetFromJsonAsync<JsonElement[]>("/api/repositories") ?? [];
        var completed = await client.GetFromJsonAsync<JsonElement[]>("/api/repositories?status=Completed") ?? [];
        var searched = await client.GetFromJsonAsync<JsonElement[]>($"/api/repositories?search={scanned.RepositoryName.ToUpperInvariant()}") ?? [];

        Assert.Equal(2, all.Length);
        Assert.Equal(scanned.Id, Assert.Single(completed).GetProperty("id").GetGuid());
        Assert.Equal(scanned.Id, Assert.Single(searched).GetProperty("id").GetGuid());
        Assert.Equal("F", completed[0].GetProperty("riskGrade").GetString());
        Assert.Equal(2, completed[0].GetProperty("totalVulnerabilities").GetInt32());
    }

    [Fact]
    public async Task Rescan_IsRejectedWhileRunningAndQueuedOtherwise()
    {
        var client = await factory.CreateSignedInClientAsync();
        var userId = await UserIdOf(client);
        var running = Seed(userId, Repository.AnalysisStatus.FetchingVulnerabilities);
        var done = Seed(userId, Repository.AnalysisStatus.Failed, r => r.ErrorMessage = "old failure");

        var conflict = await client.PostAsync($"/api/repositories/{running.Id}/rescan", null);
        var accepted = await client.PostAsync($"/api/repositories/{done.Id}/rescan", null);

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        var body = await accepted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", body.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("errorMessage").ValueKind);
        Assert.Contains(done.Id, factory.Queue.Enqueued);
    }

    [Fact]
    public async Task Delete_RemovesRepositoryAndItsData()
    {
        var client = await factory.CreateSignedInClientAsync();
        var repository = SeedScanned(await UserIdOf(client));

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/repositories/{repository.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/repositories/{repository.Id}")).StatusCode);
        Assert.False(factory.WithDb(db => db.Dependencies.Any(d => d.RepositoryId == repository.Id)));
    }

    [Fact]
    public async Task Dependencies_AreSortedMostSevereFirstWithHighestSeverity()
    {
        var client = await factory.CreateSignedInClientAsync();
        var repository = SeedScanned(await UserIdOf(client));

        var dependencies = await client.GetFromJsonAsync<JsonElement[]>($"/api/repositories/{repository.Id}/dependencies") ?? [];

        Assert.Equal(["lodash", "express"], dependencies.Select(d => d.GetProperty("packageName").GetString()));
        Assert.Equal("Critical", dependencies[0].GetProperty("highestSeverity").GetString());
        Assert.Equal(2, dependencies[0].GetProperty("vulnerabilityCount").GetInt32());
        Assert.Equal("4.17.4", dependencies[0].GetProperty("resolvedVersion").GetString());
        Assert.True(dependencies[1].GetProperty("isDevDependency").GetBoolean());
    }

    [Fact]
    public async Task Vulnerabilities_AreSortedByRiskAndIncludePackageContext()
    {
        var client = await factory.CreateSignedInClientAsync();
        var repository = SeedScanned(await UserIdOf(client));

        var vulnerabilities = await client.GetFromJsonAsync<JsonElement[]>($"/api/repositories/{repository.Id}/vulnerabilities") ?? [];

        Assert.Equal(["CVE-2019-10744", "CVE-2020-0001"], vulnerabilities.Select(v => v.GetProperty("cveId").GetString()));
        var top = vulnerabilities[0];
        Assert.Equal("lodash", top.GetProperty("packageName").GetString());
        Assert.Equal("4.17.4", top.GetProperty("packageVersion").GetString());
        Assert.Equal("NPM", top.GetProperty("ecosystem").GetString());
        Assert.Equal("4.17.12", top.GetProperty("remediationVersion").GetString());
        Assert.True(top.GetProperty("isExploitable").GetBoolean());
    }

    [Fact]
    public async Task Snapshots_ReturnHistory()
    {
        var client = await factory.CreateSignedInClientAsync();
        var repository = SeedScanned(await UserIdOf(client));

        var snapshots = await client.GetFromJsonAsync<JsonElement[]>($"/api/repositories/{repository.Id}/snapshots");

        Assert.Equal(90, Assert.Single(snapshots!).GetProperty("riskScore").GetDouble());
    }

    [Fact]
    public async Task Sbom_IsDownloadableOnlyAfterCompletion()
    {
        var client = await factory.CreateSignedInClientAsync();
        var userId = await UserIdOf(client);
        var pending = Seed(userId, Repository.AnalysisStatus.Pending);
        var scanned = SeedScanned(userId);

        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync($"/api/repositories/{pending.Id}/sbom")).StatusCode);

        var response = await client.GetAsync($"/api/repositories/{scanned.Id}/sbom");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.cyclonedx+json", response.Content.Headers.ContentType?.MediaType);
        Assert.EndsWith(".cdx.json", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var bom = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, bom.GetProperty("components").GetArrayLength());
        Assert.Equal(2, bom.GetProperty("vulnerabilities").GetArrayLength());
    }

    [Fact]
    public async Task UnknownRepositoryIs404()
    {
        var client = await factory.CreateSignedInClientAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/repositories/{Guid.NewGuid()}")).StatusCode);
    }
}
