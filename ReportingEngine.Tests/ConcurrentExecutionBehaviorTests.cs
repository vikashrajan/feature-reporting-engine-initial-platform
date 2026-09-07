using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReportingEngine.Application.Abstractions.Data;
using ReportingEngine.Application.Abstractions.Delivery;
using ReportingEngine.Application.Abstractions.Execution;
using ReportingEngine.Application.Abstractions.Files;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Application.Options;
using ReportingEngine.Domain.Entities;
using ReportingEngine.Domain.Enums;
using ReportingEngine.Infrastructure.Execution;
using Xunit;

namespace ReportingEngine.Tests;

public class ConcurrentExecutionBehaviorTests : IDisposable
{
    private readonly string _tempDirectory;

    public ConcurrentExecutionBehaviorTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ReportingEngineConcurrent_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConcurrentReports_ShouldExecuteInParallelWithoutStateInterference()
    {
        // Arrange
        var execOptions = Options.Create(new ExecutionOptions { TemporaryFilePath = _tempDirectory });
        var retryOptions = Options.Create(new RetryOptions { MaxRetryCount = 1 });
        var logger = new Mock<ILogger<JobExecutor>>().Object;

        var report1 = CreateReport(1, "REP_A");
        var report2 = CreateReport(2, "REP_B");

        var reportRepoMock = new Mock<IReportRepository>();
        reportRepoMock.Setup(r => r.GetByIdWithDetailsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(report1);
        reportRepoMock.Setup(r => r.GetByIdWithDetailsAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(report2);

        var executionRepoMock = new Mock<IJobExecutionRepository>();
        var exec1 = new JobExecution { ExecutionId = 101, ReportId = 1, Status = JobExecutionStatuses.Created };
        var exec2 = new JobExecution { ExecutionId = 102, ReportId = 2, Status = JobExecutionStatuses.Created };

        executionRepoMock.Setup(e => e.GetByIdempotencyKeyAsync("key1", It.IsAny<CancellationToken>())).ReturnsAsync(exec1);
        executionRepoMock.Setup(e => e.GetByIdempotencyKeyAsync("key2", It.IsAny<CancellationToken>())).ReturnsAsync(exec2);

        var idempotencyMock = new Mock<IIdempotencyService>();
        idempotencyMock.Setup(i => i.BuildKey(1, It.IsAny<DateTime?>(), false)).Returns("key1");
        idempotencyMock.Setup(i => i.BuildKey(2, It.IsAny<DateTime?>(), false)).Returns("key2");
        idempotencyMock.Setup(i => i.TryClaimAsync("key1", 1, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        idempotencyMock.Setup(i => i.TryClaimAsync("key2", 2, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var fileRepoMock = new Mock<IFileExecutionRepository>();
        fileRepoMock.Setup(f => f.GetByExecutionIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FileExecution>());
        var uowMock = new Mock<IUnitOfWork>();
        var paramResolverMock = new Mock<IParameterResolver>();
        paramResolverMock.Setup(p => p.ResolveAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object?>());

        var dsProviderMock = new Mock<IDataSourceProvider>();
        dsProviderMock.Setup(d => d.ExecuteQueryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToAsync(new[] { new Dictionary<string, object?> { ["Val"] = "Test" } }));

        var dsResolverMock = new Mock<IDataSourceProviderResolver>();
        dsResolverMock.Setup(d => d.Resolve("SQL")).Returns(dsProviderMock.Object);

        var fileSplitterMock = new Mock<IFileSplitter>();
        fileSplitterMock.Setup(s => s.SplitAsync(It.IsAny<IAsyncEnumerable<IReadOnlyDictionary<string, object?>>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<FileNameTokenContext>(), false, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IAsyncEnumerable<IReadOnlyDictionary<string, object?>> rows, string fmt, string dir, string pat, FileNameTokenContext ctx, bool s1, string? s2, long? s3, CancellationToken ct) =>
            {
                Directory.CreateDirectory(dir);
                var filePath = Path.Combine(dir, $"{ctx.ReportCode}_{ctx.ExecutionId}.csv");
                File.WriteAllText(filePath, "Val\nTest");
                return new[] { new GeneratedFileResult(filePath, 1, 8, "HASH123") };
            });

        var compressorMock = new Mock<IFileCompressor>();
        var deliveryProviderMock = new Mock<IDeliveryProvider>();
        var deliveryResolverMock = new Mock<IDeliveryProviderResolver>();
        deliveryResolverMock.Setup(d => d.Resolve("SHARED_FOLDER")).Returns(deliveryProviderMock.Object);

        var auditMock = new Mock<IAuditService>();

        var executor = new JobExecutor(
            reportRepoMock.Object,
            executionRepoMock.Object,
            fileRepoMock.Object,
            Mock.Of<IDeliveryConfigurationRepository>(),
            uowMock.Object,
            paramResolverMock.Object,
            dsResolverMock.Object,
            fileSplitterMock.Object,
            compressorMock.Object,
            deliveryResolverMock.Object,
            idempotencyMock.Object,
            auditMock.Object,
            execOptions,
            retryOptions,
            logger);

        // Act: Execute Report 1 and Report 2 concurrently
        var task1 = executor.ExecuteAsync(1, DateTime.UtcNow);
        var task2 = executor.ExecuteAsync(2, DateTime.UtcNow);

        await Task.WhenAll(task1, task2);

        // Assert
        exec1.Status.Should().Be(JobExecutionStatuses.Success);
        exec2.Status.Should().Be(JobExecutionStatuses.Success);
        exec1.RecordCount.Should().Be(1);
        exec2.RecordCount.Should().Be(1);
    }

    private static ReportDefinition CreateReport(long id, string code) => new()
    {
        ReportId = id,
        ReportCode = code,
        ReportName = $"Report {code}",
        QueryText = "SELECT 1",
        Customer = new Customer { CustomerId = 1, CustomerCode = "CUST1" },
        DataSource = new DataSource { DataSourceId = 1, DataSourceType = "SQL", ConnectionReference = "SqlConn" },
        FileConfiguration = new FileConfiguration { FileConfigId = 1, FileFormat = "CSV", FileNamePattern = "Pattern.csv", SplitEnabled = false },
        DeliveryConfiguration = new DeliveryConfiguration { DeliveryConfigId = 1, DeliveryType = "SHARED_FOLDER", DestinationReference = Path.GetTempPath() }
    };

    private static async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ToAsync(IEnumerable<IReadOnlyDictionary<string, object?>> rows)
    {
        foreach (var r in rows)
        {
            yield return r;
            await Task.Yield();
        }
    }
}
