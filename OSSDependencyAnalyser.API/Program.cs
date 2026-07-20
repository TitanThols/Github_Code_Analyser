using OSSDependencyAnalyzer.API.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.SignalR;
using Scalar.AspNetCore;
using Refit;
using OSSDependencyAnalyzer.API.Integrations.Github;
using OSSDependencyAnalyzer.API.Integrations.DependencyParsing;
using OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsers;
using OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsesrs;
using Hangfire;
using Hangfire.PostgreSql;
using OSSDependencyAnalyzer.API.Services;
using OSSDependencyAnalyzer.API.Integrations;
using OSSDependencyAnalyzer.API.Integrations.CVE;

public partial class Program
{
    [Obsolete]
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddTransient<IDependencyParser, NpmPackageJsonParser>();
        builder.Services.AddTransient<IDependencyParser, CsProjParser>();
        builder.Services.AddTransient<IDependencyParser, GemfileParser>();
        builder.Services.AddTransient<IDependencyParser, PomXmlParser>();
        builder.Services.AddTransient<IDependencyParser, PyprojectTomlParser>();
        builder.Services.AddTransient<IDependencyParser, PythonRequirementsParser>();
        builder.Services.AddTransient<IRiskScoringService, RiskScoringService>();
        builder.Services.AddTransient<IDependencyParserFactory, DependencyParserFactory>();
        builder.Services.AddTransient<ICveService, CveService>();
        builder.Services.AddTransient<IAnalyzerService, AnalyzerService>();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

        builder.Host.UseSerilog();

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApi();
        builder.Services.AddSignalR();

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString)
            .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
        );

        builder.Services.AddHangfire(config =>
            config.UsePostgreSqlStorage(connectionString)
        );
        builder.Services.AddHangfireServer();

        var githubToken = builder.Configuration["GithubApi:Token"];
        
        builder.Services.AddRefitClient<IGithubApiClient>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri("https://api.github.com");
                c.DefaultRequestHeaders.Add("Authorization", $"Bearer {githubToken}");
                c.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
                c.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3.raw");
                c.DefaultRequestHeaders.Add("User-Agent", "OSSDependencyAnalyzer");
            });

        builder.Services.AddRefitClient<IgithubAdvisoriesClient>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri("https://api.github.com");
                c.DefaultRequestHeaders.Add("Authorization", $"Bearer {githubToken}");
                c.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            });

        builder.Services.AddRefitClient<INvdApiClient>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri("https://services.nvd.nist.gov/rest/json/cves/2.0");
                c.DefaultRequestHeaders.Add("User-Agent", "OSSDependencyAnalyzer");
            });

        builder.Services.AddTransient<IGithubApiService, GithubApiService>();

        builder.Services.AddStackExchangeRedisCache(Options =>
        {
            Options.Configuration = builder.Configuration.GetConnectionString("Redis");
        });

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("ReactLocalhost", policy =>
            {
                policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
        });

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.UseCors("ReactLocalhost");
        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHub<AlertHub>("/hubs/alerts");
        app.MapHangfireDashboard();

        app.Run();
    }
}

public class AlertHub : Microsoft.AspNetCore.SignalR.Hub
{
    public async Task SendAlert(string message)
    {
        await Clients.All.SendAsync("ReceiveAlert", message);
    }
}