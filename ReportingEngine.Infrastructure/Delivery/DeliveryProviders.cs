using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Azure;
using Azure.Storage.Files.Shares;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReportingEngine.Application.Abstractions.Delivery;
using ReportingEngine.Application.Options;
using ReportingEngine.Domain.Enums;
using System.Net;
using System.Net.Mail;

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
        var smtpConfig = request.SmtpConnection is null
            ? EmailConnectionConfig.From(_options, request.SecretReference)
            : EmailConnectionConfig.From(request.SmtpConnection);
        smtpConfig.Validate();

        if (smtpConfig.UseFileDrop)
        {
            Directory.CreateDirectory(smtpConfig.FileDropPath);
            var dropFolder = Path.Combine(smtpConfig.FileDropPath, $"{request.Tokens.ReportCode}_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}");
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

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(smtpConfig.FromAddress, smtpConfig.FromDisplayName),
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

            using var client = new SmtpClient(smtpConfig.Host, smtpConfig.Port)
            {
                EnableSsl = smtpConfig.EnableSsl,
                Timeout = Math.Max(1, smtpConfig.TimeoutSeconds) * 1000
            };

            if (!string.IsNullOrWhiteSpace(smtpConfig.UserName))
            {
                client.Credentials = new NetworkCredential(smtpConfig.UserName, smtpConfig.Password?.Replace(" ", ""));
            }

            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Email delivered to {To} with {AttachmentCount} attachments", request.EmailTo, request.AttachmentPaths.Count);
        }
        catch (SmtpException smtpEx)
        {
            var innerMsg = smtpEx.InnerException != null ? $" Inner: {smtpEx.InnerException.Message}" : "";
            var errorDetails = $"SMTP Error (StatusCode: {smtpEx.StatusCode}): {smtpEx.Message}.{innerMsg}";
            _logger.LogError(smtpEx, "Failed email delivery to {To}: {Details}", request.EmailTo, errorDetails);
            throw new InvalidOperationException($"Email delivery failed via SMTP ({smtpConfig.Host}:{smtpConfig.Port}). {errorDetails}", smtpEx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed email delivery to {To}", request.EmailTo);
            throw;
        }
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
            .Replace("{FileCount}", tokens.FileCount.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{ExecutionId}", tokens.ExecutionId?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{Status}", tokens.Status ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{ErrorMessage}", tokens.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}

public sealed class EmailConnectionConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public bool EnableSsl { get; set; }
    public string FromAddress { get; set; } = "noreply@reportingengine.local";
    public string FromDisplayName { get; set; } = "ReportingEngine";
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public int TimeoutSeconds { get; set; } = 120;
    public bool UseFileDrop { get; set; }
    public string FileDropPath { get; set; } = "./temp-emails";

    public void Validate()
    {
        if (UseFileDrop)
        {
            if (string.IsNullOrWhiteSpace(FileDropPath))
            {
                throw new InvalidOperationException("Email file-drop path is required when Local Debug Drop Mode is enabled.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(Host) ||
            string.Equals(Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Host, "filedrop", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Live SMTP host is required. Disable Local Debug Drop Mode only after configuring a real SMTP host.");
        }

        if (Port <= 0)
        {
            throw new InvalidOperationException("SMTP port must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(FromAddress))
        {
            throw new InvalidOperationException("SMTP From Sender Email is required.");
        }
    }

    public static EmailConnectionConfig From(EmailOptions options, string? secretReference)
    {
        var cfg = new EmailConnectionConfig
        {
            Host = options.Host,
            Port = options.Port,
            EnableSsl = options.EnableSsl,
            FromAddress = options.FromAddress,
            FromDisplayName = options.FromDisplayName,
            UserName = options.UserName,
            Password = options.Password,
            TimeoutSeconds = options.SmtpTimeoutSeconds,
            UseFileDrop = options.UseFileDrop,
            FileDropPath = options.FileDropPath
        };

        if (string.IsNullOrWhiteSpace(secretReference))
        {
            return cfg;
        }

        if (secretReference.TrimStart().StartsWith("{"))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(secretReference);
                var root = doc.RootElement;
                if (root.TryGetProperty("Host", out var h)) cfg.Host = h.GetString() ?? cfg.Host;
                if (root.TryGetProperty("Port", out var p) && p.TryGetInt32(out var port)) cfg.Port = port;
                if (root.TryGetProperty("EnableSsl", out var ssl)) cfg.EnableSsl = ssl.GetBoolean();
                if (root.TryGetProperty("FromAddress", out var from)) cfg.FromAddress = from.GetString() ?? cfg.FromAddress;
                if (root.TryGetProperty("FromDisplayName", out var display)) cfg.FromDisplayName = display.GetString() ?? cfg.FromDisplayName;
                if (root.TryGetProperty("UserName", out var user) || root.TryGetProperty("Username", out user)) cfg.UserName = user.GetString();
                if (root.TryGetProperty("Password", out var pwd)) cfg.Password = pwd.GetString();
                if (root.TryGetProperty("SmtpTimeoutSeconds", out var timeout) && timeout.TryGetInt32(out var seconds)) cfg.TimeoutSeconds = seconds;
                if (root.TryGetProperty("UseFileDrop", out var drop)) cfg.UseFileDrop = drop.GetBoolean();
                if (root.TryGetProperty("FileDropPath", out var path)) cfg.FileDropPath = path.GetString() ?? cfg.FileDropPath;
            }
            catch
            {
                ParseKeyValuePairs(secretReference, cfg);
            }
        }
        else
        {
            ParseKeyValuePairs(secretReference, cfg);
        }

        return cfg;
    }

    public static EmailConnectionConfig From(SmtpConnectionSettings settings) =>
        new()
        {
            Host = settings.Host,
            Port = settings.Port,
            EnableSsl = settings.EnableSsl,
            FromAddress = settings.FromAddress,
            FromDisplayName = settings.FromDisplayName,
            UserName = settings.UserName,
            Password = settings.Password,
            TimeoutSeconds = settings.TimeoutSeconds,
            UseFileDrop = settings.UseFileDrop,
            FileDropPath = settings.FileDropPath ?? "./temp-emails"
        };

    private static void ParseKeyValuePairs(string str, EmailConnectionConfig cfg)
    {
        var kvps = str.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var kv in kvps)
        {
            var parts = kv.Split('=', 2);
            if (parts.Length != 2) continue;
            var key = parts[0].Trim();
            var val = parts[1].Trim();
            if (key.Equals("Host", StringComparison.OrdinalIgnoreCase)) cfg.Host = val;
            else if (key.Equals("Port", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out var port)) cfg.Port = port;
            else if (key.Equals("EnableSsl", StringComparison.OrdinalIgnoreCase) && bool.TryParse(val, out var ssl)) cfg.EnableSsl = ssl;
            else if (key.Equals("FromAddress", StringComparison.OrdinalIgnoreCase)) cfg.FromAddress = val;
            else if (key.Equals("FromDisplayName", StringComparison.OrdinalIgnoreCase)) cfg.FromDisplayName = val;
            else if (key.Equals("Username", StringComparison.OrdinalIgnoreCase) || key.Equals("UserName", StringComparison.OrdinalIgnoreCase) || key.Equals("User", StringComparison.OrdinalIgnoreCase)) cfg.UserName = val;
            else if (key.Equals("Password", StringComparison.OrdinalIgnoreCase) || key.Equals("Pass", StringComparison.OrdinalIgnoreCase)) cfg.Password = val;
            else if (key.Equals("SmtpTimeoutSeconds", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out var timeout)) cfg.TimeoutSeconds = timeout;
            else if (key.Equals("UseFileDrop", StringComparison.OrdinalIgnoreCase) && bool.TryParse(val, out var drop)) cfg.UseFileDrop = drop;
            else if (key.Equals("FileDropPath", StringComparison.OrdinalIgnoreCase)) cfg.FileDropPath = val;
        }
    }
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
    private readonly ILogger<SftpDeliveryProvider> _logger;

    public SftpDeliveryProvider(ILogger<SftpDeliveryProvider> logger) => _logger = logger;

    public string DeliveryType => DeliveryTypes.Sftp;

    public async Task DeliverAsync(DeliveryRequest request, CancellationToken cancellationToken = default)
    {
        var destRef = request.DestinationReference;
        var secretRef = request.SecretReference;

        if (string.IsNullOrWhiteSpace(destRef) && string.IsNullOrWhiteSpace(secretRef))
        {
            throw new InvalidOperationException("DestinationReference or SecretReference is required for SFTP delivery.");
        }

        if (!string.IsNullOrWhiteSpace(destRef))
        {
            destRef = EmailDeliveryProvider.ReplaceTokens(destRef, request.Tokens);
        }

        var config = SftpConnectionConfig.Parse(destRef, secretRef);

        if (config.UseFileDrop || string.Equals(config.Host, "localhost", StringComparison.OrdinalIgnoreCase) || string.Equals(config.Host, "filedrop", StringComparison.OrdinalIgnoreCase))
        {
            var dropFolder = config.RemotePath;
            if (string.IsNullOrWhiteSpace(dropFolder) || dropFolder.StartsWith("/") || dropFolder.StartsWith("\\"))
            {
                dropFolder = Path.Combine("./temp-sftp", config.RemotePath?.TrimStart('/', '\\') ?? "");
            }
            Directory.CreateDirectory(dropFolder);
            foreach (var attachment in request.AttachmentPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var destPath = Path.Combine(dropFolder, Path.GetFileName(attachment));
                File.Copy(attachment, destPath, overwrite: true);
                _logger.LogInformation("[SFTP File-Drop] Saved file {File} to drop location {DropFolder}", Path.GetFileName(attachment), dropFolder);
            }
            return;
        }

        _logger.LogInformation("Connecting to SFTP server {Host}:{Port} as user {Username}...", config.Host, config.Port, config.Username);

        Renci.SshNet.AuthenticationMethod authMethod;
        if (!string.IsNullOrWhiteSpace(config.PrivateKeyFilePath) && File.Exists(config.PrivateKeyFilePath))
        {
            var keyFile = string.IsNullOrWhiteSpace(config.PrivateKeyPassphrase)
                ? new Renci.SshNet.PrivateKeyFile(config.PrivateKeyFilePath)
                : new Renci.SshNet.PrivateKeyFile(config.PrivateKeyFilePath, config.PrivateKeyPassphrase);
            authMethod = new Renci.SshNet.PrivateKeyAuthenticationMethod(config.Username, keyFile);
        }
        else
        {
            authMethod = new Renci.SshNet.PasswordAuthenticationMethod(config.Username, config.Password ?? "");
        }

        var connectionInfo = new Renci.SshNet.ConnectionInfo(config.Host, config.Port, config.Username, authMethod);

        using var client = new Renci.SshNet.SftpClient(connectionInfo);
        await Task.Run(() => client.Connect(), cancellationToken);

        try
        {
            var remoteDir = string.IsNullOrWhiteSpace(config.RemotePath) ? "." : config.RemotePath.Replace('\\', '/');
            if (!client.Exists(remoteDir))
            {
                CreateRemoteDirectoryRecursively(client, remoteDir);
            }
            client.ChangeDirectory(remoteDir);

            foreach (var attachment in request.AttachmentPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileName = Path.GetFileName(attachment);
                using var fs = File.OpenRead(attachment);
                await Task.Run(() => client.UploadFile(fs, fileName, canOverride: true), cancellationToken);
                _logger.LogInformation("SFTP Uploaded file {File} to {Host}:{RemoteDir}", fileName, config.Host, remoteDir);
            }
        }
        finally
        {
            if (client.IsConnected)
            {
                client.Disconnect();
            }
        }
    }

    private static void CreateRemoteDirectoryRecursively(Renci.SshNet.SftpClient client, string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = path.StartsWith("/") ? "/" : "";
        foreach (var part in parts)
        {
            current = string.IsNullOrEmpty(current) || current == "/" ? "/" + part : current + "/" + part;
            if (!client.Exists(current))
            {
                client.CreateDirectory(current);
            }
        }
    }
}

public sealed class SftpConnectionConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "anonymous";
    public string? Password { get; set; }
    public string? PrivateKeyFilePath { get; set; }
    public string? PrivateKeyPassphrase { get; set; }
    public string RemotePath { get; set; } = "./delivered-sftp";
    public bool UseFileDrop { get; set; }

    public static SftpConnectionConfig Parse(string? destRef, string? secretRef)
    {
        var cfg = new SftpConnectionConfig();

        if (!string.IsNullOrWhiteSpace(destRef))
        {
            if (destRef.StartsWith("sftp://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(destRef);
                cfg.Host = uri.Host;
                cfg.Port = uri.Port > 0 ? uri.Port : 22;
                if (!string.IsNullOrEmpty(uri.UserInfo))
                {
                    var userParts = uri.UserInfo.Split(':', 2);
                    cfg.Username = userParts[0];
                    if (userParts.Length > 1) cfg.Password = Uri.UnescapeDataString(userParts[1]);
                }
                cfg.RemotePath = uri.AbsolutePath.TrimStart('/');
            }
            else if (destRef.Contains('='))
            {
                ParseKeyValuePairs(destRef, cfg);
            }
            else
            {
                cfg.RemotePath = destRef;
            }
        }

        if (!string.IsNullOrWhiteSpace(secretRef))
        {
            if (secretRef.TrimStart().StartsWith("{"))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(secretRef);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("Host", out var h)) cfg.Host = h.GetString() ?? cfg.Host;
                    if (root.TryGetProperty("Port", out var p)) cfg.Port = p.GetInt32();
                    if (root.TryGetProperty("Username", out var u) || root.TryGetProperty("UserName", out u)) cfg.Username = u.GetString() ?? cfg.Username;
                    if (root.TryGetProperty("Password", out var pwd)) cfg.Password = pwd.GetString();
                    if (root.TryGetProperty("PrivateKeyFilePath", out var pk)) cfg.PrivateKeyFilePath = pk.GetString();
                    if (root.TryGetProperty("PrivateKeyPassphrase", out var pkp)) cfg.PrivateKeyPassphrase = pkp.GetString();
                    if (root.TryGetProperty("RemotePath", out var rp)) cfg.RemotePath = rp.GetString() ?? cfg.RemotePath;
                    if (root.TryGetProperty("UseFileDrop", out var fd)) cfg.UseFileDrop = fd.GetBoolean();
                }
                catch { }
            }
            else if (secretRef.Contains('='))
            {
                ParseKeyValuePairs(secretRef, cfg);
            }
        }

        return cfg;
    }

    private static void ParseKeyValuePairs(string str, SftpConnectionConfig cfg)
    {
        var kvps = str.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var kv in kvps)
        {
            var parts = kv.Split('=', 2);
            if (parts.Length != 2) continue;
            var key = parts[0].Trim();
            var val = parts[1].Trim();
            if (key.Equals("Host", StringComparison.OrdinalIgnoreCase)) cfg.Host = val;
            else if (key.Equals("Port", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out var port)) cfg.Port = port;
            else if (key.Equals("Username", StringComparison.OrdinalIgnoreCase) || key.Equals("User", StringComparison.OrdinalIgnoreCase)) cfg.Username = val;
            else if (key.Equals("Password", StringComparison.OrdinalIgnoreCase) || key.Equals("Pass", StringComparison.OrdinalIgnoreCase)) cfg.Password = val;
            else if (key.Equals("RemotePath", StringComparison.OrdinalIgnoreCase) || key.Equals("Path", StringComparison.OrdinalIgnoreCase)) cfg.RemotePath = val;
            else if (key.Equals("PrivateKey", StringComparison.OrdinalIgnoreCase) || key.Equals("PrivateKeyFilePath", StringComparison.OrdinalIgnoreCase)) cfg.PrivateKeyFilePath = val;
            else if (key.Equals("UseFileDrop", StringComparison.OrdinalIgnoreCase) && bool.TryParse(val, out var drop)) cfg.UseFileDrop = drop;
        }
    }
}

