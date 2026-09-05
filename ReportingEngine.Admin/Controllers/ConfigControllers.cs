using Microsoft.AspNetCore.Mvc;
using ReportingEngine.Application.DTOs;
using ReportingEngine.Application.Services;

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

    private string UserName() => User?.Identity?.Name ?? "admin";
}

[ApiController]
[Route("api/datasources")]
public sealed class DataSourcesController : ControllerBase
{
    private readonly IDataSourceService _service;
    public DataSourcesController(IDataSourceService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DataSourceDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.GetAllAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<DataSourceDto>> Create([FromBody] CreateDataSourceRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.CreateAsync(request, User?.Identity?.Name ?? "admin", cancellationToken));

    [HttpPut("{id:long}")]
    public async Task<ActionResult<DataSourceDto>> Update(long id, [FromBody] UpdateDataSourceRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.UpdateAsync(id, request, User?.Identity?.Name ?? "admin", cancellationToken));
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

    [HttpPost]
    public async Task<ActionResult<ScheduleDto>> Create([FromBody] CreateScheduleRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.CreateAsync(request, User?.Identity?.Name ?? "admin", cancellationToken));

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ScheduleDto>> Update(long id, [FromBody] UpdateScheduleRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.UpdateAsync(id, request, User?.Identity?.Name ?? "admin", cancellationToken));
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

    [HttpPost]
    public async Task<ActionResult<FileConfigurationDto>> Create([FromBody] CreateFileConfigurationRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.CreateAsync(request, User?.Identity?.Name ?? "admin", cancellationToken));

    [HttpPut("{id:long}")]
    public async Task<ActionResult<FileConfigurationDto>> Update(long id, [FromBody] UpdateFileConfigurationRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.UpdateAsync(id, request, User?.Identity?.Name ?? "admin", cancellationToken));
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

    [HttpPost]
    public async Task<ActionResult<DeliveryConfigurationDto>> Create([FromBody] CreateDeliveryConfigurationRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.CreateAsync(request, User?.Identity?.Name ?? "admin", cancellationToken));

    [HttpPut("{id:long}")]
    public async Task<ActionResult<DeliveryConfigurationDto>> Update(long id, [FromBody] UpdateDeliveryConfigurationRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.UpdateAsync(id, request, User?.Identity?.Name ?? "admin", cancellationToken));
}
