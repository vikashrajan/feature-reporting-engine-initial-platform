namespace ReportingEngine.Application.DTOs;

public sealed record CustomerDto(long CustomerId, string CustomerCode, string CustomerName, string? TimeZoneId, bool IsActive);
public sealed record CreateCustomerRequest(string CustomerCode, string CustomerName, string? TimeZoneId);
public sealed record UpdateCustomerRequest(string CustomerName, string? TimeZoneId, bool IsActive);

public sealed record DataSourceDto(long DataSourceId, string DataSourceName, string DataSourceType, string ConnectionReference, bool IsActive);
public sealed record CreateDataSourceRequest(string DataSourceName, string DataSourceType, string ConnectionReference);
public sealed record UpdateDataSourceRequest(string DataSourceName, string DataSourceType, string ConnectionReference, bool IsActive);

public sealed record ScheduleDto(long ScheduleId, string ScheduleName, string ScheduleType, string? CronExpression, string TimeZoneId, DateTime? StartDate, DateTime? EndDate, bool IsActive);
public sealed record CreateScheduleRequest(string ScheduleName, string ScheduleType, string? CronExpression, string TimeZoneId, DateTime? StartDate, DateTime? EndDate);
public sealed record UpdateScheduleRequest(string ScheduleName, string ScheduleType, string? CronExpression, string TimeZoneId, DateTime? StartDate, DateTime? EndDate, bool IsActive);

public sealed record FileConfigurationDto(long FileConfigId, string ConfigurationName, string FileFormat, string FileNamePattern, bool SplitEnabled, string? SplitType, long? SplitValue, string? CompressionType, bool EncryptionEnabled);
public sealed record CreateFileConfigurationRequest(string ConfigurationName, string FileFormat, string FileNamePattern, bool SplitEnabled, string? SplitType, long? SplitValue, string? CompressionType, bool EncryptionEnabled);
public sealed record UpdateFileConfigurationRequest(string ConfigurationName, string FileFormat, string FileNamePattern, bool SplitEnabled, string? SplitType, long? SplitValue, string? CompressionType, bool EncryptionEnabled);

public sealed record DeliveryConfigurationDto(long DeliveryConfigId, string DeliveryName, string DeliveryType, string? DestinationReference, string? EmailTo, string? EmailCc, string? EmailBcc, string? EmailSubjectTemplate, string? EmailBodyTemplate, string? SecretReference, bool IsActive);
public sealed record CreateDeliveryConfigurationRequest(string DeliveryName, string DeliveryType, string? DestinationReference, string? EmailTo, string? EmailCc, string? EmailBcc, string? EmailSubjectTemplate, string? EmailBodyTemplate, string? SecretReference);
public sealed record UpdateDeliveryConfigurationRequest(string DeliveryName, string DeliveryType, string? DestinationReference, string? EmailTo, string? EmailCc, string? EmailBcc, string? EmailSubjectTemplate, string? EmailBodyTemplate, string? SecretReference, bool IsActive);

public sealed record ReportParameterDto(long ParameterId, long ReportId, string ParameterName, string ParameterType, string? ParameterValue, string ValueSource);
public sealed record ReportParameterInput(string ParameterName, string ParameterType, string? ParameterValue, string ValueSource);

public sealed record ReportDto(
    long ReportId,
    long CustomerId,
    string ReportCode,
    string ReportName,
    long DataSourceId,
    long ScheduleId,
    long FileConfigId,
    long DeliveryConfigId,
    string QueryText,
    string? Description,
    int VersionNumber,
    string Status,
    bool IsActive,
    IReadOnlyList<ReportParameterDto> Parameters);

public sealed record CreateReportRequest(
    long CustomerId,
    string ReportCode,
    string ReportName,
    long DataSourceId,
    long ScheduleId,
    long FileConfigId,
    long DeliveryConfigId,
    string QueryText,
    string? Description,
    IReadOnlyList<ReportParameterInput>? Parameters);

public sealed record UpdateReportRequest(
    string ReportName,
    long DataSourceId,
    long ScheduleId,
    long FileConfigId,
    long DeliveryConfigId,
    string QueryText,
    string? Description,
    IReadOnlyList<ReportParameterInput>? Parameters);

public sealed record FileExecutionDto(long FileExecutionId, int SequenceNumber, string FileName, string? FilePath, long? FileSizeBytes, long? RecordCount, string GenerationStatus, string DeliveryStatus, string? Checksum);

public sealed record JobExecutionDto(
    long ExecutionId,
    long ReportId,
    DateTime? ScheduledTime,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string Status,
    long? RecordCount,
    int? FileCount,
    int RetryCount,
    string? ErrorCode,
    string? ErrorMessage,
    IReadOnlyList<FileExecutionDto> Files);

public sealed record DashboardDto(int RunningReports, int SuccessfulReports, int FailedReports, int UpcomingReports);
