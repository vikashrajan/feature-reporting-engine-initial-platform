using Microsoft.Azure.Cosmos;
using ReportingEngine.Application.Abstractions.Data;
using ReportingEngine.Domain.Enums;
using System.Runtime.CompilerServices;

namespace ReportingEngine.Infrastructure.Data;

public sealed class CosmosDataSourceProvider : IDataSourceProvider
{
    private readonly IConnectionStringResolver _connectionStringResolver;

    public CosmosDataSourceProvider(IConnectionStringResolver connectionStringResolver) =>
        _connectionStringResolver = connectionStringResolver;

    public string ProviderType => DataSourceTypes.Cosmos;

    public Task<IAsyncEnumerable<IReadOnlyDictionary<string, object?>>> ExecuteQueryAsync(
        string connectionReference,
        string queryText,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        // Connection reference format: "AccountEndpoint=...;AccountKey=...;Database=db;Container=container"
        var connectionString = _connectionStringResolver.Resolve(connectionReference);
        return Task.FromResult(ExecuteInternalAsync(connectionString, queryText, parameters, cancellationToken));
    }

    private async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ExecuteInternalAsync(
        string connectionString,
        string queryText,
        IReadOnlyDictionary<string, object?> parameters,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var parts = ParseConnection(connectionString);
        using var client = new CosmosClient(parts.AccountEndpoint, parts.AccountKey);
        var container = client.GetContainer(parts.Database, parts.Container);

        var definition = new QueryDefinition(queryText);
        foreach (var parameter in parameters)
        {
            var name = parameter.Key.StartsWith('@') ? parameter.Key : "@" + parameter.Key;
            definition = definition.WithParameter(name, parameter.Value);
        }

        using var iterator = container.GetItemQueryIterator<Dictionary<string, object?>>(definition);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            foreach (var item in page)
            {
                yield return item;
            }
        }
    }

    private static (string AccountEndpoint, string AccountKey, string Database, string Container) ParseConnection(string connectionString)
    {
        var map = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => p[1], StringComparer.OrdinalIgnoreCase);

        if (!map.TryGetValue("AccountEndpoint", out var endpoint) ||
            !map.TryGetValue("AccountKey", out var key) ||
            !map.TryGetValue("Database", out var database) ||
            !map.TryGetValue("Container", out var container))
        {
            throw new InvalidOperationException(
                "Cosmos connection reference must include AccountEndpoint, AccountKey, Database, and Container.");
        }

        return (endpoint, key, database, container);
    }
}

public sealed class DataSourceProviderResolver : IDataSourceProviderResolver
{
    private readonly IReadOnlyDictionary<string, IDataSourceProvider> _providers;

    public DataSourceProviderResolver(IEnumerable<IDataSourceProvider> providers) =>
        _providers = providers.ToDictionary(p => p.ProviderType, StringComparer.OrdinalIgnoreCase);

    public bool IsSupported(string dataSourceType) =>
        _providers.ContainsKey(dataSourceType);

    public IDataSourceProvider Resolve(string dataSourceType)
    {
        if (_providers.TryGetValue(dataSourceType, out var provider))
        {
            return provider;
        }

        throw new NotSupportedException(
            $"Data source type '{dataSourceType}' is not implemented. Supported now: SQL, COSMOS. Future: POSTGRES, ORACLE, API.");
    }
}
