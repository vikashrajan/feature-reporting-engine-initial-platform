using Microsoft.AspNetCore.Mvc;
using ReportingEngine.Application.DTOs;
using ReportingEngine.Application.Services;

namespace ReportingEngine.Admin.Controllers;

[ApiController]
[Route("api/email")]
public sealed class EmailSettingsController : ControllerBase
{
    private readonly IEmailSettingsService _emailSettingsService;

    public EmailSettingsController(IEmailSettingsService emailSettingsService)
    {
        _emailSettingsService = emailSettingsService;
    }

    [HttpGet("settings")]
    public ActionResult<EmailSettingsDto> GetSettings()
    {
        return Ok(_emailSettingsService.GetSettings());
    }

    [HttpPut("settings")]
    public ActionResult<EmailSettingsDto> UpdateSettings([FromBody] UpdateEmailSettingsRequest request)
    {
        return Ok(_emailSettingsService.UpdateSettings(request));
    }

    [HttpPost("test")]
    public async Task<IActionResult> TestEmail([FromBody] TestEmailRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var success = await _emailSettingsService.TestEmailAsync(request, cancellationToken);
            return Ok(new { success, message = "Test email processed successfully!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}
