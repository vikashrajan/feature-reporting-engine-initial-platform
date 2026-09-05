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
            _ => (StatusCodes.Status500InternalServerError, "Server Error")
        };

        context.Result = new ObjectResult(new ProblemDetails
        {
            Title = title,
            Detail = context.Exception.Message,
            Status = status
        })
        {
            StatusCode = status
        };
        context.ExceptionHandled = true;
    }
}
