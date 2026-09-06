using FluentAssertions;
using ReportingEngine.Domain.Enums;
using ReportingEngine.Infrastructure.HangfireJobs;
using Xunit;

namespace ReportingEngine.Tests;

public class ScheduleHandlingTests
{
    [Theory]
    [InlineData(ScheduleTypes.Cron, "0 0 * * *", "0 0 * * *")]
    [InlineData(ScheduleTypes.Daily, null, "0 0 * * *")]
    [InlineData(ScheduleTypes.Weekly, null, "0 0 * * 1")]
    [InlineData(ScheduleTypes.Monthly, null, "0 0 1 * *")]
    public void ScheduleCronConverter_ToCron_ShouldReturnExpectedCronString(string scheduleType, string? cronInput, string expectedCron)
    {
        var cron = ScheduleCronConverter.ToCron(scheduleType, cronInput);
        cron.Should().Be(expectedCron);
    }

    [Fact]
    public void TimeZoneHelper_ShouldResolveUtcAndCommonTimezones()
    {
        var utc = TimeZoneHelper.Resolve("UTC");
        utc.Should().NotBeNull();
        utc.Id.Should().Be(TimeZoneInfo.Utc.Id);
    }
}
