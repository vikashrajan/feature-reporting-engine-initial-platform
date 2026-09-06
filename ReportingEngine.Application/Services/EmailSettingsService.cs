using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReportingEngine.Application.DTOs;
using ReportingEngine.Application.Options;

namespace ReportingEngine.Application.Services;

public sealed class EmailSettingsService : IEmailSettingsService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailSettingsService> _logger;

    public EmailSettingsService(IOptions<EmailOptions> options, ILogger<EmailSettingsService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public EmailSettingsDto GetSettings()
    {
        return new EmailSettingsDto(
            _options.Host,
            _options.Port,
            _options.EnableSsl,
            _options.FromAddress,
            _options.FromDisplayName,
            _options.UserName,
            !string.IsNullOrEmpty(_options.Password),
            _options.SmtpTimeoutSeconds,
            _options.UseFileDrop,
            _options.FileDropPath,
            _options.FailureNotificationEnabled,
            _options.FailureNotificationTo,
            _options.FailureNotificationCc,
            _options.FailureNotificationSubjectTemplate,
            _options.FailureNotificationBodyTemplate
        );
    }

    public EmailSettingsDto UpdateSettings(UpdateEmailSettingsRequest request)
    {
        _options.Host = request.Host;
        _options.Port = request.Port;
        _options.EnableSsl = request.EnableSsl;
        _options.FromAddress = request.FromAddress;
        _options.FromDisplayName = request.FromDisplayName;
        _options.UserName = request.UserName;
        if (request.Password != null)
        {
            _options.Password = request.Password.Replace(" ", "");
        }
        _options.SmtpTimeoutSeconds = request.SmtpTimeoutSeconds > 0 ? request.SmtpTimeoutSeconds : 120;
        _options.UseFileDrop = request.UseFileDrop;
        _options.FileDropPath = request.FileDropPath;
        _options.FailureNotificationEnabled = request.FailureNotificationEnabled;
        _options.FailureNotificationTo = request.FailureNotificationTo;
        _options.FailureNotificationCc = request.FailureNotificationCc;
        _options.FailureNotificationSubjectTemplate = string.IsNullOrWhiteSpace(request.FailureNotificationSubjectTemplate)
            ? "ReportingEngine job failed: {ReportCode}"
            : request.FailureNotificationSubjectTemplate;
        _options.FailureNotificationBodyTemplate = string.IsNullOrWhiteSpace(request.FailureNotificationBodyTemplate)
            ? "<p>Report {ReportCode} failed.</p><p>Execution: {ExecutionId}</p><p>Error: {ErrorMessage}</p>"
            : request.FailureNotificationBodyTemplate;

        PersistToLocalAppSettingsJson();

        _logger.LogInformation("Updated email settings. Host: {Host}:{Port}, From: {FromAddress}, DropMode: {UseFileDrop}",
            _options.Host, _options.Port, _options.FromAddress, _options.UseFileDrop);

        return GetSettings();
    }

    private void PersistToLocalAppSettingsJson()
    {
        try
        {
            foreach (var filePath in GetLocalSettingsCandidatePaths())
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                var json = File.Exists(filePath) ? File.ReadAllText(filePath) : "{}";
                var node = System.Text.Json.Nodes.JsonNode.Parse(json)?.AsObject()
                    ?? new System.Text.Json.Nodes.JsonObject();

                var emailObj = new System.Text.Json.Nodes.JsonObject
                {
                    ["Host"] = _options.Host,
                    ["Port"] = _options.Port,
                    ["EnableSsl"] = _options.EnableSsl,
                    ["FromAddress"] = _options.FromAddress,
                    ["FromDisplayName"] = _options.FromDisplayName,
                    ["UserName"] = _options.UserName,
                    ["Password"] = _options.Password,
                    ["SmtpTimeoutSeconds"] = _options.SmtpTimeoutSeconds,
                    ["UseFileDrop"] = _options.UseFileDrop,
                    ["FileDropPath"] = _options.FileDropPath,
                    ["FailureNotificationEnabled"] = _options.FailureNotificationEnabled,
                    ["FailureNotificationTo"] = _options.FailureNotificationTo,
                    ["FailureNotificationCc"] = _options.FailureNotificationCc,
                    ["FailureNotificationSubjectTemplate"] = _options.FailureNotificationSubjectTemplate,
                    ["FailureNotificationBodyTemplate"] = _options.FailureNotificationBodyTemplate
                };

                node["Email"] = emailObj;

                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(filePath, node.ToJsonString(options));
                _logger.LogInformation("Persisted updated Email settings to local override {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist Email settings to local override");
        }
    }

    private static IReadOnlyList<string> GetLocalSettingsCandidatePaths()
    {
        var paths = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "appsettings.Local.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Local.json")
        };

        foreach (var root in GetRepositoryRoots())
        {
            paths.Add(Path.Combine(root, "ReportingEngine.Admin", "appsettings.Local.json"));
            paths.Add(Path.Combine(root, "ReportingEngine.Worker", "appsettings.Local.json"));
        }

        return paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> GetRepositoryRoots()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ReportingEngine.slnx")) ||
                    Directory.Exists(Path.Combine(directory.FullName, "ReportingEngine.Admin")))
                {
                    yield return directory.FullName;
                    break;
                }

                directory = directory.Parent;
            }
        }
    }

    public async Task<bool> TestEmailAsync(TestEmailRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RecipientEmail))
        {
            throw new ArgumentException("Recipient email address is required.", nameof(request));
        }

        var subject = request.CustomSubject ?? "Test Email from ReportingEngine SMTP Settings";
        var body = request.CustomBody ?? "<div style='font-family:sans-serif;'><h2>ReportingEngine SMTP Configuration Test</h2><p>This email confirms that your SMTP server connection and credentials are correctly configured.</p><hr/><p><small>Sent at UTC: " + DateTime.UtcNow.ToString("O") + "</small></p></div>";

        if (_options.UseFileDrop)
        {
            Directory.CreateDirectory(_options.FileDropPath);
            var dropPath = Path.Combine(_options.FileDropPath, $"test_email_{DateTime.UtcNow:yyyyMMddHHmmssfff}.txt");
            await File.WriteAllTextAsync(dropPath, $"To: {request.RecipientEmail}\nSubject: {subject}\n\n{body}", cancellationToken);
            _logger.LogInformation("Test email written to file-drop path {DropPath}", dropPath);
            return true;
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_options.FromAddress, _options.FromDisplayName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(request.RecipientEmail);

            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.EnableSsl,
                Timeout = Math.Max(1, _options.SmtpTimeoutSeconds) * 1000
            };

            if (!string.IsNullOrWhiteSpace(_options.UserName))
            {
                client.Credentials = new NetworkCredential(_options.UserName, _options.Password?.Replace(" ", ""));
            }

            _logger.LogInformation("Sending test email to {Recipient} via SMTP {Host}:{Port} (SSL={EnableSsl}, User={User})...",
                request.RecipientEmail, _options.Host, _options.Port, _options.EnableSsl, _options.UserName);

            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Test email successfully sent via SMTP to {Recipient}", request.RecipientEmail);
            return true;
        }
        catch (SmtpException smtpEx)
        {
            var innerMsg = smtpEx.InnerException != null ? $" Inner: {smtpEx.InnerException.Message}" : "";
            var errorDetails = $"SMTP Error (StatusCode: {smtpEx.StatusCode}): {smtpEx.Message}.{innerMsg}";
            _logger.LogError(smtpEx, "Failed to send test email to {Recipient}: {Details}", request.RecipientEmail, errorDetails);
            throw new InvalidOperationException($"Failed to send email via SMTP ({_options.Host}:{_options.Port}). {errorDetails}", smtpEx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending test email to {Recipient}", request.RecipientEmail);
            throw new InvalidOperationException($"Failed to send test email: {ex.Message}", ex);
        }
    }
}