public sealed class FtpDeliveryProvider : IDeliveryProvider
{
    public string DeliveryType => DeliveryTypes.Ftp;
    public Task DeliverAsync(DeliveryRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FTP delivery is scaffolded but not implemented yet.");
}

public sealed class BlobDeliveryProvider : IDeliveryProvider
{
    private readonly AzureFileShareDeliveryProvider _azureFileShareDeliveryProvider;

    public BlobDeliveryProvider(ILogger<AzureFileShareDeliveryProvider> logger) =>
        _azureFileShareDeliveryProvider = new AzureFileShareDeliveryProvider(logger);

    public string DeliveryType => DeliveryTypes.Blob;

    public Task DeliverAsync(DeliveryRequest request, CancellationToken cancellationToken = default) =>
        _azureFileShareDeliveryProvider.DeliverAsync(request, cancellationToken);
}

public sealed class AzureFileShareDeliveryProvider : IDeliveryProvider
{
    private const int AzureFileShareMaxRangeSize = 4 * 1024 * 1024;
    private readonly ILogger<AzureFileShareDeliveryProvider> _logger;

    public AzureFileShareDeliveryProvider(ILogger<AzureFileShareDeliveryProvider> logger) => _logger = logger;

    public string DeliveryType => DeliveryTypes.AzureFileShare;

