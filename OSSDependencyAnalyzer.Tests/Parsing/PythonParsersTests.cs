using OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsers;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.Tests.Parsing;

public class PythonRequirementsParserTests
{
    private readonly PythonRequirementsParser _parser = new();

    [Fact]
    public void Parse_HandlesSpecifiersExtrasMarkersAndComments()
    {
        const string content = """
            # Core
            Django==4.2.7
            requests[socks]>=2.31.0,<3  # http client
            numpy ; python_version >= "3.9"
            pydantic~=2.5
            gunicorn

            -r base.txt
            -e .
            --index-url https://pypi.org/simple
            flask @ https://github.com/pallets/flask/archive/main.zip
            uvicorn==0.24.0 \
                --hash=sha256:abc
            """;

        var result = _parser.Parse(content).ToDictionary(d => d.PackageName, d => d.Version);

        Assert.Equal(7, result.Count);
        Assert.Equal("==4.2.7", result["Django"]);
        Assert.Equal(">=2.31.0,<3", result["requests"]);
        Assert.Equal("unknown", result["numpy"]);
        Assert.Equal("~=2.5", result["pydantic"]);
        Assert.Equal("unknown", result["gunicorn"]);
        Assert.Equal("unknown", result["flask"]);
        Assert.Equal("==0.24.0", result["uvicorn"]);
        Assert.All(_parser.Parse(content), d => Assert.Equal(DependencyType.PyPI, d.Type));
    }

    [Fact]
    public void Parse_HashInsideUrlIsNotAComment()
    {
        var result = _parser.Parse("pkg @ git+https://github.com/o/r.git#egg=pkg");
        Assert.Equal("pkg", Assert.Single(result).PackageName);
    }

    [Theory]
    [InlineData("requirements.txt", true)]
    [InlineData("requirements-dev.txt", true)]
    [InlineData("dev-requirements.txt", true)]
    [InlineData("README.txt", false)]
    [InlineData("requirements.in", false)]
    public void CanParse(string fileName, bool expected) => Assert.Equal(expected, _parser.CanParse(fileName));
}

public class PyprojectTomlParserTests
{
    private readonly PyprojectTomlParser _parser = new();

    [Fact]
    public void Parse_Pep621ProjectAndOptionalDependencies()
    {
        const string toml = """
            [project]
            name = "demo"
            version = "1.0.0"
            dependencies = [
                "httpx>=0.25",
                "pydantic[email]==2.5.0",  # validation
                'rich',
            ]

            [project.optional-dependencies]
            test = ["pytest>=7", "coverage[toml]"]

            [tool.ruff]
            line-length = 100
            """;

        var result = _parser.Parse(toml);

        Assert.Equal(5, result.Count);
        Assert.Contains(result, d => d is { PackageName: "httpx", Version: ">=0.25", IsDev: false });
        Assert.Contains(result, d => d is { PackageName: "pydantic", Version: "==2.5.0", IsDev: false });
        Assert.Contains(result, d => d is { PackageName: "rich", Version: "unknown", IsDev: false });
        Assert.Contains(result, d => d is { PackageName: "pytest", IsDev: true });
        Assert.Contains(result, d => d is { PackageName: "coverage", IsDev: true });
    }

    [Fact]
    public void Parse_SingleLineDependencyArray()
    {
        var result = _parser.Parse("""
            [project]
            dependencies = ["a>=1", "b==2"]
            """);
        Assert.Equal(["a", "b"], result.Select(d => d.PackageName));
    }

    [Fact]
    public void Parse_PoetryDependenciesAndGroups()
    {
        const string toml = """
            [tool.poetry.dependencies]
            python = "^3.11"
            fastapi = "^0.104.1"
            sqlalchemy = { version = "^2.0", extras = ["asyncio"] }
            mylib = { path = "../mylib" }

            [tool.poetry.group.dev.dependencies]
            black = "^23.11"

            [tool.poetry.dev-dependencies]
            mypy = "1.7.0"
            """;

        var result = _parser.Parse(toml);

        Assert.DoesNotContain(result, d => d.PackageName == "python");
        Assert.Contains(result, d => d is { PackageName: "fastapi", Version: "^0.104.1", IsDev: false });
        Assert.Contains(result, d => d is { PackageName: "sqlalchemy", Version: "^2.0", IsDev: false });
        Assert.Contains(result, d => d is { PackageName: "mylib", Version: "unknown" });
        Assert.Contains(result, d => d is { PackageName: "black", IsDev: true });
        Assert.Contains(result, d => d is { PackageName: "mypy", Version: "1.7.0", IsDev: true });
    }

    [Fact]
    public void Parse_DependencyGroupsArePep735DevDependencies()
    {
        var result = _parser.Parse("""
            [dependency-groups]
            lint = ["ruff>=0.1"]
            """);
        Assert.True(Assert.Single(result).IsDev);
    }

    [Fact]
    public void Parse_IgnoresUnrelatedTablesAndArrays()
    {
        var result = _parser.Parse("""
            [tool.setuptools]
            packages = ["demo", "demo.sub"]
            [build-system]
            requires = ["setuptools>=61"]
            """);
        Assert.Empty(result);
    }
}
