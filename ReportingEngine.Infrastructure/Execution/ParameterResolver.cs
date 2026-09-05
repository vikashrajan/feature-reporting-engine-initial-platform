using ReportingEngine.Application.Abstractions.Execution;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Domain.Enums;

namespace ReportingEngine.Infrastructure.Execution;

public sealed class ParameterResolver : IParameterResolver
{
    private readonly IReportRepository _reportRepository;
    private readonly IJobExecutionRepository _executionRepository;

    public ParameterResolver(IReportRepository reportRepository, IJobExecutionRepository executionRepository)
    {
        _reportRepository = reportRepository;
        _executionRepository = executionRepository;
    }

    public async Task<IReadOnlyDictionary<string, object?>> ResolveAsync(
        long reportId,
        DateTime currentExecutionUtc,
        CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdWithDetailsAsync(reportId, cancellationToken)
            ?? throw new KeyNotFoundException($"Report {reportId} was not found.");

        var previous = await _executionRepository.GetLastSuccessfulAsync(reportId, cancellationToken);
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in report.Parameters)
        {
            var value = parameter.ValueSource.ToUpperInvariant() switch
            {
                ParameterValueSources.Static => ConvertValue(parameter.ParameterType, parameter.ParameterValue),
                ParameterValueSources.System => ResolveSystem(parameter.ParameterName, currentExecutionUtc),
                ParameterValueSources.PreviousExecution => previous?.CompletedAt ?? previous?.StartedAt ?? DateTime.UnixEpoch,
                ParameterValueSources.CurrentExecution => currentExecutionUtc,
                _ => throw new InvalidOperationException($"Unsupported parameter value source '{parameter.ValueSource}'.")
            };

            // Special well-known names for incremental extraction examples
            if (parameter.ParameterName.Equals("PreviousSuccessfulExecution", StringComparison.OrdinalIgnoreCase)
                || parameter.ParameterName.Equals("@PreviousSuccessfulExecution", StringComparison.OrdinalIgnoreCase))
            {
                value = previous?.CompletedAt ?? previous?.StartedAt ?? DateTime.UnixEpoch;
            }

            if (parameter.ParameterName.Equals("CurrentExecution", StringComparison.OrdinalIgnoreCase)
                || parameter.ParameterName.Equals("@CurrentExecution", StringComparison.OrdinalIgnoreCase))
            {
                value = currentExecutionUtc;
            }

            var key = parameter.ParameterName.TrimStart('@');
            result[key] = value;
        }

        return result;
    }

    private static object? ResolveSystem(string name, DateTime currentExecutionUtc) =>
        name.TrimStart('@').ToUpperInvariant() switch
        {
            "UTCNOW" => currentExecutionUtc,
            "TODAY" => currentExecutionUtc.Date,
            _ => currentExecutionUtc
        };

    internal static object? ConvertValue(string parameterType, string? raw)
    {
        if (raw is null)
        {
            return null;
        }

        return parameterType.ToUpperInvariant() switch
        {
            ParameterTypes.String => raw,
            ParameterTypes.Int => int.Parse(raw),
            ParameterTypes.Decimal => decimal.Parse(raw),
            ParameterTypes.Date => DateTime.Parse(raw).Date,
            ParameterTypes.DateTime => DateTime.Parse(raw),
            ParameterTypes.Bool => bool.Parse(raw),
            _ => raw
        };
    }
}
