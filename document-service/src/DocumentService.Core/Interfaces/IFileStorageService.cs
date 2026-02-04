namespace DocumentService.Core.Interfaces;

/// <summary>
/// File storage operations (implemented with MongoDB GridFS).
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Uploads a file to storage. Returns the storage ID.
    /// </summary>
    Task<string> UploadAsync(string fileName, string contentType, Stream fileStream);

    /// <summary>
    /// Downloads a file from storage as a stream. Returns null if not found.
    /// </summary>
    Task<Stream?> DownloadAsync(string fileId);

    /// <summary>
    /// Deletes a file from storage.
    /// </summary>
    Task DeleteAsync(string fileId);
}
