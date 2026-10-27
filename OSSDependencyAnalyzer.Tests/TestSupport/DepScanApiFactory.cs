using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OSSDependencyAnalyzer.API.Data;
using OSSDependencyAnalyzer.API.Services;

namespace OSSDependencyAnalyzer.Tests.TestSupport;

/// <summary>
/// Boots the real ASP.NET pipeline (auth, rate limiting, controllers, ProblemDetails) against an
/// in-memory database, with the background queue replaced by a recorder. No Docker required.
/// </summary>
public class DepScanApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"depscan-tests-{Guid.NewGuid()}";

    public RecordingAnalysisQueue Queue { get; } = new();

    /// <summary>Overrides applied on top of the defaults below.</summary>
    protected virtual IDictionary<string, string?> Settings => new Dictionary<string, string?>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=unused-in-tests",
            ["ConnectionStrings:Redis"] = "",
            ["Hangfire:Enabled"] = "false",
            ["RateLimiting:AuthPerMinute"] = "1000",
            ["RateLimiting:ScansPerWindow"] = "1000",
            ["Serilog:MinimumLevel:Default"] = "Warning"
        };
        foreach (var (key, value) in Settings)
            settings[key] = value;
        foreach (var (key, value) in settings)
            builder.UseSetting(key, value);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<AppDbContext>));
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IAnalysisQueue>();
            services.AddSingleton<IAnalysisQueue>(Queue);
            ConfigureServices(services);
        });
    }

    protected virtual void ConfigureServices(IServiceCollection services) { }

    /// <summary>A client that keeps cookies, pointed at https so Secure cookies round-trip.</summary>
    public HttpClient CreateBrowserClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    public async Task<HttpClient> CreateSignedInClientAsync(string? email = null)
    {
        var client = CreateBrowserClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Test User",
            email = email ?? $"user-{Guid.NewGuid():N}@example.com",
            password = "correct-horse-battery"
        });
        response.EnsureSuccessStatusCode();
        return client;
    }

    public T WithDb<T>(Func<AppDbContext, T> action)
    {
        using var scope = Services.CreateScope();
        return action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}

public sealed class RecordingAnalysisQueue : IAnalysisQueue
{
    private readonly List<Guid> _enqueued = [];
    public IReadOnlyList<Guid> Enqueued { get { lock (_enqueued) return _enqueued.ToList(); } }
    public void Enqueue(Guid repositoryId) { lock (_enqueued) _enqueued.Add(repositoryId); }
}
