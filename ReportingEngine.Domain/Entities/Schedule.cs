namespace ReportingEngine.Domain.Entities;

public class Schedule
{
    public long ScheduleId { get; set; }
    public string ScheduleName { get; set; } = string.Empty;
    public string ScheduleType { get; set; } = string.Empty;
    public string? CronExpression { get; set; }
    public string TimeZoneId { get; set; } = "UTC";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public ICollection<ReportDefinition> Reports { get; set; } = new List<ReportDefinition>();
}
