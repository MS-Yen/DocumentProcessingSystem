using ReportService.Core.Models;

namespace ReportService.Core.Interfaces;

public interface IRagServiceClient
{
    /// <summary>
    /// Ask a question about a specific document.
    /// Returns the AI-generated answer with source citations.
    /// </summary>
    Task<RagQueryResult?> QueryDocumentAsync(Guid documentId, string question);

    /// <summary>
    /// Get the list of document IDs stored in the vector database.
    /// Used by Analytics Report to compare with Document Service records.
    /// </summary>
    Task<List<string>> GetIndexedDocumentIdsAsync();
}
