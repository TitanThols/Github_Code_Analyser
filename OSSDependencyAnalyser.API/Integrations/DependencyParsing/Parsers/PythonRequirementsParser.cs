using OSSDependencyAnalyzer.API.Models;
using Serilog;

namespace OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsesrs;

public class PythonRequirementsParser : IDependencyParser
{
    private readonly ILogger<PythonRequirementsParser> _logger;
    public DependencyFileType FileType => DependencyFileType.RequirementsTxt;
    public PythonRequirementsParser(ILogger<PythonRequirementsParser> logger)
    {
        _logger = logger;
    }

    public async Task<List<ParsedDependency>> ParseAsync(string fileContent)
    {
        var dependencies = new List<ParsedDependency>();
        try
        {
            var lines = fileContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    continue;

                var packageInfo = ParsePackageLine(trimmed);
                if (packageInfo != null)
                {
                    dependencies.Add(packageInfo);
                }
            }
            _logger.LogInformation("Parsed {Count} Python dependencies from requirements.txt", dependencies.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pare requirements.txt");
            throw;
        }
        return dependencies;
    }
    private ParsedDependency? ParsePackageLine(string line)
    {
        var operators = new[] { "==", ">=", "<=", ">", "<", "~=", "!=" };

        foreach (var op in operators)
        {
            var parts = line.Split([op], StringSplitOptions.None);
            if (parts.Length >= 2)
            {
                var packageName = parts[0].Trim();
                if (packageName.Contains("["))
                    packageName = packageName.Split("[")[0];

                var version = parts[1].Split(",")[0].Trim();

                return new ParsedDependency
                {
                    PackageName = packageName,
                    Version = version,
                    Type = DependencyType.PyPI
                };

            }
        }

        if (!line.Contains("["))
        {
            return new ParsedDependency
            {
                PackageName = line,
                Version = "unknown",
                Type = DependencyType.PyPI
            };
        }

        return null;
    }
} 