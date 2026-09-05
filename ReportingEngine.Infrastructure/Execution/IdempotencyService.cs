using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReportingEngine.Application.Abstractions.Execution;
using ReportingEngine.Domain.Entities;
using ReportingEngine.Domain.Enums;
using ReportingEngine.Infrastructure.Persistence;

namespace ReportingEngine.Infrastructure.Execution;

public sealed class IdempotencyService : IIdempotencyService
{
    private readonly ReportingEngineDbContext _db;
    private readonly ILogger<IdempotencyService> _logger;

    public IdempotencyService(ReportingEngineDbContext db, ILogger<IdempotencyService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public string BuildKey(long reportId, DateTime? scheduledTimeUtc, bool isManual)
    {
        if (isManual || scheduledTimeUtc is null)
        {
            return $"manual:{reportId}:{Guid.NewGuid():N}";
        }

        return $"sched:{reportId}:{scheduledTimeUtc.Value:yyyyMMddHHmm}";
    }

    public async Task<bool> TryClaimAsync(string idempotencyKey, long reportId, DateTime? scheduledTimeUtc, CancellationToken cancellationToken = default)
    {
        var existing = await _db.JobExecutions
            .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);

        if (existing is not null)
        {
            if (existing.Status == JobExecutionStatuses.Success)
            {
                _logger.LogWarning(
                    "Idempotency claim rejected for {IdempotencyKey}. Already SUCCESS ExecutionId={ExecutionId}",
                    idempotencyKey, existing.ExecutionId);
                return false;
            }

            if (existing.Status == JobExecutionStatuses.Running)
            {
                _logger.LogWarning(
                    "Idempotency claim rejected for {IdempotencyKey}. Already RUNNING ExecutionId={ExecutionId}",
                    idempotencyKey, existing.ExecutionId);
                return false;
            }

            // CREATED / RETRYING / FAILED / CANCELLED: allow resume by returning true without inserting.
            _logger.LogInformation(
                "Resuming existing execution {ExecutionId} for key {IdempotencyKey} status={Status}",
                existing.ExecutionId, idempotencyKey, existing.Status);
            return true;
        }

        try
        {
            var execution = new JobExecution
            {
                ReportId = reportId,
                ScheduledTime = scheduledTimeUtc,
                Status = JobExecutionStatuses.Created,
                IdempotencyKey = idempotencyKey,
                CreatedDate = DateTime.UtcNow,
                RetryCount = 0
            };

            _db.JobExecutions.Add(execution);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Idempotency unique constraint prevented duplicate claim for {IdempotencyKey}", idempotencyKey);
            return false;
        }
    }
}
