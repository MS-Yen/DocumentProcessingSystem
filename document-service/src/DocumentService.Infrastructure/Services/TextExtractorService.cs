using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentService.Core.Interfaces;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using Microsoft.Extensions.Logging;

namespace DocumentService.Infrastructure.Services;

/// <summary>
/// Extracts text from PDF, DOCX, and TXT files.
/// </summary>
public class TextExtractorService : ITextExtractorService
{
    private readonly ILogger<TextExtractorService> _logger;

    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "text/plain",
    };

    public TextExtractorService(ILogger<TextExtractorService> logger)
    {
        _logger = logger;
    }

    public bool IsSupported(string contentType)
    {
        return SupportedTypes.Contains(contentType);
    }

    public async Task<string> ExtractTextAsync(Stream fileStream, string contentType)
    {
        _logger.LogInformation("Extracting text from {ContentType} document", contentType);

        return contentType.ToLowerInvariant() switch
        {
            "application/pdf" => ExtractFromPdf(fileStream),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                => ExtractFromDocx(fileStream),
            "text/plain" => await ExtractFromTextAsync(fileStream),
            _ => throw new NotSupportedException($"Unsupported content type: {contentType}"),
        };
    }

    /// <summary>
    /// Extract text from a PDF file using iText7.
    /// Note: Works for text-based PDFs; scanned/image PDFs would need OCR.
    /// </summary>
    private string ExtractFromPdf(Stream fileStream)
    {
        var textBuilder = new StringBuilder();

        using var pdfReader = new PdfReader(fileStream);
        using var pdfDocument = new PdfDocument(pdfReader);

        int pageCount = pdfDocument.GetNumberOfPages();
        _logger.LogInformation("PDF has {PageCount} pages", pageCount);

        for (int i = 1; i <= pageCount; i++)
        {
            var page = pdfDocument.GetPage(i);
            string? pageText = PdfTextExtractor.GetTextFromPage(page);

            if (!string.IsNullOrWhiteSpace(pageText))
            {
                textBuilder.AppendLine(pageText);
            }
        }

        return textBuilder.ToString();
    }

    /// <summary>
    /// Extract text from a DOCX file using OpenXml SDK.
    /// DOCX files are ZIP archives containing XML; this extracts paragraph text.
    /// </summary>
    private string ExtractFromDocx(Stream fileStream)
    {
        var textBuilder = new StringBuilder();

        using var document = WordprocessingDocument.Open(fileStream, isEditable: false);

        var body = document.MainDocumentPart?.Document.Body;
        if (body is null)
            return string.Empty;

        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            string? text = paragraph.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
            {
                textBuilder.AppendLine(text);
            }
        }

        return textBuilder.ToString();
    }

    /// <summary>
    /// Read plain text from a TXT file as UTF-8.
    /// </summary>
    private static async Task<string> ExtractFromTextAsync(Stream fileStream)
    {
        using var reader = new StreamReader(fileStream, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
