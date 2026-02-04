using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReportService.Core.Interfaces;
using ReportService.Core.Models;

namespace ReportService.Infrastructure.HttpClients;

public class RagServiceClient : IRagServiceClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RagServiceClient> _logger;

    private const string ClientName = "RagService";

    // Snake_case JSON serialization for the Python RAG Service.
    // Static readonly because JsonSerializerOptions caches reflection data and should be reused.
    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public RagServiceClient(
        IHttpClientFactory httpClientFactory,
        ILogger<RagServiceClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<RagQueryResult?> QueryDocumentAsync(Guid documentId, string question)
    {
        var client = _httpClientFactory.CreateClient(ClientName);

        _logger.LogInformation(
            "Querying RAG Service for document {DocumentId}: {Question}",
            documentId, question);

        try
        {
            // Use snake_case property names to match the Python API directly
            var requestBody = new
            {
                question,
                document_ids = new[] { documentId.ToString() },
                top_k = 5
            };

            var response = await client.PostAsJsonAsync("query", requestBody);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "RAG query failed with {StatusCode}: {Error}",
                    response.StatusCode, errorBody);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<RagQueryResult>(SnakeCaseOptions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to query RAG Service for document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<List<string>> GetIndexedDocumentIdsAsync()
    {
        var client = _httpClientFactory.CreateClient(ClientName);

        _logger.LogInformation("Fetching indexed document IDs from RAG Service");

        try
        {
            var documentIds = await client.GetFromJsonAsync<List<string>>("documents");
            return documentIds ?? new List<string>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch indexed documents from RAG Service");
            throw;
        }
    }
}
