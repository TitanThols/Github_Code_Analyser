using System.Xml;
using System.Xml.Linq;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsers;

/// <summary>
/// SDK-style project files (<c>*.csproj</c>, <c>*.fsproj</c>, <c>*.vbproj</c>). Reads
/// <c>&lt;PackageReference Include="X" Version="1.0" /&gt;</c> as well as the child-element and
/// <c>VersionOverride</c> forms. With Central Package Management the version lives in
/// Directory.Packages.props, which <see cref="DirectoryPackagesPropsParser"/> handles.
/// </summary>
public sealed class CsProjParser : IDependencyParser
{
    public DependencyFileType FileType => DependencyFileType.Csproj;

    public bool CanParse(string fileName) =>
        fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
        || fileName.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase)
        || fileName.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<ParsedDependency> Parse(string fileContent) =>
        MsBuildXml.ReadPackages(fileContent, "PackageReference", "project file");
}

public sealed class DirectoryPackagesPropsParser : IDependencyParser
{
    public DependencyFileType FileType => DependencyFileType.DirectoryPackagesProps;

    public bool CanParse(string fileName) => fileName.Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<ParsedDependency> Parse(string fileContent) =>
        MsBuildXml.ReadPackages(fileContent, "PackageVersion", "Directory.Packages.props");
}

internal static class MsBuildXml
{
    public static IReadOnlyList<ParsedDependency> ReadPackages(string content, string elementName, string description)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(content);
        }
        catch (XmlException ex)
        {
            throw new DependencyParseException($"{description} is not valid XML.", ex);
        }

        var dependencies = new List<ParsedDependency>();
        // Old-style projects use the msbuild/2003 namespace; match on local name to support both.
        foreach (var element in doc.Descendants().Where(e => e.Name.LocalName == elementName))
        {
            var name = (string?)element.Attribute("Include") ?? (string?)element.Attribute("Update");
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var version = (string?)element.Attribute("Version")
                ?? (string?)element.Attribute("VersionOverride")
                ?? element.Elements().FirstOrDefault(e => e.Name.LocalName == "Version")?.Value
                ?? "unknown";

            var privateAssets = (string?)element.Attribute("PrivateAssets")
                ?? element.Elements().FirstOrDefault(e => e.Name.LocalName == "PrivateAssets")?.Value;
            var isDev = string.Equals(privateAssets?.Trim(), "all", StringComparison.OrdinalIgnoreCase);

            dependencies.Add(new ParsedDependency(name.Trim(), version.Trim(), DependencyType.NuGet, isDev));
        }

        return dependencies;
    }
}
