using ReportingEngine.Application.Abstractions.Execution;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Application.DTOs;
using ReportingEngine.Domain.Entities;
using ReportingEngine.Domain.Enums;

namespace ReportingEngine.Application.Services;

public sealed class ScheduleService : IScheduleService
{
    private readonly IScheduleRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;

    public ScheduleService(IScheduleRepository repository, IUnitOfWork unitOfWork, IAuditService audit)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<IReadOnlyList<ScheduleDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<ScheduleDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(id, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<ScheduleDto> CreateAsync(CreateScheduleRequest request, string performedBy, CancellationToken cancellationToken = default)
    {
        ValidateSchedule(request.ScheduleType, request.CronExpression, request.TimeZoneId);

        var entity = new Schedule
        {
            ScheduleName = request.ScheduleName.Trim(),
            ScheduleType = request.ScheduleType.Trim().ToUpperInvariant(),
            CronExpression = request.CronExpression,
            TimeZoneId = request.TimeZoneId.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = true,
            CreatedBy = performedBy,
            CreatedDate = DateTime.UtcNow
        };

        await _repository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(nameof(Schedule), entity.ScheduleId, AuditActions.Create, performedBy, newValue: Map(entity), cancellationToken: cancellationToken);
        return Map(entity);
    }

    public async Task<ScheduleDto> UpdateAsync(long id, UpdateScheduleRequest request, string performedBy, CancellationToken cancellationToken = default)
    {
        ValidateSchedule(request.ScheduleType, request.CronExpression, request.TimeZoneId);

        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Schedule {id} was not found.");

        var old = Map(entity);
        entity.ScheduleName = request.ScheduleName.Trim();
        entity.ScheduleType = request.ScheduleType.Trim().ToUpperInvariant();
        entity.CronExpression = request.CronExpression;
        entity.TimeZoneId = request.TimeZoneId.Trim();
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.IsActive = request.IsActive;
        entity.ModifiedBy = performedBy;
        entity.ModifiedDate = DateTime.UtcNow;

        await _repository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(nameof(Schedule), entity.ScheduleId, AuditActions.Update, performedBy, old, Map(entity), cancellationToken);
        return Map(entity);
    }

    internal static void ValidateSchedule(string scheduleType, string? cronExpression, string timeZoneId)
    {
        var type = scheduleType.Trim().ToUpperInvariant();
        var valid = new[] { ScheduleTypes.Cron, ScheduleTypes.Daily, ScheduleTypes.Weekly, ScheduleTypes.Monthly, ScheduleTypes.Once };
        if (!valid.Contains(type))
        {
            throw new InvalidOperationException($"Unsupported schedule type '{scheduleType}'.");
        }

        if (type == ScheduleTypes.Cron && string.IsNullOrWhiteSpace(cronExpression))
        {
            throw new InvalidOperationException("CronExpression is required for CRON schedules.");
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            // On Linux, Windows IDs may fail; try conversion later. Still allow common IANA/Windows ids via try.
            try
            {
                _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch
            {
                throw new InvalidOperationException($"Unknown TimeZoneId '{timeZoneId}'.");
            }
        }
    }

    private static ScheduleDto Map(Schedule s) =>
        new(s.ScheduleId, s.ScheduleName, s.ScheduleType, s.CronExpression, s.TimeZoneId, s.StartDate, s.EndDate, s.IsActive);
}
