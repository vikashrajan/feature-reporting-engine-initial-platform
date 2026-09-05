namespace ReportingEngine.Application.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public string ConnectionString { get; set; } = string.Empty;
}

public sealed class HangfireOptions
{
    public const string SectionName = "Hangfire";
    public int WorkerCount { get; set; } = 5;
    public string SchemaName { get; set; } = "HangFire";
    public bool DashboardEnabled { get; set; } = true;
    public string DashboardPath { get; set; } = "/hangfire";
}

public sealed class RetryOptions
{
    public const string SectionName = "Retry";
    public int MaxRetryCount { get; set; } = 3;
    public int InitialDelaySeconds { get; set; } = 30;
    public double BackoffMultiplier { get; set; } = 2.0;
}

public sealed class ExecutionOptions
{
    public const string SectionName = "Execution";
    public int CommandTimeoutSeconds { get; set; } = 120;
    public string TemporaryFilePath { get; set; } = "./temp-reports";
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public bool EnableSsl { get; set; }
    public string FromAddress { get; set; } = "noreply@reportingengine.local";
    public string FromDisplayName { get; set; } = "ReportingEngine";
    public string? UserName { get; set; }
    public string? Password { get; set; }
    /// <summary>When true, emails are written to disk instead of SMTP (local/dev).</summary>
    public bool UseFileDrop { get; set; } = true;
    public string FileDropPath { get; set; } = "./temp-emails";
}

public sealed class ConnectionReferencesOptions
{
    public const string SectionName = "ConnectionReferences";
    public Dictionary<string, string> Values { get; set; } = new();
}

public sealed class SecretReferencesOptions
{
    public const string SectionName = "SecretReferences";
    public Dictionary<string, string> Values { get; set; } = new();
}

public sealed class SeedOptions
{
    public const string SectionName = "Seed";
    public bool Enabled { get; set; } = true;
}
