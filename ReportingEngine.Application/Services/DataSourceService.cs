using ReportingEngine.Application.Abstractions.Execution;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Application.DTOs;
using ReportingEngine.Application.Options;
using ReportingEngine.Domain.Entities;
using ReportingEngine.Domain.Enums;
using Microsoft.Extensions.Options;

namespace ReportingEngine.Application.Services;

public sealed class DataSourceService : IDataSourceService
{
    private readonly IDataSourceRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;
    private readonly IOptionsMonitor<ConnectionReferencesOptions> _connectionReferences;

    public DataSourceService(
        IDataSourceRepository repository,
        IUnitOfWork unitOfWork,
        IAuditService audit,
        IOptionsMonitor<ConnectionReferencesOptions> connectionReferences)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _connectionReferences = connectionReferences;
    }

    public async Task<IReadOnlyList<DataSourceDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<DataSourceDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(id, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<DataSourceDto> CreateAsync(CreateDataSourceRequest request, string performedBy, CancellationToken cancellationToken = default)
    {
        var connectionReference = NormalizeConnectionReference(request.ConnectionReference);
        var entity = new DataSource
        {
            DataSourceName = request.DataSourceName.Trim(),
            DataSourceType = request.DataSourceType.Trim().ToUpperInvariant(),
            ConnectionReference = connectionReference,
            ConnectionString = NormalizeConnectionStringOrNull(request.ConnectionString),
            IsActive = true,
            CreatedBy = performedBy,
            CreatedDate = DateTime.UtcNow
        };

        await _repository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(nameof(DataSource), entity.DataSourceId, AuditActions.Create, performedBy, newValue: Map(entity), cancellationToken: cancellationToken);
        return Map(entity);
    }

    public async Task<DataSourceDto> UpdateAsync(long id, UpdateDataSourceRequest request, string performedBy, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Data source {id} was not found.");

        var old = Map(entity);
        var connectionReference = NormalizeConnectionReference(request.ConnectionReference);
        entity.DataSourceName = request.DataSourceName.Trim();
        entity.DataSourceType = request.DataSourceType.Trim().ToUpperInvariant();
        entity.ConnectionReference = connectionReference;
        if (request.ConnectionString is not null)
        {
            entity.ConnectionString = NormalizeConnectionStringOrNull(request.ConnectionString);
        }
        entity.IsActive = request.IsActive;
        entity.ModifiedBy = performedBy;
        entity.ModifiedDate = DateTime.UtcNow;

        await _repository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(nameof(DataSource), entity.DataSourceId, AuditActions.Update, performedBy, old, Map(entity), cancellationToken);
        return Map(entity);
    }

    public async Task DeleteAsync(long id, string performedBy, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Data source {id} was not found.");

        if (await _repository.IsReferencedAsync(id, cancellationToken))
        {
            throw new InvalidOperationException("Cannot delete this data source because it is used by one or more reports.");
        }

        var old = Map(entity);
        await _repository.DeleteAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(nameof(DataSource), id, AuditActions.Delete, performedBy, old, cancellationToken: cancellationToken);
    }

    private DataSourceDto Map(DataSource d) =>
        new(
            d.DataSourceId,
            d.DataSourceName,
            d.DataSourceType,
            d.ConnectionReference,
            !string.IsNullOrWhiteSpace(d.ConnectionString),
            !string.IsNullOrWhiteSpace(d.ConnectionString) || HasConfiguredConnection(d.ConnectionReference),
            d.IsActive);

    private bool HasConfiguredConnection(string connectionReference)
    {
        if (string.IsNullOrWhiteSpace(connectionReference))
        {
            return false;
        }

        var envKey = $"ConnectionReferences__{connectionReference}";
        var nestedEnvKey = $"ConnectionReferences__Values__{connectionReference}";
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(envKey)) ||
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(nestedEnvKey)))
        {
            return true;
        }

        return _connectionReferences.CurrentValue.Values.TryGetValue(connectionReference, out var value) &&
            !string.IsNullOrWhiteSpace(value);
    }

    private static string NormalizeConnectionReference(string connectionReference)
    {
        if (string.IsNullOrWhiteSpace(connectionReference))
        {
            throw new ArgumentException("Connection reference is required.", nameof(connectionReference));
        }

        return connectionReference.Trim();
    }

    private static string? NormalizeConnectionStringOrNull(string? connectionString) =>
        string.IsNullOrWhiteSpace(connectionString)
            ? null
            : connectionString.Trim()
                .Replace("(localdb)\\\\", "(localdb)\\", StringComparison.OrdinalIgnoreCase)
                .Replace("localhost\\\\", "localhost\\", StringComparison.OrdinalIgnoreCase)
                .Replace(".\\\\", ".\\", StringComparison.OrdinalIgnoreCase);
}
