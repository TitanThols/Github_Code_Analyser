using Microsoft.EntityFrameworkCore;
using OSSDependencyAnalyzer.API.Data;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.Tests.TestSupport;

public static class TestDb
{
    public static AppDbContext Create(string? name = null) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options);

    public static Repository AddRepository(
        this AppDbContext db,
        Guid? userId = null,
        string owner = "acme",
        string name = "widgets",
        Repository.AnalysisStatus status = Repository.AnalysisStatus.Pending)
    {
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            UserId = userId ?? Guid.NewGuid(),
            GithubUrl = $"https://github.com/{owner}/{name}".ToLowerInvariant(),
            RepositoryOwner = owner,
            RepositoryName = name,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Repositories.Add(repository);
        db.SaveChanges();
        return repository;
    }
}
