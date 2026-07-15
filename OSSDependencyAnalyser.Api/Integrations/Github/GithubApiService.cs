using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using OSSDependencyAnalyzer.API.Integrations.API;
using OSSDependencyAnalyzer.API.Models;
using Refit;
using Serilog;

namespace OSSDependencyAnalyzer.API.Integrations.Github;

public interface IGithubApiService
{
    Task<GithubRepositoryDto> GetRepositoryAsync(string owner, string repo);
    Task<string?> GetFileContentAsync(string owner, string repo, string filePath);
}

public class GithubApiService : IGithubApiService
{
    private readonly IGithubApiClient _client;
    private readonly IDistributedCache _cache;
    private readonly ILogger<GithubApiService> _logger;
    private const int CacheDurationMinutes = 60;

    public GithubApiService(IGithubApiClient client, IDistributedCache cache, ILogger<GithubApiService> logger)
    {
        _client = client;
        _cache = cache;
        _logger = logger;
    }

    public async Task<GithubRepositoryDto> GetRepositoryAsync(string owner, string repo)
    {
        var cacheKey = $"github:repo:{owner}:{repo}";

        var cachedData = await _cache.GetAsync(cacheKey);
        if(cachedData != null)
        {
            _logger.LogInformation("Cache hit repository {Owner}/{Repo}", owner, repo);
            var json = Encoding.UTF8.GetString(cachedData);
            return System.Text.Json.JsonSerializer.Deserialize<GithubRepositoryDto>(json)!;
        }

        try
        {
            _logger.LogInformation("Fetching repository metadata from Github: {Owner}/{Repo}", owner, repo);
            var repo_data = await _client.GetRepositoryAsync(owner, repo);
            var json = System.Text.Json.JsonSerializer.Serialize(repo_data);
            await _cache.SetAsync(cacheKey, Encoding.UTF8.GetBytes(json), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheDurationMinutes)
            });

            return repo_data;
        } catch(HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch repository {Owner}/{Repo}", owner, repo);
            throw;
        }
    }
    public async Task<string?> GetFileContentAsync(string owner, string repo, string filePath)
    {
        var cacheKey = $"github;file:{owner}:{repo}:{filePath}";

        var cachedData = await _cache.GetAsync(cacheKey);
        if (cachedData != null)
        {
            _logger.LogInformation("Cache hit for file {filepath}", filePath);
            return Encoding.UTF8.GetString(cachedData);
        }

        try
        {
            _logger.LogInformation("Fetching file content: {Owner}/{Repo}/{FilePath}", owner, repo, filePath);
            var fileContent = await _client.GetFileContentAsync(owner, repo, filePath);

            string decodedContent = string.Empty;
            if (!string.IsNullOrEmpty(fileContent.Content))
            {
                try
                {
                    decodedContent = Encoding.UTF8.GetString(Convert.FromBase64String(fileContent.Content));
                }catch(FormatException ex)
                {
                    _logger.LogWarning(ex, "Failed to decode Base64 content for {FilePath}", filePath);
                    return null;
                }
            }

            await _cache.SetAsync(cacheKey, Encoding.UTF8.GetBytes(decodedContent), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheDurationMinutes)
            });

            return decodedContent;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("File not found: {Owner}/{Repo}/{FilePath}", owner, repo, filePath);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch file {Owner}/{Repo}/{FilePath}", owner, repo, filePath);
            throw;
        }
    }
}