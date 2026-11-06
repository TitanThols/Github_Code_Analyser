using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Domain;

/// <summary>
/// Computes CVSS v3.0 / v3.1 base scores from a vector string, following the FIRST specification
/// (https://www.first.org/cvss/v3.1/specification-document §7). OSV publishes vectors rather than
/// scores, so this is what turns "CVSS:3.1/AV:N/AC:L/..." into a sortable 0–10 number.
/// </summary>
public static class CvssCalculator
{
    public static bool TryCalculateBaseScore(string? vector, out double score)
    {
        score = 0;
        if (string.IsNullOrWhiteSpace(vector))
            return false;

        var parts = vector.Trim().Split('/');
        if (parts.Length < 9 || !parts[0].StartsWith("CVSS:3.", StringComparison.Ordinal))
            return false;

        var metrics = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in parts.Skip(1))
        {
            var kv = part.Split(':');
            if (kv.Length != 2)
                return false;
            metrics[kv[0]] = kv[1];
        }

        if (!metrics.TryGetValue("S", out var scope) || scope is not ("U" or "C"))
            return false;
        var changed = scope == "C";

        if (!TryWeight(metrics, "AV", out var av, ("N", 0.85), ("A", 0.62), ("L", 0.55), ("P", 0.2))
            || !TryWeight(metrics, "AC", out var ac, ("L", 0.77), ("H", 0.44))
            || !TryWeight(metrics, "PR", out var pr, ("N", 0.85), ("L", changed ? 0.68 : 0.62), ("H", changed ? 0.5 : 0.27))
            || !TryWeight(metrics, "UI", out var ui, ("N", 0.85), ("R", 0.62))
            || !TryWeight(metrics, "C", out var c, ("H", 0.56), ("L", 0.22), ("N", 0))
            || !TryWeight(metrics, "I", out var i, ("H", 0.56), ("L", 0.22), ("N", 0))
            || !TryWeight(metrics, "A", out var a, ("H", 0.56), ("L", 0.22), ("N", 0)))
            return false;

        var iss = 1 - (1 - c) * (1 - i) * (1 - a);
        var impact = changed
            ? 7.52 * (iss - 0.029) - 3.25 * Math.Pow(iss - 0.02, 15)
            : 6.42 * iss;
        var exploitability = 8.22 * av * ac * pr * ui;

        if (impact <= 0)
        {
            score = 0;
            return true;
        }

        score = changed
            ? RoundUp(Math.Min(1.08 * (impact + exploitability), 10))
            : RoundUp(Math.Min(impact + exploitability, 10));
        return true;
    }

    /// <summary>Qualitative severity rating scale from the CVSS specification.</summary>
    public static Severity ToSeverity(double score) => score switch
    {
        >= 9.0 => Severity.Critical,
        >= 7.0 => Severity.High,
        >= 4.0 => Severity.Medium,
        _ => Severity.Low
    };

    /// <summary>CVSS v3.1 Appendix A "Roundup" — avoids floating point artefacts like 4.000000001 → 4.1.</summary>
    internal static double RoundUp(double value)
    {
        var intInput = (long)Math.Round(value * 100000);
        return intInput % 10000 == 0
            ? intInput / 100000.0
            : (Math.Floor(intInput / 10000.0) + 1) / 10.0;
    }

    private static bool TryWeight(
        Dictionary<string, string> metrics,
        string key,
        out double weight,
        params (string Code, double Weight)[] table)
    {
        weight = 0;
        if (!metrics.TryGetValue(key, out var code))
            return false;
        foreach (var (candidate, value) in table)
        {
            if (candidate == code)
            {
                weight = value;
                return true;
            }
        }
        return false;
    }
}
