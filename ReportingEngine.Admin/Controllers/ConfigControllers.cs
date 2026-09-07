using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ReportingEngine.Application.Abstractions.Data;
using ReportingEngine.Application.Abstractions.Delivery;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Application.DTOs;
using ReportingEngine.Application.Services;
using ReportingEngine.Domain.Entities;
using ReportingEngine.Domain.Enums;

namespace ReportingEngine.Admin.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerService _service;
    public CustomersController(ICustomerService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.GetAllAsync(cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<CustomerDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, UserName(), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.CustomerId }, created);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<CustomerDto>> Update(long id, [FromBody] UpdateCustomerRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.UpdateAsync(id, request, UserName(), cancellationToken));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, UserName(), cancellationToken);
        return NoContent();
    }

    private string UserName() => User?.Identity?.Name ?? "admin";
}

[ApiController]
[Route("api/timezones")]
public sealed class TimeZonesController : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<TimeZoneDto>> GetAll()
    {
        var zones = TimeZoneInfo.GetSystemTimeZones()
            .Select(z => new TimeZoneDto(z.Id, z.DisplayName, z.BaseUtcOffset.ToString(@"hh\:mm")))
            .Prepend(new TimeZoneDto("UTC", "(UTC) Coordinated Universal Time", "00:00"))
            .GroupBy(z => z.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(z => z.DisplayName)
            .ToList();

        return Ok(zones);
    }
}

public sealed record TimeZoneDto(string Id, string DisplayName, string UtcOffset);

[ApiController]
[Route("api/datasources")]
public sealed class DataSourcesController : ControllerBase
{
    private readonly IDataSourceService _service;
    private readonly IDataSourceRepository _repository;
    private readonly IConnectionStringResolver _connectionStringResolver;

    public DataSourcesController(
        IDataSourceService service,
        IDataSourceRepository repository,
        IConnectionStringResolver connectionStringResolver)
    {
        _service = service;
        _repository = repository;
        _connectionStringResolver = connectionStringResolver;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DataSourceDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.GetAllAsync(cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<DataSourceDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<DataSourceDto>> Create([FromBody] CreateDataSourceRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.CreateAsync(request, User?.Identity?.Name ?? "admin", cancellationToken));

    [HttpPut("{id:long}")]
    public async Task<ActionResult<DataSourceDto>> Update(long id, [FromBody] UpdateDataSourceRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.UpdateAsync(id, request, User?.Identity?.Name ?? "admin", cancellationToken));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, User?.Identity?.Name ?? "admin", cancellationToken);
        return NoContent();
    }

    [HttpPost("test-connection")]
    public async Task<IActionResult> TestConnection([FromBody] TestDataSourceConnectionRequest request, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.DataSourceType, DataSourceTypes.Sql, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { success = false, message = "Connection test is currently available for SQL data sources." });
        }

        try
        {
            var dataSource = long.TryParse(Request.Query["dataSourceId"], out var dataSourceId)
                ? await _repository.GetByIdAsync(dataSourceId, cancellationToken)
                : null;
            var connectionString = !string.IsNullOrWhiteSpace(request.ConnectionString)
                ? NormalizeConnectionString(request.ConnectionString)
                : !string.IsNullOrWhiteSpace(dataSource?.ConnectionString)
                    ? dataSource.ConnectionString
                    : _connectionStringResolver.Resolve(request.ConnectionReference);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            return Ok(new
            {
                success = true,
                message = $"Connected successfully to SQL database '{connection.Database}' on '{connection.DataSource}'."
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return BadRequest(new { success = false, message = "Connection failed: " + ex.Message });
        }
    }

    private static string NormalizeConnectionString(string connectionString) =>
        connectionString.Trim()
            .Replace("(localdb)\\\\", "(localdb)\\", StringComparison.OrdinalIgnoreCase)
            .Replace("localhost\\\\", "localhost\\", StringComparison.OrdinalIgnoreCase)
            .Replace(".\\\\", ".\\", StringComparison.OrdinalIgnoreCase);
}

