using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsers;

/// <summary>
/// Maven POM. Emits <c>groupId:artifactId</c> names (the OSV Maven naming scheme), resolves
/// <c>${property}</c> versions from &lt;properties&gt; and falls back to &lt;dependencyManagement&gt;
/// for dependencies declared without a version.
/// </summary>
public sealed partial class PomXmlParser : IDependencyParser
{
    public DependencyFileType FileType => DependencyFileType.PomXml;

    public bool CanParse(string fileName) => fileName.Equals("pom.xml", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<ParsedDependency> Parse(string fileContent)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(fileContent);
        }
        catch (XmlException ex)
        {
            throw new DependencyParseException("pom.xml is not valid XML.", ex);
        }

        var project = doc.Root ?? throw new DependencyParseException("pom.xml has no root element.");
        var ns = project.Name.Namespace;

        var properties = project.Element(ns + "properties")?.Elements()
            .GroupBy(e => e.Name.LocalName)
            .ToDictionary(g => g.Key, g => g.First().Value.Trim(), StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);

        var projectVersion = project.Element(ns + "version")?.Value.Trim()
            ?? project.Element(ns + "parent")?.Element(ns + "version")?.Value.Trim();
        if (projectVersion is not null)
        {
            properties.TryAdd("project.version", projectVersion);
            properties.TryAdd("version", projectVersion);
        }

        var managed = project.Element(ns + "dependencyManagement")?.Element(ns + "dependencies")?.Elements(ns + "dependency")
            .Select(d => (Key: Coordinates(d, ns), Version: d.Element(ns + "version")?.Value.Trim()))
            .Where(d => d.Key is not null && d.Version is not null)
            .GroupBy(d => d.Key!)
            .ToDictionary(g => g.Key, g => g.First().Version!)
            ?? new Dictionary<string, string>();

        // Direct dependencies of the project and of any profile — not plugin or management entries.
        var dependencyLists = new[] { project }
            .Concat(project.Element(ns + "profiles")?.Elements(ns + "profile") ?? [])
            .Select(owner => owner.Element(ns + "dependencies"))
            .Where(list => list is not null);

        var dependencies = new List<ParsedDependency>();
        foreach (var dependency in dependencyLists.SelectMany(list => list!.Elements(ns + "dependency")))
        {
            var key = Coordinates(dependency, ns);
            if (key is null)
                continue;

            var version = dependency.Element(ns + "version")?.Value.Trim();
            if (string.IsNullOrEmpty(version))
                managed.TryGetValue(key, out version);

            version = version is null ? "unknown" : ResolveProperties(version, properties);
            var scope = dependency.Element(ns + "scope")?.Value.Trim();

            dependencies.Add(new ParsedDependency(key, version, DependencyType.Maven, IsDev: scope == "test"));
        }

        return dependencies;
    }

    private static string? Coordinates(XElement dependency, XNamespace ns)
    {
        var groupId = dependency.Element(ns + "groupId")?.Value.Trim();
        var artifactId = dependency.Element(ns + "artifactId")?.Value.Trim();
        return string.IsNullOrEmpty(groupId) || string.IsNullOrEmpty(artifactId) ? null : $"{groupId}:{artifactId}";
    }

    private static string ResolveProperties(string value, IReadOnlyDictionary<string, string> properties)
    {
        // Resolve up to a few levels of indirection (${a} -> ${b} -> 1.0); unknown keys stay as-is.
        for (var depth = 0; depth < 5 && value.Contains("${", StringComparison.Ordinal); depth++)
        {
            value = PropertyReference().Replace(value, m =>
                properties.TryGetValue(m.Groups[1].Value, out var resolved) ? resolved : m.Value);
        }
        return value;
    }

    [GeneratedRegex(@"\$\{([^}]+)\}")]
    private static partial Regex PropertyReference();
}
