namespace OSSDependencyAnalyzer.API.Integrations.DependencyParsing;

public interface IDependencyParserFactory
{
    IDependencyParser? TryGetParserByFileName(string fileNameOrPath);
    bool IsManifest(string fileNameOrPath);
}

public sealed class DependencyParserFactory(IEnumerable<IDependencyParser> parsers) : IDependencyParserFactory
{
    private readonly IReadOnlyList<IDependencyParser> _parsers = parsers.ToList();

    public IDependencyParser? TryGetParserByFileName(string fileNameOrPath)
    {
        var fileName = Path.GetFileName(fileNameOrPath);
        return _parsers.FirstOrDefault(parser => parser.CanParse(fileName));
    }

    public bool IsManifest(string fileNameOrPath) => TryGetParserByFileName(fileNameOrPath) is not null;
}
