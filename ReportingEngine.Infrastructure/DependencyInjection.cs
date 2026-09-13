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
        services.AddScoped<ISmtpConfigurationRepository, SmtpConfigurationRepository>();
        services.AddScoped<IJobFailureNotificationProfileRepository, JobFailureNotificationProfileRepository>();
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

                IF COL_LENGTH('RepScdhedularProject_JobExecution', 'ExecutionQuery') IS NULL
                BEGIN
                    ALTER TABLE RepScdhedularProject_JobExecution ADD ExecutionQuery nvarchar(max) NULL
                END

                IF COL_LENGTH('RepScdhedularProject_FileConfiguration', 'KeepLocalFiles') IS NULL
                BEGIN
                    ALTER TABLE RepScdhedularProject_FileConfiguration ADD KeepLocalFiles bit NOT NULL CONSTRAINT DF_RepScdhedularProject_FileConfiguration_KeepLocalFiles DEFAULT(1)
                END

                IF COL_LENGTH('RepScdhedularProject_DataSource', 'ConnectionString') IS NULL
                BEGIN
                    ALTER TABLE RepScdhedularProject_DataSource ADD ConnectionString nvarchar(max) NULL
                END

                IF COL_LENGTH('RepScdhedularProject_DeliveryConfiguration', 'IsFailureNotification') IS NULL
                BEGIN
                    ALTER TABLE RepScdhedularProject_DeliveryConfiguration ADD IsFailureNotification bit NOT NULL CONSTRAINT DF_RepScdhedularProject_DeliveryConfiguration_IsFailureNotification DEFAULT(0)
                END

                IF OBJECT_ID('RepScdhedularProject_SmtpConfiguration', 'U') IS NULL
                BEGIN
                    CREATE TABLE RepScdhedularProject_SmtpConfiguration (
                        SmtpConfigId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RepScdhedularProject_SmtpConfiguration PRIMARY KEY,
                        ProfileName nvarchar(200) NOT NULL,
                        Host nvarchar(300) NOT NULL,
                        Port int NOT NULL CONSTRAINT DF_RepScdhedularProject_SmtpConfiguration_Port DEFAULT(587),
                        EnableSsl bit NOT NULL CONSTRAINT DF_RepScdhedularProject_SmtpConfiguration_EnableSsl DEFAULT(1),
                        FromAddress nvarchar(320) NOT NULL,
                        FromDisplayName nvarchar(200) NOT NULL CONSTRAINT DF_RepScdhedularProject_SmtpConfiguration_FromDisplayName DEFAULT(''),
                        UserName nvarchar(320) NULL,
                        Password nvarchar(max) NULL,
                        TimeoutSeconds int NOT NULL CONSTRAINT DF_RepScdhedularProject_SmtpConfiguration_TimeoutSeconds DEFAULT(120),
                        UseFileDrop bit NOT NULL CONSTRAINT DF_RepScdhedularProject_SmtpConfiguration_UseFileDrop DEFAULT(0),
                        FileDropPath nvarchar(1000) NULL,
                        IsActive bit NOT NULL CONSTRAINT DF_RepScdhedularProject_SmtpConfiguration_IsActive DEFAULT(1),
                        CreatedBy nvarchar(100) NOT NULL,
                        CreatedDate datetime2 NOT NULL CONSTRAINT DF_RepScdhedularProject_SmtpConfiguration_CreatedDate DEFAULT(SYSUTCDATETIME()),
                        ModifiedBy nvarchar(100) NULL,
                        ModifiedDate datetime2 NULL
                    )
                    CREATE UNIQUE INDEX IX_RepScdhedularProject_SmtpConfiguration_ProfileName ON RepScdhedularProject_SmtpConfiguration(ProfileName)
                END

                IF COL_LENGTH('RepScdhedularProject_DeliveryConfiguration', 'SmtpConfigId') IS NULL
                BEGIN
                    ALTER TABLE RepScdhedularProject_DeliveryConfiguration ADD SmtpConfigId bigint NULL
                END

                IF OBJECT_ID('FK_RepScdhedularProject_DeliveryConfiguration_SmtpConfiguration_SmtpConfigId', 'F') IS NULL
                BEGIN
                    ALTER TABLE RepScdhedularProject_DeliveryConfiguration
                    ADD CONSTRAINT FK_RepScdhedularProject_DeliveryConfiguration_SmtpConfiguration_SmtpConfigId
                    FOREIGN KEY (SmtpConfigId) REFERENCES RepScdhedularProject_SmtpConfiguration(SmtpConfigId)
                END

                IF OBJECT_ID('RepScdhedularProject_JobFailureNotificationProfile', 'U') IS NULL
                BEGIN
                    CREATE TABLE RepScdhedularProject_JobFailureNotificationProfile (
                        FailureProfileId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RepScdhedularProject_JobFailureNotificationProfile PRIMARY KEY,
                        ProfileName nvarchar(200) NOT NULL,
                        SmtpConfigId bigint NOT NULL,
                        EmailTo nvarchar(2000) NOT NULL,
                        EmailCc nvarchar(2000) NULL,
                        EmailBcc nvarchar(2000) NULL,
                        SubjectTemplate nvarchar(1000) NULL,
                        BodyTemplate nvarchar(max) NULL,
                        IsActive bit NOT NULL CONSTRAINT DF_RepScdhedularProject_JobFailureNotificationProfile_IsActive DEFAULT(1),
                        CreatedBy nvarchar(100) NOT NULL,
                        CreatedDate datetime2 NOT NULL CONSTRAINT DF_RepScdhedularProject_JobFailureNotificationProfile_CreatedDate DEFAULT(SYSUTCDATETIME()),
                        ModifiedBy nvarchar(100) NULL,
                        ModifiedDate datetime2 NULL,
                        CONSTRAINT FK_RepScdhedularProject_JobFailureNotificationProfile_SmtpConfiguration_SmtpConfigId
                            FOREIGN KEY (SmtpConfigId) REFERENCES RepScdhedularProject_SmtpConfiguration(SmtpConfigId)
                    )
                    CREATE UNIQUE INDEX IX_RepScdhedularProject_JobFailureNotificationProfile_ProfileName ON RepScdhedularProject_JobFailureNotificationProfile(ProfileName)
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
