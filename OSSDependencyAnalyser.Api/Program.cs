using OSSDependencyAnalyzer.API.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.SignalR;
using Scalar.AspNetCore;

public partial class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

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