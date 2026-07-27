using OSSDependencyAnalyzer.API.Models;
using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace OSSDependencyAnalyzer.API.Services;

public interface IAlertService
{
    Task SendVulnerabilityAlertAsync(Guid repositoryId, Vulnerability vulnerability);
    Task SendAnalysisCompletedAlertAsync(Guid repositoryId, string repositoryName);
}

public class AlertService : IAlertService
{
    private readonly IHubContext<AlertHub> _hubContext;
    private readonly ILogger<AlertService> _logger;

    public AlertService(IHubContext<AlertHub> hubContext, ILogger<AlertService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendAnalysisCompletedAlertAsync(Guid repositoryId, string repositoryName)
    {
        try
        {
            var message = new
            {
                type = "ANALYSIS_COMPLETED",
                repositoryId = repositoryId,
                repositoryName = repositoryName,
                timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.All.SendAsync("ReceiveAlert", message);

            _logger.LogInformation(
                "Sent analysis completed alert for repository {RepositoryName}",
                repositoryName
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send analysis completed alert");
        }
    }

    public async Task SendVulnerabilityAlertAsync(Guid repositoryId, Vulnerability vulnerability)
    {
        try
        {
            var severity = vulnerability.Severity.ToString().ToUpper();
            var message = new
            {
                type = "NEW_VULNERABILITY",
                repositoryId = repositoryId,
                cveId = vulnerability.CveId,
                title = vulnerability.Title,
                severity = severity,
                riskScore = vulnerability.RiskScore,
                timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.All.SendAsync("ReceiveAlert", message);

            _logger.LogInformation(
                "Sent vulnerability alert for CVE {CveId} in repository {RepositoryId}",
                vulnerability.CveId,
                repositoryId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send vulnerability alert");
        }
    }

    public class AlertHub : Microsoft.AspNetCore.SignalR.Hub
    {
        public async Task SendAlert(string message)
        {
            await Clients.All.SendAsync("ReceiveAlert", message);
        }
    }
    
}