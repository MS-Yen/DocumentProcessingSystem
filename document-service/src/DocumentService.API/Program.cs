using DocumentService.API.Middleware;
using DocumentService.Core.Interfaces;
using DocumentService.Infrastructure.Data;
using DocumentService.Infrastructure.Repositories;
using DocumentService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog structured logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// PostgreSQL via EF Core
builder.Services.AddDbContext<DocumentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));

// MongoDB GridFS for file storage
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("MongoDB")
        ?? "mongodb://localhost:27017";
    return new MongoClient(connectionString);
});

builder.Services.AddSingleton<IGridFSBucket>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    var databaseName = builder.Configuration["MongoDB:DatabaseName"] ?? "document_db";
    var database = client.GetDatabase(databaseName);
    return new GridFSBucket(database);
});

// Application services
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddSingleton<IFileStorageService, MongoFileStorageService>();
builder.Services.AddSingleton<ITextExtractorService, TextExtractorService>();
builder.Services.AddSingleton<IRagServiceClient, RagServiceClient>();

// Named HTTP client for RAG Service
builder.Services.AddHttpClient("RagService", client =>
{
    // Trailing slash is required for correct relative URI resolution
    var ragBaseUrl = builder.Configuration["RagService:BaseUrl"]
        ?? "http://rag-service:5002/api/rag/";
    if (!ragBaseUrl.EndsWith('/'))
        ragBaseUrl += "/";
    client.BaseAddress = new Uri(ragBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(120);
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Document Service API",
        Version = "v1",
        Description = "Manages document uploads, storage, and RAG indexing",
    });
});

var app = builder.Build();

// Auto-create database tables on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();
    dbContext.Database.EnsureCreated();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Document Service v1");
});

app.MapControllers();

Log.Information("Document Service starting on port 5001...");
app.Run();
