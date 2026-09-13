using FluentAssertions;
using Moq;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Domain.Entities;
using ReportingEngine.Domain.Enums;
using ReportingEngine.Infrastructure.Execution;

namespace ReportingEngine.Tests;

public class ParameterResolverTests
{
    private readonly Mock<IReportRepository> _reportRepoMock = new();
    private readonly Mock<IJobExecutionRepository> _executionRepoMock = new();
    private readonly ParameterResolver _sut;

    public ParameterResolverTests()
    {
        _sut = new ParameterResolver(_reportRepoMock.Object, _executionRepoMock.Object);
    }

    [Fact]
    public async Task ResolveAsync_ShouldResolveStaticAndSystemAndExecutionParameters()
    {
        // Arrange
        var reportId = 1L;
        var now = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
        var lastSuccessCompletedAt = new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);

        var report = new ReportDefinition
        {
            ReportId = reportId,
            Customer = new Customer { TimeZoneId = "UTC" },
            Parameters = new List<ReportParameter>
            {
                new() { ParameterName = "MinAmount", ParameterType = ParameterTypes.Decimal, ParameterValue = "100.50", ValueSource = ParameterValueSources.Static },
                new() { ParameterName = "@PreviousSuccessfulExecution", ParameterType = ParameterTypes.DateTime, ValueSource = ParameterValueSources.PreviousExecution },
                new() { ParameterName = "CurrentExecution", ParameterType = ParameterTypes.DateTime, ValueSource = ParameterValueSources.CurrentExecution }
            }
        };

        _reportRepoMock.Setup(r => r.GetByIdWithDetailsAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        _executionRepoMock.Setup(e => e.GetLastSuccessfulAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JobExecution
            {
                ExecutionId = 42,
                ReportId = reportId,
                Status = JobExecutionStatuses.Success,
                CompletedAt = lastSuccessCompletedAt
            });

        // Act
        var result = await _sut.ResolveAsync(reportId, now);

        // Assert
        result.Should().ContainKey("MinAmount");
        result["MinAmount"].Should().Be(100.50m);

        result.Should().ContainKey("PreviousSuccessfulExecution");
        result["PreviousSuccessfulExecution"].Should().Be(lastSuccessCompletedAt);

        result.Should().ContainKey("CurrentExecution");
        result["CurrentExecution"].Should().Be(now);
    }

    [Fact]
    public async Task ResolveAsync_WhenNoPreviousSuccessfulExecution_ShouldFallbackToUnixEpoch()
    {
        // Arrange
        var reportId = 2L;
        var now = DateTime.UtcNow;

        var report = new ReportDefinition
        {
            ReportId = reportId,
            Parameters = new List<ReportParameter>
            {
                new() { ParameterName = "PreviousSuccessfulExecution", ParameterType = ParameterTypes.DateTime, ValueSource = ParameterValueSources.PreviousExecution }
            }
        };

        _reportRepoMock.Setup(r => r.GetByIdWithDetailsAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        _executionRepoMock.Setup(e => e.GetLastSuccessfulAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobExecution?)null);

        // Act
        var result = await _sut.ResolveAsync(reportId, now);

        // Assert
        result["PreviousSuccessfulExecution"].Should().Be(DateTime.UnixEpoch);
    }

    [Fact]
    public async Task ResolveAsync_ShouldResolveDynamicDateWindowParameters()
    {
        // Arrange
        var reportId = 3L;
        var now = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
        var report = new ReportDefinition
        {
            ReportId = reportId,
            Customer = new Customer { TimeZoneId = "UTC" },
            Parameters = new List<ReportParameter>
            {
                new() { ParameterName = "FromDate", ParameterType = ParameterTypes.DateTime, ParameterValue = "YESTERDAY_START", ValueSource = ParameterValueSources.System },
                new() { ParameterName = "ToDate", ParameterType = ParameterTypes.DateTime, ParameterValue = "YESTERDAY_END", ValueSource = ParameterValueSources.System },
                new() { ParameterName = "MonthFrom", ParameterType = ParameterTypes.DateTime, ParameterValue = "PREVIOUS_MONTH_START", ValueSource = ParameterValueSources.System },
                new() { ParameterName = "MonthTo", ParameterType = ParameterTypes.DateTime, ParameterValue = "PREVIOUS_MONTH_END", ValueSource = ParameterValueSources.System }
            }
        };

        _reportRepoMock.Setup(r => r.GetByIdWithDetailsAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _executionRepoMock.Setup(e => e.GetLastSuccessfulAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobExecution?)null);

        // Act
        var result = await _sut.ResolveAsync(reportId, now);

        // Assert
        result["FromDate"].Should().Be(new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc));
        result["ToDate"].Should().Be(new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc));
        result["MonthFrom"].Should().Be(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        result["MonthTo"].Should().Be(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData(ParameterTypes.String, "Hello", "Hello")]
    [InlineData(ParameterTypes.Int, "42", 42)]
    [InlineData(ParameterTypes.Decimal, "123.45", 123.45)]
    [InlineData(ParameterTypes.Bool, "true", true)]
    public void ConvertValue_ShouldConvertTypesCorrectly(string type, string raw, object expected)
    {
        var result = ParameterResolver.ConvertValue(type, raw);
        result.Should().Be(expected);
    }
}
