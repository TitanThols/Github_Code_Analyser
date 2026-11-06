using System.Text.RegularExpressions;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsers;

/// <summary>
/// Handles the three common pyproject layouts: PEP 621 (<c>[project] dependencies = [...]</c>,
/// <c>[project.optional-dependencies]</c>), PEP 735 (<c>[dependency-groups]</c>) and Poetry
/// (<c>[tool.poetry.dependencies]</c>, <c>[tool.poetry.group.*.dependencies]</c>). This is a
/// purpose-built line scanner, not a general TOML parser.
/// </summary>
public sealed partial class PyprojectTomlParser : IDependencyParser
{
    public DependencyFileType FileType => DependencyFileType.PyprojectToml;

    public bool CanParse(string fileName) => fileName.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<ParsedDependency> Parse(string fileContent)
    {
        var dependencies = new List<ParsedDependency>();
        var table = string.Empty;
        bool? collectingArrayAsDev = null; // non-null while inside a multi-line PEP 508 array

        foreach (var rawLine in fileContent.Split(["\r\n", "\r", "\n"], StringSplitOptions.None))
        {
            var line = StripComment(rawLine).Trim();
            if (line.Length == 0)
                continue;

            if (collectingArrayAsDev is { } arrayIsDev)
            {
                CollectArrayItems(line, arrayIsDev, dependencies);
                if (line.Contains(']') && !InsideQuotes(line, line.LastIndexOf(']')))
                    collectingArrayAsDev = null;
                continue;
            }

            var header = TableHeader().Match(line);
            if (header.Success)
            {
                table = header.Groups["name"].Value.Trim();
                continue;
            }

            var assignment = Assignment().Match(line);
            if (!assignment.Success)
                continue;

            var key = assignment.Groups["key"].Value.Trim().Trim('"', '\'');
            var value = assignment.Groups["value"].Value.Trim();

            var pep508ArrayIsDev = table switch
            {
                "project" when key == "dependencies" => false,
                "project.optional-dependencies" or "dependency-groups" => true,
                _ => (bool?)null
            };

            if (pep508ArrayIsDev is { } isDev && value.StartsWith('['))
            {
                CollectArrayItems(value, isDev, dependencies);
                var closed = value.Contains(']') && !InsideQuotes(value, value.LastIndexOf(']'));
                if (!closed)
                    collectingArrayAsDev = isDev;
                continue;
            }

            if (IsPoetryTable(table, out var poetryIsDev) && !key.Equals("python", StringComparison.OrdinalIgnoreCase))
            {
                dependencies.Add(new ParsedDependency(key, ExtractPoetryVersion(value), DependencyType.PyPI, poetryIsDev));
            }
        }

        return dependencies;
    }

    private static bool IsPoetryTable(string table, out bool isDev)
    {
        isDev = false;
        if (table == "tool.poetry.dependencies")
            return true;
        if (table == "tool.poetry.dev-dependencies"
            || (table.StartsWith("tool.poetry.group.", StringComparison.Ordinal) && table.EndsWith(".dependencies", StringComparison.Ordinal)))
        {
            isDev = true;
            return true;
        }
        return false;
    }

    private static string ExtractPoetryVersion(string value)
    {
        if (value.StartsWith('"') || value.StartsWith('\''))
            return value.Trim('"', '\'', ' ');

        if (value.StartsWith('{'))
        {
            var version = InlineVersion().Match(value);
            return version.Success ? version.Groups[1].Value : "unknown";
        }

        return "unknown";
    }

    private static void CollectArrayItems(string text, bool isDev, List<ParsedDependency> output)
    {
        foreach (Match item in QuotedString().Matches(text))
        {
            var requirement = item.Groups["s"].Value;
            if (PythonRequirementsParser.TryParsePep508(requirement, out var name, out var spec))
                output.Add(new ParsedDependency(name, spec, DependencyType.PyPI, isDev));
        }
    }

    private static string StripComment(string line)
    {
        var inQuote = '\0';
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuote != '\0')
            {
                if (c == inQuote) inQuote = '\0';
            }
            else if (c is '"' or '\'')
            {
                inQuote = c;
            }
            else if (c == '#')
            {
                return line[..i];
            }
        }
        return line;
    }

    private static bool InsideQuotes(string text, int index)
    {
        var quotes = text[..index].Count(c => c == '"') + text[..index].Count(c => c == '\'');
        return quotes % 2 == 1;
    }

    [GeneratedRegex(@"^\[(?!\[)(?<name>[^\]]+)\]$")]
    private static partial Regex TableHeader();

    [GeneratedRegex(@"^(?<key>""[^""]+""|'[^']+'|[A-Za-z0-9_.\-]+)\s*=\s*(?<value>.*)$")]
    private static partial Regex Assignment();

    [GeneratedRegex(@"version\s*=\s*['""]([^'""]+)['""]")]
    private static partial Regex InlineVersion();

    [GeneratedRegex(@"""(?<s>[^""]*)""|'(?<s>[^']*)'")]
    private static partial Regex QuotedString();
}
