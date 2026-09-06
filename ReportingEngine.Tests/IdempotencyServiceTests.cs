using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ReportingEngine.Domain.Entities;
using ReportingEngine.Domain.Enums;
using ReportingEngine.Infrastructure.Execution;
using ReportingEngine.Infrastructure.Persistence;
using Xunit;

namespace ReportingEngine.Tests;

public class IdempotencyServiceTests
{
    private readonly ReportingEngineDbContext _db;
    private readonly Mock<ILogger<IdempotencyService>> _loggerMock = new();
    private readonly IdempotencyService _sut;

    public IdempotencyServiceTests()
    {
        var options = new DbContextOptionsBuilder<ReportingEngineDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ReportingEngineDbContext(options);
        _sut = new IdempotencyService(_db, _loggerMock.Object);
    }

    [Fact]
    public void BuildKey_ShouldBeDeterministic()
    {
        var time = new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc);

        var key1 = _sut.BuildKey(10, time, isManual: false);
        var key2 = _sut.BuildKey(10, time, isManual: false);
        var keyManual = _sut.BuildKey(10, time, isManual: true);

        key1.Should().Be(key2);
        key1.Should().NotBe(keyManual);
    }

    [Fact]
    public async Task TryClaimAsync_WhenKeyAlreadyExistsAndSuccess_ShouldReturnFalse()
    {
        // Arrange
        var key = "sched:1:202609061000";
        _db.JobExecutions.Add(new JobExecution
        {
            ReportId = 1,
            Status = JobExecutionStatuses.Success,
            IdempotencyKey = key,
            CreatedDate = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.TryClaimAsync(key, 1, DateTime.UtcNow);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryClaimAsync_WhenKeyNew_ShouldCreateExecutionAndReturnTrue()
    {
        // Arrange
        var key = "sched:2:202609061000";

        // Act
        var result = await _sut.TryClaimAsync(key, 2, DateTime.UtcNow);

        // Assert
        result.Should().BeTrue();
        var execution = await _db.JobExecutions.FirstOrDefaultAsync(x => x.IdempotencyKey == key);
        execution.Should().NotBeNull();
        execution!.ReportId.Should().Be(2);
        execution.Status.Should().Be(JobExecutionStatuses.Created);
    }
}
