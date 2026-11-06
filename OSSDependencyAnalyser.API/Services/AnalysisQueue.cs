using Hangfire;

namespace OSSDependencyAnalyzer.API.Services;

/// <summary>Decouples request handling from the background job library.</summary>
public interface IAnalysisQueue
{
    void Enqueue(Guid repositoryId);
}

public sealed class HangfireAnalysisQueue(IBackgroundJobClient jobs) : IAnalysisQueue
{
    // Hangfire substitutes its own shutdown-aware token for CancellationToken.None at execution time.
    public void Enqueue(Guid repositoryId) =>
        jobs.Enqueue<IAnalyzerService>(service => service.AnalyzeRepositoryAsync(repositoryId, CancellationToken.None));
}
