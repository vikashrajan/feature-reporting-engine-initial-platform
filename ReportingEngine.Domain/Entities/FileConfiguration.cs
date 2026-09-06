namespace ReportingEngine.Domain.Entities;

public class FileConfiguration
{
    public long FileConfigId { get; set; }
    public string ConfigurationName { get; set; } = string.Empty;
    public string FileFormat { get; set; } = string.Empty;
    public string FileNamePattern { get; set; } = string.Empty;
    public bool SplitEnabled { get; set; }
    public string? SplitType { get; set; }
    public long? SplitValue { get; set; }
    public string? CompressionType { get; set; }
    public int? ZipBatchSize { get; set; }
    public bool KeepLocalFiles { get; set; } = true;
    public bool EncryptionEnabled { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public ICollection<ReportDefinition> Reports { get; set; } = new List<ReportDefinition>();
}
