using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;
using ReportService.API.Middleware;
using ReportService.Core.Interfaces;
using ReportService.Infrastructure.HttpClients;
using ReportService.Infrastructure.Reports;
using Serilog;

// QuestPDF license configuration (Community = free for < $1M revenue)
QuestPDF.Settings.License = LicenseType.Community;

// Serilog structured logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting Report Service...");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // --- Controllers ---
    builder.Services.AddControllers();

    // --- Swagger/OpenAPI ---
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Report Service API",
            Version = "v1",
            Description = "Generates professional PDF reports from document analysis, " +
                          "Q&A sessions, and system analytics."
        });
    });

    // --- HTTP Clients (Named Clients via IHttpClientFactory) ---
    // IMPORTANT: BaseAddress MUST end with "/" for relative URI resolution to work correctly.

    // Document Service HTTP Client
    builder.Services.AddHttpClient("DocumentService", client =>
    {
        var baseUrl = builder.Configuration["Services:DocumentService"]
            ?? "http://document-service:5001/api/documents/";

        if (!baseUrl.EndsWith('/'))
            baseUrl += "/";

        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    // RAG Service HTTP Client
    builder.Services.AddHttpClient("RagService", client =>
    {
        var baseUrl = builder.Configuration["Services:RagService"]
            ?? "http://rag-service:5002/api/rag/";

        if (!baseUrl.EndsWith('/'))
            baseUrl += "/";

        client.BaseAddress = new Uri(baseUrl);

        // RAG queries can take time (Ollama model loading + inference)
        client.Timeout = TimeSpan.FromMinutes(3);
    });

    // --- Service Implementations (stateless singletons) ---

    // HTTP client wrappers
    builder.Services.AddSingleton<IDocumentServiceClient, DocumentServiceClient>();
    builder.Services.AddSingleton<IRagServiceClient, RagServiceClient>();

    // Report generators
    builder.Services.AddSingleton<IDocumentSummaryReportGenerator, DocumentSummaryReportGenerator>();
    builder.Services.AddSingleton<IQaSessionReportGenerator, QaSessionReportGenerator>();
    builder.Services.AddSingleton<IAnalyticsReportGenerator, AnalyticsReportGenerator>();

    var app = builder.Build();

    // --- Middleware Pipeline ---
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    app.UseSwagger();
    app.UseSwaggerUI();

    app.MapControllers();

    app.MapGet("/health", () => Results.Ok(new
    {
        status = "healthy",
        service = "report-service",
        timestamp = DateTime.UtcNow
    }));

    Log.Information("Report Service started on port 5003");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Report Service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
