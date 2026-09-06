using FluentAssertions;
using ReportingEngine.Application.Abstractions.Files;
using ReportingEngine.Domain.Enums;
using ReportingEngine.Infrastructure.Files;
using Xunit;

namespace ReportingEngine.Tests;

public class FileSplittingTests : IDisposable
{
    private readonly string _tempDirectory;

    public FileSplittingTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ReportingEngineTests_" + Guid.NewGuid().ToString("N"));
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
    public async Task SplitAsync_WhenSplitDisabled_ShouldGenerateSingleFile()
    {
        // Arrange
        var generatorResolver = new FileGeneratorResolver(new IFileGenerator[] { new CsvFileGenerator() });
        var splitter = new FileSplitter(generatorResolver, new FileNameTokenReplacer());
        var context = new FileNameTokenContext("CUST", "REP", 1, DateTime.UtcNow, 1);

        var rows = CreateSampleRows(10);

        // Act
        var results = await splitter.SplitAsync(
            rows,
            FileFormats.Csv,
            _tempDirectory,
            "Report_{Sequence}.csv",
            context,
            splitEnabled: false,
            splitType: null,
            splitValue: null);

        // Assert
        results.Should().HaveCount(1);
        results[0].RecordCount.Should().Be(10);
        File.Exists(results[0].FilePath).Should().BeTrue();
    }

    [Fact]
    public async Task SplitAsync_WhenSplitByRecordCount_ShouldGenerateMultipleFiles()
    {
        // Arrange
        var generatorResolver = new FileGeneratorResolver(new IFileGenerator[] { new CsvFileGenerator() });
        var splitter = new FileSplitter(generatorResolver, new FileNameTokenReplacer());
        var context = new FileNameTokenContext("CUST", "REP", 1, DateTime.UtcNow, 1);

        var rows = CreateSampleRows(7);

        // Act
        var results = await splitter.SplitAsync(
            rows,
            FileFormats.Csv,
            _tempDirectory,
            "SplitReport_{Sequence}.csv",
            context,
            splitEnabled: true,
            splitType: SplitTypes.RecordCount,
            splitValue: 3);

        // Assert
        // 7 rows split by limit 3 => File 1 (3), File 2 (3), File 3 (1)
        results.Should().HaveCount(3);
        results[0].RecordCount.Should().Be(3);
        results[1].RecordCount.Should().Be(3);
        results[2].RecordCount.Should().Be(1);

        Path.GetFileName(results[0].FilePath).Should().Be("SplitReport_001.csv");
        Path.GetFileName(results[1].FilePath).Should().Be("SplitReport_002.csv");
        Path.GetFileName(results[2].FilePath).Should().Be("SplitReport_003.csv");
    }

    private static async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> CreateSampleRows(int count)
    {
        for (int i = 1; i <= count; i++)
        {
            yield return new Dictionary<string, object?>
            {
                ["Id"] = i,
                ["Name"] = $"Item_{i}",
                ["Amount"] = i * 10.5
            };
            await Task.Yield();
        }
    }
}
