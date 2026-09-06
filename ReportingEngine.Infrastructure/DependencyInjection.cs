using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ReportingEngine.Application;
using ReportingEngine.Application.Abstractions.Data;
using ReportingEngine.Application.Abstractions.Delivery;
using ReportingEngine.Application.Abstractions.Execution;
using ReportingEngine.Application.Abstractions.Files;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Application.Options;
using ReportingEngine.Domain.Enums;
using ReportingEngine.Infrastructure.Data;
using ReportingEngine.Infrastructure.Delivery;
using ReportingEngine.Infrastructure.Execution;
using ReportingEngine.Infrastructure.Files;
using ReportingEngine.Infrastructure.HangfireJobs;
using ReportingEngine.Infrastructure.Persistence;
using ReportingEngine.Infrastructure.Persistence.Repositories;
using ReportingEngine.Infrastructure.Services;

namespace ReportingEngine.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool enableHangfireServer)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<HangfireOptions>(configuration.GetSection(HangfireOptions.SectionName));
        services.Configure<RetryOptions>(configuration.GetSection(RetryOptions.SectionName));
        services.Configure<ExecutionOptions>(configuration.GetSection(ExecutionOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<ConnectionReferencesOptions>(configuration.GetSection(ConnectionReferencesOptions.SectionName));
        services.Configure<SecretReferencesOptions>(configuration.GetSection(SecretReferencesOptions.SectionName));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        var connectionString = configuration.GetSection(DatabaseOptions.SectionName)["ConnectionString"]
            ?? configuration.GetConnectionString("ReportingEngine")
            ?? throw new InvalidOperationException("Database:ConnectionString is required.");

        services.AddDbContext<ReportingEngineDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IDataSourceRepository, DataSourceRepository>();
        services.AddScoped<IScheduleRepository, ScheduleRepository>();
        services.AddScoped<IFileConfigurationRepository, FileConfigurationRepository>();
        services.AddScoped<IDeliveryConfigurationRepository, DeliveryConfigurationRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IJobExecutionRepository, JobExecutionRepository>();
        services.AddScoped<IFileExecutionRepository, FileExecutionRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAuditService, AuditService>();

        services.AddSingleton<IConnectionStringResolver, ConnectionStringResolver>();
        services.AddSingleton<ISecretResolver, SecretResolver>();
        services.AddScoped<IDataSourceProvider, SqlDataSourceProvider>();
        services.AddScoped<IDataSourceProvider, CosmosDataSourceProvider>();
        services.AddScoped<IDataSourceProviderResolver, DataSourceProviderResolver>();

        services.AddSingleton<IFileGenerator, CsvFileGenerator>();
        services.AddSingleton<IFileGenerator, JsonFileGenerator>();
        services.AddSingleton<IFileGenerator, TxtFileGenerator>();
        services.AddSingleton<IFileGenerator, ExcelFileGenerator>();
        services.AddSingleton<IFileGenerator, XmlFileGenerator>();
        services.AddSingleton<IFileGeneratorResolver, FileGeneratorResolver>();
        services.AddSingleton<IFileNameTokenReplacer, FileNameTokenReplacer>();
        services.AddSingleton<IFileCompressor, FileCompressor>();
        services.AddScoped<IFileSplitter, FileSplitter>();

        services.AddScoped<IDeliveryProvider, EmailDeliveryProvider>();
        services.AddScoped<IDeliveryProvider, SharedFolderDeliveryProvider>();
        services.AddScoped<IDeliveryProvider, SftpDeliveryProvider>();
        services.AddScoped<IDeliveryProvider, FtpDeliveryProvider>();
        services.AddScoped<IDeliveryProvider, BlobDeliveryProvider>();
        services.AddScoped<IDeliveryProvider, AzureFileShareDeliveryProvider>();
        services.AddScoped<IDeliveryProvider, S3DeliveryProvider>();
        services.AddScoped<IDeliveryProviderResolver, DeliveryProviderResolver>();

        services.AddScoped<IParameterResolver, ParameterResolver>();
        services.AddScoped<IReportValidator, ReportValidator>();
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        services.AddScoped<IJobExecutor, JobExecutor>();
        services.AddScoped<ReportRecurringJob>();

        services.AddHangfireInfrastructure(connectionString, configuration, enableHangfireServer);
        services.AddApplication();

        services.AddHealthChecks()
            .AddSqlServer(connectionString, name: "sqlserver")
            .AddCheck<HangfireHealthCheck>("hangfire");

        return services;
    }

    private static void AddHangfireInfrastructure(this IServiceCollection services, string connectionString, IConfiguration configuration, bool enableHangfireServer)
    {
        var hangfireOptions = configuration.GetSection(HangfireOptions.SectionName).Get<HangfireOptions>() ?? new HangfireOptions();

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                SchemaName = hangfireOptions.SchemaName,
                PrepareSchemaIfNecessary = true,
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.FromSeconds(1),
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true
            }));

        services.AddSingleton<IReportJobDispatcher, HangfireReportJobDispatcher>();
        services.AddScoped<IScheduleSyncService, HangfireScheduleSyncService>();

        // Admin registers storage + clients only; Worker hosts the Hangfire server and executes jobs.
        if (enableHangfireServer)
        {
            services.AddHangfireServer(options =>
            {
                options.WorkerCount = Math.Max(1, hangfireOptions.WorkerCount);
                options.Queues = new[] { "default" };
                options.ServerName = $"{Environment.MachineName}:ReportingEngine.Worker";
            });
        }
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        // Pre-create output directories so they always exist at fixed known locations
        Directory.CreateDirectory(@"C:\ReportingEngineOutput\temp-reports");
        Directory.CreateDirectory(@"C:\ReportingEngineOutput\temp-emails");

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportingEngineDbContext>();
        var databaseCreator = db.Database.GetService<IRelationalDatabaseCreator>();
        if (!await databaseCreator.ExistsAsync(cancellationToken))
        {
            await databaseCreator.CreateAsync(cancellationToken);
        }

        var tableExists = false;
        try
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync(cancellationToken);
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE name = 'RepScdhedularProject_Customer'";
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
            tableExists = count > 0;
        }
        catch
        {
            tableExists = false;
        }

        if (!tableExists)
        {
            var script = db.Database.GenerateCreateScript();
            var batches = script.Split(new[] { "\nGO\n", "\r\nGO\r\n", "GO\n", "GO\r\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var batch in batches)
            {
                if (!string.IsNullOrWhiteSpace(batch))
                {
                    await db.Database.ExecuteSqlRawAsync(batch, cancellationToken);
                }
            }
        }
        else
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF COL_LENGTH('RepScdhedularProject_FileConfiguration', 'ZipBatchSize') IS NULL
                BEGIN
                    ALTER TABLE RepScdhedularProject_FileConfiguration ADD ZipBatchSize int NULL
                END

                IF COL_LENGTH('RepScdhedularProject_FileConfiguration', 'KeepLocalFiles') IS NULL
                BEGIN
                    ALTER TABLE RepScdhedularProject_FileConfiguration ADD KeepLocalFiles bit NOT NULL CONSTRAINT DF_RepScdhedularProject_FileConfiguration_KeepLocalFiles DEFAULT(1)
                END
                """, cancellationToken);
        }

        var seedOptions = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;
        if (seedOptions.Enabled)
        {
            await DatabaseSeeder.SeedAsync(scope.ServiceProvider, cancellationToken);
        }

        var scheduleSync = scope.ServiceProvider.GetRequiredService<IScheduleSyncService>();
        await scheduleSync.SyncAllActiveSchedulesAsync(cancellationToken);
    }
}

public sealed class HangfireHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    public Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var monitoring = JobStorage.Current.GetMonitoringApi();
            var stats = monitoring.GetStatistics();
            return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                $"Hangfire OK. Servers={stats.Servers}, Enqueued={stats.Enqueued}, Succeeded={stats.Succeeded}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Hangfire unavailable", ex));
        }
    }
}
