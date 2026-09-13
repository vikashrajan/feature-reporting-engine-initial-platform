using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ReportingEngine.Admin.Filters;

public sealed class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;

    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger) => _logger = logger;

    public void OnException(ExceptionContext context)
    {
        _logger.LogError(context.Exception, "API error");

        var (status, title) = context.Exception switch
        {
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            InvalidOperationException => (StatusCodes.Status400BadRequest, "Bad Request"),
            NotSupportedException => (StatusCodes.Status400BadRequest, "Not Supported"),
            OperationCanceledException when !context.HttpContext.RequestAborted.IsCancellationRequested =>
                (StatusCodes.Status504GatewayTimeout, "Operation Timed Out"),
            _ => (StatusCodes.Status500InternalServerError, "Server Error")
        };

        var detail = context.Exception is OperationCanceledException && !context.HttpContext.RequestAborted.IsCancellationRequested
            ? "The server operation timed out. Check the database connection, firewall/network access, and the admin log for the full exception."
            : context.Exception.Message;

        context.Result = new ObjectResult(new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = status,
            Instance = context.HttpContext.TraceIdentifier
        })
        {
            StatusCode = status
        };
        context.ExceptionHandled = true;
    }
}
