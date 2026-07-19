namespace OSSDependencyAnalyzer.API.Integrations.CVE;

public class GithubAdvisoryDto
{
    public string Ghsa_Id { get; set; } = null!;
    public string Cve_Id { get; set; } = null!;
    public string Summary { get; set; } = null!;
    public string  Description { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public double? Cvss_Score { get; set; }
    public string? Cvss_Vector_String { get; set; }
    public string? Published_At { get; set; }
    public string? Updated_At { get; set; }
}

public class NvdCveDto
{
    public string Id { get; set; } = null!;
    public MetricsDto Metrics { get; set; } = null!;
    public string? Description { get; set; }
    public string? Published { get; set; }
}

public class MetricsDto
{
    public CvssV3DataDto? Cvss_V3_1 { get; set; }
    public CvssV2DataDto? Cvss_V2_0 { get; set; }
}

public class CvssV3DataDto
{
    public double Base_Score { get; set; }
    public string Vector_String { get; set; } = null!;
}

public class CvssV2DataDto
{
    public double Base_Score { get; set; }
    public string Vector_String { get; set; } = null!;
}

public class SnykVulnerabilityDto
{
    public string Id { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public double? Cvss_Score { get; set; }
    public string? Cve { get; set; }
    public bool Is_Exploited { get; set; }
}
