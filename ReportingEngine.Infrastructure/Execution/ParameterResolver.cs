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
                ParameterValueSources.System => ResolveSystem(
                    string.IsNullOrWhiteSpace(parameter.ParameterValue) ? parameter.ParameterName : parameter.ParameterValue,
                    currentExecutionUtc,
                    report.Customer?.TimeZoneId),
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

    private static object? ResolveSystem(string name, DateTime currentExecutionUtc, string? timeZoneId)
    {
        var timeZone = ResolveTimeZone(timeZoneId);
        var currentLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(currentExecutionUtc, DateTimeKind.Utc), timeZone);
        var todayStartLocal = currentLocal.Date;
        var currentMonthStartLocal = new DateTime(currentLocal.Year, currentLocal.Month, 1);
        var previousMonthStartLocal = currentMonthStartLocal.AddMonths(-1);

        return name.TrimStart('@').ToUpperInvariant() switch
        {
            "UTCNOW" => currentExecutionUtc,
            "NOW" => currentExecutionUtc,
            "CURRENT_EXECUTION" => currentExecutionUtc,
            "TODAY" => ToUtc(todayStartLocal, timeZone),
            "TODAY_START" => ToUtc(todayStartLocal, timeZone),
            "TODAY_END" => ToUtc(todayStartLocal.AddDays(1), timeZone),
            "YESTERDAY" => ToUtc(todayStartLocal.AddDays(-1), timeZone),
            "YESTERDAY_START" => ToUtc(todayStartLocal.AddDays(-1), timeZone),
            "YESTERDAY_END" => ToUtc(todayStartLocal, timeZone),
            "LAST_7_DAYS_START" => ToUtc(todayStartLocal.AddDays(-7), timeZone),
            "LAST_30_DAYS_START" => ToUtc(todayStartLocal.AddDays(-30), timeZone),
            "CURRENT_MONTH_START" => ToUtc(currentMonthStartLocal, timeZone),
            "MONTH_START" => ToUtc(currentMonthStartLocal, timeZone),
            "CURRENT_MONTH_END" => ToUtc(currentMonthStartLocal.AddMonths(1), timeZone),
            "NEXT_MONTH_START" => ToUtc(currentMonthStartLocal.AddMonths(1), timeZone),
            "PREVIOUS_MONTH_START" => ToUtc(previousMonthStartLocal, timeZone),
            "LAST_MONTH_START" => ToUtc(previousMonthStartLocal, timeZone),
            "PREVIOUS_MONTH_END" => ToUtc(currentMonthStartLocal, timeZone),
            "LAST_MONTH_END" => ToUtc(currentMonthStartLocal, timeZone),
            _ => currentExecutionUtc
        };
    }

    private static DateTime ToUtc(DateTime localDateTime, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), timeZone);

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId) || string.Equals(timeZoneId, "UTC", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }

    public static object? ConvertValue(string parameterType, string? raw)
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
