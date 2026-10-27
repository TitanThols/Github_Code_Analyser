using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OSSDependencyAnalyzer.Tests.TestSupport;

namespace OSSDependencyAnalyzer.Tests.Api;

public class AuthEndpointsTests(DepScanApiFactory factory) : IClassFixture<DepScanApiFactory>
{
    private const string Password = "correct-horse-battery";

    [Fact]
    public async Task Register_SignsInAndMeReturnsTheAccount()
    {
        var client = factory.CreateBrowserClient();
        var email = $"Ada.{Guid.NewGuid():N}@Example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register", new { displayName = "Ada", email, password = Password });

        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        Assert.Contains(register.Headers.GetValues("Set-Cookie"), c => c.StartsWith("depscan.session=") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));

        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("Ada", me.GetProperty("displayName").GetString());
        Assert.Equal(email.ToLowerInvariant(), me.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Register_DuplicateEmailIsConflict()
    {
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        await factory.CreateSignedInClientAsync(email);

        var response = await factory.CreateBrowserClient().PostAsJsonAsync("/api/auth/register",
            new { displayName = "Again", email = email.ToUpperInvariant(), password = Password });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("short", "Password")]
    [InlineData(null, "Password")]
    public async Task Register_ValidatesInput(string? password, string field)
    {
        var response = await factory.CreateBrowserClient().PostAsJsonAsync("/api/auth/register",
            new { displayName = "X Y", email = "valid@example.com", password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Login_SucceedsCaseInsensitivelyAndFailsUniformly()
    {
        var email = $"login-{Guid.NewGuid():N}@example.com";
        await factory.CreateSignedInClientAsync(email);

        var ok = await factory.CreateBrowserClient().PostAsJsonAsync("/api/auth/login", new { email = email.ToUpperInvariant(), password = Password });
        var wrongPassword = await factory.CreateBrowserClient().PostAsJsonAsync("/api/auth/login", new { email, password = "nope-nope-nope" });
        var unknownUser = await factory.CreateBrowserClient().PostAsJsonAsync("/api/auth/login", new { email = "nobody@example.com", password = Password });

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        // Same message either way, so the endpoint doesn't reveal which emails are registered.
        Assert.Equal(
            (await wrongPassword.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString(),
            (await unknownUser.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var client = await factory.CreateSignedInClientAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Me_WithoutSessionIs401NotARedirect()
    {
        var response = await factory.CreateBrowserClient().GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public class AuthRateLimitTests : IClassFixture<AuthRateLimitTests.StrictFactory>
{
    private readonly StrictFactory _factory;
    public AuthRateLimitTests(StrictFactory factory) => _factory = factory;

    public sealed class StrictFactory : DepScanApiFactory
    {
        protected override IDictionary<string, string?> Settings => new Dictionary<string, string?> { ["RateLimiting:AuthPerMinute"] = "3" };
    }

    [Fact]
    public async Task LoginAttemptsAreThrottled()
    {
        var client = _factory.CreateBrowserClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
            statuses.Add((await client.PostAsJsonAsync("/api/auth/login", new { email = "a@b.com", password = "x" })).StatusCode);

        Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.Unauthorized));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }
}
