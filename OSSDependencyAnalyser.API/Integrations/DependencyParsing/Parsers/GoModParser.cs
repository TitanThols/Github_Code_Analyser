using System.Text.RegularExpressions;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsers;

public sealed partial class GoModParser : IDependencyParser
{
    public DependencyFileType FileType => DependencyFileType.GoMod;

    public bool CanParse(string fileName) => fileName.Equals("go.mod", StringComparison.Ordinal);

    public IReadOnlyList<ParsedDependency> Parse(string fileContent)
    {
        var dependencies = new List<ParsedDependency>();
        var inRequireBlock = false;

        foreach (var rawLine in fileContent.Split(["\r\n", "\r", "\n"], StringSplitOptions.None))
        {
            var commentIndex = rawLine.IndexOf("//", StringComparison.Ordinal);
            // "// indirect" marks transitive requirements; they're still part of the build.
            var line = (commentIndex >= 0 ? rawLine[..commentIndex] : rawLine).Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith("require (", StringComparison.Ordinal) || line == "require(")
            {
                inRequireBlock = true;
                continue;
            }

            if (inRequireBlock && line == ")")
            {
                inRequireBlock = false;
                continue;
            }

            var candidate = inRequireBlock ? line : line.StartsWith("require ", StringComparison.Ordinal) ? line[8..].Trim() : null;
            if (candidate is null)
                continue;

            var match = Requirement().Match(candidate);
            if (match.Success)
                dependencies.Add(new ParsedDependency(match.Groups["module"].Value, match.Groups["version"].Value, DependencyType.Go));
        }

        return dependencies;
    }

    [GeneratedRegex(@"^(?<module>[^\s]+)\s+(?<version>v[^\s]+)$")]
    private static partial Regex Requirement();
}
