using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReportingEngine.Domain.Entities;
using ReportingEngine.Domain.Enums;
using ReportingEngine.Infrastructure.Persistence;

namespace ReportingEngine.Infrastructure;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var db = services.GetRequiredService<ReportingEngineDbContext>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        if (await db.Customers.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Seed skipped; customers already exist.");
            return;
        }

        var customer = new Customer
        {
            CustomerCode = "DEMO",
            CustomerName = "Demo Customer",
            TimeZoneId = "UTC",
            IsActive = true,
            CreatedBy = "seed",
            CreatedDate = DateTime.UtcNow
        };

        var dataSource = new DataSource
        {
            DataSourceName = "Demo SQL Source",
            DataSourceType = DataSourceTypes.Sql,
            ConnectionReference = "SampleSql",
            IsActive = true,
            CreatedBy = "seed",
            CreatedDate = DateTime.UtcNow
        };

        var schedule = new Schedule
        {
            ScheduleName = "Every Hour",
            ScheduleType = ScheduleTypes.Cron,
            CronExpression = "0 * * * *",
            TimeZoneId = "UTC",
            IsActive = true,
            CreatedBy = "seed",
            CreatedDate = DateTime.UtcNow
        };

        var fileConfig = new FileConfiguration
        {
            ConfigurationName = "Demo CSV",
            FileFormat = FileFormats.Csv,
            FileNamePattern = "Orders_{CustomerCode}_{yyyyMMdd}_{Sequence}.csv",
            SplitEnabled = false,
            CreatedBy = "seed",
            CreatedDate = DateTime.UtcNow
        };

        var delivery = new DeliveryConfiguration
        {
            DeliveryName = "Demo Email",
            DeliveryType = DeliveryTypes.Email,
            EmailTo = "reports@example.com",
            EmailSubjectTemplate = "Report {ReportCode} - {ExecutionDate}",
            EmailBodyTemplate = "<p>Customer {CustomerCode}: {RecordCount} records in {FileCount} file(s).</p>",
            SecretReference = """{"Host":"filedrop","Port":25,"UseFileDrop":true,"FileDropPath":"./temp-emails"}""",
            IsActive = true,
            CreatedBy = "seed",
            CreatedDate = DateTime.UtcNow
        };

        var sftpDelivery = new DeliveryConfiguration
        {
            DeliveryName = "Demo SFTP Storage",
            DeliveryType = DeliveryTypes.Sftp,
            DestinationReference = "./delivered-sftp/reports",
            SecretReference = "Host=localhost;Port=22;Username=sftpuser;Password=sftppass;UseFileDrop=true",
            IsActive = true,
            CreatedBy = "seed",
            CreatedDate = DateTime.UtcNow
        };

        db.Customers.Add(customer);
        db.DataSources.Add(dataSource);
        db.Schedules.Add(schedule);
        db.FileConfigurations.Add(fileConfig);
        db.DeliveryConfigurations.Add(delivery);
        db.DeliveryConfigurations.Add(sftpDelivery);
        await db.SaveChangesAsync(cancellationToken);

        var report = new ReportDefinition
        {
            CustomerId = customer.CustomerId,
            ReportCode = "DEMO_ORDERS",
            ReportName = "Demo Orders Report",
            DataSourceId = dataSource.DataSourceId,
            ScheduleId = schedule.ScheduleId,
            FileConfigId = fileConfig.FileConfigId,
            DeliveryConfigId = delivery.DeliveryConfigId,
            QueryText = """
                SELECT CAST(1 AS INT) AS OrderId, CAST('Sample' AS VARCHAR(50)) AS Product, SYSUTCDATETIME() AS ModifiedDate
                UNION ALL
                SELECT 2, 'Widget', SYSUTCDATETIME()
                """,
            Description = "Seeded demo report using inline sample rows (no external DB required beyond config DB).",
            Status = ReportStatuses.Draft,
            IsActive = true,
            CreatedBy = "seed",
            CreatedDate = DateTime.UtcNow,
            Parameters =
            {
                new ReportParameter
                {
                    ParameterName = "PreviousSuccessfulExecution",
                    ParameterType = ParameterTypes.DateTime,
                    ValueSource = ParameterValueSources.PreviousExecution
                },
                new ReportParameter
                {
                    ParameterName = "CurrentExecution",
                    ParameterType = ParameterTypes.DateTime,
                    ValueSource = ParameterValueSources.CurrentExecution
                }
            }
        };

        db.Reports.Add(report);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded demo customer, datasource, schedule, file/delivery configs, and DEMO_ORDERS report.");
    }
}
