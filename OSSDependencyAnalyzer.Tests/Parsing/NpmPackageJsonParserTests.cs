using OSSDependencyAnalyzer.API.Integrations.DependencyParsing;
using OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsers;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.Tests.Parsing;

public class NpmPackageJsonParserTests
{
    private readonly NpmPackageJsonParser _parser = new();

    [Fact]
    public void Parse_ReadsRuntimeOptionalAndDevDependencies()
    {
        const string json = """
            {
              "name": "demo",
              "dependencies": { "express": "^4.18.2", "lodash": "4.17.4" },
              "optionalDependencies": { "fsevents": "~2.3.2" },
              "devDependencies": { "vitest": "^1.0.0" },
              "peerDependencies": { "react": ">=18" }
            }
            """;

        var result = _parser.Parse(json);

        Assert.Equal(4, result.Count);
        Assert.Contains(new ParsedDependency("express", "^4.18.2", DependencyType.NPM, false), result);
        Assert.Contains(new ParsedDependency("fsevents", "~2.3.2", DependencyType.NPM, false), result);
        Assert.Contains(new ParsedDependency("vitest", "^1.0.0", DependencyType.NPM, true), result);
        Assert.DoesNotContain(result, d => d.PackageName == "react"); // peers are provided by the host
    }

    [Fact]
    public void Parse_SkipsNonStringVersions()
    {
        var result = _parser.Parse("""{ "dependencies": { "ok": "1.0.0", "weird": { "version": "1" }, "nil": null } }""");
        Assert.Single(result);
    }

    [Fact]
    public void Parse_ToleratesTrailingCommasAndComments()
    {
        var result = _parser.Parse("""
            {
              // comment
              "dependencies": { "a": "1.0.0", },
            }
            """);
        Assert.Single(result);
    }

    [Fact]
    public void Parse_EmptyManifestYieldsNothing() => Assert.Empty(_parser.Parse("{}"));

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[1, 2, 3]")]
    public void Parse_InvalidContentThrowsParseException(string content) =>
        Assert.Throws<DependencyParseException>(() => _parser.Parse(content));

    [Theory]
    [InlineData("package.json", true)]
    [InlineData("Package.JSON", true)]
    [InlineData("package-lock.json", false)]
    public void CanParse(string fileName, bool expected) => Assert.Equal(expected, _parser.CanParse(fileName));
}
