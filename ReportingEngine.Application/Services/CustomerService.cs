using ReportingEngine.Application.Abstractions.Execution;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Application.DTOs;
using ReportingEngine.Domain.Entities;
using ReportingEngine.Domain.Enums;

namespace ReportingEngine.Application.Services;

public sealed class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;

    public CustomerService(ICustomerRepository repository, IUnitOfWork unitOfWork, IAuditService audit)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<CustomerDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(id, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, string performedBy, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByCodeAsync(request.CustomerCode, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Customer code '{request.CustomerCode}' already exists.");
        }

        var entity = new Customer
        {
            CustomerCode = request.CustomerCode.Trim(),
            CustomerName = request.CustomerName.Trim(),
            TimeZoneId = request.TimeZoneId,
            IsActive = true,
            CreatedBy = performedBy,
            CreatedDate = DateTime.UtcNow
        };

        await _repository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(nameof(Customer), entity.CustomerId, AuditActions.Create, performedBy, newValue: Map(entity), cancellationToken: cancellationToken);
        return Map(entity);
    }

    public async Task<CustomerDto> UpdateAsync(long id, UpdateCustomerRequest request, string performedBy, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer {id} was not found.");

        var old = Map(entity);
        entity.CustomerName = request.CustomerName.Trim();
        entity.TimeZoneId = request.TimeZoneId;
        entity.IsActive = request.IsActive;
        entity.ModifiedBy = performedBy;
        entity.ModifiedDate = DateTime.UtcNow;

        await _repository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(nameof(Customer), entity.CustomerId, AuditActions.Update, performedBy, old, Map(entity), cancellationToken);
        return Map(entity);
    }

    private static CustomerDto Map(Customer c) =>
        new(c.CustomerId, c.CustomerCode, c.CustomerName, c.TimeZoneId, c.IsActive);
}
