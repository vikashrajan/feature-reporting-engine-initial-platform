namespace ReportingEngine.Domain.Entities;

public class DeliveryConfiguration
{
    public long DeliveryConfigId { get; set; }
    public string DeliveryName { get; set; } = string.Empty;
    public string DeliveryType { get; set; } = string.Empty;
    public string? DestinationReference { get; set; }
    public string? EmailTo { get; set; }
    public string? EmailCc { get; set; }
    public string? EmailBcc { get; set; }
    public string? EmailSubjectTemplate { get; set; }
    public string? EmailBodyTemplate { get; set; }
    public string? SecretReference { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public ICollection<ReportDefinition> Reports { get; set; } = new List<ReportDefinition>();
}
