using System.Text.Json.Serialization;

namespace OSSDependencyAnalyzer.API.Integrations.Osv;

// Subset of the OSV schema (https://ossf.github.io/osv-schema/) that the analyzer uses.

public sealed record OsvPackage(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("ecosystem")] string Ecosystem);

public sealed record OsvQuery(
    [property: JsonPropertyName("package")] OsvPackage Package,
    [property: JsonPropertyName("version")] string Version);

public sealed record OsvBatchRequest([property: JsonPropertyName("queries")] IReadOnlyList<OsvQuery> Queries);

public sealed class OsvBatchResponse
{
    [JsonPropertyName("results")] public List<OsvBatchResult> Results { get; set; } = [];
}

public sealed class OsvBatchResult
{
    [JsonPropertyName("vulns")] public List<OsvVulnerabilityRef>? Vulns { get; set; }
}

public sealed class OsvVulnerabilityRef
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("modified")] public string? Modified { get; set; }
}

public sealed class OsvVulnerability
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("summary")] public string? Summary { get; set; }
    [JsonPropertyName("details")] public string? Details { get; set; }
    [JsonPropertyName("aliases")] public List<string>? Aliases { get; set; }
    [JsonPropertyName("published")] public DateTimeOffset? Published { get; set; }
    [JsonPropertyName("modified")] public DateTimeOffset? Modified { get; set; }
    [JsonPropertyName("severity")] public List<OsvSeverity>? Severity { get; set; }
    [JsonPropertyName("affected")] public List<OsvAffected>? Affected { get; set; }
    [JsonPropertyName("database_specific")] public OsvDatabaseSpecific? DatabaseSpecific { get; set; }
}

public sealed class OsvSeverity
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("score")] public string Score { get; set; } = string.Empty;
}

public sealed class OsvAffected
{
    [JsonPropertyName("package")] public OsvPackage? Package { get; set; }
    [JsonPropertyName("ranges")] public List<OsvRange>? Ranges { get; set; }
    [JsonPropertyName("versions")] public List<string>? Versions { get; set; }
}

public sealed class OsvRange
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("events")] public List<OsvEvent> Events { get; set; } = [];
}

public sealed class OsvEvent
{
    [JsonPropertyName("introduced")] public string? Introduced { get; set; }
    [JsonPropertyName("fixed")] public string? Fixed { get; set; }
    [JsonPropertyName("last_affected")] public string? LastAffected { get; set; }
}

public sealed class OsvDatabaseSpecific
{
    [JsonPropertyName("severity")] public string? Severity { get; set; }
}
