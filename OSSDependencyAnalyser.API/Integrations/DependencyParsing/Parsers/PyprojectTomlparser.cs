using OSSDependencyAnalyzer.API.Integrations.DependencyParsing;
using OSSDependencyAnalyzer.API.Models;
using System.Text.RegularExpressions;
using Serilog;

namespace OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsers;

public class PyprojectTomlParser : IDependencyParser
{
    private readonly ILogger<PyprojectTomlParser> _logger;

    public DependencyFileType FileType => DependencyFileType.PyprojectToml;

    public PyprojectTomlParser(ILogger<PyprojectTomlParser> logger)
    {
        _logger = logger;
    }

    public async Task<List<ParsedDependency>> ParseAsync(string fileContent)
    {
        var dependencies = new List<ParsedDependency>();

        try
        {
            var lines = fileContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            var inDependenciesSection = false;
            var inDevDependenciesSection = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    continue;

                if (trimmed.StartsWith("[tool.poetry.dependencies]"))
                {
                    inDependenciesSection = true;
                    inDevDependenciesSection = false;
                    continue;
                }

                if (trimmed.StartsWith("[tool.poetry.dev-dependencies]"))
                {
                    inDevDependenciesSection = true;
                    inDependenciesSection = false;
                    continue;
                }

                if (trimmed.StartsWith("[") && !trimmed.StartsWith("[tool.poetry"))
                {
                    inDependenciesSection = false;
                    inDevDependenciesSection = false;
                }

                if (inDependenciesSection || inDevDependenciesSection)
                {
                    var dependency = ParseDependencyLine(trimmed);
                    if (dependency != null)
                    {
                        dependencies.Add(dependency);
                    }
                }
            }

            _logger.LogInformation("Parsed {Count} Poetry dependencies from pyproject.toml", dependencies.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse pyproject.toml");
            throw;
        }

        return dependencies;
    }

    private ParsedDependency? ParseDependencyLine(string line)
    {
        if (!line.Contains("="))
            return null;

        var parts = line.Split("=", StringSplitOptions.None);
        if (parts.Length < 2)
            return null;

        var packageName = parts[0].Trim();

        if (packageName.Equals("python", StringComparison.OrdinalIgnoreCase))
            return null;

        var versionPart = parts[1].Trim();

        if (versionPart.StartsWith("\"") || versionPart.StartsWith("'"))
        {
            var version = versionPart.Trim('"', '\'', ' ');

            return new ParsedDependency
            {
                PackageName = packageName,
                Version = version,
                Type = DependencyType.PyPI
            };
        }

        if (versionPart.StartsWith("{"))
        {
            var versionMatch = Regex.Match(
                versionPart,
                @"version\s*=\s*['""]([^'""]+)['""]"
            );

            if (versionMatch.Success)
            {
                return new ParsedDependency
                {
                    PackageName = packageName,
                    Version = versionMatch.Groups[1].Value,
                    Type = DependencyType.PyPI
                };
            }
        }

        return new ParsedDependency
        {
            PackageName = packageName,
            Version = "unknown",
            Type = DependencyType.PyPI
        };
    }
}