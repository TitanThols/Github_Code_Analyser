namespace OSSDependencyAnalyzer.API.Integrations.API;

public class GithubRepositoryDto
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public string Full_Name { get; set; } = null!;
    public string Owner { get; set; } = null!;
    public string? Language { get; set; }
    public int Stargazers_Count { get; set; }
    public int Forks_Count { get; set; }
}

public class GithubFileContentDto
{
    public string Name { get; set; } = null!;
    public string Path { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string? Content { get; set; }
    public string? Download_Url { get; set; }   
}

public class GithubDirectoryContentDto
{
    public string Name { get; set; } = null!;
    public string Path { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Url { get; set; } = null!;
}

public class GithubErrorDto
{
    public string? Message { get; set; }
    public string? Documentation_Url { get; set; }
}