    public async Task DeliverAsync(DeliveryRequest request, CancellationToken cancellationToken = default)
    {
        var config = AzureFileShareConnectionConfig.Parse(request.DestinationReference, request.SecretReference, request.Tokens);
        if (string.IsNullOrWhiteSpace(config.ConnectionString))
        {
            throw new InvalidOperationException("Azure File Share connection string is required in SecretReference.");
        }

        if (string.IsNullOrWhiteSpace(config.ShareName))
        {
            throw new InvalidOperationException("Azure File Share name is required.");
        }

        var shareClient = new ShareClient(config.ConnectionString, config.ShareName);
        await shareClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var directoryClient = shareClient.GetRootDirectoryClient();
        foreach (var part in config.DirectoryPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var nextDirectory = directoryClient.GetSubdirectoryClient(part);
            await nextDirectory.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            directoryClient = nextDirectory;
        }

        foreach (var attachment in request.AttachmentPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileInfo = new FileInfo(attachment);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException("Attachment was not found for Azure File Share delivery.", attachment);
            }

            var fileClient = directoryClient.GetFileClient(fileInfo.Name);
            await fileClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            await fileClient.CreateAsync(fileInfo.Length, cancellationToken: cancellationToken);
            if (fileInfo.Length > 0)
            {
                await UploadInRangesAsync(fileClient, attachment, cancellationToken);
            }

            _logger.LogInformation("Uploaded file {FileName} to Azure File Share {ShareName}/{DirectoryPath}", fileInfo.Name, config.ShareName, config.DirectoryPath);
        }
    }

