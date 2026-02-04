using DocumentService.Core.DTOs;
using DocumentService.Core.Interfaces;
using DocumentService.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace DocumentService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly ITextExtractorService _textExtractorService;
    private readonly IRagServiceClient _ragServiceClient;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IDocumentRepository documentRepository,
        IFileStorageService fileStorageService,
        ITextExtractorService textExtractorService,
        IRagServiceClient ragServiceClient,
        ILogger<DocumentsController> logger)
    {
        _documentRepository = documentRepository;
        _fileStorageService = fileStorageService;
        _textExtractorService = textExtractorService;
        _ragServiceClient = ragServiceClient;
        _logger = logger;
    }

    /// <summary>
    /// Upload a document, store in MongoDB GridFS, save metadata to PostgreSQL,
    /// and trigger RAG indexing.
    /// </summary>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(DocumentUploadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file.Length == 0)
        {
            return BadRequest(new { error = "No file provided or file is empty" });
        }

        if (!_textExtractorService.IsSupported(file.ContentType))
        {
            return BadRequest(new { error = $"Unsupported file type: {file.ContentType}. Supported: PDF, DOCX, TXT" });
        }

        _logger.LogInformation("Uploading document: {FileName} ({ContentType}, {Size} bytes)",
            file.FileName, file.ContentType, file.Length);

        // Store file in MongoDB GridFS
        string mongoFileId;
        using (var stream = file.OpenReadStream())
        {
            mongoFileId = await _fileStorageService.UploadAsync(file.FileName, file.ContentType, stream);
        }

        // Save metadata to PostgreSQL
        var document = new Document
        {
            Id = Guid.NewGuid(),
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            UploadedAt = DateTime.UtcNow,
            MongoFileId = mongoFileId,
            Status = DocumentStatus.Pending,
            RagIndexed = false,
        };

        await _documentRepository.AddAsync(document);
        _logger.LogInformation("Document metadata saved: {DocumentId}", document.Id);

        // Extract text and send to RAG service for indexing
        try
        {
            string textContent;
            using (var stream = file.OpenReadStream())
            {
                textContent = await _textExtractorService.ExtractTextAsync(stream, file.ContentType);
            }

            _logger.LogInformation("Extracted {Length} characters from {FileName}",
                textContent.Length, file.FileName);

            bool indexed = await _ragServiceClient.IndexDocumentAsync(
                document.Id.ToString(), textContent, file.FileName);

            document.RagIndexed = indexed;
            document.Status = indexed ? DocumentStatus.Indexed : DocumentStatus.Failed;
            if (!indexed)
            {
                document.RagError = "RAG service returned an error during indexing";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG indexing failed for document {DocumentId}", document.Id);
            document.Status = DocumentStatus.Failed;
            document.RagError = ex.Message;
        }

        await _documentRepository.UpdateAsync(document);

        var response = new DocumentUploadResponse(
            document.Id,
            document.FileName,
            document.FileSize,
            document.Status.ToString(),
            document.RagIndexed
                ? "Document uploaded and indexed successfully"
                : $"Document uploaded but RAG indexing failed: {document.RagError}"
        );

        return CreatedAtAction(nameof(GetById), new { id = document.Id }, response);
    }

    /// <summary>
    /// List all documents with their metadata.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DocumentMetadataDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var documents = await _documentRepository.GetAllAsync();

        var dtos = documents.Select(d => new DocumentMetadataDto(
            d.Id,
            d.FileName,
            d.ContentType,
            d.FileSize,
            d.UploadedAt,
            d.Status.ToString(),
            d.RagIndexed,
            d.RagError
        ));

        return Ok(dtos);
    }

    /// <summary>
    /// Get a single document's metadata by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DocumentMetadataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var document = await _documentRepository.GetByIdAsync(id);
        if (document is null)
        {
            return NotFound(new { error = $"Document {id} not found" });
        }

        var dto = new DocumentMetadataDto(
            document.Id,
            document.FileName,
            document.ContentType,
            document.FileSize,
            document.UploadedAt,
            document.Status.ToString(),
            document.RagIndexed,
            document.RagError
        );

        return Ok(dto);
    }

    /// <summary>
    /// Download the original document file from MongoDB GridFS.
    /// </summary>
    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id)
    {
        var document = await _documentRepository.GetByIdAsync(id);
        if (document is null)
        {
            return NotFound(new { error = $"Document {id} not found" });
        }

        var fileStream = await _fileStorageService.DownloadAsync(document.MongoFileId);
        if (fileStream is null)
        {
            return NotFound(new { error = "File content not found in storage" });
        }

        return File(fileStream, document.ContentType, document.FileName);
    }

    /// <summary>
    /// Delete a document from MongoDB, PostgreSQL, and RAG service.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var document = await _documentRepository.GetByIdAsync(id);
        if (document is null)
        {
            return NotFound(new { error = $"Document {id} not found" });
        }

        _logger.LogInformation("Deleting document {DocumentId} ({FileName})", id, document.FileName);

        if (document.RagIndexed)
        {
            await _ragServiceClient.DeleteDocumentAsync(id.ToString());
        }

        await _fileStorageService.DeleteAsync(document.MongoFileId);
        await _documentRepository.DeleteAsync(id);

        _logger.LogInformation("Document {DocumentId} fully deleted", id);
        return NoContent();
    }
}
