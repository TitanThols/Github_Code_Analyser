using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OSSDependencyAnalyzer.API.Integrations;
using OSSDependencyAnalyzer.API.Integrations.DependencyParsing;
using OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsers;
using OSSDependencyAnalyzer.API.Integrations.Github;
using OSSDependencyAnalyzer.API.Integrations.Kev;
using OSSDependencyAnalyzer.API.Integrations.Osv;
using OSSDependencyAnalyzer.Tests.TestSupport;

namespace OSSDependencyAnalyzer.Tests.Integrations;

public class OsvClientTests
{
    [Fact]
    public async Task QueryBatch_PostsQueriesAndPreservesOrder()
    {
        var stub = new StubHttpMessageHandler().OnPostJson("/v1/querybatch", _ => new
        {
            results = new object[] { new { vulns = new[] { new { id = "GHSA-1", modified = "x" } } }, new { } }
        });
        var client = new OsvClient(new HttpClient(stub) { BaseAddress = new Uri("https://api.osv.dev/") });

        var result = await client.QueryBatchAsync(
            [new OsvQuery(new OsvPackage("a", "npm"), "1.0.0"), new OsvQuery(new OsvPackage("b", "npm"), "2.0.0")],
            CancellationToken.None);

        Assert.Equal("GHSA-1", Assert.Single(result[0]).Id);
        Assert.Empty(result[1]);
        var body = JsonDocument.Parse(stub.Requests.Single().Body!);
        Assert.Equal("a", body.RootElement.GetProperty("queries")[0].GetProperty("package").GetProperty("name").GetString());
        Assert.Equal("1.0.0", body.RootElement.GetProperty("queries")[0].GetProperty("version").GetString());
    }

    [Fact]
    public async Task QueryBatch_ChunksAtTheDocumentedLimit()
    {
        var stub = new StubHttpMessageHandler().OnPostJson("/v1/querybatch", body =>
        {
            var count = JsonDocument.Parse(body).RootElement.GetProperty("queries").GetArrayLength();
            return new { results = Enumerable.Range(0, count).Select(_ => new { }).ToArray() };
        });
        var client = new OsvClient(new HttpClient(stub) { BaseAddress = new Uri("https://api.osv.dev/") });
        var queries = Enumerable.Range(0, OsvClient.MaxBatchSize + 5)
            .Select(i => new OsvQuery(new OsvPackage($"p{i}", "npm"), "1.0.0"))
            .ToList();

        var result = await client.QueryBatchAsync(queries, CancellationToken.None);

        Assert.Equal(queries.Count, result.Count);
        Assert.Equal(2, stub.Requests.Count);
    }

    [Fact]
    public async Task QueryBatch_RejectsMismatchedResultCount()
    {
        var stub = new StubHttpMessageHandler().OnPostJson("/v1/querybatch", _ => new { results = Array.Empty<object>() });
        var client = new OsvClient(new HttpClient(stub) { BaseAddress = new Uri("https://api.osv.dev/") });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.QueryBatchAsync([new OsvQuery(new OsvPackage("a", "npm"), "1")], CancellationToken.None));
    }

    [Fact]
    public async Task GetVulnerability_ReturnsNullOn404AndThrowsOnServerError()
    {
        var stub = new StubHttpMessageHandler()
            .OnGet("/v1/vulns/MISSING", HttpStatusCode.NotFound)
            .OnGet("/v1/vulns/BROKEN", HttpStatusCode.InternalServerError)
            .OnGetJson("/v1/vulns/GHSA-ok", new { id = "GHSA-ok", summary = "s", aliases = new[] { "CVE-1" } });
        var client = new OsvClient(new HttpClient(stub) { BaseAddress = new Uri("https://api.osv.dev/") });

        Assert.Null(await client.GetVulnerabilityAsync("MISSING", CancellationToken.None));
        Assert.Equal("CVE-1", (await client.GetVulnerabilityAsync("GHSA-ok", CancellationToken.None))!.Aliases!.Single());
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetVulnerabilityAsync("BROKEN", CancellationToken.None));
    }
}

