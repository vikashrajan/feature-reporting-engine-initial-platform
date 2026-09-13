using FluentAssertions;
using ReportingEngine.Application.Abstractions.Files;
using ReportingEngine.Infrastructure.Files;

namespace ReportingEngine.Tests;

public class FileNameTokenReplacementTests
{
    private readonly FileNameTokenReplacer _sut = new();

    [Fact]
    public void Replace_ShouldReplaceAllTokensCorrectly()
    {
        // Arrange
        var date = new DateTime(2026, 9, 6, 14, 30, 45, DateTimeKind.Utc);
        var context = new FileNameTokenContext("CUST01", "REP999", 101, date, Sequence: 2);
        var pattern = "Export_{CustomerCode}_{ReportCode}_{ExecutionId}_{yyyyMMdd}_{Sequence}.csv";

        // Act
        var result = _sut.Replace(pattern, context);

        // Assert
        result.Should().Be("Export_CUST01_REP999_101_20260906_002.csv");
    }

    [Fact]
    public void Replace_ShouldSupportExecutionDateAndTimestampTokens()
    {
        // Arrange
        var date = new DateTime(2026, 12, 25, 08, 15, 30, DateTimeKind.Utc);
        var context = new FileNameTokenContext("ACME", "SALES", 55, date, Sequence: 1);
        var pattern = "Sales_{ExecutionDate}_{yyyyMMddHHmmss}.csv";

        // Act
        var result = _sut.Replace(pattern, context);

        // Assert
        result.Should().Be("Sales_2026-12-25_20261225081530.csv");
    }
}
