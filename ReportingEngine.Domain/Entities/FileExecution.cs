namespace ReportingEngine.Domain.Entities;

public class FileExecution
{
    public long FileExecutionId { get; set; }
    public long ExecutionId { get; set; }
    public int SequenceNumber { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public long? FileSizeBytes { get; set; }
    public long? RecordCount { get; set; }
    public string GenerationStatus { get; set; } = string.Empty;
    public string DeliveryStatus { get; set; } = string.Empty;
    public string? Checksum { get; set; }
    public DateTime CreatedDate { get; set; }

    public JobExecution Execution { get; set; } = null!;
}
