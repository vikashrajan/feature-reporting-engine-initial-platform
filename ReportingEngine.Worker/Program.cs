using ReportingEngine.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "ReportingEngine.Worker");
builder.Services.AddInfrastructure(builder.Configuration, enableHangfireServer: true);

var host = builder.Build();

try
{
    await host.Services.InitializeDatabaseAsync();
}
catch (Exception ex)
{
    var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    logger.LogWarning(ex, "Database initialization failed at Worker startup. Fix connection string and restart.");
}

await host.RunAsync();
