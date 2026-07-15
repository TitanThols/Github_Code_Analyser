using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Integrations.DependencyParsing;

public class ParsedDependency
{
    public string PackageName { get; set; } = null!;
    public string Version { get; set; } = null!;
    public DependencyType Type { get; set; }
}

public enum DependencyFileType
{
    PackageJson,
    RequirementsTxt,
    Csproj,
    PomXml,
    Gemfile,
    PyprojectToml,
    GoMod,
    ComposerJson
}