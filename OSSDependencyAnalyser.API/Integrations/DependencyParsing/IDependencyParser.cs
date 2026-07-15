namespace OSSDependencyAnalyzer.API.Integrations.DependencyParsing;

public interface IDependencyParser
{
    DependencyFileType FileType { get; }
    Task<List<ParsedDependency>> ParseAsync(string fileContent);
}