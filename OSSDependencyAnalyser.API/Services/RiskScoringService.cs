using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Services;

public interface IRiskScoringService
{
    double CalculateVulnerabilityRiskScore(Vulnerability vulnerability);
    double CalculateRepositoryRiskScore(IEnumerable<double> vulnerabilityRiskScores);
}

/// <summary>
/// Risk model
/// ----------
/// Per finding (0–100):  severity weight (5/15/25/40) + CVSS × 5 (max 50) + 10 if actively exploited (CISA KEV).
/// When an advisory has no CVSS vector, a representative score for its severity band is used so that
/// a "critical" without a vector is not ranked below a "medium" with one.
///
/// Per repository (0–100): the worst finding sets the floor and every additional finding closes part of the
/// remaining gap ("noisy-OR"):  R = r₁ + (100 − r₁) · (1 − Π_{i≥2}(1 − rᵢ/200)).
/// Unlike an average, one critical issue can't be diluted by many low ones; unlike a sum, it stays bounded.
/// </summary>
public sealed class RiskScoringService : IRiskScoringService
{
    public const double ExploitedBonus = 10;

    public double CalculateVulnerabilityRiskScore(Vulnerability vulnerability)
    {
        var severityWeight = vulnerability.Severity switch
        {
            Severity.Critical => 40,
            Severity.High => 25,
            Severity.Medium => 15,
            _ => 5
        };

        var cvss = vulnerability.CvssScore is { } score
            ? Math.Clamp((double)score, 0, 10)
            : FallbackCvss(vulnerability.Severity);

        var risk = severityWeight + cvss * 5 + (vulnerability.IsExploitable ? ExploitedBonus : 0);
        return Math.Round(Math.Min(risk, 100), 1);
    }

    public double CalculateRepositoryRiskScore(IEnumerable<double> vulnerabilityRiskScores)
    {
        var ordered = vulnerabilityRiskScores
            .Select(score => Math.Clamp(score, 0, 100))
            .OrderByDescending(score => score)
            .ToList();

        if (ordered.Count == 0)
            return 0;

        var worst = ordered[0];
        var remainingFactor = ordered.Skip(1).Aggregate(1.0, (acc, score) => acc * (1 - score / 200));
        var total = worst + (100 - worst) * (1 - remainingFactor);
        return Math.Round(Math.Min(total, 100), 1);
    }

    /// <summary>Letter grade shown in the UI. Thresholds line up with single-finding scores per severity.</summary>
    public static string Grade(double repositoryRiskScore) => repositoryRiskScore switch
    {
        <= 0 => "A",
        < 30 => "B",
        < 50 => "C",
        < 75 => "D",
        _ => "F"
    };

    private static double FallbackCvss(Severity severity) => severity switch
    {
        Severity.Critical => 9.0,
        Severity.High => 7.5,
        Severity.Medium => 5.5,
        _ => 2.5
    };
}
