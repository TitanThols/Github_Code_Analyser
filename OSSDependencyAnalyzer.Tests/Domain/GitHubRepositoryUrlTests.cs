using OSSDependencyAnalyzer.API.Domain;

namespace OSSDependencyAnalyzer.Tests.Domain;

public class GitHubRepositoryUrlTests
{
    [Theory]
    [InlineData("https://github.com/expressjs/express", "expressjs", "express")]
    [InlineData("http://github.com/expressjs/express", "expressjs", "express")]
    [InlineData("https://www.github.com/expressjs/express", "expressjs", "express")]
    [InlineData("github.com/expressjs/express", "expressjs", "express")]
    [InlineData("expressjs/express", "expressjs", "express")]
    [InlineData("  expressjs/express  ", "expressjs", "express")]
    [InlineData("https://github.com/expressjs/express/", "expressjs", "express")]
    [InlineData("https://github.com/expressjs/express.git", "expressjs", "express")]
    [InlineData("https://github.com/vercel/next.js", "vercel", "next.js")]
    [InlineData("https://github.com/vercel/next.js/tree/canary/packages/next", "vercel", "next.js")]
    [InlineData("https://github.com/pallets/flask/blob/main/pyproject.toml", "pallets", "flask")]
    [InlineData("https://github.com/pallets/flask?tab=readme-ov-file#flask", "pallets", "flask")]
    [InlineData("git@github.com:dotnet/runtime.git", "dotnet", "runtime")]
    [InlineData("git@github.com:dotnet/runtime", "dotnet", "runtime")]
    [InlineData("HTTPS://GITHUB.COM/Microsoft/TypeScript", "Microsoft", "TypeScript")]
    [InlineData("https://github.com/a-b-c/repo_name-1.x", "a-b-c", "repo_name-1.x")]
    public void TryParse_AcceptsCommonForms(string input, string owner, string name)
    {
        Assert.True(GitHubRepositoryUrl.TryParse(input, out var result));
        Assert.Equal(owner, result.Owner);
        Assert.Equal(name, result.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("expressjs")]
    [InlineData("https://github.com/expressjs")]
    [InlineData("https://gitlab.com/gitlab-org/gitlab")]
    [InlineData("https://bitbucket.org/atlassian/repo")]
    [InlineData("gitlab.com/owner/repo")]
    [InlineData("https://github.com/-leadinghyphen/repo")]
    [InlineData("https://github.com/owner/..")]
    [InlineData("https://github.com/own er/repo")]
    [InlineData("https://github.com/owner/re$po")]
    [InlineData("ftp://github.com/owner/repo")]
    public void TryParse_RejectsInvalidInput(string? input)
    {
        Assert.False(GitHubRepositoryUrl.TryParse(input, out var result));
        Assert.Null(result);
    }

    [Fact]
    public void TryParse_RejectsOversizedInput() =>
        Assert.False(GitHubRepositoryUrl.TryParse("a/" + new string('b', 600), out _));

    [Fact]
    public void TryParse_RejectsOwnerLongerThanGitHubAllows() =>
        Assert.False(GitHubRepositoryUrl.TryParse(new string('a', 40) + "/repo", out _));

    [Fact]
    public void CanonicalUrl_IsLowerCasedSoDuplicatesCollapse()
    {
        var a = GitHubRepositoryUrl.Parse("https://github.com/Microsoft/TypeScript");
        var b = GitHubRepositoryUrl.Parse("microsoft/typescript.git");

        Assert.Equal("https://github.com/microsoft/typescript", a.CanonicalUrl);
        Assert.Equal(a.CanonicalUrl, b.CanonicalUrl);
        Assert.Equal("Microsoft/TypeScript", a.FullName);
    }

    [Fact]
    public void Parse_ThrowsFormatExceptionForInvalidInput() =>
        Assert.Throws<FormatException>(() => GitHubRepositoryUrl.Parse("not a repo"));
}
