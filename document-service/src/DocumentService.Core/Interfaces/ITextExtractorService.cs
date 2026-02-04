namespace DocumentService.Core.Interfaces;

/// <summary>
/// Extracts plain text content from document files (PDF, DOCX, TXT).
/// </summary>
public interface ITextExtractorService
{
    /// <summary>
    /// Extracts text from a document stream based on its content type.
    /// Throws <see cref="NotSupportedException"/> for unsupported file types.
    /// </summary>
    Task<string> ExtractTextAsync(Stream fileStream, string contentType);

    /// <summary>
    /// Checks whether a given content type is supported for text extraction.
    /// </summary>
    bool IsSupported(string contentType);
}
