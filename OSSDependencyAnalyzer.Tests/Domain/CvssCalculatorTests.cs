using OSSDependencyAnalyzer.API.Domain;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.Tests.Domain;

public class CvssCalculatorTests
{
    // Reference scores from the FIRST CVSS v3.1 calculator / NVD.
    [Theory]
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H", 9.8)]  // e.g. Log4Shell-class RCE
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:C/C:H/I:H/A:H", 10.0)]
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:N/I:H/A:H", 9.1)]  // lodash CVE-2019-10744
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:N/I:N/A:H", 7.5)]  // typical ReDoS
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:N/UI:R/S:C/C:L/I:L/A:N", 6.1)]  // reflected XSS
    [InlineData("CVSS:3.1/AV:L/AC:L/PR:L/UI:N/S:U/C:H/I:H/A:H", 7.8)]  // local privilege escalation
    [InlineData("CVSS:3.1/AV:N/AC:H/PR:N/UI:N/S:U/C:H/I:N/A:N", 5.9)]
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:L/UI:N/S:C/C:L/I:L/A:N", 6.4)]  // stored XSS
    [InlineData("CVSS:3.1/AV:P/AC:H/PR:H/UI:R/S:U/C:L/I:N/A:N", 1.6)]
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:H/UI:N/S:C/C:H/I:H/A:H", 9.1)]
    [InlineData("CVSS:3.0/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H", 9.8)]
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H/E:P/RL:O", 9.8)] // temporal metrics ignored
    public void TryCalculateBaseScore_MatchesReferenceScores(string vector, double expected)
    {
        Assert.True(CvssCalculator.TryCalculateBaseScore(vector, out var score));
        Assert.Equal(expected, score, precision: 1);
    }

    [Fact]
    public void TryCalculateBaseScore_NoImpactIsZero()
    {
        Assert.True(CvssCalculator.TryCalculateBaseScore("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:N/I:N/A:N", out var score));
        Assert.Equal(0, score);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AV:N/AC:L/Au:N/C:P/I:P/A:P")]                                  // CVSS v2
    [InlineData("CVSS:4.0/AV:N/AC:L/AT:N/PR:N/UI:N/VC:H/VI:H/VA:H/SC:N/SI:N/SA:N")] // CVSS v4 not supported
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H")]                  // missing metric
    [InlineData("CVSS:3.1/AV:Q/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H")]              // invalid value
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:X/C:H/I:H/A:H")]              // invalid scope
    [InlineData("CVSS:3.1/AV:N/AC/PR:N/UI:N/S:U/C:H/I:H/A:H")]                // malformed pair
    public void TryCalculateBaseScore_RejectsUnsupportedOrMalformedVectors(string? vector) =>
        Assert.False(CvssCalculator.TryCalculateBaseScore(vector, out _));

    [Theory]
    [InlineData(10.0, Severity.Critical)]
    [InlineData(9.0, Severity.Critical)]
    [InlineData(8.9, Severity.High)]
    [InlineData(7.0, Severity.High)]
    [InlineData(6.9, Severity.Medium)]
    [InlineData(4.0, Severity.Medium)]
    [InlineData(3.9, Severity.Low)]
    [InlineData(0.0, Severity.Low)]
    public void ToSeverity_FollowsQualitativeRatingScale(double score, Severity expected) =>
        Assert.Equal(expected, CvssCalculator.ToSeverity(score));

    [Theory]
    [InlineData(4.000000001, 4.0)]  // the floating point artefact the spec's Roundup exists to absorb
    [InlineData(4.02, 4.1)]
    [InlineData(4.0, 4.0)]
    public void RoundUp_FollowsSpecificationAppendixA(double input, double expected) =>
        Assert.Equal(expected, CvssCalculator.RoundUp(input));
}
