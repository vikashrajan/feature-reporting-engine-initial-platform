using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ReportingEngine.Application.Abstractions.Data;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Application.DTOs;
using ReportingEngine.Application.Services;
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
    public DeliveryConfigurationsController(IDeliveryConfigurationService service) => _service = service;

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
}

[ApiController]
[Route("api/smtpconfigurations")]
public sealed class SmtpConfigurationsController : ControllerBase
{
    private readonly ISmtpConfigurationService _service;
    public SmtpConfigurationsController(ISmtpConfigurationService service) => _service = service;

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
}

[ApiController]
[Route("api/jobfailureprofiles")]
public sealed class JobFailureProfilesController : ControllerBase
{
    private readonly IJobFailureNotificationProfileService _service;
    public JobFailureProfilesController(IJobFailureNotificationProfileService service) => _service = service;

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
}
