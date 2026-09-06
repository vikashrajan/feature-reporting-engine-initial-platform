using Microsoft.AspNetCore.Mvc;
using ReportingEngine.Application.DTOs;
using ReportingEngine.Application.Services;

namespace ReportingEngine.Admin.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _service;
    public ReportsController(IReportService service) => _service = service;

    private string UserName() => User?.Identity?.Name ?? "admin";

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReportDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.GetAllAsync(cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ReportDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<ReportDto>> Create([FromBody] CreateReportRequest request, CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, UserName(), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.ReportId }, created);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ReportDto>> Update(long id, [FromBody] UpdateReportRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.UpdateAsync(id, request, UserName(), cancellationToken));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, UserName(), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/activate")]
    public async Task<IActionResult> Activate(long id, CancellationToken cancellationToken)
    {
        await _service.ActivateAsync(id, UserName(), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/pause")]
    public async Task<IActionResult> Pause(long id, CancellationToken cancellationToken)
    {
        await _service.PauseAsync(id, UserName(), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/resume")]
    public async Task<IActionResult> Resume(long id, CancellationToken cancellationToken)
    {
        await _service.ResumeAsync(id, UserName(), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/run")]
    public async Task<IActionResult> Run(long id, CancellationToken cancellationToken)
    {
        await _service.RunNowAsync(id, UserName(), cancellationToken);
        return Accepted();
    }

    [HttpGet("{id:long}/executions")]
    public async Task<ActionResult<IReadOnlyList<JobExecutionDto>>> Executions(long id, CancellationToken cancellationToken) =>
        Ok(await _service.GetExecutionsAsync(id, cancellationToken));
}

[ApiController]
[Route("api/executions")]
public sealed class ExecutionsController : ControllerBase
{
    private readonly IExecutionService _service;
    public ExecutionsController(IExecutionService service) => _service = service;

    private string UserName() => User?.Identity?.Name ?? "admin";

    [HttpGet("{id:long}")]
    public async Task<ActionResult<JobExecutionDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{id:long}/retry")]
    public async Task<IActionResult> Retry(long id, CancellationToken cancellationToken)
    {
        await _service.RetryAsync(id, UserName(), cancellationToken);
        return Accepted();
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await _service.CancelAsync(id, UserName(), cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly IExecutionService _service;
    public DashboardController(IExecutionService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken cancellationToken) =>
        Ok(await _service.GetDashboardAsync(cancellationToken));

    [HttpGet("{category}")]
    public async Task<ActionResult<IReadOnlyList<DashboardDetailDto>>> Details(string category, CancellationToken cancellationToken) =>
        Ok(await _service.GetDashboardDetailsAsync(category, cancellationToken));
}
