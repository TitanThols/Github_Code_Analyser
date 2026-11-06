using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Integrations.DependencyParsing;

public sealed record ParsedDependency(string PackageName, string Version, DependencyType Type, bool IsDev = false);

public enum DependencyFileType
{
    PackageJson,
    RequirementsTxt,
    Csproj,
    PomXml,
    Gemfile,
    PyprojectToml,
    GoMod,
    ComposerJson,
    Gradle,
    CargoToml,
    DirectoryPackagesProps
}

/// <summary>Thrown when a manifest is syntactically invalid. The analyzer records it and moves on.</summary>
public sealed class DependencyParseException(string message, Exception? inner = null) : Exception(message, inner);
