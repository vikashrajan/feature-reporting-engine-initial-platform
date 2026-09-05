using ReportingEngine.Application.Abstractions.Execution;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Application.DTOs;
using ReportingEngine.Domain.Entities;
using ReportingEngine.Domain.Enums;

namespace ReportingEngine.Application.Services;

public sealed class DeliveryConfigurationService : IDeliveryConfigurationService
{
    private readonly IDeliveryConfigurationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;

    public DeliveryConfigurationService(IDeliveryConfigurationRepository repository, IUnitOfWork unitOfWork, IAuditService audit)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<IReadOnlyList<DeliveryConfigurationDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<DeliveryConfigurationDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(id, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<DeliveryConfigurationDto> CreateAsync(CreateDeliveryConfigurationRequest request, string performedBy, CancellationToken cancellationToken = default)
    {
        Validate(request.DeliveryType, request.EmailTo, request.DestinationReference);

        var entity = new DeliveryConfiguration
        {
            DeliveryName = request.DeliveryName.Trim(),
            DeliveryType = request.DeliveryType.Trim().ToUpperInvariant(),
            DestinationReference = request.DestinationReference,
            EmailTo = request.EmailTo,
            EmailCc = request.EmailCc,
            EmailBcc = request.EmailBcc,
            EmailSubjectTemplate = request.EmailSubjectTemplate,
            EmailBodyTemplate = request.EmailBodyTemplate,
            SecretReference = request.SecretReference,
            IsActive = true,
            CreatedBy = performedBy,
            CreatedDate = DateTime.UtcNow
        };

        await _repository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(nameof(DeliveryConfiguration), entity.DeliveryConfigId, AuditActions.Create, performedBy, newValue: Map(entity), cancellationToken: cancellationToken);
        return Map(entity);
    }

    public async Task<DeliveryConfigurationDto> UpdateAsync(long id, UpdateDeliveryConfigurationRequest request, string performedBy, CancellationToken cancellationToken = default)
    {
        Validate(request.DeliveryType, request.EmailTo, request.DestinationReference);

        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Delivery configuration {id} was not found.");

        var old = Map(entity);
        entity.DeliveryName = request.DeliveryName.Trim();
        entity.DeliveryType = request.DeliveryType.Trim().ToUpperInvariant();
        entity.DestinationReference = request.DestinationReference;
        entity.EmailTo = request.EmailTo;
        entity.EmailCc = request.EmailCc;
        entity.EmailBcc = request.EmailBcc;
        entity.EmailSubjectTemplate = request.EmailSubjectTemplate;
        entity.EmailBodyTemplate = request.EmailBodyTemplate;
        entity.SecretReference = request.SecretReference;
        entity.IsActive = request.IsActive;
        entity.ModifiedBy = performedBy;
        entity.ModifiedDate = DateTime.UtcNow;

        await _repository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(nameof(DeliveryConfiguration), entity.DeliveryConfigId, AuditActions.Update, performedBy, old, Map(entity), cancellationToken);
        return Map(entity);
    }

    internal static void Validate(string deliveryType, string? emailTo, string? destinationReference)
    {
        var type = deliveryType.Trim().ToUpperInvariant();
        var valid = new[] { DeliveryTypes.Email, DeliveryTypes.Sftp, DeliveryTypes.Ftp, DeliveryTypes.SharedFolder, DeliveryTypes.Blob };
        if (!valid.Contains(type))
        {
            throw new InvalidOperationException($"Unsupported delivery type '{deliveryType}'.");
        }

        if (type == DeliveryTypes.Email && string.IsNullOrWhiteSpace(emailTo))
        {
            throw new InvalidOperationException("EmailTo is required for EMAIL delivery.");
        }

        if (type is DeliveryTypes.SharedFolder or DeliveryTypes.Sftp or DeliveryTypes.Ftp or DeliveryTypes.Blob
            && string.IsNullOrWhiteSpace(destinationReference))
        {
            throw new InvalidOperationException("DestinationReference is required for this delivery type.");
        }
    }

    private static DeliveryConfigurationDto Map(DeliveryConfiguration d) =>
        new(d.DeliveryConfigId, d.DeliveryName, d.DeliveryType, d.DestinationReference, d.EmailTo, d.EmailCc, d.EmailBcc, d.EmailSubjectTemplate, d.EmailBodyTemplate, d.SecretReference, d.IsActive);
}
