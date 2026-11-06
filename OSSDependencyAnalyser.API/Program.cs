using Hangfire;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using OSSDependencyAnalyzer.API.Data;
using OSSDependencyAnalyzer.API.Hubs;
using OSSDependencyAnalyzer.API.Infrastructure;
using OSSDependencyAnalyzer.API.Jobs;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services
    .AddPersistence(builder.Configuration, builder.Environment)
    .AddBackgroundJobs(builder.Configuration)
    .AddAnalysisPipeline(builder.Configuration)
    .AddWebSecurity(builder.Configuration, builder.Environment);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddSignalR();
builder.Services.AddOpenApi();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // PaaS load balancers terminate TLS and forward the original scheme/IP; their addresses aren't known up front.
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging(options =>
    options.GetLevel = (context, _, ex) =>
        ex is not null || context.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error
        : context.Request.Path.StartsWithSegments("/health") ? Serilog.Events.LogEventLevel.Verbose
        : Serilog.Events.LogEventLevel.Information);

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
if (app.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() is { Length: > 0 })
    app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHub<AlertHub>(AlertHub.Path);
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

if (app.Configuration.GetValue("ApiDocs:Enabled", true))
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("DepScan API"));
}

if (app.Configuration.GetValue("Hangfire:Enabled", true) && app.Environment.IsDevelopment())
    app.MapHangfireDashboard("/hangfire");

// Unknown API routes are 404s, not the SPA shell; everything else falls through to the React router.
app.Map("/api/{**path}", () => Results.NotFound());
if (File.Exists(Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "index.html")))
    app.MapFallbackToFile("index.html");

await InitializeAsync(app);
app.Run();

static async Task InitializeAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();

    if (app.Configuration.GetValue("Hangfire:Enabled", true))
    {
        var recurringJobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
        recurringJobs.RemoveIfExists("daily-vulnerability-snapshots"); // superseded by the daily re-scan
        recurringJobs.AddOrUpdate<DailyRescanJob>(DailyRescanJob.RecurringJobId, job => job.ExecuteAsync(), "0 3 * * *");
    }
}

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program;
