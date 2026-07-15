using System.Text.Json;
using OSSDependencyAnalyzer.API.Integrations.DependencyParsing;
using OSSDependencyAnalyzer.API.Models;
using Serilog;

namespace OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsers;

public class NpmPackageJsonParser : IDependencyParser
{
   private readonly ILogger<NpmPackageJsonParser> _logger;
   public DependencyFileType FileType => DependencyFileType.PackageJson;
   public NpmPackageJsonParser(ILogger<NpmPackageJsonParser> logger)
    {
        _logger = logger;
    }

    public async Task<List<ParsedDependency>> ParseAsync(string fileContent)
    {
        var dependencies = new List<ParsedDependency>();

        try
        {
            using var doc = JsonDocument.Parse(fileContent);
            var root = doc.RootElement;

            if(root.TryGetProperty("dependencies", out var depObj))
            {
                foreach(var dep in depObj.EnumerateObject())
                {
                    var version = dep.Value.GetString() ?? "Unknown";
                    dependencies.Add(new ParsedDependency
                    {
                        PackageName = dep.Name,
                        Version = version,
                        Type = DependencyType.NPM
                    });
                }
            }

            if(root.TryGetProperty("devDependencies", out var devDepObj))
            {
                foreach(var dep in depObj.EnumerateObject())
                {
                    var version = dep.Value.GetString() ?? "Unknown";
                    dependencies.Add(new ParsedDependency
                    {
                        PackageName = dep.Name,
                        Version = version,
                        Type = DependencyType.NPM
                    });
                }
            }
            _logger.LogInformation("Parsed {count} NPM dependencies from package.json", dependencies.Count);
        } catch(JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse package.json");
            throw;
        }
        return dependencies;
    }
}
