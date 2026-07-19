using OSSDependencyAnalyzer.API.Models;
using OSSDependencyAnalyzer.API.Data;
using OSSDependencyAnalyzer.API.Integrations.Github;
using OSSDependencyAnalyzer.API.Integrations.DependencyParsing;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace OSSDependencyAnalyzer.API.Services;

public interface IAnalyzerService
{
    Task AnalyzeRepositoryAsync(Guid repositoryId, CancellationToken cancellationToken);
}

public class AnalyzerService : IAnalyzerService
{
    private readonly AppDbContext _db;
    public IGithubApiService _githubApiService;

    private static readonly List<string> DependencyFiles = new()
    {
        "package.json",
        "requirements.txt",
        "*.csproj",
        "pom.xml",
        "Gemfile",
        "puproject.toml"
    };
    private readonly IDependencyParserFactory _parserFactory;
    private readonly ILogger<AnalyzerService> _logger;

    public AnalyzerService(AppDbContext db, IGithubApiService githubApiService, IDependencyParserFactory parserFactory, ILogger<AnalyzerService> logger)
    {
        _db = db;
        _githubApiService = githubApiService;
        _parserFactory = parserFactory;
        _logger = logger;
    }

    public async Task AnalyzeRepositoryAsync(Guid repositoryId, CancellationToken cancellationToken)
    {
        try
        {
            var repository = await _db.Repositories.FirstOrDefaultAsync(r => r.Id == repositoryId, cancellationToken);
            if (repository == null)
            {
                _logger.LogError("Repository {RepositoryId} not found", repositoryId);
                return;
            }

            repository.Status = Repository.AnalysisStatus.Parsing;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Starting analysis for {Url}", repository.GithubUrl);

            var repoMetadata = await _githubApiService.GetRepositoryAsync(repository.RepositoryOwner, repository.RepositoryName);
            _logger.LogInformation("Fetched metadata for {Owner}/{Repo}", repository.RepositoryOwner, repository.RepositoryName);
            
            var parsedDependencies = new List<ParsedDependency>();

            foreach(var fileName in new[] {"package.json", "requirements.txt", "pom.xml", "Gemfile", "pyproject.toml" })
            {
                try
                {
                    var fileContent = await _githubApiService.GetFileContentAsync(repository.RepositoryOwner, repository.RepositoryName, fileName);

                    if (fileContent == null)
                    {
                        _logger.LogInformation("File {FileName} not found in repo", fileName);
                        continue;
                    }

                    var parser = _parserFactory.TryGetParserByFileName(fileName);
                    if (parser == null)
                    {
                        _logger.LogWarning("No parser found for {FileName}", fileName);
                        continue;
                    }

                    var dependencies = await parser.ParseAsync(fileContent);
                    parsedDependencies.AddRange(dependencies);

                    _logger.LogInformation("Parsed {Count} dependencies form {FileName}", dependencies.Count, fileName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse {FileName}", fileName);
                }
            }
            var dependencyEntities = parsedDependencies.Select(pd => new Dependency{
                Id = Guid.NewGuid(),
                RepositoryId = repositoryId,
                PackageName = pd.PackageName,
                CurrentVersion = pd.Version,
                Type = pd.Type,
                DiscoveredAt = DateTime.UtcNow  
            }).ToList();

            _db.Dependencies.AddRange(dependencyEntities);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Saved {Count} dependencies to database", dependencyEntities.Count);
            repository.Status = Repository.AnalysisStatus.Completed;
            repository.CompletedAt = DateTime.UtcNow;
            repository.LastAnalysedAt = DateTime.UtcNow;
            repository.TotalDependencies = dependencyEntities.Count;

            await _db.SaveChangesAsync();

            _logger.LogInformation("Analysis completed for {Url}. Found {Count} dependencies", repository.GithubUrl, dependencyEntities.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analysis failed for repository {RepositoryId}", repositoryId);

            var repository = await _db.Repositories.FirstOrDefaultAsync(r => r.Id == repositoryId);
            if (repository != null)
            {
                repository.Status = Repository.AnalysisStatus.Failed;
                repository.ErrorMessage = ex.Message;
                await _db.SaveChangesAsync();
            }
            throw;
        }
    }
}
