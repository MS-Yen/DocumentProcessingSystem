using Serilog.Context;

namespace ApiGateway.Middleware;

/// <summary>
/// Assigns a unique correlation ID to each request for distributed tracing across services.
/// Reuses the client-provided X-Correlation-ID if present.
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        // Set on request so YARP forwards it to downstream services
        context.Request.Headers[CorrelationIdHeader] = correlationId;

        // Set on response so clients can read it
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        // Push into Serilog context so all log entries include the correlation ID
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
