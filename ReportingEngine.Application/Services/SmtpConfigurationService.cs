using ReportingEngine.Application.Abstractions.Execution;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Application.DTOs;
using ReportingEngine.Domain.Entities;
using ReportingEngine.Domain.Enums;

namespace ReportingEngine.Application.Services;

public sealed class SmtpConfigurationService : ISmtpConfigurationService
{
    private readonly ISmtpConfigurationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;

    public SmtpConfigurationService(ISmtpConfigurationRepository repository, IUnitOfWork unitOfWork, IAuditService audit)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<IReadOnlyList<SmtpConfigurationDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await _repository.GetAllAsync(cancellationToken)).Select(Map).ToList();

    public async Task<SmtpConfigurationDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(id, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<SmtpConfigurationDto> CreateAsync(CreateSmtpConfigurationRequest request, string performedBy, CancellationToken cancellationToken = default)
    {
        Validate(request.ProfileName, request.Host, request.Port, request.FromAddress, request.UserName, request.Password, request.UseFileDrop, request.FileDropPath);
        var entity = new SmtpConfiguration
        {
            ProfileName = request.ProfileName.Trim(),
            Host = NormalizeHost(request.Host, request.UseFileDrop),
            Port = request.Port <= 0 ? 587 : request.Port,
            EnableSsl = request.EnableSsl,
            FromAddress = request.FromAddress.Trim(),
            FromDisplayName = request.FromDisplayName?.Trim() ?? string.Empty,
            UserName = TrimToNull(request.UserName),
            Password = TrimToNull(request.Password),
            TimeoutSeconds = request.TimeoutSeconds <= 0 ? 120 : request.TimeoutSeconds,
            UseFileDrop = request.UseFileDrop,
            FileDropPath = TrimToNull(request.FileDropPath),
            IsActive = true,
            CreatedBy = performedBy,
            CreatedDate = DateTime.UtcNow
        };

        await _repository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(nameof(SmtpConfiguration), entity.SmtpConfigId, AuditActions.Create, performedBy, newValue: Safe(entity), cancellationToken: cancellationToken);
        return Map(entity);
    }

    public async Task<SmtpConfigurationDto> UpdateAsync(long id, UpdateSmtpConfigurationRequest request, string performedBy, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"SMTP profile {id} was not found.");
        var password = string.IsNullOrWhiteSpace(request.Password) ? entity.Password : request.Password.Trim();
        Validate(request.ProfileName, request.Host, request.Port, request.FromAddress, request.UserName, password, request.UseFileDrop, request.FileDropPath);

        var old = Safe(entity);
        entity.ProfileName = request.ProfileName.Trim();
        entity.Host = NormalizeHost(request.Host, request.UseFileDrop);
        entity.Port = request.Port <= 0 ? 587 : request.Port;
        entity.EnableSsl = request.EnableSsl;
        entity.FromAddress = request.FromAddress.Trim();
        entity.FromDisplayName = request.FromDisplayName?.Trim() ?? string.Empty;
        entity.UserName = TrimToNull(request.UserName);
        entity.Password = password;
        entity.TimeoutSeconds = request.TimeoutSeconds <= 0 ? 120 : request.TimeoutSeconds;
        entity.UseFileDrop = request.UseFileDrop;
        entity.FileDropPath = TrimToNull(request.FileDropPath);
        entity.IsActive = request.IsActive;
        entity.ModifiedBy = performedBy;
        entity.ModifiedDate = DateTime.UtcNow;

        await _repository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(nameof(SmtpConfiguration), entity.SmtpConfigId, AuditActions.Update, performedBy, old, Safe(entity), cancellationToken);
        return Map(entity);
    }

    public async Task DeleteAsync(long id, string performedBy, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"SMTP profile {id} was not found.");
        if (await _repository.IsReferencedAsync(id, cancellationToken))
        {
            throw new InvalidOperationException("Cannot delete this SMTP profile because it is used by one or more delivery or failure notification profiles.");
        }

        var old = Safe(entity);
        await _repository.DeleteAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(nameof(SmtpConfiguration), id, AuditActions.Delete, performedBy, old, cancellationToken: cancellationToken);
    }

    private static void Validate(string profileName, string host, int port, string fromAddress, string? userName, string? password, bool useFileDrop, string? fileDropPath)
    {
        if (string.IsNullOrWhiteSpace(profileName)) throw new InvalidOperationException("SMTP profile name is required.");
        if (string.IsNullOrWhiteSpace(fromAddress)) throw new InvalidOperationException("SMTP From email is required.");
        if (port <= 0) throw new InvalidOperationException("SMTP port must be greater than zero.");
        if (useFileDrop)
        {
            if (string.IsNullOrWhiteSpace(fileDropPath)) throw new InvalidOperationException("File drop path is required when SMTP file-drop mode is enabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(host) || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || string.Equals(host, "filedrop", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Live SMTP host is required.");
        }

        if (!string.IsNullOrWhiteSpace(userName) && string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("SMTP password is required when SMTP username is configured.");
        }
    }

    private static string NormalizeHost(string host, bool useFileDrop) => useFileDrop ? "filedrop" : host.Trim();
    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static SmtpConfigurationDto Map(SmtpConfiguration x) =>
        new(x.SmtpConfigId, x.ProfileName, x.Host, x.Port, x.EnableSsl, x.FromAddress, x.FromDisplayName, x.UserName, !string.IsNullOrWhiteSpace(x.Password), x.TimeoutSeconds, x.UseFileDrop, x.FileDropPath, x.IsActive);
    private static object Safe(SmtpConfiguration x) => new { x.SmtpConfigId, x.ProfileName, x.Host, x.Port, x.EnableSsl, x.FromAddress, x.FromDisplayName, x.UserName, HasPassword = !string.IsNullOrWhiteSpace(x.Password), x.TimeoutSeconds, x.UseFileDrop, x.FileDropPath, x.IsActive };
}
