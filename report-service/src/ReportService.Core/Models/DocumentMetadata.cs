namespace ReportService.Core.Models;

/// <summary>
/// Document metadata as returned by the Document Service.
/// </summary>
public record DocumentMetadata(
    Guid Id,
    string FileName,
    string ContentType,
    long FileSize,
    DateTime UploadedAt,
    string Status,
    bool RagIndexed,
    string? RagError
);
