using DocumentService.Core.Models;

namespace DocumentService.Core.Interfaces;

/// <summary>
/// Repository interface for document metadata operations (PostgreSQL).
/// </summary>
public interface IDocumentRepository
{
    /// <summary>
    /// Gets a document by its ID. Returns null if not found.
    /// </summary>
    Task<Document?> GetByIdAsync(Guid id);

    /// <summary>
    /// Gets all documents, ordered by upload date (newest first).
    /// </summary>
    Task<IReadOnlyList<Document>> GetAllAsync();

    /// <summary>
    /// Inserts a new document record.
    /// </summary>
    Task AddAsync(Document document);

    /// <summary>
    /// Updates an existing document record.
    /// </summary>
    Task UpdateAsync(Document document);

    /// <summary>
    /// Deletes a document by ID. Returns true if found and deleted.
    /// </summary>
    Task<bool> DeleteAsync(Guid id);
}
