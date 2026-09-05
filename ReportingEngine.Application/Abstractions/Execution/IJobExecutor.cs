namespace ReportingEngine.Application.Abstractions.Execution;

public interface IJobExecutor
{
    Task ExecuteAsync(long reportId, DateTime? scheduledTimeUtc = null, bool isManual = false, CancellationToken cancellationToken = default);
    Task ExecuteByExecutionIdAsync(long executionId, CancellationToken cancellationToken = default);
}

public interface IParameterResolver
{
    Task<IReadOnlyDictionary<string, object?>> ResolveAsync(
        long reportId,
        DateTime currentExecutionUtc,
        CancellationToken cancellationToken = default);
}

public interface IReportValidator
{
    Task<ReportValidationResult> ValidateForActivationAsync(long reportId, CancellationToken cancellationToken = default);
}

public sealed record ReportValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ReportValidationResult Success() => new(true, Array.Empty<string>());
    public static ReportValidationResult Failure(params string[] errors) => new(false, errors);
}

public interface IScheduleSyncService
{
    Task SyncReportScheduleAsync(long reportId, CancellationToken cancellationToken = default);
    Task RemoveReportScheduleAsync(long reportId, CancellationToken cancellationToken = default);
    Task SyncAllActiveSchedulesAsync(CancellationToken cancellationToken = default);
}

public interface IIdempotencyService
{
    string BuildKey(long reportId, DateTime? scheduledTimeUtc, bool isManual);
    Task<bool> TryClaimAsync(string idempotencyKey, long reportId, DateTime? scheduledTimeUtc, CancellationToken cancellationToken = default);
}

public interface ISecretResolver
{
    string? Resolve(string? secretReference);
}

public interface IAuditService
{
    Task WriteAsync(string entityType, long entityId, string action, string performedBy, object? oldValue = null, object? newValue = null, CancellationToken cancellationToken = default);
}
