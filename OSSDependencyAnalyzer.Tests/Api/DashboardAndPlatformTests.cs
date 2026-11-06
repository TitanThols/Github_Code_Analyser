using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OSSDependencyAnalyzer.API.Models;
using OSSDependencyAnalyzer.Tests.TestSupport;

namespace OSSDependencyAnalyzer.Tests.Api;

public class DashboardEndpointTests(DepScanApiFactory factory) : IClassFixture<DepScanApiFactory>
{
    [Fact]
    public async Task NewUserGetsAnEmptyDashboard()
    {
        var client = await factory.CreateSignedInClientAsync();

        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/dashboard");

        Assert.Equal(0, dashboard.GetProperty("totalRepositories").GetInt32());
        Assert.Equal(0, dashboard.GetProperty("totalVulnerabilities").GetInt32());
        Assert.Equal(0, dashboard.GetProperty("trend").GetArrayLength());
    }

    [Fact]
    public async Task AggregatesTheUsersPortfolio()
    {
        var client = await factory.CreateSignedInClientAsync();
        var userId = (await client.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("id").GetGuid();

        factory.WithDb(db =>
        {
            Repository Scanned(string name, double risk, int critical, int high, int daysAgo)
            {
                var r = db.AddRepository(userId, name: name, status: Repository.AnalysisStatus.Completed);
                r.RiskScore = risk;
                r.CriticalVulnerabilities = critical;
                r.HighVulnerabilities = high;
                db.VulnerabilitySnapshots.Add(new VulnerabilitySnapshot
                {
                    Id = Guid.NewGuid(), RepositoryId = r.Id, SnapshotDate = DateTime.UtcNow.AddDays(-daysAgo),
                    CriticalCount = critical, HighCount = high, TotalVulnerabilities = critical + high
                });
                return r;
            }

            var api = Scanned("api", 95, 1, 1, daysAgo: 3);
            var web = Scanned("web", 62.5, 0, 1, daysAgo: 1);
            db.AddRepository(userId, name: "queued", status: Repository.AnalysisStatus.Pending);

            Dependency Dep(Repository r, string name, DependencyType type) =>
                new() { Id = Guid.NewGuid(), RepositoryId = r.Id, PackageName = name, CurrentVersion = "1", Type = type };

            var lodashApi = Dep(api, "lodash", DependencyType.NPM);
            lodashApi.Vulnerabilities.Add(new Vulnerability { Id = Guid.NewGuid(), CveId = "CVE-1", Title = "t", Description = "d", Severity = Severity.Critical, IsExploitable = true, RemediationVersion = "2" });
            var django = Dep(api, "django", DependencyType.PyPI);
            django.Vulnerabilities.Add(new Vulnerability { Id = Guid.NewGuid(), CveId = "CVE-2", Title = "t", Description = "d", Severity = Severity.High });
            var lodashWeb = Dep(web, "lodash", DependencyType.NPM);
            lodashWeb.Vulnerabilities.Add(new Vulnerability { Id = Guid.NewGuid(), CveId = "CVE-3", Title = "t", Description = "d", Severity = Severity.High, RemediationVersion = "3" });
            db.Dependencies.AddRange(lodashApi, django, lodashWeb, Dep(web, "react", DependencyType.NPM));
            db.SaveChanges();
            return 0;
        });

        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/dashboard");

        Assert.Equal(3, dashboard.GetProperty("totalRepositories").GetInt32());
        Assert.Equal(2, dashboard.GetProperty("completedRepositories").GetInt32());
        Assert.Equal(1, dashboard.GetProperty("inProgressRepositories").GetInt32());
        Assert.Equal(4, dashboard.GetProperty("totalDependencies").GetInt32());
        Assert.Equal(3, dashboard.GetProperty("totalVulnerabilities").GetInt32());
        Assert.Equal(1, dashboard.GetProperty("totalCritical").GetInt32());
        Assert.Equal(2, dashboard.GetProperty("totalHigh").GetInt32());
        Assert.Equal(1, dashboard.GetProperty("exploitedVulnerabilities").GetInt32());
        Assert.Equal(2, dashboard.GetProperty("fixableVulnerabilities").GetInt32());
        Assert.Equal(78.8, dashboard.GetProperty("averageRiskScore").GetDouble());

        var ecosystems = dashboard.GetProperty("ecosystems").EnumerateArray().ToList();
        Assert.Equal("NPM", ecosystems[0].GetProperty("ecosystem").GetString());
        Assert.Equal(3, ecosystems[0].GetProperty("dependencies").GetInt32());
        Assert.Equal(2, ecosystems[0].GetProperty("vulnerabilities").GetInt32());

        var riskiest = dashboard.GetProperty("riskiestRepositories").EnumerateArray().ToList();
        Assert.Equal(["acme/api", "acme/web"], riskiest.Select(r => r.GetProperty("fullName").GetString()));
        Assert.Equal("F", riskiest[0].GetProperty("riskGrade").GetString());

        var topPackage = dashboard.GetProperty("topVulnerablePackages")[0];
        Assert.Equal("lodash", topPackage.GetProperty("packageName").GetString());
        Assert.Equal(2, topPackage.GetProperty("repositories").GetInt32());
        Assert.Equal("Critical", topPackage.GetProperty("highestSeverity").GetString());

        // Trend starts at the first snapshot (3 days ago) and carries each repo's latest snapshot forward.
        var trend = dashboard.GetProperty("trend").EnumerateArray().ToList();
        Assert.Equal(4, trend.Count);
        Assert.Equal(2, trend[0].GetProperty("total").GetInt32());   // only "api" scanned so far
        Assert.Equal(3, trend[^1].GetProperty("total").GetInt32());  // api (2) + web (1)
    }
}

public class PlatformEndpointTests(DepScanApiFactory factory) : IClassFixture<DepScanApiFactory>
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthChecksAreAnonymousAndHealthy(string path)
    {
        var response = await factory.CreateBrowserClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OpenApiDocumentDescribesTheApi()
    {
        var document = await factory.CreateBrowserClient().GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var paths = document.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/repositories", out _));
        Assert.True(paths.TryGetProperty("/api/repositories/{id}/sbom", out _));
        Assert.True(paths.TryGetProperty("/api/dashboard", out _));
    }

    [Fact]
    public async Task UnknownApiRoutesAre404()
    {
        var client = await factory.CreateSignedInClientAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/does-not-exist")).StatusCode);
    }

    [Fact]
    public async Task AlertHubRequiresAuthentication()
    {
        var anonymous = await factory.CreateBrowserClient().PostAsync("/hubs/alerts/negotiate?negotiateVersion=1", null);
        var signedIn = await (await factory.CreateSignedInClientAsync()).PostAsync("/hubs/alerts/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
    }

    [Fact]
    public async Task UnhandledErrorsDoNotLeakStackTraces()
    {
        var response = await factory.CreateBrowserClient().PostAsync("/api/auth/login",
            new StringContent("{ definitely not json", System.Text.Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("   at ", body);
    }
}
