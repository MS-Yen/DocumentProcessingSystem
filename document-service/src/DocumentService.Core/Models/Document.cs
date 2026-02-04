namespace DocumentService.Core.Models;

/// <summary>
/// Represents a document stored in the system.
/// Metadata is stored in PostgreSQL, the actual file is stored in MongoDB GridFS.
/// </summary>
public class Document
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// MongoDB GridFS file ID stored as a string to avoid coupling the domain model to MongoDB types.
    /// </summary>
    public string MongoFileId { get; set; } = string.Empty;

    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

    public bool RagIndexed { get; set; }

    public string? RagError { get; set; }
}

/// <summary>
/// Tracks the document's processing lifecycle.
/// </summary>
public enum DocumentStatus
{
    /// <summary>Document uploaded, waiting for RAG indexing.</summary>
    Pending = 0,

    /// <summary>RAG service successfully indexed the document.</summary>
    Indexed = 1,

    /// <summary>RAG indexing failed (see RagError for details).</summary>
    Failed = 2,
}
