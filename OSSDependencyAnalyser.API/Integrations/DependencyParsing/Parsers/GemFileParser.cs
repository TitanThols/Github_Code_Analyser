using OSSDependencyAnalyzer.API.Models;
using OSSDependencyAnalyzer.API.Integrations.DependencyParsing;
using System.Text.RegularExpressions;
using Serilog;

namespace OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsers;

public class GemfileParser : IDependencyParser
{
    private readonly ILogger<GemfileParser> _logger;

    public DependencyFileType FileType => DependencyFileType.Gemfile;

    public GemfileParser(ILogger<GemfileParser> logger)
    {
        _logger = logger;
    }

    public async Task<List<ParsedDependency>> ParseAsync(string fileContent)
    {
        var dependencies = new List<ParsedDependency>();

        try
        {
            var lines = fileContent.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    continue;

                var dependency = ParseGemLine(trimmed);
                if (dependency != null)
                {
                    dependencies.Add(dependency);
                }
            }

            _logger.LogInformation("Parsed {Count} Ruby gem dependencies from Gemfile", dependencies.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Gemfile");
            throw;
        }
        return dependencies;
    }
    private ParsedDependency? ParseGemLine(string line)
    {
        var match = Regex.Match(line, @"gem\s+['""]([^'""]+)['""](?:\s*,\s*['""]([^'""]+)['""])?");
        if (match.Success)
        {
            var gemName = match.Groups[1].Value;
            var version = match.Groups[2].Success ? match.Groups[2].Value : "unknown";

            return new ParsedDependency
            {
                PackageName = gemName,
                Version = version,
                Type = DependencyType.Gem
            };
        }
        return null;
    }
}
