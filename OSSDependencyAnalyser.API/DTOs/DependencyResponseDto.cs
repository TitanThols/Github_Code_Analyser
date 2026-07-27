namespace OSSDependencyAnalyzer.API.DTOs;

public class DependencyResponseDto
{
    public Guid Id { get; set; }
    public string PackageName { get; set; } = null!;
    public string CurrentVersion { get; set; } = null!;
    public string? LatestVersion { get; set; }
    public string Type { get; set; } = null!;
    public int VulnerabilityCount { get; set; }
    public List<VulnerabilityResponseDto> Vulnerabilities { get; set; } = [];
}