using ReportService.Core.Models;

namespace ReportService.Core.Interfaces;

public interface IDocumentServiceClient
{
    /// <summary>
    /// Fetch metadata for a single document by ID.
    /// Returns null if the document doesn't exist (404).
    /// </summary>
    Task<DocumentMetadata?> GetDocumentAsync(Guid documentId);

    /// <summary>
    /// Fetch metadata for all documents.
    /// Used by the Analytics Report to compute aggregate statistics.
    /// </summary>
    Task<List<DocumentMetadata>> GetAllDocumentsAsync();
}
