namespace ReportingEngine.Domain.Entities;

public class JobFailureNotificationProfile
{
    public long FailureProfileId { get; set; }
    public string ProfileName { get; set; } = string.Empty;
    public long SmtpConfigId { get; set; }
    public string EmailTo { get; set; } = string.Empty;
    public string? EmailCc { get; set; }
    public string? EmailBcc { get; set; }
    public string? SubjectTemplate { get; set; }
    public string? BodyTemplate { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public SmtpConfiguration SmtpConfiguration { get; set; } = null!;
}