    private static async Task UploadInRangesAsync(ShareFileClient fileClient, string sourcePath, CancellationToken cancellationToken)
    {
        var buffer = new byte[AzureFileShareMaxRangeSize];
        await using var stream = File.OpenRead(sourcePath);
        long offset = 0;

        while (offset < stream.Length)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            using var chunk = new MemoryStream(buffer, 0, bytesRead, writable: false);
            await fileClient.UploadRangeAsync(new HttpRange(offset, bytesRead), chunk, cancellationToken: cancellationToken);
            offset += bytesRead;
        }
    }
}

public sealed class AzureFileShareConnectionConfig
{
    public string? ConnectionString { get; set; }
    public string ShareName { get; set; } = string.Empty;
    public string DirectoryPath { get; set; } = string.Empty;

    public static AzureFileShareConnectionConfig Parse(string? destinationReference, string? secretReference, DeliveryTokenContext tokens)
    {
        var cfg = new AzureFileShareConnectionConfig();

        if (!string.IsNullOrWhiteSpace(destinationReference))
        {
            ApplyDestination(destinationReference, cfg);
        }

        if (!string.IsNullOrWhiteSpace(secretReference))
        {
            if (secretReference.TrimStart().StartsWith("{"))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(secretReference);
                var root = doc.RootElement;
                if (root.TryGetProperty("ConnectionString", out var cs)) cfg.ConnectionString = cs.GetString()?.Trim();
                if (root.TryGetProperty("ShareName", out var share)) cfg.ShareName = share.GetString()?.Trim() ?? cfg.ShareName;
                if (root.TryGetProperty("DirectoryPath", out var path)) cfg.DirectoryPath = path.GetString()?.Trim() ?? cfg.DirectoryPath;
            }
            else if (secretReference.Contains("DefaultEndpointsProtocol=", StringComparison.OrdinalIgnoreCase))
            {
                cfg.ConnectionString = secretReference.Trim();
            }
            else
            {
                ApplyDestination(secretReference, cfg);
            }
        }

