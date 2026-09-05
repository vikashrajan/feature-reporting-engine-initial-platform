using System.Data;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using ReportingEngine.Application.Abstractions.Data;
using ReportingEngine.Application.Options;
using ReportingEngine.Domain.Enums;

namespace ReportingEngine.Infrastructure.Data;

public sealed class SqlDataSourceProvider : IDataSourceProvider
{
    private readonly IConnectionStringResolver _connectionStringResolver;
    private readonly ExecutionOptions _executionOptions;

    public SqlDataSourceProvider(
        IConnectionStringResolver connectionStringResolver,
        IOptions<ExecutionOptions> executionOptions)
    {
        _connectionStringResolver = connectionStringResolver;
        _executionOptions = executionOptions.Value;
    }

    public string ProviderType => DataSourceTypes.Sql;

    public Task<IAsyncEnumerable<IReadOnlyDictionary<string, object?>>> ExecuteQueryAsync(
        string connectionReference,
        string queryText,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _connectionStringResolver.Resolve(connectionReference);
        return Task.FromResult(ExecuteInternalAsync(connectionString, queryText, parameters, cancellationToken));
    }

    private async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ExecuteInternalAsync(
        string connectionString,
        string queryText,
        IReadOnlyDictionary<string, object?> parameters,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = queryText;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = _executionOptions.CommandTimeoutSeconds;

        foreach (var parameter in parameters)
        {
            var name = parameter.Key.StartsWith('@') ? parameter.Key : "@" + parameter.Key;
            command.Parameters.AddWithValue(name, parameter.Value ?? DBNull.Value);
        }

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
        var fieldCount = reader.FieldCount;
        var names = Enumerable.Range(0, fieldCount).Select(reader.GetName).ToArray();

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < fieldCount; i++)
            {
                row[names[i]] = await reader.IsDBNullAsync(i, cancellationToken) ? null : reader.GetValue(i);
            }

            yield return row;
        }
    }
}
