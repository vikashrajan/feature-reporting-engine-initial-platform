namespace ReportingEngine.Application.Abstractions.Data;

public interface IDataSourceProvider
{
    string ProviderType { get; }
    Task<IAsyncEnumerable<IReadOnlyDictionary<string, object?>>> ExecuteQueryAsync(
        string connectionReference,
        string queryText,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default);
}

public interface IDataSourceProviderResolver
{
    IDataSourceProvider Resolve(string dataSourceType);
    bool IsSupported(string dataSourceType);
}

public interface IConnectionStringResolver
{
    string Resolve(string connectionReference);
}
