using ReportingEngine.Application.Abstractions.Execution;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Application.DTOs;
using ReportingEngine.Domain.Enums;

namespace ReportingEngine.Application.Services;

public sealed class ExecutionService : IExecutionService
{
    private readonly IJobExecutionRepository _executionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;
    private readonly IReportJobDispatcher _jobDispatcher;

    public ExecutionService(
        IJobExecutionRepository executionRepository,
        IUnitOfWork unitOfWork,
        IAuditService audit,
        IReportJobDispatcher jobDispatcher)
    {
        _executionRepository = executionRepository;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _jobDispatcher = jobDispatcher;
    }

    public async Task<JobExecutionDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var item = await _executionRepository.GetByIdWithFilesAsync(id, cancellationToken);
        return item is null ? null : ExecutionMappings.Map(item);
    }

    public async Task RetryAsync(long id, string performedBy, CancellationToken cancellationToken = default)
    {
        var execution = await _executionRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Execution {id} was not found.");

        if (execution.Status is not (JobExecutionStatuses.Failed or JobExecutionStatuses.Cancelled))
        {
            throw new InvalidOperationException($"Execution {id} cannot be retried from status '{execution.Status}'.");
        }

        execution.Status = JobExecutionStatuses.Retrying;
        execution.RetryCount += 1;
        execution.ErrorCode = null;
        execution.ErrorMessage = null;
        await _executionRepository.UpdateAsync(execution, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("JobExecution", id, "RETRY", performedBy, cancellationToken: cancellationToken);
        _jobDispatcher.EnqueueExecutionRetry(id);
    }

    public async Task CancelAsync(long id, string performedBy, CancellationToken cancellationToken = default)
    {
        var execution = await _executionRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Execution {id} was not found.");

        if (execution.Status is JobExecutionStatuses.Success or JobExecutionStatuses.Cancelled)
        {
            throw new InvalidOperationException($"Execution {id} cannot be cancelled from status '{execution.Status}'.");
        }

        execution.Status = JobExecutionStatuses.Cancelled;
        execution.CompletedAt = DateTime.UtcNow;
        await _executionRepository.UpdateAsync(execution, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("JobExecution", id, "CANCEL", performedBy, cancellationToken: cancellationToken);
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var counts = await _executionRepository.GetDashboardCountsAsync(cancellationToken);
        return new DashboardDto(counts.Running, counts.Successful, counts.Failed, counts.Upcoming);
    }

    public async Task<IReadOnlyList<DashboardDetailDto>> GetDashboardDetailsAsync(string category, CancellationToken cancellationToken = default)
    {
        var items = await _executionRepository.GetDashboardDetailsAsync(category, cancellationToken);
        return items.Select(x => new DashboardDetailDto(
            x.ExecutionId,
            x.ReportId,
            x.ReportCode,
            x.ReportName,
            x.CustomerName,
            x.Status,
            x.ScheduledTime,
            x.StartedAt,
            x.CompletedAt,
            x.RecordCount,
            x.FileCount,
            x.ErrorMessage)).ToList();
    }
}
