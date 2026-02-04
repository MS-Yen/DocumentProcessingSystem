using System.Net.Http.Json;
using DocumentService.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DocumentService.Infrastructure.Services;

/// <summary>
/// HTTP client for communicating with the RAG microservice.
/// </summary>
public class RagServiceClient : IRagServiceClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RagServiceClient> _logger;

    private const string ClientName = "RagService";

    public RagServiceClient(IHttpClientFactory httpClientFactory, ILogger<RagServiceClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<bool> IndexDocumentAsync(string documentId, string content, string fileName)
    {
        try
        {
            _logger.LogInformation("Sending document {DocumentId} to RAG service for indexing", documentId);

            var client = _httpClientFactory.CreateClient(ClientName);

            // IMPORTANT: Use a relative URI without leading slash.
            // "/index" would resolve against the host root, ignoring the base path.
            // "index" resolves relative to the base address.
            var response = await client.PostAsJsonAsync("index", new
            {
                document_id = documentId,
                content,
                filename = fileName,
            });

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Document {DocumentId} indexed successfully", documentId);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogWarning(
                "RAG service returned {StatusCode} for document {DocumentId}: {Error}",
                response.StatusCode, documentId, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index document {DocumentId} in RAG service", documentId);
            return false;
        }
    }

    public async Task<bool> DeleteDocumentAsync(string documentId)
    {
        try
        {
            _logger.LogInformation("Requesting RAG service to delete document {DocumentId}", documentId);

            var client = _httpClientFactory.CreateClient(ClientName);

            var response = await client.PostAsJsonAsync("delete", new
            {
                document_id = documentId,
            });

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Document {DocumentId} deleted from RAG service", documentId);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogWarning(
                "RAG service returned {StatusCode} for delete {DocumentId}: {Error}",
                response.StatusCode, documentId, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document {DocumentId} from RAG service", documentId);
            return false;
        }
    }
}
