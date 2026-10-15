using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Distributed;
using OSSDependencyAnalyzer.API.Integrations.DependencyParsing;

namespace OSSDependencyAnalyzer.API.Integrations.Github;

public interface IGithubApiService
{
    /// <exception cref="GitHubRepositoryNotFoundException">The repository doesn't exist or isn't public.</exception>
    Task<RepositoryMetadata> GetRepositoryAsync(string owner, string repo, CancellationToken cancellationToken);

    Task<IReadOnlyList<ManifestFile>> FindManifestsAsync(string owner, string repo, string gitRef, CancellationToken cancellationToken);

    Task<string?> GetFileContentAsync(string owner, string repo, string gitRef, ManifestFile file, CancellationToken cancellationToken);
}

public sealed class GithubApiService(
    IHttpClientFactory httpClientFactory,
    IDependencyParserFactory parserFactory,
    IDistributedCache cache,
    ILogger<GithubApiService> logger) : IGithubApiService
{
    public const string ApiClientName = "GitHubApi";
    public const string RawClientName = "GitHubRaw";

    internal const int MaxManifests = 40;
    internal const long MaxManifestBytes = 1_000_000;

    /// <summary>Directories whose manifests describe vendored, generated, sample or fixture code rather than the project itself.</summary>
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bower_components", "vendor", "third_party", "third-party", ".git", ".yarn", ".venv", "venv",
        "dist", "build", "out", "target", "bin", "obj", "fixtures", "__fixtures__", "testdata", "test-fixtures",
        "examples", "example", "samples", "sample", "docs", "site-packages"
    };

    /// <summary>Probed at the repository root when the tree API is unavailable (rate limit, huge repo).</summary>
    internal static readonly string[] RootCandidates =
    [
        "package.json", "requirements.txt", "pyproject.toml", "pom.xml", "build.gradle", "build.gradle.kts",
        "Gemfile", "go.mod", "composer.json", "Cargo.toml", "Directory.Packages.props"
    ];

    public async Task<RepositoryMetadata> GetRepositoryAsync(string owner, string repo, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(ApiClientName);
        using var response = await client.GetAsync($"repos/{Escape(owner)}/{Escape(repo)}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new GitHubRepositoryNotFoundException($"{owner}/{repo}");

        if (!response.IsSuccessStatusCode)
        {
            // Usually the unauthenticated 60 req/h quota. Raw content still works, so carry on without metadata.
            logger.LogWarning("GitHub metadata request for {Owner}/{Repo} returned {Status}; continuing without metadata",
                owner, repo, (int)response.StatusCode);
            return new RepositoryMetadata(null, null, null, "HEAD");
        }

        var dto = await response.Content.ReadFromJsonAsync<GithubRepositoryDto>(cancellationToken);
        if (dto is null || dto.Private)
            throw new GitHubRepositoryNotFoundException($"{owner}/{repo}");

        return new RepositoryMetadata(dto.Description, dto.Language, dto.StargazersCount, dto.DefaultBranch ?? "HEAD");
    }

    public async Task<IReadOnlyList<ManifestFile>> FindManifestsAsync(string owner, string repo, string gitRef, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(ApiClientName);
        try
        {
            using var response = await client.GetAsync(
                $"repos/{Escape(owner)}/{Escape(repo)}/git/trees/{Escape(gitRef)}?recursive=1", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var tree = await response.Content.ReadFromJsonAsync<GithubTreeResponse>(cancellationToken);
                if (tree is not null)
                {
                    var manifests = SelectManifests(tree.Tree);
                    logger.LogInformation("Discovered {Count} manifests in {Owner}/{Repo} (tree truncated: {Truncated})",
                        manifests.Count, owner, repo, tree.Truncated);
                    if (manifests.Count > 0 || !tree.Truncated)
                        return manifests;
                }
            }
            else
            {
                logger.LogWarning("GitHub tree request for {Owner}/{Repo} returned {Status}; probing root manifests instead",
                    owner, repo, (int)response.StatusCode);
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "GitHub tree request for {Owner}/{Repo} failed; probing root manifests instead", owner, repo);
        }

        return RootCandidates.Select(path => new ManifestFile(path, null)).ToList();
    }

    internal IReadOnlyList<ManifestFile> SelectManifests(IEnumerable<GithubTreeEntry> entries) =>
        entries
            .Where(e => e.Type == "blob"
                        && (e.Size is null || e.Size <= MaxManifestBytes)
                        && parserFactory.IsManifest(e.Path)
                        && !e.Path.Split('/')[..^1].Any(IgnoredDirectories.Contains))
            // Shallow manifests first: the root project matters more than a deeply nested package.
            .OrderBy(e => e.Path.Count(c => c == '/'))
            .ThenBy(e => e.Path, StringComparer.Ordinal)
            .Take(MaxManifests)
            .Select(e => new ManifestFile(e.Path, e.Sha))
            .ToList();

    public async Task<string?> GetFileContentAsync(string owner, string repo, string gitRef, ManifestFile file, CancellationToken cancellationToken)
    {
        // Blob SHAs are content hashes, so a cached copy can never be stale.
        var cacheKey = file.BlobSha is null ? null : $"github:blob:{file.BlobSha}";
        if (cacheKey is not null && await cache.GetStringAsync(cacheKey, cancellationToken) is { } cached)
            return cached;

        var client = httpClientFactory.CreateClient(RawClientName);
        var path = string.Join('/', file.Path.Split('/').Select(Escape));
        using var response = await client.GetAsync($"{Escape(owner)}/{Escape(repo)}/{Escape(gitRef)}/{path}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (cacheKey is not null)
        {
            await cache.SetStringAsync(cacheKey, content,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) }, cancellationToken);
        }
        return content;
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);
}
