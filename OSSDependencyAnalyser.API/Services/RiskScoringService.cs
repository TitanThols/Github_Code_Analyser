using OSSDependencyAnalyzer.API.Models;
using Serilog.Core;

namespace OSSDependencyAnalyzer.API.Services;

public interface IRiskScoringService
{
    double CalculateVulnerabilityRiskScore(Vulnerability vuln);
    double CalculateRepositoryRiskScore(List<Vulnerability> vulnerabilities);
}

public class RiskScoringService : IRiskScoringService
{
    private readonly ILogger<RiskScoringService> _logger;

    public RiskScoringService(ILogger<RiskScoringService> logger)
    {
        _logger = logger;
    }

    public double CalculateRepositoryRiskScore(List<Vulnerability> vulnerabilities)
    {
        if (!vulnerabilities.Any())
        {
            return 0;
        }
            var totalRisk = vulnerabilities.Sum(v => CalculateVulnerabilityRiskScore(v));
            var averageRisk = totalRisk/vulnerabilities.Count;

            _logger.LogInformation(
            "Calculated repository risk score {RiskScore} based on {Count} vulnerabilities",
            averageRisk,
            vulnerabilities.Count
        );

        return averageRisk;
    }

    public double CalculateVulnerabilityRiskScore(Vulnerability vuln)
    {
        try
        {
            double severityWeight = vuln.Severity switch
            {
                Severity.Critical => 40,
                Severity.High => 25,
                Severity.Medium => 15,
                Severity.Low => 5,
                _ => 0
            };

            double cvssBonus = 0;
            if (vuln.CvssScore.HasValue)
            {
                cvssBonus = (double)vuln.CvssScore.Value / 10.0 * 50.0;
                cvssBonus = Math.Min(cvssBonus, 50);
            }

            double exploitabilityBonus = vuln.IsExploitable ? 10 : 0;
            double riskScore = severityWeight + cvssBonus + exploitabilityBonus;
            riskScore = Math.Min(riskScore, 100);

            _logger.LogInformation(
                "Calculated risk score {RiskScore} for CVE {CveId} (Severity={Severity}, CVSS={CvssScore}, Exploitable={IsExploitable})",
                riskScore,
                vuln.CveId,
                vuln.Severity,
                vuln.CvssScore ?? 0,
                vuln.IsExploitable
            );

            return riskScore;
        }
                catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate risk score for CVE {CveId}", vuln.CveId);
            return 0;
        }
    }
}