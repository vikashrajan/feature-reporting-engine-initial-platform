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
        var customerCode = Required(request.CustomerCode, "Customer code");
        var customerName = Required(request.CustomerName, "Customer name");
        var timeZoneId = NormalizeTimeZone(request.TimeZoneId);

        var existing = await _repository.GetByCodeAsync(customerCode, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Customer code '{customerCode}' already exists.");
        }

        var entity = new Customer
        {
            CustomerCode = customerCode,
            CustomerName = customerName,
            TimeZoneId = timeZoneId,
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

        var customerName = Required(request.CustomerName, "Customer name");
        var timeZoneId = NormalizeTimeZone(request.TimeZoneId);

        var old = Map(entity);
        entity.CustomerName = customerName;
        entity.TimeZoneId = timeZoneId;
        entity.IsActive = request.IsActive;
        entity.ModifiedBy = performedBy;
        entity.ModifiedDate = DateTime.UtcNow;

        await _repository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(nameof(Customer), entity.CustomerId, AuditActions.Update, performedBy, old, Map(entity), cancellationToken);
        return Map(entity);
    }

    public async Task DeleteAsync(long id, string performedBy, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer {id} was not found.");

        if (await _repository.IsReferencedAsync(id, cancellationToken))
        {
            throw new InvalidOperationException("Cannot delete this customer because it is used by one or more reports.");
        }

        var old = Map(entity);
        await _repository.DeleteAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(nameof(Customer), id, AuditActions.Delete, performedBy, old, cancellationToken: cancellationToken);
    }

    private static CustomerDto Map(Customer c) =>
        new(c.CustomerId, c.CustomerCode, c.CustomerName, c.TimeZoneId, c.IsActive);

    private static string Required(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{label} is required.");
        }

        return value.Trim();
    }

    private static string NormalizeTimeZone(string? timeZoneId)
    {
        var value = string.IsNullOrWhiteSpace(timeZoneId) ? "UTC" : timeZoneId.Trim();
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(value);
            return value;
        }
        catch (TimeZoneNotFoundException)
        {
            throw new InvalidOperationException($"Time zone '{value}' was not found. Please select a valid time zone from the list.");
        }
        catch (InvalidTimeZoneException)
        {
            throw new InvalidOperationException($"Time zone '{value}' is invalid. Please select a valid time zone from the list.");
        }
    }
}
