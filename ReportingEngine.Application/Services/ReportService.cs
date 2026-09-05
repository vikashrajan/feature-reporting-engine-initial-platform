using ReportingEngine.Application.Abstractions.Execution;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Application.DTOs;
using ReportingEngine.Domain.Entities;
using ReportingEngine.Domain.Enums;

namespace ReportingEngine.Application.Services;

public sealed class ReportService : IReportService
{
    private readonly IReportRepository _reportRepository;
    private readonly IJobExecutionRepository _executionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;
    private readonly IReportValidator _validator;
    private readonly IScheduleSyncService _scheduleSync;
    private readonly IReportJobDispatcher _jobDispatcher;

    public ReportService(
        IReportRepository reportRepository,
        IJobExecutionRepository executionRepository,
        IUnitOfWork unitOfWork,
        IAuditService audit,
        IReportValidator validator,
        IScheduleSyncService scheduleSync,
        IReportJobDispatcher jobDispatcher)
    {
        _reportRepository = reportRepository;
        _executionRepository = executionRepository;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _validator = validator;
        _scheduleSync = scheduleSync;
        _jobDispatcher = jobDispatcher;
    }

    public async Task<IReadOnlyList<ReportDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _reportRepository.GetAllAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<ReportDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var item = await _reportRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<ReportDto> CreateAsync(CreateReportRequest request, string performedBy, CancellationToken cancellationToken = default)
    {
        var existing = await _reportRepository.GetByCodeAsync(request.ReportCode, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Report code '{request.ReportCode}' already exists.");
        }

        var entity = new ReportDefinition
        {
            CustomerId = request.CustomerId,
            ReportCode = request.ReportCode.Trim(),
            ReportName = request.ReportName.Trim(),
            DataSourceId = request.DataSourceId,
            ScheduleId = request.ScheduleId,
            FileConfigId = request.FileConfigId,
            DeliveryConfigId = request.DeliveryConfigId,
            QueryText = request.QueryText,
            Description = request.Description,
            VersionNumber = 1,
            Status = ReportStatuses.Draft,
            IsActive = true,
            CreatedBy = performedBy,
            CreatedDate = DateTime.UtcNow,
            Parameters = (request.Parameters ?? Array.Empty<ReportParameterInput>())
                .Select(p => new ReportParameter
                {
                    ParameterName = p.ParameterName.Trim(),
                    ParameterType = p.ParameterType.Trim().ToUpperInvariant(),
                    ParameterValue = p.ParameterValue,
                    ValueSource = p.ValueSource.Trim().ToUpperInvariant()
                }).ToList()
        };

        await _reportRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(nameof(ReportDefinition), entity.ReportId, AuditActions.Create, performedBy, newValue: Map(entity), cancellationToken: cancellationToken);
        return Map(entity);
    }

    public async Task<ReportDto> UpdateAsync(long id, UpdateReportRequest request, string performedBy, CancellationToken cancellationToken = default)
    {
        var entity = await _reportRepository.GetByIdWithDetailsAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Report {id} was not found.");

        if (entity.Status == ReportStatuses.Retired)
        {
            throw new InvalidOperationException("Retired reports cannot be updated.");
        }

        var old = Map(entity);
        entity.ReportName = request.ReportName.Trim();
        entity.DataSourceId = request.DataSourceId;
        entity.ScheduleId = request.ScheduleId;
        entity.FileConfigId = request.FileConfigId;
        entity.DeliveryConfigId = request.DeliveryConfigId;
        entity.QueryText = request.QueryText;
        entity.Description = request.Description;
        entity.VersionNumber += 1;
        entity.ModifiedBy = performedBy;
        entity.ModifiedDate = DateTime.UtcNow;

        entity.Parameters.Clear();
        foreach (var p in request.Parameters ?? Array.Empty<ReportParameterInput>())
        {
            entity.Parameters.Add(new ReportParameter
            {
                ReportId = entity.ReportId,
                ParameterName = p.ParameterName.Trim(),
                ParameterType = p.ParameterType.Trim().ToUpperInvariant(),
                ParameterValue = p.ParameterValue,
                ValueSource = p.ValueSource.Trim().ToUpperInvariant()
            });
        }

        await _reportRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (entity.Status == ReportStatuses.Active)
        {
            await _scheduleSync.SyncReportScheduleAsync(entity.ReportId, cancellationToken);
        }

        await _audit.WriteAsync(nameof(ReportDefinition), entity.ReportId, AuditActions.Update, performedBy, old, Map(entity), cancellationToken);
        return Map(entity);
    }

