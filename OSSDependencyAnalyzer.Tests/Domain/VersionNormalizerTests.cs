using OSSDependencyAnalyzer.API.Domain;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.Tests.Domain;

public class VersionNormalizerTests
{
    [Theory]
    [InlineData("4.17.21", "4.17.21")]
    [InlineData("^4.17.1", "4.17.1")]
    [InlineData("~1.2.3", "1.2.3")]
    [InlineData(">=1.2.3", "1.2.3")]
    [InlineData(">= 1.2.3 < 2.0.0", "1.2.3")]
    [InlineData("=2.0.0", "2.0.0")]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.x", "1.2.0")]
    [InlineData("1.x", "1.0.0")]
    [InlineData("^18", "18.0.0")]
    [InlineData("^1.0.0 || ^2.0.0", "1.0.0")]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.2")]
    [InlineData("^5.0.0-rc.1", "5.0.0-rc.1")]
    public void Normalize_Npm(string constraint, string expected) =>
        Assert.Equal(expected, VersionNormalizer.Normalize(constraint, DependencyType.NPM));

    [Theory]
    [InlineData("*")]
    [InlineData("latest")]
    [InlineData("x")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("git+https://github.com/user/repo.git")]
    [InlineData("github:user/repo")]
    [InlineData("file:../local")]
    [InlineData("workspace:*")]
    [InlineData("npm:other-package@1.0.0")]
    [InlineData("https://example.com/pkg.tgz")]
    [InlineData("<2.0.0")]
    [InlineData("link:../lib")]
    public void Normalize_Npm_ReturnsNullWhenUnpinnable(string? constraint) =>
        Assert.Null(VersionNormalizer.Normalize(constraint, DependencyType.NPM));

    [Theory]
    [InlineData("==2.31.0", "2.31.0")]
    [InlineData(">=2.0", "2.0")]
    [InlineData("~=1.4.2", "1.4.2")]
    [InlineData(">=2.0,<3", "2.0")]
    [InlineData("<3,>=2.0", "2.0")]
    [InlineData("===1.0", "1.0")]
    [InlineData("1.0rc1", "1.0rc1")]
    [InlineData("==1.0.post1", "1.0.post1")]
    public void Normalize_PyPI(string constraint, string expected) =>
        Assert.Equal(expected, VersionNormalizer.Normalize(constraint, DependencyType.PyPI));

    [Theory]
    [InlineData("<3")]
    [InlineData("!=1.5")]
    [InlineData("<3,!=2.5")]
    [InlineData("unknown")]
    public void Normalize_PyPI_ReturnsNullWithoutLowerBound(string constraint) =>
        Assert.Null(VersionNormalizer.Normalize(constraint, DependencyType.PyPI));

    [Theory]
    [InlineData("13.0.3", "13.0.3")]
    [InlineData("[1.2,2.0)", "1.2")]
    [InlineData("[6.0.0]", "6.0.0")]
    [InlineData("8.0.0-preview.1", "8.0.0-preview.1")]
    public void Normalize_NuGet(string constraint, string expected) =>
        Assert.Equal(expected, VersionNormalizer.Normalize(constraint, DependencyType.NuGet));

    [Fact]
    public void Normalize_NuGet_IntervalWithoutLowerBound() =>
        Assert.Null(VersionNormalizer.Normalize("(,2.0]", DependencyType.NuGet));

    [Theory]
    [InlineData("5.3.20.RELEASE", "5.3.20.RELEASE")]
    [InlineData("2.9.8", "2.9.8")]
    public void Normalize_Maven(string constraint, string expected) =>
        Assert.Equal(expected, VersionNormalizer.Normalize(constraint, DependencyType.Maven));

    [Theory]
    [InlineData("${spring.version}")]
    [InlineData("$kotlinVersion")]
    public void Normalize_UnresolvedPropertyReferences_ReturnNull(string constraint) =>
        Assert.Null(VersionNormalizer.Normalize(constraint, DependencyType.Gradle));

    [Theory]
    [InlineData("~> 7.0", "7.0")]
    [InlineData("~> 5.0, >= 5.0.1", "5.0")]
    [InlineData(">= 1.0", "1.0")]
    public void Normalize_RubyGems(string constraint, string expected) =>
        Assert.Equal(expected, VersionNormalizer.Normalize(constraint, DependencyType.Gem));

    [Theory]
    [InlineData("v0.17.0", "0.17.0")]
    [InlineData("v0.0.0-20210226172049-e18ecbb05110", "0.0.0-20210226172049-e18ecbb05110")]
    public void Normalize_Go(string constraint, string expected) =>
        Assert.Equal(expected, VersionNormalizer.Normalize(constraint, DependencyType.Go));

    [Theory]
    [InlineData("1.0", "1.0.0")]
    [InlineData("=1.28.1", "1.28.1")]
    public void Normalize_Cargo_PadsToSemVer(string constraint, string expected) =>
        Assert.Equal(expected, VersionNormalizer.Normalize(constraint, DependencyType.Cargo));
}
