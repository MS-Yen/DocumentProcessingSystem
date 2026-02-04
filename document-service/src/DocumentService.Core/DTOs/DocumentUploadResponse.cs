namespace DocumentService.Core.DTOs;

/// <summary>
/// Returned after a successful document upload.
/// </summary>
public record DocumentUploadResponse(
    Guid DocumentId,
    string FileName,
    long FileSize,
    string Status,
    string Message
);
