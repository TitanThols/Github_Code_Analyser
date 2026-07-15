using OSSDependencyAnalyzer.API.Integrations.API;
using Refit;

namespace OSSDependencyAnalyzer.API.Integrations.Github;

public interface IGithubApiClient
{
    [Get("/repos/{owner}/{repo}")]
    Task<GithubRepositoryDto> GetRepositoryAsync(string owner, string repo);

    [Get("/repo/{owner}/{repo}/contents/{path}")]
    Task<GithubFileContentDto> GetFileContentAsync(string owner, string repo, string path);

    [Get("/repos/{owner}/{repo}/contents/{path}")]
    Task<IEnumerable<GithubDirectoryContentDto>> ListDirectoryContentsAsync(string owner, string repo, string path);
}