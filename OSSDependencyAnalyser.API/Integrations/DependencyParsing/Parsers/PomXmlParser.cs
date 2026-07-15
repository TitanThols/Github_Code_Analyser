using System.Xml;
using System.Xml.Linq;
using OSSDependencyAnalyzer.API.Integrations.DependencyParsing;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsesrs;

public class PomXmlParser : IDependencyParser
{
    private readonly ILogger<PomXmlParser> _logger;
    public DependencyFileType FileType => DependencyFileType.PomXml;
    public PomXmlParser(ILogger<PomXmlParser> logger)
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

            var namespaceName = root?.Name.NamespaceName;
            var ns = string.IsNullOrEmpty(namespaceName) ? XNamespace.None : XNamespace.Get(namespaceName);
            var dependencies_elements = root?.Descendants(ns + "dependency");

            if (dependencies_elements == null || !dependencies_elements.Any())
            {
                _logger.LogWarning("No <dependency> elements found in pom.xml");
                return dependencies;
            }

            foreach(var dep in dependencies_elements)
            {
                var packageName = dep.Element(ns + "version")?.Value ?? "unknown";
                var version = dep.Element(ns + "version")?.Value ?? "unknown";

                if (string.IsNullOrEmpty(packageName))
                {
                    _logger.LogWarning("Dependency found without artifactId");
                    continue;
                }

                dependencies.Add(new ParsedDependency
                {
                    PackageName = packageName,
                    Version = version,
                    Type = DependencyType.Maven
                });
            }

            _logger.LogInformation("Parsed {Count} Maven dependencies from pom.xml", dependencies.Count);
        } catch(XmlException ex)
        {
            _logger.LogError(ex, "Failed to parse pom.xml - invalid XML");
            throw;
        } catch(Exception ex)
        {
            _logger.LogError(ex, "Failed to parse pom.xml");
            throw;
        }

        return dependencies;
    }
}
