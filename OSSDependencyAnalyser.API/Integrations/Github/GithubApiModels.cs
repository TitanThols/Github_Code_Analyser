using System.Text.Json.Serialization;

namespace OSSDependencyAnalyzer.API.Integrations.Github;

public sealed class GithubRepositoryDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("full_name")] public string FullName { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("language")] public string? Language { get; set; }
    [JsonPropertyName("stargazers_count")] public int StargazersCount { get; set; }
    [JsonPropertyName("default_branch")] public string? DefaultBranch { get; set; }
    [JsonPropertyName("private")] public bool Private { get; set; }
}

public sealed class GithubTreeResponse
{
    [JsonPropertyName("sha")] public string Sha { get; set; } = string.Empty;
    [JsonPropertyName("truncated")] public bool Truncated { get; set; }
    [JsonPropertyName("tree")] public List<GithubTreeEntry> Tree { get; set; } = [];
}

public sealed class GithubTreeEntry
{
    [JsonPropertyName("path")] public string Path { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("sha")] public string? Sha { get; set; }
    [JsonPropertyName("size")] public long? Size { get; set; }
}

/// <summary>A manifest found in the repository. <see cref="BlobSha"/> is null when discovered by probing.</summary>
public sealed record ManifestFile(string Path, string? BlobSha);

public sealed record RepositoryMetadata(string? Description, string? Language, int? Stars, string Ref);

public sealed class GitHubRepositoryNotFoundException(string fullName)
    : Exception($"Repository '{fullName}' was not found on GitHub, or it is private.");
