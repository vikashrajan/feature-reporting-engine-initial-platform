using FluentAssertions;
using Moq;
using ReportingEngine.Application.Abstractions.Data;
using ReportingEngine.Application.Abstractions.Delivery;
using ReportingEngine.Application.Abstractions.Files;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Domain.Entities;
using ReportingEngine.Domain.Enums;
using ReportingEngine.Infrastructure.Execution;
using Xunit;

namespace ReportingEngine.Tests;

public class ReportValidatorTests
{
    private readonly Mock<IReportRepository> _reportRepoMock = new();
    private readonly Mock<IDataSourceProviderResolver> _dsResolverMock = new();
    private readonly Mock<IFileGeneratorResolver> _fgResolverMock = new();
    private readonly Mock<IDeliveryProviderResolver> _deliveryResolverMock = new();
    private readonly ReportValidator _sut;

    public ReportValidatorTests()
    {
        _sut = new ReportValidator(
            _reportRepoMock.Object,
            _dsResolverMock.Object,
            _fgResolverMock.Object,
            _deliveryResolverMock.Object);
    }

    [Fact]
    public async Task ValidateForActivationAsync_WhenReportNotFound_ShouldReturnFailure()
    {
        // Arrange
        _reportRepoMock.Setup(r => r.GetByIdWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReportDefinition?)null);

        // Act
        var result = await _sut.ValidateForActivationAsync(1);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Report 1 was not found.");
    }

    [Fact]
    public async Task ValidateForActivationAsync_WhenAllValid_ShouldReturnSuccess()
    {
        // Arrange
        var report = CreateValidReport();
        _reportRepoMock.Setup(r => r.GetByIdWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _dsResolverMock.Setup(d => d.IsSupported("SQL")).Returns(true);
        _fgResolverMock.Setup(f => f.IsSupported("CSV")).Returns(true);
        _deliveryResolverMock.Setup(d => d.IsSupported("EMAIL")).Returns(true);

        // Act
        var result = await _sut.ValidateForActivationAsync(1);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateForActivationAsync_WhenCustomerInactive_ShouldReturnFailure()
    {
        // Arrange
        var report = CreateValidReport();
        report.Customer!.IsActive = false;

        _reportRepoMock.Setup(r => r.GetByIdWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _dsResolverMock.Setup(d => d.IsSupported("SQL")).Returns(true);
        _fgResolverMock.Setup(f => f.IsSupported("CSV")).Returns(true);
        _deliveryResolverMock.Setup(d => d.IsSupported("EMAIL")).Returns(true);

        // Act
        var result = await _sut.ValidateForActivationAsync(1);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Customer must exist and be active.");
    }

    [Fact]
    public async Task ValidateForActivationAsync_WhenSplitEnabledWithoutValue_ShouldReturnFailure()
    {
        // Arrange
        var report = CreateValidReport();
        report.FileConfiguration!.SplitEnabled = true;
        report.FileConfiguration.SplitType = SplitTypes.RecordCount;
        report.FileConfiguration.SplitValue = null;

        _reportRepoMock.Setup(r => r.GetByIdWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _dsResolverMock.Setup(d => d.IsSupported("SQL")).Returns(true);
        _fgResolverMock.Setup(f => f.IsSupported("CSV")).Returns(true);
        _deliveryResolverMock.Setup(d => d.IsSupported("EMAIL")).Returns(true);

        // Act
        var result = await _sut.ValidateForActivationAsync(1);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Split configuration is invalid.");
    }

    private static ReportDefinition CreateValidReport() => new()
    {
        ReportId = 1,
        ReportCode = "REP001",
        ReportName = "Test Report",
        QueryText = "SELECT 1",
        Customer = new Customer { CustomerId = 1, CustomerCode = "CUST1", CustomerName = "Customer 1", IsActive = true },
        DataSource = new DataSource { DataSourceId = 1, DataSourceName = "DS1", DataSourceType = "SQL", IsActive = true },
        Schedule = new Schedule { ScheduleId = 1, ScheduleName = "SCH1", ScheduleType = ScheduleTypes.Cron, CronExpression = "0 * * * *", TimeZoneId = "UTC", IsActive = true },
        FileConfiguration = new FileConfiguration { FileConfigId = 1, ConfigurationName = "FC1", FileFormat = "CSV", FileNamePattern = "File.csv", SplitEnabled = false },
        DeliveryConfiguration = new DeliveryConfiguration
        {
            DeliveryConfigId = 1,
            DeliveryName = "DC1",
            DeliveryType = "EMAIL",
            EmailTo = "test@example.com",
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
            },
            IsActive = true
        }
    };
}
