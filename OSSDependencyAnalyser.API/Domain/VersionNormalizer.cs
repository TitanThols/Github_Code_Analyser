using System.Text.RegularExpressions;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Domain;

/// <summary>
/// Turns a manifest version constraint into a single concrete version suitable for advisory lookup.
/// Without a lockfile we can't know the installed version, so we use the constraint's lower bound —
/// the oldest version the manifest permits, i.e. the conservative choice for security purposes.
/// Returns null when no meaningful version can be derived (git URLs, "*", "latest", upper-bound-only
/// constraints, unresolved Maven properties, ...); those dependencies are listed but not matched.
/// </summary>
public static partial class VersionNormalizer
{
    private static readonly string[] UnpinnablePrefixes =
    [
        "git", "http:", "https:", "file:", "link:", "workspace:", "npm:", "github:", "portal:", "patch:", "catalog:"
    ];

    public static string? Normalize(string? constraint, DependencyType type)
    {
        if (string.IsNullOrWhiteSpace(constraint))
            return null;

        var value = constraint.Trim();

        if (value is "*" or "latest" or "next" or "x" or "X" or "unknown" or "Unknown"
            || value.Contains('$')
            || value.Contains("://", StringComparison.Ordinal)
            || UnpinnablePrefixes.Any(p => value.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return null;

        // npm "a || b": take the first alternative's lower bound.
        var orIndex = value.IndexOf("||", StringComparison.Ordinal);
        if (orIndex >= 0)
            value = value[..orIndex].Trim();

        // Comma-separated clauses (PEP 440 ">=2.0,<3", RubyGems "~> 5.0, >= 5.0.1"): use the first lower bound.
        if (!value.StartsWith('[') && !value.StartsWith('(') && value.Contains(','))
        {
            value = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(clause => !clause.StartsWith('<') && !clause.StartsWith("!=", StringComparison.Ordinal))
                ?? string.Empty;
            if (value.Length == 0)
                return null;
        }

        // An upper-bound or exclusion as the leading clause gives us no lower bound to work with.
        if (value.StartsWith('<') || value.StartsWith("!=", StringComparison.Ordinal))
            return null;

        // NuGet/Maven interval notation: "[1.2,2.0)" -> "1.2"; "(,2.0]" has no lower bound.
        if (value.StartsWith('[') || value.StartsWith('('))
        {
            value = value[1..].TrimStart();
            if (value.StartsWith(','))
                return null;
        }

        value = LeadingOperators().Replace(value, string.Empty);

        var match = VersionToken().Match(value);
        if (!match.Success)
            return null;

        var core = match.Groups["core"].Value;
        var suffix = match.Groups["suffix"].Value;

        // Wildcard components ("1.2.x", "1.*") mean "any", whose lower bound is 0.
        var components = core.Split('.')
            .Select(part => part is "x" or "X" or "*" ? "0" : part)
            .ToList();

        // A wildcard truncates the precision, so a pre-release tag after it is meaningless.
        if (core.Contains('x') || core.Contains('X') || core.Contains('*'))
            suffix = string.Empty;

        // SemVer ecosystems require MAJOR.MINOR.PATCH.
        if (type is DependencyType.NPM or DependencyType.Cargo)
        {
            while (components.Count < 3)
                components.Add("0");
        }

        return string.Join('.', components) + suffix;
    }

    [GeneratedRegex(@"^(?:[\^~=<>!v\s]|~>)+")]
    private static partial Regex LeadingOperators();

    [GeneratedRegex(@"^(?<core>\d+(?:\.(?:\d+|[xX*]))*)(?<suffix>(?:[-+.]?[A-Za-z][0-9A-Za-z.\-+]*)|(?:[-+][0-9A-Za-z.\-+]+))?")]
    private static partial Regex VersionToken();
}
