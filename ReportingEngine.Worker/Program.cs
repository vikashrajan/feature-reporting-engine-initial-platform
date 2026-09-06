using ReportingEngine.Infrastructure;
using Serilog;
using Serilog.Events;

// Bootstrap Serilog early so startup errors are captured
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Hangfire", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: @"C:\ReportingEngineOutput\logs\worker-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Services.AddSerilog();

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
