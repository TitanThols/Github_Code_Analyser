using System.Text.Json;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsers;

public sealed class ComposerJsonParser : IDependencyParser
{
    public DependencyFileType FileType => DependencyFileType.ComposerJson;

    public bool CanParse(string fileName) => fileName.Equals("composer.json", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<ParsedDependency> Parse(string fileContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(fileContent);
            var dependencies = new List<ParsedDependency>();
            Collect(doc.RootElement, "require", false, dependencies);
            Collect(doc.RootElement, "require-dev", true, dependencies);
            return dependencies;
        }
        catch (JsonException ex)
        {
            throw new DependencyParseException("composer.json is not valid JSON.", ex);
        }
    }

    private static void Collect(JsonElement root, string section, bool isDev, List<ParsedDependency> output)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty(section, out var deps)
            || deps.ValueKind != JsonValueKind.Object)
            return;

        foreach (var dep in deps.EnumerateObject())
        {
            // Platform requirements ("php", "ext-json", "lib-curl") aren't Packagist packages.
            if (!dep.Name.Contains('/') || dep.Value.ValueKind != JsonValueKind.String)
                continue;
            output.Add(new ParsedDependency(dep.Name, dep.Value.GetString()!, DependencyType.Composer, isDev));
        }
    }
}
