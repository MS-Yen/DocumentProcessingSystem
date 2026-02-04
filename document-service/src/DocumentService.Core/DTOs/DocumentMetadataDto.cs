namespace DocumentService.Core.DTOs;

/// <summary>
/// Document metadata returned by list and detail endpoints.
/// Excludes internal fields like MongoFileId.
/// </summary>
public record DocumentMetadataDto(
    Guid Id,
    string FileName,
    string ContentType,
    long FileSize,
    DateTime UploadedAt,
    string Status,
    bool RagIndexed,
    string? RagError
);
