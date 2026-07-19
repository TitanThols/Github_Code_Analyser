using Refit;

namespace OSSDependencyAnalyzer.API.Integrations.CVE;
public interface IgithubAdvisoriesClient
{
    [Post("/graphql")]
    Task<dynamic> QueryAdvisoriesAsync([Body] GithubGraphQLRequestDto query);
}

public class GithubGraphQLRequestDto
{
    public string Query { get; set; } = null!;
    public Dictionary<string, object>? Variables { get; set; }
}