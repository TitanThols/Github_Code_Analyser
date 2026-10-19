using Microsoft.AspNetCore.SignalR;
using OSSDependencyAnalyzer.API.Hubs;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Services;

public interface IAlertService
{
    Task SendAnalysisStatusAsync(Repository repository, CancellationToken cancellationToken = default);
    Task SendNewVulnerabilitiesAsync(Repository repository, IReadOnlyCollection<(Vulnerability Vulnerability, string PackageName)> introduced, CancellationToken cancellationToken = default);
}

public sealed class AlertService(IHubContext<AlertHub> hubContext, ILogger<AlertService> logger) : IAlertService
{
    /// <summary>Cap per scan so a first-time scan of a large repo doesn't flood the client.</summary>
    internal const int MaxVulnerabilityAlertsPerScan = 10;

    public Task SendAnalysisStatusAsync(Repository repository, CancellationToken cancellationToken = default) =>
        SendAsync(repository, new
        {
            type = repository.Status switch
            {
                Repository.AnalysisStatus.Completed => "ANALYSIS_COMPLETED",
                Repository.AnalysisStatus.Failed => "ANALYSIS_FAILED",
                _ => "ANALYSIS_PROGRESS"
            },
            repositoryId = repository.Id,
            repositoryName = $"{repository.RepositoryOwner}/{repository.RepositoryName}",
            status = repository.Status.ToString(),
            riskScore = repository.RiskScore,
            critical = repository.CriticalVulnerabilities,
            high = repository.HighVulnerabilities,
            message = repository.ErrorMessage,
            timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);

    public async Task SendNewVulnerabilitiesAsync(
        Repository repository,
        IReadOnlyCollection<(Vulnerability Vulnerability, string PackageName)> introduced,
        CancellationToken cancellationToken = default)
    {
        foreach (var (vulnerability, packageName) in introduced
                     .OrderByDescending(v => v.Vulnerability.RiskScore)
                     .Take(MaxVulnerabilityAlertsPerScan))
        {
            await SendAsync(repository, new
            {
                type = "NEW_VULNERABILITY",
                repositoryId = repository.Id,
                repositoryName = $"{repository.RepositoryOwner}/{repository.RepositoryName}",
                cveId = vulnerability.CveId,
                title = vulnerability.Title,
                packageName,
                severity = vulnerability.Severity.ToString(),
                riskScore = vulnerability.RiskScore,
                timestamp = DateTimeOffset.UtcNow
            }, cancellationToken);
        }
    }

    private async Task SendAsync(Repository repository, object message, CancellationToken cancellationToken)
    {
        if (repository.UserId is not { } userId)
            return;

        try
        {
            await hubContext.Clients.User(userId.ToString()).SendAsync(AlertHub.ReceiveAlert, message, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Notifications are best-effort; never fail an analysis because a socket hiccupped.
            logger.LogWarning(ex, "Failed to push alert for repository {RepositoryId}", repository.Id);
        }
    }
}
