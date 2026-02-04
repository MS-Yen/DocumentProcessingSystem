using ApiGateway;
using ApiGateway.Middleware;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog structured logging with correlation ID support
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// YARP reverse proxy — routes defined in appsettings.json
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// CORS — allow all origins in development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("X-Correlation-ID");
    });
});

// HTTP client for downstream health checks (10s timeout)
builder.Services.AddHttpClient("HealthCheck", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "API Gateway",
        Version = "v1",
        Description = "Single entry point for the Document Processing System. "
            + "Routes requests to Document Service, RAG Service, and Report Service.",
    });
});

var app = builder.Build();

// Middleware pipeline — order matters
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "API Gateway v1");
});

app.UseCors();
app.MapHealthCheckEndpoints();
app.MapReverseProxy();

Log.Information("API Gateway starting on port 5000...");
Log.Information("Routes configured:");
Log.Information("  /api/documents/* -> Document Service (port 5001)");
Log.Information("  /api/rag/*       -> RAG Service (port 5002)");
Log.Information("  /api/reports/*   -> Report Service (port 5003)");
Log.Information("  /health          -> Gateway health check");
Log.Information("  /health/services -> Downstream services health");
Log.Information("  /swagger         -> API documentation");

app.Run();
