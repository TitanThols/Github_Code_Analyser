using System.Net;
using System.Net.Http.Json;

namespace OSSDependencyAnalyzer.API.Integrations.Osv;

public interface IOsvClient
{
    /// <summary>Returns, for each query (same order), the ids of matching advisories.</summary>
    Task<IReadOnlyList<IReadOnlyList<OsvVulnerabilityRef>>> QueryBatchAsync(IReadOnlyList<OsvQuery> queries, CancellationToken cancellationToken);

    Task<OsvVulnerability?> GetVulnerabilityAsync(string id, CancellationToken cancellationToken);
}

/// <summary>Typed client for https://api.osv.dev — free, keyless, and version-aware.</summary>
public sealed class OsvClient(HttpClient httpClient) : IOsvClient
{
    /// <summary>Documented maximum number of queries per /v1/querybatch request.</summary>
    public const int MaxBatchSize = 1000;

    public async Task<IReadOnlyList<IReadOnlyList<OsvVulnerabilityRef>>> QueryBatchAsync(
        IReadOnlyList<OsvQuery> queries,
        CancellationToken cancellationToken)
    {
        var results = new List<IReadOnlyList<OsvVulnerabilityRef>>(queries.Count);

        foreach (var chunk in queries.Chunk(MaxBatchSize))
        {
            using var response = await httpClient.PostAsJsonAsync("v1/querybatch", new OsvBatchRequest(chunk), cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<OsvBatchResponse>(cancellationToken)
                ?? throw new InvalidOperationException("OSV returned an empty batch response.");

            if (payload.Results.Count != chunk.Length)
                throw new InvalidOperationException($"OSV returned {payload.Results.Count} results for {chunk.Length} queries.");

            results.AddRange(payload.Results.Select(r => (IReadOnlyList<OsvVulnerabilityRef>)(r.Vulns ?? [])));
        }

        return results;
    }

    public async Task<OsvVulnerability?> GetVulnerabilityAsync(string id, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"v1/vulns/{Uri.EscapeDataString(id)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OsvVulnerability>(cancellationToken);
    }
}
