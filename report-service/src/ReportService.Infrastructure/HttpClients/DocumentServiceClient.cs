using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using ReportService.Core.Interfaces;
using ReportService.Core.Models;

namespace ReportService.Infrastructure.HttpClients;

public class DocumentServiceClient : IDocumentServiceClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DocumentServiceClient> _logger;

    private const string ClientName = "DocumentService";

    public DocumentServiceClient(
        IHttpClientFactory httpClientFactory,
        ILogger<DocumentServiceClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<DocumentMetadata?> GetDocumentAsync(Guid documentId)
    {
        var client = _httpClientFactory.CreateClient(ClientName);

        _logger.LogInformation("Fetching document metadata for {DocumentId}", documentId);

        try
        {
            var response = await client.GetAsync(documentId.ToString());

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Document {DocumentId} not found", documentId);
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<DocumentMetadata>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch document {DocumentId} from Document Service", documentId);
            throw;
        }
    }

    public async Task<List<DocumentMetadata>> GetAllDocumentsAsync()
    {
        var client = _httpClientFactory.CreateClient(ClientName);

        _logger.LogInformation("Fetching all documents from Document Service");

        try
        {
            var documents = await client.GetFromJsonAsync<List<DocumentMetadata>>("");
            return documents ?? new List<DocumentMetadata>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch documents from Document Service");
            throw;
        }
    }
}
