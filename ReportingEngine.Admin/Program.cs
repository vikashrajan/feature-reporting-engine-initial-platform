using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using ReportingEngine.Application.DTOs;
using ReportingEngine.Application.Services;
using ReportingEngine.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

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
