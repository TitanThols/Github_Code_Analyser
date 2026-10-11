using System.Text.RegularExpressions;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsers;

public sealed partial class GemfileParser : IDependencyParser
{
    private static readonly HashSet<string> DevGroups = ["development", "test"];

    public DependencyFileType FileType => DependencyFileType.Gemfile;

    public bool CanParse(string fileName) => fileName == "Gemfile" || fileName == "gems.rb";

    public IReadOnlyList<ParsedDependency> Parse(string fileContent)
    {
        var dependencies = new List<ParsedDependency>();
        // Each open `do` block records whether it is a development/test group.
        var blocks = new Stack<bool>();

        foreach (var rawLine in fileContent.Split(["\r\n", "\r", "\n"], StringSplitOptions.None))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var group = GroupBlock().Match(line);
            if (group.Success)
            {
                var groups = Symbol().Matches(group.Groups["groups"].Value).Select(m => m.Groups[1].Value);
                blocks.Push(groups.All(DevGroups.Contains));
                continue;
            }

            if (line == "end")
            {
                if (blocks.Count > 0) blocks.Pop();
                continue;
            }

            if (DoBlock().IsMatch(line))
            {
                // Other blocks (platforms, source ... do) inherit the enclosing group's dev-ness.
                blocks.Push(blocks.Count > 0 && blocks.Peek());
            }

            var gem = GemLine().Match(line);
            if (!gem.Success)
                continue;

            var constraints = Quoted().Matches(gem.Groups["args"].Value)
                .Select(m => m.Groups[1].Value)
                .Where(LooksLikeConstraint)
                .ToList();

            var inlineGroup = InlineGroup().Match(gem.Groups["args"].Value);
            var isDev = (blocks.Count > 0 && blocks.Peek())
                || (inlineGroup.Success && Symbol().Matches(inlineGroup.Value).Select(m => m.Groups[1].Value).All(DevGroups.Contains));

            dependencies.Add(new ParsedDependency(
                gem.Groups["name"].Value,
                constraints.Count > 0 ? string.Join(", ", constraints) : "unknown",
                DependencyType.Gem,
                isDev));
        }

        return dependencies;
    }

    private static bool LooksLikeConstraint(string value) =>
        value.Length > 0 && (char.IsDigit(value[0]) || "~><=!".Contains(value[0]));

    [GeneratedRegex(@"^gem\s*\(?\s*['""](?<name>[^'""]+)['""](?<args>.*)$")]
    private static partial Regex GemLine();

    [GeneratedRegex(@"^group\s*\(?(?<groups>[^)]*?)\)?\s+do\b")]
    private static partial Regex GroupBlock();

    [GeneratedRegex(@"\bdo(\s*\|[^|]*\|)?\s*$")]
    private static partial Regex DoBlock();

    [GeneratedRegex(@":(\w+)")]
    private static partial Regex Symbol();

    [GeneratedRegex(@"['""]([^'""]*)['""]")]
    private static partial Regex Quoted();

    [GeneratedRegex(@"(?:group|groups):\s*(?:\[[^\]]*\]|:\w+)|:group\s*=>\s*(?:\[[^\]]*\]|:\w+)")]
    private static partial Regex InlineGroup();
}
