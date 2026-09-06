using ReportingEngine.Application.Abstractions.Execution;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Application.DTOs;
using ReportingEngine.Domain.Entities;
using ReportingEngine.Domain.Enums;

namespace ReportingEngine.Application.Services;

public sealed class FileConfigurationService : IFileConfigurationService
{
    private readonly IFileConfigurationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;

    public FileConfigurationService(IFileConfigurationRepository repository, IUnitOfWork unitOfWork, IAuditService audit)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<IReadOnlyList<FileConfigurationDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<FileConfigurationDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(id, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<FileConfigurationDto> CreateAsync(CreateFileConfigurationRequest request, string performedBy, CancellationToken cancellationToken = default)
    {
        Validate(request.FileFormat, request.SplitEnabled, request.SplitType, request.SplitValue, request.CompressionType, request.ZipBatchSize);

        var entity = new FileConfiguration
        {
            ConfigurationName = request.ConfigurationName.Trim(),
            FileFormat = request.FileFormat.Trim().ToUpperInvariant(),
            FileNamePattern = request.FileNamePattern.Trim(),
            SplitEnabled = request.SplitEnabled,
            SplitType = request.SplitType,
            SplitValue = request.SplitValue,
            CompressionType = request.CompressionType,
            ZipBatchSize = NormalizeZipBatchSize(request.ZipBatchSize),
            KeepLocalFiles = request.KeepLocalFiles,
            EncryptionEnabled = request.EncryptionEnabled,
            CreatedBy = performedBy,
            CreatedDate = DateTime.UtcNow
        };

        await _repository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(nameof(FileConfiguration), entity.FileConfigId, AuditActions.Create, performedBy, newValue: Map(entity), cancellationToken: cancellationToken);
        return Map(entity);
    }

    public async Task<FileConfigurationDto> UpdateAsync(long id, UpdateFileConfigurationRequest request, string performedBy, CancellationToken cancellationToken = default)
    {
        Validate(request.FileFormat, request.SplitEnabled, request.SplitType, request.SplitValue, request.CompressionType, request.ZipBatchSize);

        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"File configuration {id} was not found.");

        var old = Map(entity);
        entity.ConfigurationName = request.ConfigurationName.Trim();
        entity.FileFormat = request.FileFormat.Trim().ToUpperInvariant();
        entity.FileNamePattern = request.FileNamePattern.Trim();
        entity.SplitEnabled = request.SplitEnabled;
        entity.SplitType = request.SplitType;
        entity.SplitValue = request.SplitValue;
        entity.CompressionType = request.CompressionType;
        entity.ZipBatchSize = NormalizeZipBatchSize(request.ZipBatchSize);
        entity.KeepLocalFiles = request.KeepLocalFiles;
        entity.EncryptionEnabled = request.EncryptionEnabled;
        entity.ModifiedBy = performedBy;
        entity.ModifiedDate = DateTime.UtcNow;

        await _repository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(nameof(FileConfiguration), entity.FileConfigId, AuditActions.Update, performedBy, old, Map(entity), cancellationToken);
        return Map(entity);
    }

    public async Task DeleteAsync(long id, string performedBy, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"File configuration {id} was not found.");

        if (await _repository.IsReferencedAsync(id, cancellationToken))
        {
            throw new InvalidOperationException("Cannot delete this file configuration because it is used by one or more reports.");
        }

        var old = Map(entity);
        await _repository.DeleteAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(nameof(FileConfiguration), id, AuditActions.Delete, performedBy, old, cancellationToken: cancellationToken);
    }

    internal static void Validate(string fileFormat, bool splitEnabled, string? splitType, long? splitValue, string? compressionType = null, int? zipBatchSize = null)
    {
        var format = fileFormat.Trim().ToUpperInvariant();
        var formats = new[] { FileFormats.Csv, FileFormats.Excel, FileFormats.Json, FileFormats.Xml, FileFormats.Txt };
        if (!formats.Contains(format))
        {
            throw new InvalidOperationException($"Unsupported file format '{fileFormat}'.");
        }

        if (splitEnabled)
        {
            if (string.IsNullOrWhiteSpace(splitType) || splitValue is null or <= 0)
            {
                throw new InvalidOperationException("SplitType and positive SplitValue are required when SplitEnabled is true.");
            }

            var type = splitType.Trim().ToUpperInvariant();
            if (type is not (SplitTypes.RecordCount or SplitTypes.FileSize))
            {
                throw new InvalidOperationException($"Unsupported split type '{splitType}'.");
            }
        }

        if (zipBatchSize is <= 0)
        {
            throw new InvalidOperationException("ZipBatchSize must be positive when supplied.");
        }

        if (zipBatchSize is > 0 && !string.Equals(compressionType?.Trim(), CompressionTypes.Zip, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("ZipBatchSize can be used only when ZIP compression is enabled.");
        }
    }

    private static int? NormalizeZipBatchSize(int? zipBatchSize) => zipBatchSize is > 0 ? zipBatchSize : null;

    private static FileConfigurationDto Map(FileConfiguration f) =>
        new(f.FileConfigId, f.ConfigurationName, f.FileFormat, f.FileNamePattern, f.SplitEnabled, f.SplitType, f.SplitValue, f.CompressionType, f.ZipBatchSize, f.KeepLocalFiles, f.EncryptionEnabled);
}
