using Microsoft.Extensions.Options;
using ReportingEngine.Application.Abstractions.Data;
using ReportingEngine.Application.Abstractions.Execution;
using ReportingEngine.Application.Options;

namespace ReportingEngine.Infrastructure.Data;

public sealed class ConnectionStringResolver : IConnectionStringResolver
{
    private readonly IOptionsMonitor<ConnectionReferencesOptions> _options;

    public ConnectionStringResolver(IOptionsMonitor<ConnectionReferencesOptions> options) =>
        _options = options;

    public string Resolve(string connectionReference)
    {
        if (string.IsNullOrWhiteSpace(connectionReference))
        {
            throw new InvalidOperationException("Connection reference is required.");
        }

        if (LooksLikeConnectionString(connectionReference))
        {
            return connectionReference;
        }

        var envKey = $"ConnectionReferences__{connectionReference}";
        var nestedEnvKey = $"ConnectionReferences__Values__{connectionReference}";
        var fromEnv = Environment.GetEnvironmentVariable(envKey)
            ?? Environment.GetEnvironmentVariable(nestedEnvKey);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv;
        }

        if (_options.CurrentValue.Values.TryGetValue(connectionReference, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException(
            $"Connection reference '{connectionReference}' was not found. Configure ConnectionReferences:Values:{connectionReference} or environment variable {envKey} / {nestedEnvKey}.");
    }

    private static bool LooksLikeConnectionString(string value) =>
        value.Contains('=') && value.Contains(';');
}

public sealed class SecretResolver : ISecretResolver
{
    private readonly SecretReferencesOptions _options;

    public SecretResolver(IOptions<SecretReferencesOptions> options) =>
        _options = options.Value;

    public string? Resolve(string? secretReference)
    {
        if (string.IsNullOrWhiteSpace(secretReference))
        {
            return null;
        }

        var envKey = $"SecretReferences__{secretReference}";
        var fromEnv = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv;
        }

        return _options.Values.TryGetValue(secretReference, out var value) ? value : null;
    }
}
