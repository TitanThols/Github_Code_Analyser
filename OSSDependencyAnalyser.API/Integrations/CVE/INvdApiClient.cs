using Refit;

namespace OSSDependencyAnalyzer.API.Integrations.CVE;

public interface INvdApiClient
{
    [Get("/")]
    Task<NvdCveResponseDto> GetCveAsync([Query("cveId")] string cveId);
}

public class NvdCveResponseDto
{
    public List<NvdCveDto> Vulnerabilities { get; set; } = new();
}