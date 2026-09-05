using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReportingEngine.Application.Abstractions.Execution;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Application.Options;
using ReportingEngine.Domain.Enums;

namespace ReportingEngine.Infrastructure.HangfireJobs;

public sealed class HangfireReportJobDispatcher : IReportJobDispatcher
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public HangfireReportJobDispatcher(IBackgroundJobClient backgroundJobClient) =>
        _backgroundJobClient = backgroundJobClient;

    public void EnqueueReportExecution(long reportId, DateTime? scheduledTimeUtc, bool isManual) =>
        _backgroundJobClient.Enqueue<IJobExecutor>(x => x.ExecuteAsync(reportId, scheduledTimeUtc, isManual, CancellationToken.None));

    public void EnqueueExecutionRetry(long executionId) =>
        _backgroundJobClient.Enqueue<IJobExecutor>(x => x.ExecuteByExecutionIdAsync(executionId, CancellationToken.None));
}

/// <summary>
/// Admin can enqueue jobs into the shared Hangfire SQL storage even when Worker performs execution.
/// </summary>
public sealed class HangfireScheduleSyncService : IScheduleSyncService
{
    private readonly IReportRepository _reportRepository;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly ILogger<HangfireScheduleSyncService> _logger;

    public HangfireScheduleSyncService(
        IReportRepository reportRepository,
        IRecurringJobManager recurringJobManager,
        ILogger<HangfireScheduleSyncService> logger)
    {
        _reportRepository = reportRepository;
        _recurringJobManager = recurringJobManager;
        _logger = logger;
    }

    public async Task SyncReportScheduleAsync(long reportId, CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdWithDetailsAsync(reportId, cancellationToken);
        if (report is null || report.Status != ReportStatuses.Active || report.Schedule is null)
        {
            await RemoveReportScheduleAsync(reportId, cancellationToken);
            return;
        }

        var cron = ScheduleCronConverter.ToCron(report.Schedule.ScheduleType, report.Schedule.CronExpression);
        var timeZone = TimeZoneHelper.Resolve(report.Schedule.TimeZoneId);
        var jobId = RecurringJobId(reportId);

        _recurringJobManager.AddOrUpdate<ReportRecurringJob>(
            jobId,
            x => x.ExecuteScheduledAsync(reportId),
            cron,
            new RecurringJobOptions { TimeZone = timeZone });

        _logger.LogInformation("Synced Hangfire recurring job {JobId} cron={Cron} tz={TimeZone}", jobId, cron, timeZone.Id);
    }

    public Task RemoveReportScheduleAsync(long reportId, CancellationToken cancellationToken = default)
    {
        _recurringJobManager.RemoveIfExists(RecurringJobId(reportId));
        _logger.LogInformation("Removed Hangfire recurring job for report {ReportId}", reportId);
        return Task.CompletedTask;
    }

    public async Task SyncAllActiveSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var reports = await _reportRepository.GetActiveReportsAsync(cancellationToken);
        foreach (var report in reports)
        {
            await SyncReportScheduleAsync(report.ReportId, cancellationToken);
        }
    }

    public static string RecurringJobId(long reportId) => $"report-{reportId}";
}

public sealed class ReportRecurringJob
{
    private readonly IJobExecutor _jobExecutor;
    private readonly ILogger<ReportRecurringJob> _logger;

    public ReportRecurringJob(IJobExecutor jobExecutor, ILogger<ReportRecurringJob> logger)
    {
        _jobExecutor = jobExecutor;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
    public async Task ExecuteScheduledAsync(long reportId)
    {
        // DisableConcurrentExecution ensures the SAME report does not overlap.
        // Different reports use different recurring job ids and run concurrently across Hangfire workers.
        var scheduledTimeUtc = DateTime.UtcNow;
        _logger.LogInformation("Recurring trigger for ReportId={ReportId} at {ScheduledTimeUtc}", reportId, scheduledTimeUtc);
        await _jobExecutor.ExecuteAsync(reportId, scheduledTimeUtc, isManual: false, CancellationToken.None);
    }
}

public static class ScheduleCronConverter
{
    public static string ToCron(string scheduleType, string? cronExpression) =>
        scheduleType.Trim().ToUpperInvariant() switch
        {
            ScheduleTypes.Cron => cronExpression ?? throw new InvalidOperationException("CronExpression required."),
            ScheduleTypes.Daily => "0 0 * * *",
            ScheduleTypes.Weekly => "0 0 * * 1",
            ScheduleTypes.Monthly => "0 0 1 * *",
            ScheduleTypes.Once => cronExpression ?? "0 0 1 1 *",
            _ => throw new InvalidOperationException($"Unsupported schedule type '{scheduleType}'.")
        };
}

public static class TimeZoneHelper
{
    public static TimeZoneInfo Resolve(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            // Common mapping for Windows/IANA mismatches in local/dev.
            if (string.Equals(timeZoneId, "UTC", StringComparison.OrdinalIgnoreCase))
            {
                return TimeZoneInfo.Utc;
            }

            if (string.Equals(timeZoneId, "India Standard Time", StringComparison.OrdinalIgnoreCase)
                || string.Equals(timeZoneId, "Asia/Kolkata", StringComparison.OrdinalIgnoreCase))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
                catch { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
            }

            throw;
        }
    }
}

public sealed class ExponentialBackoffAttribute : JobFilterAttribute, IElectStateFilter
{
    private readonly int _maxAttempts;
    private readonly int _initialDelaySeconds;
    private readonly double _multiplier;

    public ExponentialBackoffAttribute(int maxAttempts = 3, int initialDelaySeconds = 30, double multiplier = 2.0)
    {
        _maxAttempts = maxAttempts;
        _initialDelaySeconds = initialDelaySeconds;
        _multiplier = multiplier;
    }

    public void OnStateElection(ElectStateContext context)
    {
        if (context.CandidateState is not FailedState)
        {
            return;
        }

        var retryCount = context.GetJobParameter<int>("RetryCount");
        if (retryCount >= _maxAttempts)
        {
            return;
        }

        var delay = TimeSpan.FromSeconds(_initialDelaySeconds * Math.Pow(_multiplier, retryCount));
        context.SetJobParameter("RetryCount", retryCount + 1);
        context.CandidateState = new ScheduledState(delay)
        {
            Reason = $"Exponential backoff retry #{retryCount + 1}"
        };
    }
}
