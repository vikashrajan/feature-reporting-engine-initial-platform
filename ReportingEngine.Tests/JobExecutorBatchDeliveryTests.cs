using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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

public class JobExecutorBatchDeliveryTests : IDisposable
{
    private readonly string _tempDirectory;

    public JobExecutorBatchDeliveryTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ReportingEngineBatchTests_" + Guid.NewGuid().ToString("N"));
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
    public async Task ExecuteByExecutionIdAsync_WhenZipBatchSizeIsConfigured_ShouldDeliverOneZipPerBatch()
    {
        var report = CreateReport(zipBatchSize: 10);
        var execution = new JobExecution { ExecutionId = 99, ReportId = report.ReportId, Status = JobExecutionStatuses.Created };
        var fileExecutions = new List<FileExecution>();
        var generatedFiles = CreateGeneratedFiles(23);
        var deliveredRequests = new List<DeliveryRequest>();

        var reportRepo = new Mock<IReportRepository>();
        reportRepo.Setup(r => r.GetByIdWithDetailsAsync(report.ReportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var executionRepo = new Mock<IJobExecutionRepository>();
        executionRepo.Setup(r => r.GetByIdWithFilesAsync(execution.ExecutionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(execution);

        var fileRepo = new Mock<IFileExecutionRepository>();
        fileRepo.Setup(r => r.AddAsync(It.IsAny<FileExecution>(), It.IsAny<CancellationToken>()))
            .Callback<FileExecution, CancellationToken>((file, _) => fileExecutions.Add(file))
            .ReturnsAsync((FileExecution file, CancellationToken _) => file);
        fileRepo.Setup(r => r.GetByExecutionIdAsync(execution.ExecutionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => fileExecutions.ToList());

        var dataProvider = new Mock<IDataSourceProvider>();
        dataProvider.Setup(p => p.ExecuteQueryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyRows());

        var dataSourceResolver = new Mock<IDataSourceProviderResolver>();
        dataSourceResolver.Setup(r => r.Resolve(DataSourceTypes.Sql)).Returns(dataProvider.Object);

        var splitter = new Mock<IFileSplitter>();
        splitter.Setup(s => s.SplitAsync(
                It.IsAny<IAsyncEnumerable<IReadOnlyDictionary<string, object?>>>(),
                FileFormats.Csv,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<FileNameTokenContext>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(generatedFiles);

        var compressor = new Mock<IFileCompressor>();
        compressor.Setup(c => c.CompressMultipleAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> _, string zipDestinationPath, CancellationToken _) => zipDestinationPath);

        var deliveryProvider = new Mock<IDeliveryProvider>();
        deliveryProvider.SetupGet(p => p.DeliveryType).Returns(DeliveryTypes.Email);
        deliveryProvider.Setup(p => p.DeliverAsync(It.IsAny<DeliveryRequest>(), It.IsAny<CancellationToken>()))
            .Callback<DeliveryRequest, CancellationToken>((request, _) => deliveredRequests.Add(request))
            .Returns(Task.CompletedTask);

        var deliveryResolver = new Mock<IDeliveryProviderResolver>();
        deliveryResolver.Setup(r => r.Resolve(DeliveryTypes.Email)).Returns(deliveryProvider.Object);

        var parameterResolver = new Mock<IParameterResolver>();
        parameterResolver.Setup(r => r.ResolveAsync(report.ReportId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object?>());

        var sut = new JobExecutor(
            reportRepo.Object,
            executionRepo.Object,
            fileRepo.Object,
            Mock.Of<IJobFailureNotificationProfileRepository>(),
            Mock.Of<IUnitOfWork>(),
            parameterResolver.Object,
            dataSourceResolver.Object,
            splitter.Object,
            compressor.Object,
            deliveryResolver.Object,
            Mock.Of<IIdempotencyService>(),
            Mock.Of<IAuditService>(),
            Options.Create(new ExecutionOptions { TemporaryFilePath = _tempDirectory }),
            Options.Create(new RetryOptions { MaxRetryCount = 3 }),
            NullLogger<JobExecutor>.Instance);

        await sut.ExecuteByExecutionIdAsync(execution.ExecutionId);

        compressor.Verify(c => c.CompressMultipleAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        deliveredRequests.Should().HaveCount(3);
        deliveredRequests.Select(r => Path.GetFileName(r.AttachmentPaths.Single()))
            .Should().ContainInOrder(
                "BATCH_DEMO_CUST_99_" + deliveredRequests[0].Tokens.ExecutionDateUtc.ToString("yyyyMMddHHmmss") + "_batch001.zip",
                "BATCH_DEMO_CUST_99_" + deliveredRequests[1].Tokens.ExecutionDateUtc.ToString("yyyyMMddHHmmss") + "_batch002.zip",
                "BATCH_DEMO_CUST_99_" + deliveredRequests[2].Tokens.ExecutionDateUtc.ToString("yyyyMMddHHmmss") + "_batch003.zip");
        deliveredRequests.Select(r => r.Tokens.FileCount).Should().Equal(10, 10, 3);
        fileExecutions.Should().OnlyContain(f => f.DeliveryStatus == FileDeliveryStatuses.Delivered);
        execution.Status.Should().Be(JobExecutionStatuses.Success);
        execution.FileCount.Should().Be(23);
    }

    [Fact]
    public async Task ExecuteByExecutionIdAsync_WhenKeepLocalFilesIsFalse_ShouldDeleteExecutionOutputDirectory()
    {
        var report = CreateReport(zipBatchSize: 0);
        report.FileConfiguration.CompressionType = null;
        report.FileConfiguration.KeepLocalFiles = false;
        var execution = new JobExecution { ExecutionId = 100, ReportId = report.ReportId, Status = JobExecutionStatuses.Created };
        var fileExecutions = new List<FileExecution>();

        var reportRepo = new Mock<IReportRepository>();
        reportRepo.Setup(r => r.GetByIdWithDetailsAsync(report.ReportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var executionRepo = new Mock<IJobExecutionRepository>();
        executionRepo.Setup(r => r.GetByIdWithFilesAsync(execution.ExecutionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(execution);

        var fileRepo = new Mock<IFileExecutionRepository>();
        fileRepo.Setup(r => r.AddAsync(It.IsAny<FileExecution>(), It.IsAny<CancellationToken>()))
            .Callback<FileExecution, CancellationToken>((file, _) => fileExecutions.Add(file))
            .ReturnsAsync((FileExecution file, CancellationToken _) => file);
        fileRepo.Setup(r => r.GetByExecutionIdAsync(execution.ExecutionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => fileExecutions.ToList());

        var dataProvider = new Mock<IDataSourceProvider>();
        dataProvider.Setup(p => p.ExecuteQueryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyRows());

        var dataSourceResolver = new Mock<IDataSourceProviderResolver>();
        dataSourceResolver.Setup(r => r.Resolve(DataSourceTypes.Sql)).Returns(dataProvider.Object);

        var outputDirectory = Path.Combine(_tempDirectory, report.ReportCode, execution.ExecutionId.ToString());
        var splitter = new Mock<IFileSplitter>();
        splitter.Setup(s => s.SplitAsync(
                It.IsAny<IAsyncEnumerable<IReadOnlyDictionary<string, object?>>>(),
                FileFormats.Csv,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<FileNameTokenContext>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IAsyncEnumerable<IReadOnlyDictionary<string, object?>> _, string _, string dir, string _, FileNameTokenContext _, bool _, string? _, long? _, CancellationToken _) =>
            {
                Directory.CreateDirectory(dir);
                var filePath = Path.Combine(dir, "Report_001.csv");
                File.WriteAllText(filePath, "Id,Name\r\n1,Item");
                File.WriteAllText(Path.Combine(dir, "extra-leftover.tmp"), "left behind by generator");
                return new[] { new GeneratedFileResult(filePath, 1, new FileInfo(filePath).Length, "checksum") };
            });

        var deliveryProvider = new Mock<IDeliveryProvider>();
        deliveryProvider.SetupGet(p => p.DeliveryType).Returns(DeliveryTypes.Email);
        deliveryProvider.Setup(p => p.DeliverAsync(It.IsAny<DeliveryRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var deliveryResolver = new Mock<IDeliveryProviderResolver>();
        deliveryResolver.Setup(r => r.Resolve(DeliveryTypes.Email)).Returns(deliveryProvider.Object);

        var parameterResolver = new Mock<IParameterResolver>();
        parameterResolver.Setup(r => r.ResolveAsync(report.ReportId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object?>());

        var sut = new JobExecutor(
            reportRepo.Object,
            executionRepo.Object,
            fileRepo.Object,
            Mock.Of<IJobFailureNotificationProfileRepository>(),
            Mock.Of<IUnitOfWork>(),
            parameterResolver.Object,
            dataSourceResolver.Object,
            splitter.Object,
            Mock.Of<IFileCompressor>(),
            deliveryResolver.Object,
            Mock.Of<IIdempotencyService>(),
            Mock.Of<IAuditService>(),
            Options.Create(new ExecutionOptions { TemporaryFilePath = _tempDirectory }),
            Options.Create(new RetryOptions { MaxRetryCount = 3 }),
            NullLogger<JobExecutor>.Instance);

        await sut.ExecuteByExecutionIdAsync(execution.ExecutionId);

        Directory.Exists(outputDirectory).Should().BeFalse();
        fileExecutions.Should().OnlyContain(f => f.DeliveryStatus == FileDeliveryStatuses.Delivered);
        fileExecutions.Count(f => f.FilePath != null).Should().Be(0);
    }

    private IReadOnlyList<GeneratedFileResult> CreateGeneratedFiles(int count)
    {
        var files = new List<GeneratedFileResult>();
        for (var i = 1; i <= count; i++)
        {
            var path = Path.Combine(_tempDirectory, $"Report_{i:000}.csv");
            File.WriteAllText(path, $"Id,Name{Environment.NewLine}{i},Item_{i}");
            files.Add(new GeneratedFileResult(path, 1, new FileInfo(path).Length, $"checksum-{i}"));
        }

        return files;
    }

    private static ReportDefinition CreateReport(int zipBatchSize) => new()
    {
        ReportId = 1,
        CustomerId = 1,
        ReportCode = "BATCH_DEMO",
        ReportName = "Batch Demo",
        QueryText = "SELECT 1",
        Status = ReportStatuses.Active,
        Customer = new Customer { CustomerId = 1, CustomerCode = "CUST", CustomerName = "Customer" },
        DataSource = new DataSource { DataSourceId = 1, DataSourceType = DataSourceTypes.Sql, ConnectionReference = "Default" },
        FileConfiguration = new FileConfiguration
        {
            FileConfigId = 1,
            FileFormat = FileFormats.Csv,
            FileNamePattern = "Report_{Sequence}.csv",
            CompressionType = CompressionTypes.Zip,
            ZipBatchSize = zipBatchSize
        },
        DeliveryConfiguration = new DeliveryConfiguration
        {
            DeliveryConfigId = 1,
            DeliveryType = DeliveryTypes.Email,
            EmailTo = "reports@example.com",
            SmtpConfigId = 1,
            SmtpConfiguration = new SmtpConfiguration
            {
                SmtpConfigId = 1,
                ProfileName = "Test SMTP",
                Host = "filedrop",
                Port = 25,
                FromAddress = "noreply@test.local",
                UseFileDrop = true,
                FileDropPath = Path.GetTempPath(),
                IsActive = true
            }
        }
    };

    private static async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> EmptyRows()
    {
        await Task.CompletedTask;
        yield break;
    }
}
