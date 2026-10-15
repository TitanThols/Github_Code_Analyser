namespace OSSDependencyAnalyzer.API.Integrations.Osv;

/// <summary>
/// Best-effort, ecosystem-agnostic version ordering used only to pick the most relevant "fixed in"
/// version from an advisory. Numeric segments compare numerically; a release sorts after its
/// pre-releases (1.0.0-beta &lt; 1.0.0), matching SemVer and PEP 440 for the common cases.
/// </summary>
public sealed class GenericVersionComparer : IComparer<string>
{
    public static readonly GenericVersionComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        var (xCore, xPre) = Split(x);
        var (yCore, yPre) = Split(y);

        var length = Math.Max(xCore.Length, yCore.Length);
        for (var i = 0; i < length; i++)
        {
            var a = i < xCore.Length ? xCore[i] : "0";
            var b = i < yCore.Length ? yCore[i] : "0";
            var cmp = CompareSegment(a, b);
            if (cmp != 0) return cmp;
        }

        // Equal cores: no pre-release > pre-release.
        if (xPre.Length == 0 && yPre.Length == 0) return 0;
        if (xPre.Length == 0) return 1;
        if (yPre.Length == 0) return -1;
        return string.CompareOrdinal(xPre, yPre);
    }

    private static (string[] Core, string Pre) Split(string version)
    {
        var v = version.Trim().TrimStart('v', 'V');
        var buildIndex = v.IndexOf('+');
        if (buildIndex >= 0) v = v[..buildIndex];

        // The core is the leading run of dot-separated numeric segments.
        var end = 0;
        while (end < v.Length && (char.IsDigit(v[end]) || (v[end] == '.' && end + 1 < v.Length && char.IsDigit(v[end + 1]))))
            end++;

        var core = v[..end].Split('.', StringSplitOptions.RemoveEmptyEntries);
        var pre = v[end..].TrimStart('-', '.', '_');
        return (core.Length == 0 ? ["0"] : core, pre);
    }

    private static int CompareSegment(string a, string b)
    {
        if (long.TryParse(a, out var na) && long.TryParse(b, out var nb))
            return na.CompareTo(nb);
        return string.CompareOrdinal(a, b);
    }
}
