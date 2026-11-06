using OSSDependencyAnalyzer.API.Domain;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.Tests.Domain;

public class PackageUrlTests
{
    [Theory]
    [InlineData(DependencyType.NPM, "lodash", "4.17.21", "pkg:npm/lodash@4.17.21")]
    [InlineData(DependencyType.NPM, "@babel/core", "7.0.0", "pkg:npm/%40babel/core@7.0.0")]
    [InlineData(DependencyType.PyPI, "Django_Rest", "3.0", "pkg:pypi/django-rest@3.0")]
    [InlineData(DependencyType.NuGet, "Newtonsoft.Json", "13.0.3", "pkg:nuget/Newtonsoft.Json@13.0.3")]
    [InlineData(DependencyType.Maven, "org.springframework:spring-core", "5.3.20", "pkg:maven/org.springframework/spring-core@5.3.20")]
    [InlineData(DependencyType.Gradle, "com.google.guava:guava", "31.1-jre", "pkg:maven/com.google.guava/guava@31.1-jre")]
    [InlineData(DependencyType.Gem, "rails", "7.0.0", "pkg:gem/rails@7.0.0")]
    [InlineData(DependencyType.Go, "golang.org/x/net", "0.17.0", "pkg:golang/golang.org/x/net@0.17.0")]
    [InlineData(DependencyType.Composer, "guzzlehttp/guzzle", "7.0.0", "pkg:composer/guzzlehttp/guzzle@7.0.0")]
    [InlineData(DependencyType.Cargo, "tokio", "1.28.1", "pkg:cargo/tokio@1.28.1")]
    public void Create_FollowsPurlSpec(DependencyType type, string name, string version, string expected) =>
        Assert.Equal(expected, PackageUrl.Create(type, name, version));

    [Fact]
    public void Create_OmitsVersionWhenUnknown() =>
        Assert.Equal("pkg:npm/left-pad", PackageUrl.Create(DependencyType.NPM, "left-pad", null));
}
