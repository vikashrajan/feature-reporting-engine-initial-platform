using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReportingEngine.Application.Abstractions.Delivery;
using ReportingEngine.Application.Options;
using ReportingEngine.Domain.Enums;

namespace ReportingEngine.Infrastructure.Delivery;

public sealed class EmailDeliveryProvider : IDeliveryProvider
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailDeliveryProvider> _logger;

    public EmailDeliveryProvider(IOptions<EmailOptions> options, ILogger<EmailDeliveryProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string DeliveryType => DeliveryTypes.Email;

    public async Task DeliverAsync(DeliveryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.EmailTo))
        {
            throw new InvalidOperationException("EmailTo is required for EMAIL delivery.");
        }

        var subject = ReplaceTokens(request.EmailSubjectTemplate ?? "Report {ReportCode}", request.Tokens);
        var body = ReplaceTokens(request.EmailBodyTemplate ?? "<p>Report {ReportCode} generated with {RecordCount} records in {FileCount} file(s).</p>", request.Tokens);

        if (_options.UseFileDrop)
        {
            Directory.CreateDirectory(_options.FileDropPath);
            var dropFolder = Path.Combine(_options.FileDropPath, $"{request.Tokens.ReportCode}_{DateTime.UtcNow:yyyyMMddHHmmssfff}");
            Directory.CreateDirectory(dropFolder);
            await File.WriteAllTextAsync(Path.Combine(dropFolder, "email.txt"),
                $"To: {request.EmailTo}\nCc: {request.EmailCc}\nBcc: {request.EmailBcc}\nSubject: {subject}\n\n{body}",
                cancellationToken);

            foreach (var attachment in request.AttachmentPaths)
            {
                File.Copy(attachment, Path.Combine(dropFolder, Path.GetFileName(attachment)), overwrite: true);
            }

            _logger.LogInformation("Email file-drop written to {DropFolder}", dropFolder);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromDisplayName),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };

        AddAddresses(message.To, request.EmailTo);
        AddAddresses(message.CC, request.EmailCc);
        AddAddresses(message.Bcc, request.EmailBcc);

        foreach (var attachment in request.AttachmentPaths)
        {
            message.Attachments.Add(new Attachment(attachment));
        }

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.UserName))
        {
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
        _logger.LogInformation("Email delivered to {To} with {AttachmentCount} attachments", request.EmailTo, request.AttachmentPaths.Count);
    }

    private static void AddAddresses(MailAddressCollection collection, string? addresses)
    {
        if (string.IsNullOrWhiteSpace(addresses))
        {
            return;
        }

        foreach (var address in addresses.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            collection.Add(address);
        }
    }

    internal static string ReplaceTokens(string template, DeliveryTokenContext tokens) =>
        template
            .Replace("{CustomerCode}", tokens.CustomerCode, StringComparison.OrdinalIgnoreCase)
            .Replace("{ReportCode}", tokens.ReportCode, StringComparison.OrdinalIgnoreCase)
            .Replace("{ExecutionDate}", tokens.ExecutionDateUtc.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{RecordCount}", tokens.RecordCount.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{FileCount}", tokens.FileCount.ToString(), StringComparison.OrdinalIgnoreCase);
}

public sealed class SharedFolderDeliveryProvider : IDeliveryProvider
{
    private readonly ILogger<SharedFolderDeliveryProvider> _logger;

    public SharedFolderDeliveryProvider(ILogger<SharedFolderDeliveryProvider> logger) => _logger = logger;

    public string DeliveryType => DeliveryTypes.SharedFolder;

    public Task DeliverAsync(DeliveryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DestinationReference))
        {
            throw new InvalidOperationException("DestinationReference is required for SHARED_FOLDER delivery.");
        }

        Directory.CreateDirectory(request.DestinationReference);
        foreach (var attachment in request.AttachmentPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dest = Path.Combine(request.DestinationReference, Path.GetFileName(attachment));
            File.Copy(attachment, dest, overwrite: true);
            _logger.LogInformation("Delivered file {File} to shared folder {Folder}", Path.GetFileName(attachment), request.DestinationReference);
        }

        return Task.CompletedTask;
    }
}

public sealed class SftpDeliveryProvider : IDeliveryProvider
{
    public string DeliveryType => DeliveryTypes.Sftp;
    public Task DeliverAsync(DeliveryRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("SFTP delivery is scaffolded but not implemented yet.");
}

public sealed class FtpDeliveryProvider : IDeliveryProvider
{
    public string DeliveryType => DeliveryTypes.Ftp;
    public Task DeliverAsync(DeliveryRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FTP delivery is scaffolded but not implemented yet.");
}

public sealed class BlobDeliveryProvider : IDeliveryProvider
{
    public string DeliveryType => DeliveryTypes.Blob;
    public Task DeliverAsync(DeliveryRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("BLOB delivery is scaffolded but not implemented yet.");
}

public sealed class DeliveryProviderResolver : IDeliveryProviderResolver
{
    private readonly IReadOnlyDictionary<string, IDeliveryProvider> _providers;

    public DeliveryProviderResolver(IEnumerable<IDeliveryProvider> providers) =>
        _providers = providers.ToDictionary(p => p.DeliveryType, StringComparer.OrdinalIgnoreCase);

    public bool IsSupported(string deliveryType) =>
        string.Equals(deliveryType, DeliveryTypes.Email, StringComparison.OrdinalIgnoreCase)
        || string.Equals(deliveryType, DeliveryTypes.SharedFolder, StringComparison.OrdinalIgnoreCase);

    public IDeliveryProvider Resolve(string deliveryType)
    {
        if (_providers.TryGetValue(deliveryType, out var provider))
        {
            return provider;
        }

        throw new NotSupportedException($"Delivery type '{deliveryType}' is not registered.");
    }
}
