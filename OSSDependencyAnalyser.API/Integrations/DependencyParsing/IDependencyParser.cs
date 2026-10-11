namespace OSSDependencyAnalyzer.API.Integrations.DependencyParsing;

public interface IDependencyParser
{
    DependencyFileType FileType { get; }

    /// <summary>True when this parser understands a file with the given name (no directory component).</summary>
    bool CanParse(string fileName);

    /// <exception cref="DependencyParseException">The content is not a valid manifest.</exception>
    IReadOnlyList<ParsedDependency> Parse(string fileContent);
}
