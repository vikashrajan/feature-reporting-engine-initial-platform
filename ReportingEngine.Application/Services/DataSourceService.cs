using ReportingEngine.Application.Abstractions.Execution;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Application.DTOs;
using ReportingEngine.Domain.Entities;
using ReportingEngine.Domain.Enums;

namespace ReportingEngine.Application.Services;

public sealed class DataSourceService : IDataSourceService
{
    private readonly IDataSourceRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;

    public DataSourceService(IDataSourceRepository repository, IUnitOfWork unitOfWork, IAuditService audit)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _audit = audit;
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
        var entity = new DataSource
        {
            DataSourceName = request.DataSourceName.Trim(),
            DataSourceType = request.DataSourceType.Trim().ToUpperInvariant(),
            ConnectionReference = request.ConnectionReference.Trim(),
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
        entity.DataSourceName = request.DataSourceName.Trim();
        entity.DataSourceType = request.DataSourceType.Trim().ToUpperInvariant();
        entity.ConnectionReference = request.ConnectionReference.Trim();
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

    private static DataSourceDto Map(DataSource d) =>
        new(d.DataSourceId, d.DataSourceName, d.DataSourceType, d.ConnectionReference, d.IsActive);
}
