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

public class RetryBehaviorAndStateTransitionTests
{
    private readonly Mock<IReportRepository> _reportRepoMock = new();
    private readonly Mock<IJobExecutionRepository> _executionRepoMock = new();
    private readonly Mock<IFileExecutionRepository> _fileRepoMock = new();
    private readonly Mock<IDeliveryConfigurationRepository> _deliveryConfigRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IParameterResolver> _paramResolverMock = new();
    private readonly Mock<IDataSourceProviderResolver> _dsResolverMock = new();
    private readonly Mock<IFileSplitter> _splitterMock = new();
    private readonly Mock<IFileCompressor> _compressorMock = new();
    private readonly Mock<IDeliveryProviderResolver> _deliveryResolverMock = new();
    private readonly Mock<IIdempotencyService> _idempotencyMock = new();
    private readonly Mock<IAuditService> _auditMock = new();
    private readonly IOptions<ExecutionOptions> _execOptions = Options.Create(new ExecutionOptions { TemporaryFilePath = Path.GetTempPath() });
    private readonly IOptions<RetryOptions> _retryOptions = Options.Create(new RetryOptions { MaxRetryCount = 3, InitialDelaySeconds = 1 });
    private readonly ILogger<JobExecutor> _logger = new Mock<ILogger<JobExecutor>>().Object;

    private JobExecutor CreateSut() => new(
        _reportRepoMock.Object,
        _executionRepoMock.Object,
        _fileRepoMock.Object,
        _deliveryConfigRepoMock.Object,
        _uowMock.Object,
        _paramResolverMock.Object,
        _dsResolverMock.Object,
        _splitterMock.Object,
        _compressorMock.Object,
        _deliveryResolverMock.Object,
        _idempotencyMock.Object,
        _auditMock.Object,
        _execOptions,
        _retryOptions,
        _logger);

    [Fact]
    public async Task ExecuteByExecutionIdAsync_WhenPermanentErrorOccurs_ShouldMarkFailedWithoutThrowing()
    {
        // Arrange
        var sut = CreateSut();
        var execution = new JobExecution { ExecutionId = 10, ReportId = 1, Status = JobExecutionStatuses.Created, RetryCount = 0 };

        _executionRepoMock.Setup(e => e.GetByIdWithFilesAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(execution);

        _reportRepoMock.Setup(r => r.GetByIdWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Permanent config error"));

        // Act
        await sut.ExecuteByExecutionIdAsync(10);

        // Assert
        execution.Status.Should().Be(JobExecutionStatuses.Failed);
        execution.ErrorCode.Should().Be("PERMANENT");
        execution.ErrorMessage.Should().Be("Permanent config error");

        _auditMock.Verify(a => a.WriteAsync("JobExecution", 10, "FAILED", "system", null, It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteByExecutionIdAsync_WhenJobFailsAndFailureEmailIsEnabled_ShouldSendNotificationEmail()
    {
        // Arrange
        var sut = CreateSut();
        var execution = new JobExecution { ExecutionId = 12, ReportId = 1, Status = JobExecutionStatuses.Created, RetryCount = 0 };
        var failureEmailProvider = new Mock<IDeliveryProvider>();
        DeliveryRequest? notificationRequest = null;

        var report = new ReportDefinition
        {
            ReportId = 1,
            ReportCode = "FAIL_DEMO",
            QueryText = "SELECT 1",
            Customer = new Customer { CustomerCode = "CUST" },
            DataSource = new DataSource { DataSourceType = DataSourceTypes.Sql, ConnectionReference = "Default" },
            FileConfiguration = new FileConfiguration { FileFormat = FileFormats.Csv, FileNamePattern = "report.csv" },
            DeliveryConfiguration = new DeliveryConfiguration { DeliveryType = DeliveryTypes.Email, EmailTo = "reports@example.com" }
        };

        _executionRepoMock.Setup(e => e.GetByIdWithFilesAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(execution);
        _reportRepoMock.Setup(r => r.GetByIdWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _paramResolverMock.Setup(p => p.ResolveAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object?>());
        _dsResolverMock.Setup(d => d.Resolve(DataSourceTypes.Sql))
            .Throws(new InvalidOperationException("Source is not configured"));
        _deliveryResolverMock.Setup(d => d.Resolve(DeliveryTypes.Email))
            .Returns(failureEmailProvider.Object);
        _deliveryConfigRepoMock.Setup(r => r.GetFailureNotificationProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new DeliveryConfiguration
                {
                    DeliveryType = DeliveryTypes.Email,
                    EmailTo = "ops@example.com",
                    EmailCc = "lead@example.com",
                    EmailSubjectTemplate = "Failure {ReportCode}",
                    EmailBodyTemplate = "Execution {ExecutionId}: {ErrorMessage}",
                    IsFailureNotification = true,
                    IsActive = true
                }
            });
        failureEmailProvider.Setup(p => p.DeliverAsync(It.IsAny<DeliveryRequest>(), It.IsAny<CancellationToken>()))
            .Callback<DeliveryRequest, CancellationToken>((request, _) => notificationRequest = request)
            .Returns(Task.CompletedTask);

        // Act
        await sut.ExecuteByExecutionIdAsync(12);

        // Assert
        execution.Status.Should().Be(JobExecutionStatuses.Failed);
        notificationRequest.Should().NotBeNull();
        notificationRequest!.EmailTo.Should().Be("ops@example.com");
        notificationRequest.EmailCc.Should().Be("lead@example.com");
        notificationRequest.AttachmentPaths.Should().BeEmpty();
        notificationRequest.Tokens.ReportCode.Should().Be("FAIL_DEMO");
        notificationRequest.Tokens.ExecutionId.Should().Be(12);
        notificationRequest.Tokens.Status.Should().Be(JobExecutionStatuses.Failed);
        notificationRequest.Tokens.ErrorMessage.Should().Be("Source is not configured");
    }

    [Fact]
    public async Task ExecuteByExecutionIdAsync_WhenTransientErrorOccursAndRetryCountBelowMax_ShouldSetRetryingAndRethrow()
    {
        // Arrange
        var sut = CreateSut();
        var execution = new JobExecution { ExecutionId = 11, ReportId = 1, Status = JobExecutionStatuses.Created, RetryCount = 0 };

        _executionRepoMock.Setup(e => e.GetByIdWithFilesAsync(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(execution);

        _reportRepoMock.Setup(r => r.GetByIdWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Database connection timeout"));

        // Act
        Func<Task> act = async () => await sut.ExecuteByExecutionIdAsync(11);

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
        execution.Status.Should().Be(JobExecutionStatuses.Retrying);
        execution.RetryCount.Should().Be(1);
        execution.ErrorCode.Should().Be("TRANSIENT");
    }
}
