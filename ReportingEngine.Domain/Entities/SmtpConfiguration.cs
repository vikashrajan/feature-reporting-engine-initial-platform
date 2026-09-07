namespace ReportingEngine.Domain.Entities;

public class SmtpConfiguration
{
    public long SmtpConfigId { get; set; }
    public string ProfileName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string FromAddress { get; set; } = string.Empty;
    public string FromDisplayName { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public int TimeoutSeconds { get; set; } = 120;
    public bool UseFileDrop { get; set; }
    public string? FileDropPath { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public ICollection<DeliveryConfiguration> DeliveryConfigurations { get; set; } = new List<DeliveryConfiguration>();
    public ICollection<JobFailureNotificationProfile> FailureNotificationProfiles { get; set; } = new List<JobFailureNotificationProfile>();
}
