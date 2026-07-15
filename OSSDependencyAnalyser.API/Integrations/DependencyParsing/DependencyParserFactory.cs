using OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsers;
using OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsesrs;

namespace OSSDependencyAnalyzer.API.Integrations.DependencyParsing;

public interface IDependencyParserFactory
{
    IDependencyParser GetParser(DependencyFileType fileType);
    IDependencyParser? TryGetParserByFileName(string fileName);
}

public class DependencyParserFactory : IDependencyParserFactory
{
    private readonly IServiceProvider _serviceProvider;

    public DependencyParserFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IDependencyParser GetParser(DependencyFileType fileType)
    {
        return fileType switch
        {
            DependencyFileType.PackageJson => _serviceProvider.GetRequiredService<NpmPackageJsonParser>(),
            DependencyFileType.RequirementsTxt => _serviceProvider.GetRequiredService<PythonRequirementsParser>(),
            _ => throw new NotSupportedException($"Parser for {fileType} not implemented yet")
        };
    }

    public IDependencyParser? TryGetParserByFileName(string fileName)
    {
        return fileName switch
        {
            "package.json" => _serviceProvider.GetRequiredService<NpmPackageJsonParser>(),
            "requirements.txt" => _serviceProvider.GetRequiredService<PythonRequirementsParser>(),
            _ => null
        };
    }
}