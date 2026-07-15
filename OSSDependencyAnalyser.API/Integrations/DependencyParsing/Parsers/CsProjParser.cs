using System.Xml;
using System.Xml.Linq;
using OSSDependencyAnalyzer.API.Integrations.DependencyParsing;
using OSSDependencyAnalyzer.API.Models;
using Serilog;

namespace OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsesrs;

public  class CsProjParser : IDependencyParser
{
    private readonly ILogger<CsProjParser> _logger;

    public DependencyFileType FileType => DependencyFileType.Csproj;
    public CsProjParser(ILogger<CsProjParser> logger)
    {
        _logger = logger;
    }

    public async Task<List<ParsedDependency>> ParseAsync(string fileContent)
    {
        var dependencies = new List<ParsedDependency>();

        try
        {
            var doc = XDocument.Parse(fileContent);
            var root = doc.Root;

            var packageReference = root?.Descendants("Packagereference");

            if (packageReference == null)
            {
                _logger.LogWarning("NO PackageReference elements found in .csproj");
                return dependencies;
            }

            foreach (var packageRef in packageReference)
            {
                var packageName = packageRef.Attribute("Include")?.Value;
                var version = packageRef.Attribute("Version")?.Value ?? "unknown";

                if (string.IsNullOrEmpty(packageName))
                {
                    _logger.LogWarning("PackageReference found without Include attribute");
                    continue;
                }

                dependencies.Add(new ParsedDependency
                {
                    PackageName = packageName,
                    Version = version,
                    Type = DependencyType.NuGet
                });
            }

            _logger.LogInformation("Parsed {Count} NuGet dependencies from .csproj", dependencies.Count);
        } catch(XmlException ex)
        {
            _logger.LogError(ex, "Failed to parse .csproj file - invalid XML");
            throw;
            
        } catch(Exception ex)
    
        {
            _logger.LogError(ex, "Failed to parse .csproj file");
        }
        return dependencies;
    }
}