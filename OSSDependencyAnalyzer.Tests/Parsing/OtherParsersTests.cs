using OSSDependencyAnalyzer.API.Integrations.DependencyParsing;
using OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsers;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.Tests.Parsing;

public class GemfileParserTests
{
    private readonly GemfileParser _parser = new();

    [Fact]
    public void Parse_GemsConstraintsAndGroups()
    {
        const string gemfile = """
            source "https://rubygems.org"
            ruby "3.2.2"

            gem "rails", "~> 7.0.8"
            gem 'pg', '>= 0.18', '< 2.0'
            gem "puma"
            gem "bootsnap", require: false
            gem "rubocop", group: :development

            group :development, :test do
              gem "rspec-rails", "~> 6.0"
              platforms :mri do
                gem "byebug"
              end
            end

            group :production do
              gem "lograge"
            end
            """;

        var result = _parser.Parse(gemfile).ToDictionary(d => d.PackageName);

        Assert.Equal(8, result.Count);
        Assert.Equal("~> 7.0.8", result["rails"].Version);
        Assert.Equal(">= 0.18, < 2.0", result["pg"].Version);
        Assert.Equal("unknown", result["puma"].Version);
        Assert.Equal("unknown", result["bootsnap"].Version);
        Assert.True(result["rubocop"].IsDev);
        Assert.True(result["rspec-rails"].IsDev);
        Assert.True(result["byebug"].IsDev);       // nested block inherits group
        Assert.False(result["lograge"].IsDev);
        Assert.False(result["rails"].IsDev);
        Assert.All(result.Values, d => Assert.Equal(DependencyType.Gem, d.Type));
    }

    [Theory]
    [InlineData("Gemfile", true)]
    [InlineData("gems.rb", true)]
    [InlineData("Gemfile.lock", false)]
    public void CanParse(string fileName, bool expected) => Assert.Equal(expected, _parser.CanParse(fileName));
}

public class GoModParserTests
{
    [Fact]
    public void Parse_RequireBlockAndSingleLineRequire()
    {
        const string goMod = """
            module github.com/acme/app

            go 1.21

            require github.com/gin-gonic/gin v1.9.1

            require (
                golang.org/x/net v0.17.0
                github.com/stretchr/testify v1.8.4 // indirect
            )

            replace example.com/old => example.com/new v1.0.0
            """;

        var result = new GoModParser().Parse(goMod);

        Assert.Equal(3, result.Count);
        Assert.Contains(new ParsedDependency("github.com/gin-gonic/gin", "v1.9.1", DependencyType.Go), result);
        Assert.Contains(new ParsedDependency("golang.org/x/net", "v0.17.0", DependencyType.Go), result);
        Assert.Contains(result, d => d.PackageName == "github.com/stretchr/testify");
    }
}

public class ComposerJsonParserTests
{
    [Fact]
    public void Parse_SkipsPlatformRequirements()
    {
        const string json = """
            {
              "require": { "php": ">=8.1", "ext-json": "*", "laravel/framework": "^10.10", "guzzlehttp/guzzle": "^7.2" },
              "require-dev": { "phpunit/phpunit": "^10.1" }
            }
            """;

        var result = new ComposerJsonParser().Parse(json);

        Assert.Equal(3, result.Count);
        Assert.Contains(new ParsedDependency("laravel/framework", "^10.10", DependencyType.Composer), result);
        Assert.Contains(new ParsedDependency("phpunit/phpunit", "^10.1", DependencyType.Composer, IsDev: true), result);
    }

    [Fact]
    public void Parse_InvalidJsonThrows() =>
        Assert.Throws<DependencyParseException>(() => new ComposerJsonParser().Parse("{"));
}

public class CargoTomlParserTests
{
    [Fact]
    public void Parse_SimpleInlineRenamedAndDevDependencies()
    {
        const string toml = """
            [package]
            name = "app"
            version = "0.1.0"

            [dependencies]
            serde = "1.0"
            tokio = { version = "1.28", features = ["full"] }
            local = { path = "../local" }
            web = { package = "actix-web", version = "4" }

            [dev-dependencies]
            criterion = "0.5"

            [target.'cfg(windows)'.dependencies]
            winapi = "0.3"
            """;

        var result = new CargoTomlParser().Parse(toml).ToDictionary(d => d.PackageName);

        Assert.Equal(6, result.Count);
        Assert.Equal("1.0", result["serde"].Version);
        Assert.Equal("1.28", result["tokio"].Version);
        Assert.Equal("unknown", result["local"].Version);
        Assert.Equal("4", result["actix-web"].Version);
        Assert.True(result["criterion"].IsDev);
        Assert.Equal("0.3", result["winapi"].Version);
        Assert.DoesNotContain("name", result.Keys);
    }
}

public class DependencyParserFactoryTests
{
    private readonly DependencyParserFactory _factory = new(
    [
        new NpmPackageJsonParser(), new PythonRequirementsParser(), new PyprojectTomlParser(), new CsProjParser(),
        new DirectoryPackagesPropsParser(), new PomXmlParser(), new GradleParser(), new GemfileParser(),
        new GoModParser(), new ComposerJsonParser(), new CargoTomlParser()
    ]);

    [Theory]
    [InlineData("package.json", DependencyFileType.PackageJson)]
    [InlineData("packages/web/package.json", DependencyFileType.PackageJson)]
    [InlineData("requirements/dev-requirements.txt", DependencyFileType.RequirementsTxt)]
    [InlineData("pyproject.toml", DependencyFileType.PyprojectToml)]
    [InlineData("src/Api/Api.csproj", DependencyFileType.Csproj)]
    [InlineData("Directory.Packages.props", DependencyFileType.DirectoryPackagesProps)]
    [InlineData("pom.xml", DependencyFileType.PomXml)]
    [InlineData("app/build.gradle.kts", DependencyFileType.Gradle)]
    [InlineData("Gemfile", DependencyFileType.Gemfile)]
    [InlineData("go.mod", DependencyFileType.GoMod)]
    [InlineData("composer.json", DependencyFileType.ComposerJson)]
    [InlineData("Cargo.toml", DependencyFileType.CargoToml)]
    public void ResolvesParserByFileNameIgnoringDirectories(string path, DependencyFileType expected)
    {
        Assert.True(_factory.IsManifest(path));
        Assert.Equal(expected, _factory.TryGetParserByFileName(path)!.FileType);
    }

    [Theory]
    [InlineData("README.md")]
    [InlineData("package-lock.json")]
    [InlineData("yarn.lock")]
    [InlineData("src/index.ts")]
    public void ReturnsNullForUnsupportedFiles(string path)
    {
        Assert.False(_factory.IsManifest(path));
        Assert.Null(_factory.TryGetParserByFileName(path));
    }
}
