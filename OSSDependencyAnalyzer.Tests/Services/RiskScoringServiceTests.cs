using OSSDependencyAnalyzer.API.Models;
using OSSDependencyAnalyzer.API.Services;

namespace OSSDependencyAnalyzer.Tests.Services;

public class RiskScoringServiceTests
{
    private readonly RiskScoringService _scoring = new();

    private static Vulnerability Vuln(Severity severity, decimal? cvss = null, bool exploited = false) =>
        new() { CveId = "CVE-0000-0000", Title = "t", Description = "d", Severity = severity, CvssScore = cvss, IsExploitable = exploited };

    [Theory]
    [InlineData(Severity.Critical, 9.8, false, 89.0)]
    [InlineData(Severity.Critical, 9.8, true, 99.0)]
    [InlineData(Severity.Critical, 10.0, true, 100.0)]   // capped
    [InlineData(Severity.High, 7.5, false, 62.5)]
    [InlineData(Severity.Medium, 5.3, false, 41.5)]
    [InlineData(Severity.Low, 3.1, false, 20.5)]
    public void VulnerabilityScore_CombinesSeverityCvssAndExploitation(Severity severity, double cvss, bool exploited, double expected) =>
        Assert.Equal(expected, _scoring.CalculateVulnerabilityRiskScore(Vuln(severity, (decimal)cvss, exploited)));

    [Theory]
    [InlineData(Severity.Critical, 85.0)]
    [InlineData(Severity.High, 62.5)]
    [InlineData(Severity.Medium, 42.5)]
    [InlineData(Severity.Low, 17.5)]
    public void VulnerabilityScore_UsesSeverityBandWhenCvssMissing(Severity severity, double expected) =>
        Assert.Equal(expected, _scoring.CalculateVulnerabilityRiskScore(Vuln(severity)));

    [Fact]
    public void VulnerabilityScore_CriticalWithoutCvssOutranksMediumWithCvss() =>
        Assert.True(_scoring.CalculateVulnerabilityRiskScore(Vuln(Severity.Critical))
                    > _scoring.CalculateVulnerabilityRiskScore(Vuln(Severity.Medium, 6.9m)));

    [Fact]
    public void VulnerabilityScore_ClampsOutOfRangeCvss() =>
        Assert.Equal(90.0, _scoring.CalculateVulnerabilityRiskScore(Vuln(Severity.Critical, 42m)));

    [Fact]
    public void RepositoryScore_IsZeroWithoutFindings() => Assert.Equal(0, _scoring.CalculateRepositoryRiskScore([]));

    [Fact]
    public void RepositoryScore_SingleFindingEqualsItsScore() => Assert.Equal(62.5, _scoring.CalculateRepositoryRiskScore([62.5]));

    [Fact]
    public void RepositoryScore_ManyLowFindingsCannotDiluteOneCritical()
    {
        var withLows = _scoring.CalculateRepositoryRiskScore([89.0, .. Enumerable.Repeat(17.5, 20)]);
        Assert.True(withLows >= 89.0);  // an average would have dropped this to ~21
    }

    [Fact]
    public void RepositoryScore_IsOrderIndependentMonotonicAndBounded()
    {
        var a = _scoring.CalculateRepositoryRiskScore([20, 60, 40]);
        var b = _scoring.CalculateRepositoryRiskScore([60, 40, 20]);
        var more = _scoring.CalculateRepositoryRiskScore([60, 40, 20, 10]);
        var huge = _scoring.CalculateRepositoryRiskScore(Enumerable.Repeat(99.0, 500));

        Assert.Equal(a, b);
        Assert.True(more > a);
        Assert.InRange(huge, 99, 100);
    }

    [Theory]
    [InlineData(0, "A")]
    [InlineData(17.5, "B")]
    [InlineData(42.5, "C")]
    [InlineData(62.5, "D")]
    [InlineData(85, "F")]
    [InlineData(100, "F")]
    public void Grade_MapsScoreToLetter(double score, string grade) => Assert.Equal(grade, RiskScoringService.Grade(score));
}