    public async Task ActivateAsync(long id, string performedBy, CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateForActivationAsync(id, cancellationToken);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException("Report cannot be activated: " + string.Join("; ", validation.Errors));
        }

        var entity = await _reportRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Report {id} was not found.");

        entity.Status = ReportStatuses.Active;
        entity.IsActive = true;
        entity.ModifiedBy = performedBy;
        entity.ModifiedDate = DateTime.UtcNow;

        await _reportRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _scheduleSync.SyncReportScheduleAsync(id, cancellationToken);
        await _audit.WriteAsync(nameof(ReportDefinition), id, AuditActions.Activate, performedBy, cancellationToken: cancellationToken);
    }

    public async Task PauseAsync(long id, string performedBy, CancellationToken cancellationToken = default)
    {
        var entity = await _reportRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Report {id} was not found.");

        entity.Status = ReportStatuses.Paused;
        entity.ModifiedBy = performedBy;
        entity.ModifiedDate = DateTime.UtcNow;

        await _reportRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _scheduleSync.RemoveReportScheduleAsync(id, cancellationToken);
        await _audit.WriteAsync(nameof(ReportDefinition), id, AuditActions.Pause, performedBy, cancellationToken: cancellationToken);
    }

    public async Task ResumeAsync(long id, string performedBy, CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateForActivationAsync(id, cancellationToken);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException("Report cannot be resumed: " + string.Join("; ", validation.Errors));
        }

        var entity = await _reportRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Report {id} was not found.");

        entity.Status = ReportStatuses.Active;
        entity.IsActive = true;
        entity.ModifiedBy = performedBy;
        entity.ModifiedDate = DateTime.UtcNow;

        await _reportRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _scheduleSync.SyncReportScheduleAsync(id, cancellationToken);
        await _audit.WriteAsync(nameof(ReportDefinition), id, AuditActions.Resume, performedBy, cancellationToken: cancellationToken);
    }

    public async Task RunNowAsync(long id, string performedBy, CancellationToken cancellationToken = default)
    {
        _ = await _reportRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Report {id} was not found.");

        await _audit.WriteAsync(nameof(ReportDefinition), id, AuditActions.ManualRun, performedBy, cancellationToken: cancellationToken);
        _jobDispatcher.EnqueueReportExecution(id, null, true);
    }

    public async Task<IReadOnlyList<JobExecutionDto>> GetExecutionsAsync(long reportId, CancellationToken cancellationToken = default)
    {
        _ = await _reportRepository.GetByIdAsync(reportId, cancellationToken)
            ?? throw new KeyNotFoundException($"Report {reportId} was not found.");

        var executions = await _executionRepository.GetByReportIdAsync(reportId, cancellationToken);
        return executions.Select(ExecutionMappings.Map).ToList();
    }

    private static ReportDto Map(ReportDefinition r) =>
        new(
            r.ReportId,
            r.CustomerId,
            r.ReportCode,
            r.ReportName,
            r.DataSourceId,
            r.ScheduleId,
            r.FileConfigId,
            r.DeliveryConfigId,
            r.QueryText,
            r.Description,
            r.VersionNumber,
            r.Status,
            r.IsActive,
            r.Parameters.Select(p => new ReportParameterDto(p.ParameterId, p.ReportId, p.ParameterName, p.ParameterType, p.ParameterValue, p.ValueSource)).ToList());
}

internal static class ExecutionMappings
{
    public static JobExecutionDto Map(JobExecution e) =>
        new(
            e.ExecutionId,
            e.ReportId,
            e.ScheduledTime,
            e.StartedAt,
            e.CompletedAt,
            e.Status,
            e.RecordCount,
            e.FileCount,
            e.RetryCount,
            e.ErrorCode,
            e.ErrorMessage,
            e.Files.Select(f => new FileExecutionDto(
                f.FileExecutionId,
                f.SequenceNumber,
                f.FileName,
                f.FilePath,
                f.FileSizeBytes,
                f.RecordCount,
                f.GenerationStatus,
                f.DeliveryStatus,
                f.Checksum)).ToList());
}
