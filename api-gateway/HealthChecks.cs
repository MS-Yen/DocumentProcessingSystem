namespace ApiGateway;

/// <summary>
/// Health check endpoints using Minimal APIs.
/// </summary>
public static class HealthChecks
{
    public static void MapHealthCheckEndpoints(this WebApplication app)
    {
        // GET /health — gateway self-check
        app.MapGet("/health", () => Results.Ok(new
        {
            status = "healthy",
            service = "api-gateway",
            timestamp = DateTime.UtcNow,
        }))
        .WithTags("Health")
        .WithDescription("Gateway self-check");

        // GET /health/services — aggregate downstream health
        app.MapGet("/health/services", async (IConfiguration config, IHttpClientFactory httpClientFactory) =>
        {
            var client = httpClientFactory.CreateClient("HealthCheck");

            var services = new Dictionary<string, string>
            {
                ["document-service"] = config["Services:DocumentService"] ?? "http://document-service:5001",
                ["rag-service"] = config["Services:RagService"] ?? "http://rag-service:5002",
                ["report-service"] = config["Services:ReportService"] ?? "http://report-service:5003",
            };

            var results = new Dictionary<string, object>();
            var allHealthy = true;

            foreach (var (name, baseUrl) in services)
            {
                try
                {
                    var healthUrl = name switch
                    {
                        "rag-service" => $"{baseUrl}/api/rag/health",
                        "document-service" => $"{baseUrl}/api/documents",
                        _ => $"{baseUrl}/health",
                    };

                    var response = await client.GetAsync(healthUrl);

                    results[name] = new
                    {
                        status = response.IsSuccessStatusCode ? "healthy" : "unhealthy",
                        statusCode = (int)response.StatusCode,
                    };

                    if (!response.IsSuccessStatusCode)
                        allHealthy = false;
                }
                catch (Exception ex)
                {
                    results[name] = new
                    {
                        status = "unreachable",
                        error = ex.Message,
                    };
                    allHealthy = false;
                }
            }

            var overall = new
            {
                status = allHealthy ? "healthy" : "degraded",
                timestamp = DateTime.UtcNow,
                services = results,
            };

            return allHealthy
                ? Results.Ok(overall)
                : Results.Json(overall, statusCode: StatusCodes.Status503ServiceUnavailable);
        })
        .WithTags("Health")
        .WithDescription("Check health of all downstream services");
    }
}
