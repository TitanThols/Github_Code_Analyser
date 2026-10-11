using System.Text.Json;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsers;

public sealed class NpmPackageJsonParser : IDependencyParser
{
    public DependencyFileType FileType => DependencyFileType.PackageJson;

    public bool CanParse(string fileName) => fileName.Equals("package.json", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<ParsedDependency> Parse(string fileContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(fileContent, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new DependencyParseException("package.json root must be an object.");

            var dependencies = new List<ParsedDependency>();
            Collect(doc.RootElement, "dependencies", isDev: false, dependencies);
            Collect(doc.RootElement, "optionalDependencies", isDev: false, dependencies);
            Collect(doc.RootElement, "devDependencies", isDev: true, dependencies);
            return dependencies;
        }
        catch (JsonException ex)
        {
            throw new DependencyParseException("package.json is not valid JSON.", ex);
        }
    }

    private static void Collect(JsonElement root, string section, bool isDev, List<ParsedDependency> output)
    {
        if (!root.TryGetProperty(section, out var deps) || deps.ValueKind != JsonValueKind.Object)
            return;

        foreach (var dep in deps.EnumerateObject())
        {
            if (dep.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(dep.Name))
                continue;
            output.Add(new ParsedDependency(dep.Name, dep.Value.GetString()!, DependencyType.NPM, isDev));
        }
    }
}
