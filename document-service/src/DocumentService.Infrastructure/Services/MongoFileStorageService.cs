using DocumentService.Core.Interfaces;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

namespace DocumentService.Infrastructure.Services;

/// <summary>
/// MongoDB GridFS implementation of file storage.
/// </summary>
public class MongoFileStorageService : IFileStorageService
{
    private readonly IGridFSBucket _gridFsBucket;
    private readonly ILogger<MongoFileStorageService> _logger;

    public MongoFileStorageService(IGridFSBucket gridFsBucket, ILogger<MongoFileStorageService> logger)
    {
        _gridFsBucket = gridFsBucket;
        _logger = logger;
    }

    public async Task<string> UploadAsync(string fileName, string contentType, Stream fileStream)
    {
        _logger.LogInformation("Uploading file {FileName} to GridFS", fileName);

        var options = new GridFSUploadOptions
        {
            Metadata = new BsonDocument
            {
                { "contentType", contentType },
            },
        };

        ObjectId fileId = await _gridFsBucket.UploadFromStreamAsync(fileName, fileStream, options);

        _logger.LogInformation("File {FileName} uploaded to GridFS with ID {FileId}", fileName, fileId);

        // Return as string so our domain model doesn't depend on MongoDB types
        return fileId.ToString();
    }

    public async Task<Stream?> DownloadAsync(string fileId)
    {
        try
        {
            var objectId = new ObjectId(fileId);
            return await _gridFsBucket.OpenDownloadStreamAsync(objectId);
        }
        catch (GridFSFileNotFoundException)
        {
            _logger.LogWarning("File {FileId} not found in GridFS", fileId);
            return null;
        }
    }

    public async Task DeleteAsync(string fileId)
    {
        try
        {
            var objectId = new ObjectId(fileId);
            await _gridFsBucket.DeleteAsync(objectId);
            _logger.LogInformation("File {FileId} deleted from GridFS", fileId);
        }
        catch (GridFSFileNotFoundException)
        {
            _logger.LogWarning("Tried to delete file {FileId} but it was not found", fileId);
        }
    }
}
