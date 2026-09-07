namespace ReportingEngine.Application.Abstractions.Delivery;

public interface IDeliveryProvider
{
    string DeliveryType { get; }
    Task DeliverAsync(DeliveryRequest request, CancellationToken cancellationToken = default);
}

public interface IDeliveryProviderResolver
{
    IDeliveryProvider Resolve(string deliveryType);
    bool IsSupported(string deliveryType);
}

public sealed class DeliveryRequest
{
    public required string DeliveryType { get; init; }
    public string? DestinationReference { get; init; }
    public string? SecretReference { get; init; }
    public string? EmailTo { get; init; }
    public string? EmailCc { get; init; }
    public string? EmailBcc { get; init; }
    public string? EmailSubjectTemplate { get; init; }
    public string? EmailBodyTemplate { get; init; }
    public SmtpConnectionSettings? SmtpConnection { get; init; }
    public required IReadOnlyList<string> AttachmentPaths { get; init; }
    public required DeliveryTokenContext Tokens { get; init; }
}

public sealed record SmtpConnectionSettings(
    string ProfileName,
    string Host,
    int Port,
    bool EnableSsl,
    string FromAddress,
    string FromDisplayName,
    string? UserName,
    string? Password,
    int TimeoutSeconds,
    bool UseFileDrop,
    string? FileDropPath);

public sealed record DeliveryTokenContext(
    string CustomerCode,
    string ReportCode,
    DateTime ExecutionDateUtc,
    long RecordCount,
    int FileCount,
    long? ExecutionId = null,
    string? Status = null,
    string? ErrorMessage = null);
