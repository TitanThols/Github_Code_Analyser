namespace OSSDependencyAnalyzer.API.Infrastructure;

/// <summary>
/// Hosting platforms (Render, Railway, Heroku, Fly) expose Postgres as a URI:
/// <c>postgres://user:pass@host:5432/db?sslmode=require</c>. Npgsql expects key/value pairs,
/// so translate when needed.
/// </summary>
public static class DatabaseUrl
{
    public static string? ToNpgsqlConnectionString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return value;

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        var parts = new List<string>
        {
            $"Host={uri.Host}",
            $"Port={(uri.Port > 0 ? uri.Port : 5432)}",
            $"Database={Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'))}",
            $"Username={Uri.UnescapeDataString(userInfo[0])}"
        };

        if (userInfo.Length > 1)
            parts.Add($"Password={Uri.UnescapeDataString(userInfo[1])}");

        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var sslMode = query["sslmode"];
        if (!string.IsNullOrWhiteSpace(sslMode))
            parts.Add($"SSL Mode={MapSslMode(sslMode)}");

        return string.Join(';', parts);
    }

    private static string MapSslMode(string mode) => mode.ToLowerInvariant() switch
    {
        "disable" => "Disable",
        "allow" => "Allow",
        "prefer" => "Prefer",
        "require" => "Require",
        "verify-ca" => "VerifyCA",
        "verify-full" => "VerifyFull",
        _ => "Prefer"
    };
}
