namespace ReportingEngine.Domain.Entities;

public class ReportDefinition
{
    public long ReportId { get; set; }
    public long CustomerId { get; set; }
    public string ReportCode { get; set; } = string.Empty;
    public string ReportName { get; set; } = string.Empty;
    public long DataSourceId { get; set; }
    public long ScheduleId { get; set; }
    public long FileConfigId { get; set; }
    public long DeliveryConfigId { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int VersionNumber { get; set; } = 1;
    public string Status { get; set; } = "DRAFT";
    public bool IsActive { get; set; } = true;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public Customer Customer { get; set; } = null!;
    public DataSource DataSource { get; set; } = null!;
    public Schedule Schedule { get; set; } = null!;
    public FileConfiguration FileConfiguration { get; set; } = null!;
    public DeliveryConfiguration DeliveryConfiguration { get; set; } = null!;
    public ICollection<ReportParameter> Parameters { get; set; } = new List<ReportParameter>();
    public ICollection<JobExecution> Executions { get; set; } = new List<JobExecution>();
}