        cfg.DirectoryPath = EmailDeliveryProvider.ReplaceTokens(cfg.DirectoryPath ?? string.Empty, tokens).Trim('/', '\\');
        return cfg;
    }

    private static void ApplyDestination(string value, AzureFileShareConnectionConfig cfg)
    {
        if (!value.Contains('='))
        {
            var parts = value.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (string.IsNullOrWhiteSpace(cfg.ShareName) && parts.Length > 0)
            {
                cfg.ShareName = parts[0];
                cfg.DirectoryPath = string.Join("/", parts.Skip(1));
            }
            else
            {
                cfg.DirectoryPath = value;
            }
            return;
        }

        foreach (var kv in value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = kv.Split('=', 2);
            if (parts.Length != 2) continue;
            var key = parts[0].Trim();
            var val = parts[1].Trim();
            if (key.Equals("ShareName", StringComparison.OrdinalIgnoreCase) || key.Equals("Share", StringComparison.OrdinalIgnoreCase)) cfg.ShareName = val;
            else if (key.Equals("DirectoryPath", StringComparison.OrdinalIgnoreCase) || key.Equals("Path", StringComparison.OrdinalIgnoreCase)) cfg.DirectoryPath = val;
        }
    }
}

public sealed class S3DeliveryProvider : IDeliveryProvider
{
    private readonly ILogger<S3DeliveryProvider> _logger;

