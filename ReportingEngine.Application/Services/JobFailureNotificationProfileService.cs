using ReportingEngine.Application.Abstractions.Execution;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Application.DTOs;
using ReportingEngine.Domain.Entities;
using ReportingEngine.Domain.Enums;

namespace ReportingEngine.Application.Services;

public sealed class JobFailureNotificationProfileService : IJobFailureNotificationProfileService
{
    private readonly IJobFailureNotificationProfileRepository _repository;
    private readonly ISmtpConfigurationRepository _smtpRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;

    public JobFailureNotificationProfileService(
        IJobFailureNotificationProfileRepository repository,
        ISmtpConfigurationRepository smtpRepository,
        IUnitOfWork unitOfWork,
        IAuditService audit)
    {
        _repository = repository;
        _smtpRepository = smtpRepository;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<IReadOnlyList<JobFailureNotificationProfileDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await _repository.GetAllAsync(cancellationToken)).Select(Map).ToList();

    public async Task<JobFailureNotificationProfileDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(id, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<JobFailureNotificationProfileDto> CreateAsync(CreateJobFailureNotificationProfileRequest request, string performedBy, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(request.ProfileName, request.SmtpConfigId, request.EmailTo, cancellationToken);
        var entity = new JobFailureNotificationProfile
        {
            ProfileName = request.ProfileName.Trim(),
            SmtpConfigId = request.SmtpConfigId,
            EmailTo = request.EmailTo.Trim(),
            EmailCc = TrimToNull(request.EmailCc),
            EmailBcc = TrimToNull(request.EmailBcc),
            SubjectTemplate = TrimToNull(request.SubjectTemplate),
            BodyTemplate = TrimToNull(request.BodyTemplate),
            IsActive = true,
            CreatedBy = performedBy,
            CreatedDate = DateTime.UtcNow
        };

        await _repository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        entity = await _repository.GetByIdAsync(entity.FailureProfileId, cancellationToken) ?? entity;
        await _audit.WriteAsync(nameof(JobFailureNotificationProfile), entity.FailureProfileId, AuditActions.Create, performedBy, newValue: Map(entity), cancellationToken: cancellationToken);
        return Map(entity);
    }

    public async Task<JobFailureNotificationProfileDto> UpdateAsync(long id, UpdateJobFailureNotificationProfileRequest request, string performedBy, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(request.ProfileName, request.SmtpConfigId, request.EmailTo, cancellationToken);
        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Job failure notification profile {id} was not found.");
        var old = Map(entity);
        entity.ProfileName = request.ProfileName.Trim();
        entity.SmtpConfigId = request.SmtpConfigId;
        entity.EmailTo = request.EmailTo.Trim();
        entity.EmailCc = TrimToNull(request.EmailCc);
        entity.EmailBcc = TrimToNull(request.EmailBcc);
        entity.SubjectTemplate = TrimToNull(request.SubjectTemplate);
        entity.BodyTemplate = TrimToNull(request.BodyTemplate);
        entity.IsActive = request.IsActive;
        entity.ModifiedBy = performedBy;
        entity.ModifiedDate = DateTime.UtcNow;

        await _repository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        entity = await _repository.GetByIdAsync(id, cancellationToken) ?? entity;
        await _audit.WriteAsync(nameof(JobFailureNotificationProfile), id, AuditActions.Update, performedBy, old, Map(entity), cancellationToken);
        return Map(entity);
    }

    public async Task DeleteAsync(long id, string performedBy, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Job failure notification profile {id} was not found.");
        var old = Map(entity);
        await _repository.DeleteAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(nameof(JobFailureNotificationProfile), id, AuditActions.Delete, performedBy, old, cancellationToken: cancellationToken);
    }

    private async Task ValidateAsync(string profileName, long smtpConfigId, string emailTo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profileName)) throw new InvalidOperationException("Failure notification profile name is required.");
        if (string.IsNullOrWhiteSpace(emailTo)) throw new InvalidOperationException("Failure notification Email To is required.");
        var smtp = await _smtpRepository.GetByIdAsync(smtpConfigId, cancellationToken)
            ?? throw new InvalidOperationException("Selected SMTP profile was not found.");
        if (!smtp.IsActive) throw new InvalidOperationException("Selected SMTP profile is inactive.");
    }

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static JobFailureNotificationProfileDto Map(JobFailureNotificationProfile x) =>
        new(x.FailureProfileId, x.ProfileName, x.SmtpConfigId, x.SmtpConfiguration?.ProfileName, x.EmailTo, x.EmailCc, x.EmailBcc, x.SubjectTemplate, x.BodyTemplate, x.IsActive);
}
