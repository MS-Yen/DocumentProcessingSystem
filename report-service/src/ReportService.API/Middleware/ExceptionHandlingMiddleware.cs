using System.Net;
using System.Text.Json;

namespace ReportService.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Downstream service call failed: {Message}", ex.Message);

            context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
            context.Response.ContentType = "application/json";

            var error = new
            {
                error = "Downstream service unavailable",
                detail = ex.Message,
                traceId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(error));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var error = new
            {
                error = "Internal server error",
                detail = ex.Message,
                traceId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(error));
        }
    }
}
