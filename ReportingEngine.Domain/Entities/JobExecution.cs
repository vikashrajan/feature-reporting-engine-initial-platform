namespace ReportingEngine.Domain.Entities;

public class JobExecution
{
    public long ExecutionId { get; set; }
    public long ReportId { get; set; }
    public DateTime? ScheduledTime { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public long? RecordCount { get; set; }
    public int? FileCount { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTime CreatedDate { get; set; }

    public ReportDefinition Report { get; set; } = null!;
    public ICollection<FileExecution> Files { get; set; } = new List<FileExecution>();
}
