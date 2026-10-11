using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace OSSDependencyAnalyzer.API.Domain;

/// <summary>
/// A validated reference to a GitHub repository. Accepts the forms people actually paste:
/// <c>owner/repo</c>, <c>github.com/owner/repo</c>, full https URLs (including /tree/... deep links
/// and a trailing .git) and SSH remotes (<c>git@github.com:owner/repo.git</c>).
/// </summary>
public sealed partial record GitHubRepositoryUrl(string Owner, string Name)
{
    /// <summary>Canonical lower-case URL used as the uniqueness key (GitHub names are case-insensitive).</summary>
    public string CanonicalUrl => $"https://github.com/{Owner}/{Name}".ToLowerInvariant();

    public string FullName => $"{Owner}/{Name}";

    public static bool TryParse(string? input, [NotNullWhen(true)] out GitHubRepositoryUrl? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(input) || input.Length > 500)
            return false;

        var value = input.Trim();

        var ssh = SshPattern().Match(value);
        if (ssh.Success)
            return TryCreate(ssh.Groups["owner"].Value, ssh.Groups["repo"].Value, out result);

        // Drop query string / fragment.
        var cut = value.IndexOfAny(['?', '#']);
        if (cut >= 0)
            value = value[..cut];

        string path;
        var schemeMatch = HostPattern().Match(value);
        if (schemeMatch.Success)
        {
            path = value[schemeMatch.Length..];
        }
        else if (value.Contains("://", StringComparison.Ordinal) || value.Contains('.') && value.Split('/')[0].Contains('.'))
        {
            // Some other host (gitlab.com, bitbucket.org, ...) — not supported.
            return false;
        }
        else
        {
            path = value;
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
            return false;

        return TryCreate(segments[0], segments[1], out result);
    }

    public static GitHubRepositoryUrl Parse(string input) =>
        TryParse(input, out var result)
            ? result
            : throw new FormatException($"'{input}' is not a valid GitHub repository reference.");

    private static bool TryCreate(string owner, string repo, [NotNullWhen(true)] out GitHubRepositoryUrl? result)
    {
        result = null;
        if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            repo = repo[..^4];

        if (!OwnerPattern().IsMatch(owner) || !RepoPattern().IsMatch(repo) || repo is "." or "..")
            return false;

        result = new GitHubRepositoryUrl(owner, repo);
        return true;
    }

    [GeneratedRegex(@"^git@github\.com:(?<owner>[^/\s]+)/(?<repo>[^/\s]+?)/?$", RegexOptions.IgnoreCase)]
    private static partial Regex SshPattern();

    [GeneratedRegex(@"^(?:https?://)?(?:www\.)?github\.com/", RegexOptions.IgnoreCase)]
    private static partial Regex HostPattern();

    // GitHub usernames: alphanumeric or single hyphens, max 39 chars, cannot start with a hyphen.
    [GeneratedRegex(@"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$")]
    private static partial Regex OwnerPattern();

    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,100}$")]
    private static partial Regex RepoPattern();
}
