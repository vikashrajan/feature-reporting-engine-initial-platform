using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using ReportingEngine.Application.DTOs;
using ReportingEngine.Application.Services;
using ReportingEngine.Infrastructure;
using Serilog;
using Serilog.Events;

// Bootstrap Serilog early so startup errors are captured too
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Hangfire", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: @"C:\ReportingEngineOutput\logs\admin-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Host.UseSerilog();

builder.Services.AddInfrastructure(builder.Configuration, enableHangfireServer: false);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "ReportingEngine Admin API", Version = "v1" }));
builder.Services.AddRazorPages();
builder.Services.AddControllers(options => options.Filters.Add<ReportingEngine.Admin.Filters.ApiExceptionFilter>());

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseStaticFiles();
app.UseRouting();

var hangfireDashboard = builder.Configuration.GetValue("Hangfire:DashboardEnabled", true);
if (hangfireDashboard)
{
    app.UseHangfireDashboard(builder.Configuration.GetValue("Hangfire:DashboardPath", "/hangfire")!);
}

app.MapHealthChecks("/health");
app.MapControllers();
app.MapRazorPages();
app.MapGet("/", () => Results.Redirect("/Dashboard"));

try
{
    await app.Services.InitializeDatabaseAsync();
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Database initialization failed at startup. API will still start; fix connection string and restart.");
}

app.Run();

public partial class Program;