    public S3DeliveryProvider(ILogger<S3DeliveryProvider> logger) => _logger = logger;

    public string DeliveryType => DeliveryTypes.S3;

    public async Task DeliverAsync(DeliveryRequest request, CancellationToken cancellationToken = default)
    {
        var config = S3ConnectionConfig.Parse(request.DestinationReference, request.SecretReference, request.Tokens);
        if (string.IsNullOrWhiteSpace(config.BucketName))
        {
            throw new InvalidOperationException("S3 bucket name is required.");
        }

        using var client = CreateClient(config);

        foreach (var attachment in request.AttachmentPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileInfo = new FileInfo(attachment);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException("Attachment was not found for S3 delivery.", attachment);
            }

            var key = CombineS3Key(config.Prefix, fileInfo.Name);
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = config.BucketName,
                Key = key,
                FilePath = attachment
            }, cancellationToken);

            _logger.LogInformation("Uploaded file {FileName} to S3 bucket {BucketName} key {Key}", fileInfo.Name, config.BucketName, key);
        }
    }

    private static AmazonS3Client CreateClient(S3ConnectionConfig config)
    {
        var s3Config = new AmazonS3Config
        {
            ForcePathStyle = config.ForcePathStyle
        };

        if (!string.IsNullOrWhiteSpace(config.ServiceUrl))
        {
            s3Config.ServiceURL = config.ServiceUrl;
            if (!string.IsNullOrWhiteSpace(config.Region))
            {
                s3Config.AuthenticationRegion = config.Region;
            }
        }
        else
        {
            s3Config.RegionEndpoint = RegionEndpoint.GetBySystemName(config.Region);
        }

        if (!string.IsNullOrWhiteSpace(config.AccessKeyId) && !string.IsNullOrWhiteSpace(config.SecretAccessKey))
        {
            return new AmazonS3Client(new BasicAWSCredentials(config.AccessKeyId, config.SecretAccessKey), s3Config);
        }

        return new AmazonS3Client(s3Config);
    }

    private static string CombineS3Key(string? prefix, string fileName)
    {
        var cleanPrefix = (prefix ?? string.Empty).Trim('/');
        return string.IsNullOrWhiteSpace(cleanPrefix) ? fileName : $"{cleanPrefix}/{fileName}";
    }
}

public sealed class S3ConnectionConfig
{
    public string? AccessKeyId { get; set; }
    public string? SecretAccessKey { get; set; }
    public string Region { get; set; } = "us-east-1";
    public string BucketName { get; set; } = string.Empty;
    public string? Prefix { get; set; }
    public string? ServiceUrl { get; set; }
    public bool ForcePathStyle { get; set; }

    public static S3ConnectionConfig Parse(string? destinationReference, string? secretReference, DeliveryTokenContext tokens)
    {
        var cfg = new S3ConnectionConfig();

        if (!string.IsNullOrWhiteSpace(destinationReference))
        {
            ApplyDestination(destinationReference, cfg);
        }

        if (!string.IsNullOrWhiteSpace(secretReference))
        {
            if (secretReference.TrimStart().StartsWith("{"))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(secretReference);
                var root = doc.RootElement;
                if (root.TryGetProperty("AccessKeyId", out var accessKey) || root.TryGetProperty("AccessKey", out accessKey)) cfg.AccessKeyId = accessKey.GetString();
                if (root.TryGetProperty("SecretAccessKey", out var secretKey) || root.TryGetProperty("SecretKey", out secretKey)) cfg.SecretAccessKey = secretKey.GetString();
                if (root.TryGetProperty("Region", out var region)) cfg.Region = region.GetString() ?? cfg.Region;
                if (root.TryGetProperty("BucketName", out var bucket) || root.TryGetProperty("Bucket", out bucket)) cfg.BucketName = bucket.GetString() ?? cfg.BucketName;
                if (root.TryGetProperty("Prefix", out var prefix) || root.TryGetProperty("Path", out prefix)) cfg.Prefix = prefix.GetString();
                if (root.TryGetProperty("ServiceUrl", out var serviceUrl) || root.TryGetProperty("ServiceURL", out serviceUrl)) cfg.ServiceUrl = serviceUrl.GetString();
                if (root.TryGetProperty("ForcePathStyle", out var forcePathStyle)) cfg.ForcePathStyle = forcePathStyle.GetBoolean();
            }
            else
            {
                ApplyDestination(secretReference, cfg);
            }
        }

