using System.Text.RegularExpressions;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsers;

public sealed partial class PythonRequirementsParser : IDependencyParser
{
    public DependencyFileType FileType => DependencyFileType.RequirementsTxt;

    // requirements.txt, requirements-dev.txt, requirements/prod.txt (matched by name only), dev-requirements.txt
    public bool CanParse(string fileName) =>
        fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
        && fileName.Contains("requirements", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<ParsedDependency> Parse(string fileContent)
    {
        var dependencies = new List<ParsedDependency>();

        foreach (var rawLine in fileContent.Split(["\r\n", "\r", "\n"], StringSplitOptions.None))
        {
            var line = StripComment(rawLine).Trim().TrimEnd('\\').Trim();

            // Options (-r other.txt, -e ., --index-url ...) are not packages.
            if (line.Length == 0 || line.StartsWith('-'))
                continue;

            if (TryParsePep508(line, out var name, out var spec))
                dependencies.Add(new ParsedDependency(name, spec, DependencyType.PyPI));
        }

        return dependencies;
    }

    /// <summary>
    /// Parses a PEP 508 requirement ("requests[socks]>=2.0,<3; python_version>'3.8'") into name and version spec.
    /// Direct URL references ("pkg @ https://...") yield the name with an "unknown" spec.
    /// </summary>
    internal static bool TryParsePep508(string requirement, out string name, out string spec)
    {
        name = spec = string.Empty;

        var markerIndex = requirement.IndexOf(';');
        if (markerIndex >= 0)
            requirement = requirement[..markerIndex];

        var match = Pep508().Match(requirement.Trim());
        if (!match.Success)
            return false;

        name = match.Groups["name"].Value;
        var rest = match.Groups["rest"].Value.Trim();

        if (rest.StartsWith('@') || rest.Contains("://", StringComparison.Ordinal))
            spec = "unknown";
        else
            spec = rest.Trim('(', ')', ' ') is { Length: > 0 } s ? s.Replace(" ", string.Empty) : "unknown";

        return true;
    }

    private static string StripComment(string line)
    {
        // A '#' starts a comment only at line start or after whitespace (URLs may contain '#egg=').
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '#' && (i == 0 || char.IsWhiteSpace(line[i - 1])))
                return line[..i];
        }
        return line;
    }

    [GeneratedRegex(@"^(?<name>[A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?)\s*(?:\[[^\]]*\])?\s*(?<rest>.*)$")]
    private static partial Regex Pep508();
}
