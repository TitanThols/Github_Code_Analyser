namespace OSSDependencyAnalyser.API.DTOs;

public class SnapshotResponseDto
{
    public DateTime SnapshotDate { get; set; }
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int MediumCount { get; set; }
    public int LowCount { get; set; }
    public int TotalVulnerabilities { get; set; }
    public decimal AverageRiskScore { get; set; }
}
