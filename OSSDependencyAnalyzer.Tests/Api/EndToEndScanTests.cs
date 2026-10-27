using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OSSDependencyAnalyzer.API.Integrations.Github;
using OSSDependencyAnalyzer.API.Integrations.Kev;
using OSSDependencyAnalyzer.API.Integrations.Osv;
using OSSDependencyAnalyzer.API.Services;
using OSSDependencyAnalyzer.Tests.TestSupport;

namespace OSSDependencyAnalyzer.Tests.Api;

/// <summary>
/// Full stack: HTTP request → controller → (inline) background job → GitHub + OSV + KEV over HTTP
/// (stubbed at the transport layer) → parsing → scoring → EF → read endpoints → SBOM export.
/// </summary>
public class EndToEndScanTests : IClassFixture<EndToEndScanTests.Factory>
{
    private readonly Factory _factory;
    public EndToEndScanTests(Factory factory) => _factory = factory;

    public sealed class Factory : DepScanApiFactory
    {
        public StubHttpMessageHandler GitHubApi { get; } = new StubHttpMessageHandler()
            .OnGetJson("/repos/acme/shop/git/trees/main", new
            {
                sha = "t", truncated = false,
                tree = new object[]
                {
                    new { path = "package.json", type = "blob", sha = "pkg-sha", size = 120 },
                    new { path = "services/api/requirements.txt", type = "blob", sha = "req-sha", size = 40 }
                }
            })
            // Registered after the tree route: routes match by prefix, first match wins.
            .OnGetJson("/repos/acme/shop", new { name = "shop", description = "Storefront", language = "JavaScript", stargazers_count = 7, default_branch = "main" });

        public StubHttpMessageHandler GitHubRaw { get; } = new StubHttpMessageHandler()
            .OnGet("/acme/shop/main/package.json", HttpStatusCode.OK, """{ "dependencies": { "lodash": "^4.17.4", "react": "^18.2.0" } }""")
            .OnGet("/acme/shop/main/services/api/requirements.txt", HttpStatusCode.OK, "flask==2.0.0\n");

        public StubHttpMessageHandler Osv { get; } = new StubHttpMessageHandler()
            .OnPostJson("/v1/querybatch", body =>
            {
                var names = JsonDocument.Parse(body).RootElement.GetProperty("queries").EnumerateArray()
                    .Select(q => q.GetProperty("package").GetProperty("name").GetString()).ToList();
                return new
                {
                    results = names.Select(n => n == "lodash"
                        ? (object)new { vulns = new[] { new { id = "GHSA-jf85-cpcp-j695", modified = "2024" } } }
                        : new { }).ToArray()
                };
            })
            .OnGetJson("/v1/vulns/GHSA-jf85-cpcp-j695", OsvFixtures.LodashPrototypePollution());

        public StubHttpMessageHandler Kev { get; } = new StubHttpMessageHandler()
            .OnGetJson("/kev.json", new { vulnerabilities = new[] { new { cveID = "CVE-2019-10744" } } });

        protected override IDictionary<string, string?> Settings => new Dictionary<string, string?>
        {
            ["Kev:FeedUrl"] = "https://kev.test/kev.json"
        };

        protected override void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpClient(GithubApiService.ApiClientName).ConfigurePrimaryHttpMessageHandler(() => GitHubApi);
            services.AddHttpClient(GithubApiService.RawClientName).ConfigurePrimaryHttpMessageHandler(() => GitHubRaw);
            services.AddHttpClient<IOsvClient, OsvClient>().ConfigurePrimaryHttpMessageHandler(() => Osv);
            services.AddHttpClient<IKnownExploitedCatalog, KnownExploitedCatalog>().ConfigurePrimaryHttpMessageHandler(() => Kev);

            services.RemoveAll<IAnalysisQueue>();
            services.AddSingleton<IAnalysisQueue>(sp => new InlineQueue(sp));
        }
    }

    /// <summary>Runs the job synchronously in its own scope, like a Hangfire worker would.</summary>
    private sealed class InlineQueue(IServiceProvider root) : IAnalysisQueue
    {
        public void Enqueue(Guid repositoryId)
        {
            using var scope = root.CreateScope();
            scope.ServiceProvider.GetRequiredService<IAnalyzerService>()
                .AnalyzeRepositoryAsync(repositoryId, CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    [Fact]
    public async Task ScanningARepositoryProducesScoredFindingsAndAnSbom()
    {
        var client = await _factory.CreateSignedInClientAsync();

        var created = await client.PostAsJsonAsync("/api/repositories", new { repositoryUrl = "https://github.com/acme/shop" });
        Assert.Equal(HttpStatusCode.Accepted, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var repository = await client.GetFromJsonAsync<JsonElement>($"/api/repositories/{id}");
        Assert.True(repository.GetProperty("status").GetString() == "Completed", repository.GetProperty("errorMessage").ToString());
        Assert.Equal("Storefront", repository.GetProperty("description").GetString());
        Assert.Equal(2, repository.GetProperty("manifestCount").GetInt32());
        Assert.Equal(3, repository.GetProperty("totalDependencies").GetInt32());
        Assert.Equal(1, repository.GetProperty("criticalVulnerabilities").GetInt32());
        Assert.Equal("F", repository.GetProperty("riskGrade").GetString());

        var vulnerability = Assert.Single(await client.GetFromJsonAsync<JsonElement[]>($"/api/repositories/{id}/vulnerabilities") ?? []);
        Assert.Equal("CVE-2019-10744", vulnerability.GetProperty("cveId").GetString());
        Assert.Equal(9.1m, vulnerability.GetProperty("cvssScore").GetDecimal());
        Assert.True(vulnerability.GetProperty("isExploitable").GetBoolean());  // from the KEV feed
        Assert.Equal("4.17.12", vulnerability.GetProperty("remediationVersion").GetString());
        Assert.Equal(95.5, vulnerability.GetProperty("riskScore").GetDouble());

        var dependencies = await client.GetFromJsonAsync<JsonElement[]>($"/api/repositories/{id}/dependencies");
        Assert.Contains(dependencies!, d => d.GetProperty("packageName").GetString() == "flask"
                                            && d.GetProperty("manifestPath").GetString() == "services/api/requirements.txt");

        var bom = await client.GetFromJsonAsync<JsonElement>($"/api/repositories/{id}/sbom");
        Assert.Equal(3, bom.GetProperty("components").GetArrayLength());

        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/dashboard");
        Assert.Equal(1, dashboard.GetProperty("exploitedVulnerabilities").GetInt32());
        Assert.Single(dashboard.GetProperty("trend").EnumerateArray());
    }
}
