namespace DocumentService.Core.Interfaces;

/// <summary>
/// Client for communicating with the RAG microservice.
/// </summary>
public interface IRagServiceClient
{
    /// <summary>
    /// Send document text to the RAG service for indexing (chunking + embedding + storage).
    /// Returns true if indexing succeeded, false if it failed.
    /// </summary>
    Task<bool> IndexDocumentAsync(string documentId, string content, string fileName);

    /// <summary>
    /// Tell the RAG service to delete all indexed chunks for a document.
    /// Returns true if deletion succeeded.
    /// </summary>
    Task<bool> DeleteDocumentAsync(string documentId);
}
