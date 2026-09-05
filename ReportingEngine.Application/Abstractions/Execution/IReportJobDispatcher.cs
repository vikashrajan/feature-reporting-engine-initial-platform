namespace ReportingEngine.Application.Abstractions.Execution;

public interface IReportJobDispatcher
{
    void EnqueueReportExecution(long reportId, DateTime? scheduledTimeUtc, bool isManual);
    void EnqueueExecutionRetry(long executionId);
}
