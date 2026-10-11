using System.Text.RegularExpressions;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsers;

/// <summary>
/// Extracts string-notation coordinates (<c>implementation 'g:a:v'</c> / <c>implementation("g:a:v")</c>)
/// from Groovy and Kotlin build scripts. Map notation and version catalogs are out of scope.
/// </summary>
public sealed partial class GradleParser : IDependencyParser
{
    public DependencyFileType FileType => DependencyFileType.Gradle;

    public bool CanParse(string fileName) =>
        fileName.Equals("build.gradle", StringComparison.OrdinalIgnoreCase)
        || fileName.Equals("build.gradle.kts", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<ParsedDependency> Parse(string fileContent)
    {
        var dependencies = new List<ParsedDependency>();
        foreach (Match match in Coordinate().Matches(fileContent))
        {
            var configuration = match.Groups["conf"].Value;
            var isDev = configuration.StartsWith("test", StringComparison.OrdinalIgnoreCase)
                || configuration.StartsWith("androidTest", StringComparison.OrdinalIgnoreCase);

            dependencies.Add(new ParsedDependency(
                $"{match.Groups["group"].Value}:{match.Groups["artifact"].Value}",
                match.Groups["version"].Value,
                DependencyType.Gradle,
                isDev));
        }
        return dependencies;
    }

    [GeneratedRegex(
        @"^\s*(?<conf>implementation|api|compileOnly|runtimeOnly|compile|runtime|testImplementation|testCompileOnly|testRuntimeOnly|androidTestImplementation|kapt|ksp|annotationProcessor|classpath)\s*\(?\s*['""](?<group>[^:'""\s]+):(?<artifact>[^:'""\s]+):(?<version>[^:'""\s@]+)[^'""]*['""]",
        RegexOptions.Multiline)]
    private static partial Regex Coordinate();
}
