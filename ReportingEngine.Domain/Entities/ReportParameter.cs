namespace ReportingEngine.Domain.Entities;

public class ReportParameter
{
    public long ParameterId { get; set; }
    public long ReportId { get; set; }
    public string ParameterName { get; set; } = string.Empty;
    public string ParameterType { get; set; } = string.Empty;
    public string? ParameterValue { get; set; }
    public string ValueSource { get; set; } = string.Empty;

    public ReportDefinition Report { get; set; } = null!;
}
