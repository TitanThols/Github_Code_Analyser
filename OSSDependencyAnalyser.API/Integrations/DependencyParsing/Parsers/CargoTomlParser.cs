using System.Text.RegularExpressions;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsers;

public sealed partial class CargoTomlParser : IDependencyParser
{
    public DependencyFileType FileType => DependencyFileType.CargoToml;

    public bool CanParse(string fileName) => fileName.Equals("Cargo.toml", StringComparison.Ordinal);

    public IReadOnlyList<ParsedDependency> Parse(string fileContent)
    {
        var dependencies = new List<ParsedDependency>();
        bool? sectionIsDev = null;

        foreach (var rawLine in fileContent.Split(["\r\n", "\r", "\n"], StringSplitOptions.None))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith('['))
            {
                var table = line.Trim('[', ']', ' ');
                sectionIsDev = table switch
                {
                    "dependencies" or "workspace.dependencies" or "build-dependencies" => false,
                    "dev-dependencies" => true,
                    _ when table.StartsWith("target.", StringComparison.Ordinal) && table.EndsWith(".dependencies", StringComparison.Ordinal) => false,
                    _ => null
                };
                continue;
            }

            if (sectionIsDev is not { } isDev)
                continue;

            var entry = Entry().Match(line);
            if (!entry.Success)
                continue;

            var value = entry.Groups["value"].Value.Trim();
            string version;
            if (value.StartsWith('"'))
                version = value.Trim('"');
            else if (InlineVersion().Match(value) is { Success: true } inline)
                version = inline.Groups[1].Value;
            else
                version = "unknown"; // path/git/workspace = true dependencies

            var packageName = InlinePackage().Match(value) is { Success: true } renamed
                ? renamed.Groups[1].Value
                : entry.Groups["name"].Value;

            dependencies.Add(new ParsedDependency(packageName, version, DependencyType.Cargo, isDev));
        }

        return dependencies;
    }

    [GeneratedRegex(@"^(?<name>[A-Za-z0-9_\-]+)\s*=\s*(?<value>.+)$")]
    private static partial Regex Entry();

    [GeneratedRegex(@"version\s*=\s*""([^""]+)""")]
    private static partial Regex InlineVersion();

    [GeneratedRegex(@"package\s*=\s*""([^""]+)""")]
    private static partial Regex InlinePackage();
}
