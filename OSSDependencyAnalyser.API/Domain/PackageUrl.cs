using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Domain;

/// <summary>Builds Package URLs (https://github.com/package-url/purl-spec), the identifier SBOM tools key on.</summary>
public static class PackageUrl
{
    public static string Create(DependencyType type, string name, string? version)
    {
        var (purlType, namespacePart, namePart) = type switch
        {
            DependencyType.NPM when name.StartsWith('@') && name.Contains('/') =>
                ("npm", name[..name.IndexOf('/')], name[(name.IndexOf('/') + 1)..]),
            DependencyType.NPM => ("npm", null, name),
            // PyPI names are case-insensitive and '_' is equivalent to '-'.
            DependencyType.PyPI => ("pypi", null, name.ToLowerInvariant().Replace('_', '-')),
            DependencyType.NuGet => ("nuget", null, name),
            DependencyType.Maven or DependencyType.Gradle when name.Contains(':') =>
                ("maven", name[..name.IndexOf(':')], name[(name.IndexOf(':') + 1)..]),
            DependencyType.Maven or DependencyType.Gradle => ("maven", null, name),
            DependencyType.Gem => ("gem", null, name),
            DependencyType.Go when name.Contains('/') =>
                ("golang", name[..name.LastIndexOf('/')], name[(name.LastIndexOf('/') + 1)..]),
            DependencyType.Go => ("golang", null, name),
            DependencyType.Composer when name.Contains('/') =>
                ("composer", name[..name.IndexOf('/')], name[(name.IndexOf('/') + 1)..]),
            DependencyType.Composer => ("composer", null, name),
            DependencyType.Cargo => ("cargo", null, name),
            DependencyType.Pub => ("pub", null, name),
            _ => ("generic", null, name)
        };

        var purl = $"pkg:{purlType}/";
        if (namespacePart is not null)
            purl += string.Join('/', namespacePart.Split('/').Select(Uri.EscapeDataString)) + "/";
        purl += Uri.EscapeDataString(namePart);
        if (!string.IsNullOrWhiteSpace(version))
            purl += "@" + Uri.EscapeDataString(version);
        return purl;
    }
}
