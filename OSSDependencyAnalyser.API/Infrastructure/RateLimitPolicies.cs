namespace OSSDependencyAnalyzer.API.Infrastructure;

public static class RateLimitPolicies
{
    /// <summary>Login/registration: 10 attempts per minute per client IP.</summary>
    public const string Auth = "auth";

    /// <summary>New scans and re-scans: 20 per 10 minutes per user — each scan fans out to GitHub and OSV.</summary>
    public const string Scan = "scan";
}
