namespace ReportingEngine.Domain.Entities;

public class DataSource
{
    public long DataSourceId { get; set; }
    public string DataSourceName { get; set; } = string.Empty;
    public string DataSourceType { get; set; } = string.Empty;
    public string ConnectionReference { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public ICollection<ReportDefinition> Reports { get; set; } = new List<ReportDefinition>();
}
