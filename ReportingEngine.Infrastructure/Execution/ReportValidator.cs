using ReportingEngine.Application.Abstractions.Data;
using ReportingEngine.Application.Abstractions.Delivery;
using ReportingEngine.Application.Abstractions.Execution;
using ReportingEngine.Application.Abstractions.Files;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Domain.Enums;

namespace ReportingEngine.Infrastructure.Execution;

public sealed class ReportValidator : IReportValidator
{
    private readonly IReportRepository _reportRepository;
    private readonly IDataSourceProviderResolver _dataSourceProviderResolver;
    private readonly IFileGeneratorResolver _fileGeneratorResolver;
    private readonly IDeliveryProviderResolver _deliveryProviderResolver;

    public ReportValidator(
        IReportRepository reportRepository,
        IDataSourceProviderResolver dataSourceProviderResolver,
        IFileGeneratorResolver fileGeneratorResolver,
        IDeliveryProviderResolver deliveryProviderResolver)
    {
        _reportRepository = reportRepository;
        _dataSourceProviderResolver = dataSourceProviderResolver;
        _fileGeneratorResolver = fileGeneratorResolver;
        _deliveryProviderResolver = deliveryProviderResolver;
    }

    public async Task<ReportValidationResult> ValidateForActivationAsync(long reportId, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var report = await _reportRepository.GetByIdWithDetailsAsync(reportId, cancellationToken);
        if (report is null)
        {
            return ReportValidationResult.Failure($"Report {reportId} was not found.");
        }

        if (report.Customer is null || !report.Customer.IsActive)
        {
            errors.Add("Customer must exist and be active.");
        }

        if (report.DataSource is null || !report.DataSource.IsActive)
        {
            errors.Add("DataSource must exist and be active.");
        }
        else if (!_dataSourceProviderResolver.IsSupported(report.DataSource.DataSourceType))
        {
            errors.Add($"Data source provider '{report.DataSource.DataSourceType}' is not supported.");
        }

        if (report.Schedule is null || !report.Schedule.IsActive)
        {
            errors.Add("Schedule must exist and be active.");
        }
        else
        {
            try
            {
                _ = TimeZoneInfo.FindSystemTimeZoneById(report.Schedule.TimeZoneId);
            }
            catch
            {
                errors.Add($"Schedule TimeZoneId '{report.Schedule.TimeZoneId}' is invalid.");
            }

            if (report.Schedule.ScheduleType == ScheduleTypes.Cron && string.IsNullOrWhiteSpace(report.Schedule.CronExpression))
            {
                errors.Add("CronExpression is required for CRON schedules.");
            }
        }

        if (report.FileConfiguration is null)
        {
            errors.Add("FileConfiguration must exist.");
        }
        else
        {
            if (!_fileGeneratorResolver.IsSupported(report.FileConfiguration.FileFormat))
            {
                errors.Add($"File format '{report.FileConfiguration.FileFormat}' is not supported.");
            }

            if (report.FileConfiguration.SplitEnabled)
            {
                if (string.IsNullOrWhiteSpace(report.FileConfiguration.SplitType) || report.FileConfiguration.SplitValue is null or <= 0)
                {
                    errors.Add("Split configuration is invalid.");
                }
            }
        }

        if (report.DeliveryConfiguration is null || !report.DeliveryConfiguration.IsActive)
        {
            errors.Add("DeliveryConfiguration must exist and be active.");
        }
        else if (!_deliveryProviderResolver.IsSupported(report.DeliveryConfiguration.DeliveryType))
        {
            errors.Add($"Delivery provider '{report.DeliveryConfiguration.DeliveryType}' is not supported for activation.");
        }

        if (string.IsNullOrWhiteSpace(report.QueryText))
        {
            errors.Add("QueryText is required.");
        }

        foreach (var parameter in report.Parameters)
        {
            var sources = new[]
            {
                ParameterValueSources.Static,
                ParameterValueSources.System,
                ParameterValueSources.PreviousExecution,
                ParameterValueSources.CurrentExecution
            };

            if (!sources.Contains(parameter.ValueSource, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"Parameter '{parameter.ParameterName}' has invalid ValueSource.");
            }

            if (parameter.ValueSource.Equals(ParameterValueSources.Static, StringComparison.OrdinalIgnoreCase)
                && parameter.ParameterValue is null)
            {
                errors.Add($"Static parameter '{parameter.ParameterName}' requires ParameterValue.");
            }
        }

        return errors.Count == 0 ? ReportValidationResult.Success() : ReportValidationResult.Failure(errors.ToArray());
    }
}