        cfg.Prefix = EmailDeliveryProvider.ReplaceTokens(cfg.Prefix ?? string.Empty, tokens).Trim('/');
        return cfg;
    }

    private static void ApplyDestination(string value, S3ConnectionConfig cfg)
    {
        if (value.StartsWith("s3://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(value);
            cfg.BucketName = uri.Host;
            cfg.Prefix = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'));
            return;
        }

        if (!value.Contains('='))
        {
            var parts = value.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (string.IsNullOrWhiteSpace(cfg.BucketName) && parts.Length > 0)
            {
                cfg.BucketName = parts[0];
                cfg.Prefix = string.Join("/", parts.Skip(1));
            }
            else
            {
                cfg.Prefix = value;
            }

            return;
        }

        foreach (var kv in value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = kv.Split('=', 2);
            if (parts.Length != 2) continue;
            var key = parts[0].Trim();
            var val = parts[1].Trim();
            if (key.Equals("AccessKeyId", StringComparison.OrdinalIgnoreCase) || key.Equals("AccessKey", StringComparison.OrdinalIgnoreCase)) cfg.AccessKeyId = val;
            else if (key.Equals("SecretAccessKey", StringComparison.OrdinalIgnoreCase) || key.Equals("SecretKey", StringComparison.OrdinalIgnoreCase)) cfg.SecretAccessKey = val;
            else if (key.Equals("Region", StringComparison.OrdinalIgnoreCase)) cfg.Region = val;
            else if (key.Equals("BucketName", StringComparison.OrdinalIgnoreCase) || key.Equals("Bucket", StringComparison.OrdinalIgnoreCase)) cfg.BucketName = val;
            else if (key.Equals("Prefix", StringComparison.OrdinalIgnoreCase) || key.Equals("Path", StringComparison.OrdinalIgnoreCase)) cfg.Prefix = val;
            else if (key.Equals("ServiceUrl", StringComparison.OrdinalIgnoreCase) || key.Equals("ServiceURL", StringComparison.OrdinalIgnoreCase)) cfg.ServiceUrl = val;
            else if (key.Equals("ForcePathStyle", StringComparison.OrdinalIgnoreCase) && bool.TryParse(val, out var forcePathStyle)) cfg.ForcePathStyle = forcePathStyle;
        }
    }
}

public sealed class DeliveryProviderResolver : IDeliveryProviderResolver
{
    private readonly IReadOnlyDictionary<string, IDeliveryProvider> _providers;

    public DeliveryProviderResolver(IEnumerable<IDeliveryProvider> providers) =>
        _providers = providers.ToDictionary(p => p.DeliveryType, StringComparer.OrdinalIgnoreCase);

    public bool IsSupported(string deliveryType) =>
        string.Equals(deliveryType, DeliveryTypes.Email, StringComparison.OrdinalIgnoreCase)
        || string.Equals(deliveryType, DeliveryTypes.SharedFolder, StringComparison.OrdinalIgnoreCase)
        || string.Equals(deliveryType, DeliveryTypes.Sftp, StringComparison.OrdinalIgnoreCase)
        || string.Equals(deliveryType, DeliveryTypes.Blob, StringComparison.OrdinalIgnoreCase)
        || string.Equals(deliveryType, DeliveryTypes.AzureFileShare, StringComparison.OrdinalIgnoreCase)
        || string.Equals(deliveryType, DeliveryTypes.S3, StringComparison.OrdinalIgnoreCase);

    public IDeliveryProvider Resolve(string deliveryType)
    {
        if (_providers.TryGetValue(deliveryType, out var provider))
        {
            return provider;
        }

        throw new NotSupportedException($"Delivery type '{deliveryType}' is not registered.");
    }
}