[ApiController]
[Route("api/schedules")]
public sealed class SchedulesController : ControllerBase
{
    private readonly IScheduleService _service;
    public SchedulesController(IScheduleService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ScheduleDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.GetAllAsync(cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ScheduleDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<ScheduleDto>> Create([FromBody] CreateScheduleRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.CreateAsync(request, User?.Identity?.Name ?? "admin", cancellationToken));

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ScheduleDto>> Update(long id, [FromBody] UpdateScheduleRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.UpdateAsync(id, request, User?.Identity?.Name ?? "admin", cancellationToken));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, User?.Identity?.Name ?? "admin", cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Route("api/fileconfigurations")]
public sealed class FileConfigurationsController : ControllerBase
{
    private readonly IFileConfigurationService _service;
    public FileConfigurationsController(IFileConfigurationService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FileConfigurationDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.GetAllAsync(cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<FileConfigurationDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<FileConfigurationDto>> Create([FromBody] CreateFileConfigurationRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.CreateAsync(request, User?.Identity?.Name ?? "admin", cancellationToken));

    [HttpPut("{id:long}")]
    public async Task<ActionResult<FileConfigurationDto>> Update(long id, [FromBody] UpdateFileConfigurationRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.UpdateAsync(id, request, User?.Identity?.Name ?? "admin", cancellationToken));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, User?.Identity?.Name ?? "admin", cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Route("api/deliveryconfigurations")]
public sealed class DeliveryConfigurationsController : ControllerBase
{
    private readonly IDeliveryConfigurationService _service;
    private readonly IDeliveryConfigurationRepository _repository;
    private readonly IDeliveryProviderResolver _deliveryProviderResolver;
    private readonly ILogger<DeliveryConfigurationsController> _logger;

    public DeliveryConfigurationsController(
        IDeliveryConfigurationService service,
        IDeliveryConfigurationRepository repository,
        IDeliveryProviderResolver deliveryProviderResolver,
        ILogger<DeliveryConfigurationsController> logger)
    {
        _service = service;
        _repository = repository;
        _deliveryProviderResolver = deliveryProviderResolver;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeliveryConfigurationDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.GetAllAsync(cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<DeliveryConfigurationDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<DeliveryConfigurationDto>> Create([FromBody] CreateDeliveryConfigurationRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.CreateAsync(request, User?.Identity?.Name ?? "admin", cancellationToken));

    [HttpPut("{id:long}")]
    public async Task<ActionResult<DeliveryConfigurationDto>> Update(long id, [FromBody] UpdateDeliveryConfigurationRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.UpdateAsync(id, request, User?.Identity?.Name ?? "admin", cancellationToken));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, User?.Identity?.Name ?? "admin", cancellationToken);
        return NoContent();
    }

    [HttpPost("test-sftp")]
    public IActionResult TestSftp([FromBody] TestSftpRequest request)
    {
        try
        {
            var cfg = ReportingEngine.Infrastructure.Delivery.SftpConnectionConfig.Parse(request.DestinationReference, request.SecretReference);
            if (cfg.UseFileDrop || string.Equals(cfg.Host, "localhost", StringComparison.OrdinalIgnoreCase) || string.Equals(cfg.Host, "filedrop", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new { success = true, message = $"SFTP configured in File-Drop / Local mode. Target drop location: {cfg.RemotePath}" });
            }

            return Ok(new { success = true, message = $"SFTP configuration parsed successfully. Target: {cfg.Username}@{cfg.Host}:{cfg.Port}/{cfg.RemotePath}" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = "SFTP connection test failed: " + ex.Message });
        }
    }

    [HttpPost("{id:long}/test")]
    public async Task<IActionResult> TestDelivery(long id, [FromBody] TestProfileRequest? request, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Delivery profile {id} was not found.");

        var testFile = await ProfileTestHelpers.CreateDummyFileAsync("delivery", cancellationToken);
        try
        {
            var deliveryProvider = _deliveryProviderResolver.Resolve(profile.DeliveryType);
            var emailTo = string.IsNullOrWhiteSpace(request?.RecipientEmail) ? profile.EmailTo : request.RecipientEmail;
            await deliveryProvider.DeliverAsync(new DeliveryRequest
            {
                DeliveryType = profile.DeliveryType,
                DestinationReference = profile.DestinationReference,
                SecretReference = profile.SecretReference,
                EmailTo = emailTo,
                EmailCc = profile.EmailCc,
                EmailBcc = profile.EmailBcc,
                EmailSubjectTemplate = profile.EmailSubjectTemplate ?? "ReportingEngine delivery profile test: {ReportCode}",
                EmailBodyTemplate = profile.EmailBodyTemplate ?? "<p>This is a delivery profile test for {ReportCode}.</p>",
                SmtpConnection = ProfileTestHelpers.BuildSmtpConnection(profile.SmtpConfiguration),
                AttachmentPaths = new[] { testFile },
                Tokens = ProfileTestHelpers.TestTokens("DELIVERY_TEST")
            }, cancellationToken);

            _logger.LogInformation("Delivery profile test succeeded for DeliveryConfigId={DeliveryConfigId} Type={DeliveryType}", id, profile.DeliveryType);
            var detail = profile.DeliveryType.Equals(DeliveryTypes.Email, StringComparison.OrdinalIgnoreCase) && profile.SmtpConfiguration?.UseFileDrop == true
                ? $" Test email was written to file drop folder {profile.SmtpConfiguration.FileDropPath}."
                : string.Empty;
            return Ok(new { success = true, message = $"Delivery test succeeded for {profile.DeliveryType}. Dummy file: {Path.GetFileName(testFile)}.{detail}" });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Delivery profile test failed for DeliveryConfigId={DeliveryConfigId} Type={DeliveryType}", id, profile.DeliveryType);
            throw;
        }
        finally
        {
            ProfileTestHelpers.TryDeleteFile(testFile);
        }
    }
}

[ApiController]
[Route("api/smtpconfigurations")]
public sealed class SmtpConfigurationsController : ControllerBase
{
    private readonly ISmtpConfigurationService _service;
    private readonly ISmtpConfigurationRepository _repository;
    private readonly IDeliveryProviderResolver _deliveryProviderResolver;
    private readonly ILogger<SmtpConfigurationsController> _logger;

    public SmtpConfigurationsController(
        ISmtpConfigurationService service,
        ISmtpConfigurationRepository repository,
        IDeliveryProviderResolver deliveryProviderResolver,
        ILogger<SmtpConfigurationsController> logger)
    {
        _service = service;
        _repository = repository;
        _deliveryProviderResolver = deliveryProviderResolver;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SmtpConfigurationDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.GetAllAsync(cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<SmtpConfigurationDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<SmtpConfigurationDto>> Create([FromBody] CreateSmtpConfigurationRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.CreateAsync(request, User?.Identity?.Name ?? "admin", cancellationToken));

    [HttpPut("{id:long}")]
    public async Task<ActionResult<SmtpConfigurationDto>> Update(long id, [FromBody] UpdateSmtpConfigurationRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.UpdateAsync(id, request, User?.Identity?.Name ?? "admin", cancellationToken));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, User?.Identity?.Name ?? "admin", cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/test")]
    public async Task<IActionResult> TestSmtp(long id, [FromBody] TestProfileRequest? request, CancellationToken cancellationToken)
    {
        var smtp = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"SMTP profile {id} was not found.");
        var recipient = string.IsNullOrWhiteSpace(request?.RecipientEmail) ? smtp.FromAddress : request.RecipientEmail!;

        try
        {
            var deliveryProvider = _deliveryProviderResolver.Resolve(DeliveryTypes.Email);
            await deliveryProvider.DeliverAsync(new DeliveryRequest
            {
                DeliveryType = DeliveryTypes.Email,
                EmailTo = recipient,
                EmailSubjectTemplate = "ReportingEngine SMTP profile test: {ReportCode}",
                EmailBodyTemplate = "<p>This confirms SMTP profile '{ReportCode}' can send email.</p>",
                SmtpConnection = ProfileTestHelpers.BuildSmtpConnection(smtp),
                AttachmentPaths = Array.Empty<string>(),
                Tokens = ProfileTestHelpers.TestTokens(smtp.ProfileName)
            }, cancellationToken);

            _logger.LogInformation("SMTP profile test succeeded for SmtpConfigId={SmtpConfigId} Recipient={Recipient}", id, recipient);
            var mode = smtp.UseFileDrop ? $"written to file drop folder {smtp.FileDropPath}" : $"sent to {recipient}";
            return Ok(new { success = true, message = $"SMTP test succeeded. Test email {mode}." });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "SMTP profile test failed for SmtpConfigId={SmtpConfigId} Recipient={Recipient}", id, recipient);
            throw;
        }
    }
}

[ApiController]
[Route("api/jobfailureprofiles")]
public sealed class JobFailureProfilesController : ControllerBase
{
    private readonly IJobFailureNotificationProfileService _service;
    private readonly IJobFailureNotificationProfileRepository _repository;
    private readonly IDeliveryProviderResolver _deliveryProviderResolver;
    private readonly ILogger<JobFailureProfilesController> _logger;

    public JobFailureProfilesController(
        IJobFailureNotificationProfileService service,
        IJobFailureNotificationProfileRepository repository,
        IDeliveryProviderResolver deliveryProviderResolver,
        ILogger<JobFailureProfilesController> logger)
    {
        _service = service;
        _repository = repository;
        _deliveryProviderResolver = deliveryProviderResolver;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<JobFailureNotificationProfileDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.GetAllAsync(cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<JobFailureNotificationProfileDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<JobFailureNotificationProfileDto>> Create([FromBody] CreateJobFailureNotificationProfileRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.CreateAsync(request, User?.Identity?.Name ?? "admin", cancellationToken));

    [HttpPut("{id:long}")]
    public async Task<ActionResult<JobFailureNotificationProfileDto>> Update(long id, [FromBody] UpdateJobFailureNotificationProfileRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.UpdateAsync(id, request, User?.Identity?.Name ?? "admin", cancellationToken));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, User?.Identity?.Name ?? "admin", cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/test")]
    public async Task<IActionResult> TestFailureProfile(long id, [FromBody] TestProfileRequest? request, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Job failure notification profile {id} was not found.");
        var recipient = string.IsNullOrWhiteSpace(request?.RecipientEmail) ? profile.EmailTo : request.RecipientEmail!;

        try
        {
            var deliveryProvider = _deliveryProviderResolver.Resolve(DeliveryTypes.Email);
            await deliveryProvider.DeliverAsync(new DeliveryRequest
            {
                DeliveryType = DeliveryTypes.Email,
                EmailTo = recipient,
                EmailCc = profile.EmailCc,
                EmailBcc = profile.EmailBcc,
                EmailSubjectTemplate = profile.SubjectTemplate ?? "ReportingEngine job failed: {ReportCode}",
                EmailBodyTemplate = profile.BodyTemplate ?? "<p>Report {ReportCode} failed.</p><p>Execution: {ExecutionId}</p><p>Status: {Status}</p><p>Error: {ErrorMessage}</p>",
                SmtpConnection = ProfileTestHelpers.BuildSmtpConnection(profile.SmtpConfiguration),
                AttachmentPaths = Array.Empty<string>(),
                Tokens = new DeliveryTokenContext("TEST", "FAILURE_TEST", DateTime.UtcNow, 0, 0, 999999, JobExecutionStatuses.Failed, "This is a test failure notification.")
            }, cancellationToken);

            _logger.LogInformation("Failure profile test succeeded for FailureProfileId={FailureProfileId} Recipient={Recipient}", id, recipient);
            var mode = profile.SmtpConfiguration.UseFileDrop ? $"written to file drop folder {profile.SmtpConfiguration.FileDropPath}" : $"sent to {recipient}";
            return Ok(new { success = true, message = $"Failure email test succeeded. Test email {mode}." });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failure profile test failed for FailureProfileId={FailureProfileId} Recipient={Recipient}", id, recipient);
            throw;
        }
    }
}

internal static class ProfileTestHelpers
{
    public static async Task<string> CreateDummyFileAsync(string prefix, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(@"C:\ReportingEngineOutput", "profile-tests");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{prefix}_{DateTime.UtcNow:yyyyMMddHHmmssfff}.txt");
        await System.IO.File.WriteAllTextAsync(path, $"ReportingEngine test file generated at UTC {DateTime.UtcNow:O}{Environment.NewLine}", cancellationToken);
        return path;
    }

    public static DeliveryTokenContext TestTokens(string reportCode) =>
        new("TEST", reportCode, DateTime.UtcNow, 1, 1, 999999, "TEST", null);

    public static SmtpConnectionSettings? BuildSmtpConnection(SmtpConfiguration? smtp) =>
        smtp is null
            ? null
            : new SmtpConnectionSettings(
                smtp.ProfileName,
                smtp.Host,
                smtp.Port,
                smtp.EnableSsl,
                smtp.FromAddress,
                smtp.FromDisplayName,
                smtp.UserName,
                smtp.Password,
                smtp.TimeoutSeconds,
                smtp.UseFileDrop,
                smtp.FileDropPath);

    public static void TryDeleteFile(string path)
    {
        try
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
