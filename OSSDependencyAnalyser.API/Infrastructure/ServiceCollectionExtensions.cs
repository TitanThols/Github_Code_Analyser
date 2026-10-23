using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OSSDependencyAnalyzer.API.Data;
using OSSDependencyAnalyzer.API.Integrations;
using OSSDependencyAnalyzer.API.Integrations.DependencyParsing;
using OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsers;
using OSSDependencyAnalyzer.API.Integrations.Github;
using OSSDependencyAnalyzer.API.Integrations.Kev;
using OSSDependencyAnalyzer.API.Integrations.Osv;
using OSSDependencyAnalyzer.API.Jobs;
using OSSDependencyAnalyzer.API.Models;
using OSSDependencyAnalyzer.API.Services;

namespace OSSDependencyAnalyzer.API.Infrastructure;

public static class ServiceCollectionExtensions
{
    private const string UserAgent = "DepScan/1.0 (+https://github.com)";

    public static string GetDatabaseConnectionString(this IConfiguration configuration) =>
        DatabaseUrl.ToNpgsqlConnectionString(
            configuration.GetConnectionString("DefaultConnection") is { Length: > 0 } configured
                ? configured
                : configuration["DATABASE_URL"])
        ?? throw new InvalidOperationException("Set ConnectionStrings__DefaultConnection or DATABASE_URL.");

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionString = configuration.GetDatabaseConnectionString();
        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3))
            .EnableSensitiveDataLogging(environment.IsDevelopment()));

        if (configuration.GetConnectionString("Redis") is { Length: > 0 } redis)
            services.AddStackExchangeRedisCache(options => options.Configuration = redis);
        else
            services.AddDistributedMemoryCache();

        services.AddMemoryCache();
        services.AddDataProtection()
            .SetApplicationName("DepScan")
            .PersistKeysToDbContext<AppDbContext>();

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);
        return services;
    }

    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAnalysisQueue, HangfireAnalysisQueue>();
        services.AddScoped<DailyRescanJob>();

        if (!configuration.GetValue("Hangfire:Enabled", true))
            return services;

        var connectionString = configuration.GetDatabaseConnectionString();
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));

        services.AddHangfireServer(options =>
        {
            // Each scan is I/O bound but fans out to OSV in parallel; keep a small pool on free-tier hosts.
            options.WorkerCount = configuration.GetValue("Hangfire:WorkerCount", 4);
        });
        return services;
    }

    public static IServiceCollection AddAnalysisPipeline(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);

        // Parsers are stateless; the factory discovers them through IEnumerable<IDependencyParser>.
        services.AddSingleton<IDependencyParser, NpmPackageJsonParser>();
        services.AddSingleton<IDependencyParser, PythonRequirementsParser>();
        services.AddSingleton<IDependencyParser, PyprojectTomlParser>();
        services.AddSingleton<IDependencyParser, CsProjParser>();
        services.AddSingleton<IDependencyParser, DirectoryPackagesPropsParser>();
        services.AddSingleton<IDependencyParser, PomXmlParser>();
        services.AddSingleton<IDependencyParser, GradleParser>();
        services.AddSingleton<IDependencyParser, GemfileParser>();
        services.AddSingleton<IDependencyParser, GoModParser>();
        services.AddSingleton<IDependencyParser, ComposerJsonParser>();
        services.AddSingleton<IDependencyParser, CargoTomlParser>();
        services.AddSingleton<IDependencyParserFactory, DependencyParserFactory>();

        services.AddSingleton<IRiskScoringService, RiskScoringService>();
        services.AddScoped<ISnapshotService, SnapshotService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IAnalyzerService, AnalyzerService>();
        services.AddScoped<IGithubApiService, GithubApiService>();
        services.AddScoped<IVulnerabilityService, OsvVulnerabilityService>();

        services.AddTransient<RateLimitRetryHandler>();

        var githubToken = configuration["GithubApi:Token"];
        services.AddHttpClient(GithubApiService.ApiClientName, client =>
            {
                client.BaseAddress = new Uri("https://api.github.com/");
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
                if (!string.IsNullOrWhiteSpace(githubToken))
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
            })
            .AddHttpMessageHandler<RateLimitRetryHandler>();

        services.AddHttpClient(GithubApiService.RawClientName, client =>
            {
                client.BaseAddress = new Uri("https://raw.githubusercontent.com/");
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            })
            .AddHttpMessageHandler<RateLimitRetryHandler>();

        services.AddHttpClient<IOsvClient, OsvClient>(client =>
            {
                client.BaseAddress = new Uri(configuration["Osv:BaseUrl"] ?? "https://api.osv.dev/");
                client.Timeout = TimeSpan.FromSeconds(60);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            })
            .AddHttpMessageHandler<RateLimitRetryHandler>();

        services.AddHttpClient<IKnownExploitedCatalog, KnownExploitedCatalog>(client =>
        {
            client.BaseAddress = new Uri(configuration["Kev:FeedUrl"]
                ?? "https://www.cisa.gov/sites/default/files/feeds/known_exploited_vulnerabilities.json");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        });

        return services;
    }

    public static IServiceCollection AddWebSecurity(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddScoped<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();

        // Same-origin deployments (the default single container) use SameSite=Lax. Set Auth:CrossSiteCookies
        // when the SPA is hosted on a different site than the API (e.g. Vercel + Render).
        var crossSite = configuration.GetValue("Auth:CrossSiteCookies", false);
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "depscan.session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = crossSite ? SameSiteMode.None : SameSiteMode.Lax;
                options.Cookie.SecurePolicy = crossSite || !environment.IsDevelopment()
                    ? CookieSecurePolicy.Always
                    : CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.SlidingExpiration = true;
                // APIs answer 401/403 instead of redirecting to a login page.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });
        services.AddAuthorization();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                await Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Too many requests. Please slow down.")
                    .ExecuteAsync(context.HttpContext);
            };

            options.AddPolicy(RateLimitPolicies.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = configuration.GetValue("RateLimiting:AuthPerMinute", 10),
                    Window = TimeSpan.FromMinutes(1)
                }));

            options.AddPolicy(RateLimitPolicies.Scan, context => RateLimitPartition.GetSlidingWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = configuration.GetValue("RateLimiting:ScansPerWindow", 20),
                    Window = TimeSpan.FromMinutes(10),
                    SegmentsPerWindow = 5
                }));
        });

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length > 0)
        {
            services.AddCors(options => options.AddDefaultPolicy(policy => policy
                .WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));
        }

        return services;
    }
}
