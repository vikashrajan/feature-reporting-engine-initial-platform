using System.Text.Json;
using System.Text.Json.Serialization;
using ReportingEngine.Application.Abstractions.Execution;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Domain.Entities;

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

    public AuditService(IAuditLogRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
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

        await _repository.AddAsync(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
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
