using Microsoft.Extensions.Logging;
using ReportingEngine.Application.Abstractions.Execution;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Domain.Entities;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReportingEngine.Infrastructure.Services;

public sealed class AuditService : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private readonly IAuditLogRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuditService> _logger;

    public AuditService(IAuditLogRepository repository, IUnitOfWork unitOfWork, ILogger<AuditService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task WriteAsync(
        string entityType,
        long entityId,
        string action,
        string performedBy,
        object? oldValue = null,
        object? newValue = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            OldValue = SerializeSafe(oldValue),
            NewValue = SerializeSafe(newValue),
            PerformedBy = performedBy,
            PerformedAt = DateTime.UtcNow
        };

        try
        {
            await _repository.AddAsync(entry, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "Audit write was cancelled. EntityType={EntityType} EntityId={EntityId} Action={Action} PerformedBy={PerformedBy} CancellationRequested={CancellationRequested}",
                entityType,
                entityId,
                action,
                performedBy,
                cancellationToken.IsCancellationRequested);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Audit write failed. EntityType={EntityType} EntityId={EntityId} Action={Action} PerformedBy={PerformedBy}",
                entityType,
                entityId,
                action,
                performedBy);
            throw;
        }
    }

    private static string? SerializeSafe(object? value)
    {
        if (value is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(value, JsonOptions);
        // Defense-in-depth: strip common secret-looking keys from serialized payloads.
        foreach (var key in new[] { "Password", "Secret", "Token", "ConnectionString", "PrivateKey" })
        {
            json = System.Text.RegularExpressions.Regex.Replace(
                json,
                $"\"{key}\"\\s*:\\s*\"[^\"]*\"",
                $"\"{key}\":\"***\"",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return json;
    }
}
