namespace OSSDependencyAnalyser.API.DTOs;

public class DashboardStatsDto
{
    public int TotalRepositories { get; set; }
    public int TotalDependencies { get; set; }
    public int TotalCritical { get; set; }
    public int TotalHigh { get; set; }
    public int TotalMedium { get; set; }
    public int TotalLow { get; set; }
    public double AverageRiskScore { get; set; }
}
