namespace ReportingEngine.Domain.Enums;

public static class DataSourceTypes
{
    public const string Sql = "SQL";
    public const string Cosmos = "COSMOS";
    public const string Postgres = "POSTGRES";
    public const string Oracle = "ORACLE";
    public const string Api = "API";
}

public static class ScheduleTypes
{
    public const string Cron = "CRON";
    public const string Daily = "DAILY";
    public const string Weekly = "WEEKLY";
    public const string Monthly = "MONTHLY";
    public const string Once = "ONCE";
}

public static class FileFormats
{
    public const string Csv = "CSV";
    public const string Excel = "EXCEL";
    public const string Json = "JSON";
    public const string Xml = "XML";
    public const string Txt = "TXT";
}

public static class SplitTypes
{
    public const string RecordCount = "RECORD_COUNT";
    public const string FileSize = "FILE_SIZE";
}

public static class CompressionTypes
{
    public const string Zip = "ZIP";
    public const string Gzip = "GZIP";
}

public static class DeliveryTypes
{
    public const string Email = "EMAIL";
    public const string Sftp = "SFTP";
    public const string Ftp = "FTP";
    public const string SharedFolder = "SHARED_FOLDER";
    public const string Blob = "BLOB";
}

public static class ReportStatuses
{
    public const string Draft = "DRAFT";
    public const string Active = "ACTIVE";
    public const string Paused = "PAUSED";
    public const string Retired = "RETIRED";
}

public static class ParameterTypes
{
    public const string String = "STRING";
    public const string Int = "INT";
    public const string Decimal = "DECIMAL";
    public const string Date = "DATE";
    public const string DateTime = "DATETIME";
    public const string Bool = "BOOL";
}

public static class ParameterValueSources
{
    public const string Static = "STATIC";
    public const string System = "SYSTEM";
    public const string PreviousExecution = "PREVIOUS_EXECUTION";
    public const string CurrentExecution = "CURRENT_EXECUTION";
}

public static class JobExecutionStatuses
{
    public const string Created = "CREATED";
    public const string Running = "RUNNING";
    public const string Success = "SUCCESS";
    public const string Failed = "FAILED";
    public const string Retrying = "RETRYING";
    public const string Cancelled = "CANCELLED";
}

public static class FileGenerationStatuses
{
    public const string Pending = "PENDING";
    public const string Generated = "GENERATED";
    public const string Failed = "FAILED";
}

public static class FileDeliveryStatuses
{
    public const string Pending = "PENDING";
    public const string Delivered = "DELIVERED";
    public const string Failed = "FAILED";
    public const string Skipped = "SKIPPED";
}

public static class AuditActions
{
    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string Delete = "DELETE";
    public const string Activate = "ACTIVATE";
    public const string Pause = "PAUSE";
    public const string Retire = "RETIRE";
    public const string ManualRun = "MANUAL_RUN";
    public const string Resume = "RESUME";
}
