using ReportingEngine.Application.Abstractions.Execution;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Application.DTOs;
using ReportingEngine.Application.Options;
using ReportingEngine.Domain.Entities;
using ReportingEngine.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ReportingEngine.Application.Services;

public sealed class DataSourceService : IDataSourceService
{
    private readonly IDataSourceRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;
    private readonly IOptionsMonitor<ConnectionReferencesOptions> _connectionReferences;
    private readonly ILogger<DataSourceService> _logger;

    public DataSourceService(
        IDataSourceRepository repository,
        IUnitOfWork unitOfWork,
        IAuditService audit,
        IOptionsMonitor<ConnectionReferencesOptions> connectionReferences,
        ILogger<DataSourceService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _connectionReferences = connectionReferences;
        _logger = logger;
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
            IsActive = true,
            CreatedBy = performedBy,
            CreatedDate = DateTime.UtcNow
        };

        PersistConnectionReference(connectionReference, request.ConnectionString);

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
        entity.IsActive = request.IsActive;
        entity.ModifiedBy = performedBy;
        entity.ModifiedDate = DateTime.UtcNow;

        PersistConnectionReference(connectionReference, request.ConnectionString);

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
        new(d.DataSourceId, d.DataSourceName, d.DataSourceType, d.ConnectionReference, HasConfiguredConnection(d.ConnectionReference), d.IsActive);

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

    private void PersistConnectionReference(string connectionReference, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        try
        {
            foreach (var filePath in GetLocalSettingsCandidatePaths())
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                var json = File.Exists(filePath) ? File.ReadAllText(filePath) : "{}";
                var node = System.Text.Json.Nodes.JsonNode.Parse(json)?.AsObject()
                    ?? new System.Text.Json.Nodes.JsonObject();

                if (node["ConnectionReferences"] is not System.Text.Json.Nodes.JsonObject refs)
                {
                    refs = new System.Text.Json.Nodes.JsonObject();
                    node["ConnectionReferences"] = refs;
                }

                if (refs["Values"] is not System.Text.Json.Nodes.JsonObject values)
                {
                    values = new System.Text.Json.Nodes.JsonObject();
                    refs["Values"] = values;
                }

                values[connectionReference] = connectionString.Trim();

                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(filePath, node.ToJsonString(options));
                _logger.LogInformation("Persisted data source connection reference {ConnectionReference} to local override {FilePath}",
                    connectionReference, filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist data source connection reference {ConnectionReference} to local override", connectionReference);
        }
    }

    private static IReadOnlyList<string> GetLocalSettingsCandidatePaths()
    {
        var paths = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "appsettings.Local.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Local.json")
        };

        foreach (var root in GetRepositoryRoots())
        {
            paths.Add(Path.Combine(root, "ReportingEngine.Admin", "appsettings.Local.json"));
            paths.Add(Path.Combine(root, "ReportingEngine.Worker", "appsettings.Local.json"));
        }

        return paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> GetRepositoryRoots()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ReportingEngine.slnx")) ||
                    Directory.Exists(Path.Combine(directory.FullName, "ReportingEngine.Admin")))
                {
                    yield return directory.FullName;
                    break;
                }

                directory = directory.Parent;
            }
        }
    }
}