public class KnownExploitedCatalogTests
{
    private static KnownExploitedCatalog Create(StubHttpMessageHandler stub, IMemoryCache? cache = null) =>
        new(new HttpClient(stub) { BaseAddress = new Uri("https://kev.test/feed.json") },
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            NullLogger<KnownExploitedCatalog>.Instance);

    [Fact]
    public async Task LoadsCveIdsOnceAndCachesThem()
    {
        var stub = new StubHttpMessageHandler().OnGetJson("/feed.json", new
        {
            vulnerabilities = new[] { new { cveID = "CVE-2021-44228" }, new { cveID = "CVE-2019-10744" } }
        });
        var catalog = Create(stub);

        var first = await catalog.GetCveIdsAsync(CancellationToken.None);
        var second = await catalog.GetCveIdsAsync(CancellationToken.None);

        Assert.Contains("cve-2021-44228", first); // case-insensitive
        Assert.Same(first, second);
        Assert.Single(stub.Requests);
    }

    [Fact]
    public async Task DegradesToEmptySetWhenFeedIsUnavailable()
    {
        var stub = new StubHttpMessageHandler().OnGet("/feed.json", HttpStatusCode.ServiceUnavailable);
        Assert.Empty(await Create(stub).GetCveIdsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DegradesToEmptySetOnMalformedFeed()
    {
        var stub = new StubHttpMessageHandler().OnGet("/feed.json", HttpStatusCode.OK, "<html>maintenance</html>");
        Assert.Empty(await Create(stub).GetCveIdsAsync(CancellationToken.None));
    }
}

public class GithubApiServiceTests
{
    private readonly StubHttpMessageHandler _api = new();
    private readonly StubHttpMessageHandler _raw = new();
    private readonly IDistributedCache _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    private readonly GithubApiService _service;

    public GithubApiServiceTests()
    {
        var factory = new StubHttpClientFactory(new()
        {
            [GithubApiService.ApiClientName] = (_api, "https://api.github.com/"),
            [GithubApiService.RawClientName] = (_raw, "https://raw.githubusercontent.com/")
        });
        var parsers = new DependencyParserFactory([new NpmPackageJsonParser(), new CsProjParser(), new GoModParser()]);
        _service = new GithubApiService(factory, parsers, _cache, NullLogger<GithubApiService>.Instance);
    }

    [Fact]
    public async Task GetRepository_ReturnsMetadataAndDefaultBranch()
    {
        _api.OnGetJson("/repos/acme/app", new { name = "app", description = "Demo", language = "TypeScript", stargazers_count = 42, default_branch = "main" });

        var metadata = await _service.GetRepositoryAsync("acme", "app", CancellationToken.None);

        Assert.Equal(new RepositoryMetadata("Demo", "TypeScript", 42, "main"), metadata);
    }

    [Fact]
    public async Task GetRepository_ThrowsNotFoundFor404()
    {
        _api.OnGet("/repos/acme/missing", HttpStatusCode.NotFound);
        await Assert.ThrowsAsync<GitHubRepositoryNotFoundException>(() => _service.GetRepositoryAsync("acme", "missing", CancellationToken.None));
    }

    [Fact]
    public async Task GetRepository_ContinuesWithoutMetadataWhenRateLimited()
    {
        _api.OnGet("/repos/acme/app", HttpStatusCode.Forbidden, """{"message":"API rate limit exceeded"}""");

        var metadata = await _service.GetRepositoryAsync("acme", "app", CancellationToken.None);

        Assert.Equal("HEAD", metadata.Ref);
        Assert.Null(metadata.Stars);
    }

    [Fact]
    public async Task FindManifests_FiltersIgnoredDirectoriesAndOrdersShallowFirst()
    {
        _api.OnGetJson("/repos/acme/app/git/trees/main", new
        {
            sha = "root",
            truncated = false,
            tree = new object[]
            {
                new { path = "packages/web/package.json", type = "blob", sha = "b2", size = 300 },
                new { path = "package.json", type = "blob", sha = "b1", size = 200 },
                new { path = "node_modules/left-pad/package.json", type = "blob", sha = "x1", size = 100 },
                new { path = "examples/demo/package.json", type = "blob", sha = "x2", size = 100 },
                new { path = "src/Api/Api.csproj", type = "blob", sha = "b3", size = 500 },
                new { path = "huge/package.json", type = "blob", sha = "x3", size = 5_000_000 },
                new { path = "src", type = "tree", sha = "t1" },
                new { path = "README.md", type = "blob", sha = "x4", size = 10 }
            }
        });

        var manifests = await _service.FindManifestsAsync("acme", "app", "main", CancellationToken.None);

        Assert.Equal(["package.json", "packages/web/package.json", "src/Api/Api.csproj"], manifests.Select(m => m.Path));
        Assert.Equal("b1", manifests[0].BlobSha);
    }

    [Fact]
    public async Task FindManifests_FallsBackToRootProbesWhenTreeUnavailable()
    {
        _api.OnGet("/repos/acme/app/git/trees/HEAD", HttpStatusCode.Forbidden);

        var manifests = await _service.FindManifestsAsync("acme", "app", "HEAD", CancellationToken.None);

        Assert.Equal(GithubApiService.RootCandidates, manifests.Select(m => m.Path));
        Assert.All(manifests, m => Assert.Null(m.BlobSha));
    }

    [Fact]
    public async Task GetFileContent_UsesRawHostAndCachesByBlobSha()
    {
        _raw.OnGet("/acme/app/main/packages/web/package.json", HttpStatusCode.OK, "{}");
        var file = new ManifestFile("packages/web/package.json", "sha-123");

        var first = await _service.GetFileContentAsync("acme", "app", "main", file, CancellationToken.None);
        var second = await _service.GetFileContentAsync("acme", "app", "main", file, CancellationToken.None);

        Assert.Equal("{}", first);
        Assert.Equal("{}", second);
        Assert.Single(_raw.Requests);
        Assert.Empty(_api.Requests); // raw content doesn't consume API quota
    }

    [Fact]
    public async Task GetFileContent_ReturnsNullWhenProbedFileDoesNotExist()
    {
        Assert.Null(await _service.GetFileContentAsync("acme", "app", "HEAD", new ManifestFile("go.mod", null), CancellationToken.None));
    }
}

public class RateLimitRetryHandlerTests
{
    private static HttpClient Create(StubHttpMessageHandler stub) =>
        new(new RateLimitRetryHandler(NullLogger<RateLimitRetryHandler>.Instance) { InnerHandler = stub })
        {
            BaseAddress = new Uri("https://upstream.test/")
        };

    [Fact]
    public async Task RetriesTransientFailuresHonouringRetryAfter()
    {
        var attempts = 0;
        var stub = new StubHttpMessageHandler().On(_ => true, _ =>
        {
            attempts++;
            if (attempts < 3)
            {
                var busy = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                busy.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
                return busy;
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var response = await Create(stub).PostAsync("x", new StringContent("payload"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, attempts);
        Assert.All(stub.Requests, r => Assert.Equal("payload", r.Body)); // body replayed on every attempt
    }

    [Fact]
    public async Task DoesNotRetryClientErrors()
    {
        var stub = new StubHttpMessageHandler().On(_ => true, _ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var response = await Create(stub).GetAsync("x");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Single(stub.Requests);
    }

    [Fact]
    public async Task GivesUpImmediatelyWhenUpstreamAsksForALongWait()
    {
        var stub = new StubHttpMessageHandler().On(_ => true, _ =>
        {
            var limited = new HttpResponseMessage(HttpStatusCode.Forbidden);
            limited.Headers.Add("X-RateLimit-Remaining", "0");
            limited.Headers.Add("X-RateLimit-Reset", DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds().ToString());
            return limited;
        });

        var response = await Create(stub).GetAsync("x");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Single(stub.Requests);
    }

    [Fact]
    public void RetryDelay_FallsBackToExponentialBackoff()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        Assert.Equal(TimeSpan.FromSeconds(1), RateLimitRetryHandler.GetRetryDelay(response, 0));
        Assert.Equal(TimeSpan.FromSeconds(4), RateLimitRetryHandler.GetRetryDelay(response, 2));
    }
}
