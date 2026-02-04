using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ReportService.Core.DTOs;
using ReportService.Core.Interfaces;
using ReportService.Core.Models;

namespace ReportService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IDocumentServiceClient _documentClient;
    private readonly IRagServiceClient _ragClient;
    private readonly IDocumentSummaryReportGenerator _summaryGenerator;
    private readonly IQaSessionReportGenerator _qaGenerator;
    private readonly IAnalyticsReportGenerator _analyticsGenerator;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        IDocumentServiceClient documentClient,
        IRagServiceClient ragClient,
        IDocumentSummaryReportGenerator summaryGenerator,
        IQaSessionReportGenerator qaGenerator,
        IAnalyticsReportGenerator analyticsGenerator,
        ILogger<ReportsController> logger)
    {
        _documentClient = documentClient;
        _ragClient = ragClient;
        _summaryGenerator = summaryGenerator;
        _qaGenerator = qaGenerator;
        _analyticsGenerator = analyticsGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Generate a Document Summary Report as a downloadable PDF.
    /// </summary>
    /// <param name="request">Contains the document ID to summarize.</param>
    /// <returns>A PDF file containing the document summary.</returns>
    /// <response code="200">PDF generated successfully.</response>
    /// <response code="404">Document not found.</response>
    /// <response code="502">Downstream service unavailable.</response>
    [HttpPost("document-summary")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(502)]
    public async Task<IActionResult> GenerateDocumentSummary(
        [FromBody] DocumentSummaryRequest request)
    {
        _logger.LogInformation(
            "Generating document summary report for {DocumentId}",
            request.DocumentId);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var document = await _documentClient.GetDocumentAsync(request.DocumentId);
            if (document is null)
            {
                _logger.LogWarning(
                    "Document {DocumentId} not found for summary report",
                    request.DocumentId);

                return NotFound(new { error = $"Document {request.DocumentId} not found." });
            }

            var pdfBytes = _summaryGenerator.Generate(document);

            stopwatch.Stop();
            _logger.LogInformation(
                "Document summary report generated for {DocumentId} ({Size} bytes, {Elapsed} ms)",
                request.DocumentId, pdfBytes.Length, stopwatch.ElapsedMilliseconds);

            var fileName = $"summary-{document.FileName}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "Failed to reach downstream service while generating summary for {DocumentId}",
                request.DocumentId);

            return StatusCode(502, new
            {
                error = "Downstream service unavailable.",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Generate a Q&A Session Report by querying RAG for each question.
    /// </summary>
    /// <param name="request">Document ID, list of questions, optional title.</param>
    /// <returns>A PDF file with all Q&A pairs and source citations.</returns>
    [HttpPost("qa-session")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(502)]
    public async Task<IActionResult> GenerateQaSession(
        [FromBody] QaSessionRequest request)
    {
        _logger.LogInformation(
            "Generating Q&A session report for {DocumentId} with {QuestionCount} questions",
            request.DocumentId, request.Questions.Count);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var document = await _documentClient.GetDocumentAsync(request.DocumentId);
            if (document is null)
            {
                return NotFound(new { error = $"Document {request.DocumentId} not found." });
            }

            // Query RAG Service for each question sequentially to avoid overloading the LLM
            var entries = new List<QaEntry>();
            foreach (var question in request.Questions)
            {
                _logger.LogInformation(
                    "Querying RAG for document {DocumentId}: {Question}",
                    request.DocumentId, question);

                var result = await _ragClient.QueryDocumentAsync(request.DocumentId, question);

                entries.Add(new QaEntry
                {
                    Question = question,
                    Answer = result?.Answer ?? "Unable to generate answer — RAG service returned no result.",
                    Sources = result?.Sources ?? new List<SourceChunk>(),
                    AskedAt = DateTime.UtcNow
                });
            }

            var sessionData = new QaSessionData
            {
                Document = document,
                SessionTitle = request.SessionTitle
                    ?? $"Q&A Session — {document.FileName}",
                Entries = entries,
                GeneratedAt = DateTime.UtcNow
            };

            var pdfBytes = _qaGenerator.Generate(sessionData);

            stopwatch.Stop();
            _logger.LogInformation(
                "Q&A session report generated for {DocumentId} ({QuestionCount} questions, {Size} bytes, {Elapsed} ms)",
                request.DocumentId, entries.Count, pdfBytes.Length, stopwatch.ElapsedMilliseconds);

            var fileName = $"qa-session-{document.FileName}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "Failed to reach downstream service for Q&A session on {DocumentId}",
                request.DocumentId);

            return StatusCode(502, new
            {
                error = "Downstream service unavailable.",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Generate a System Analytics Report with aggregated statistics.
    /// </summary>
    /// <returns>A PDF file with system-wide analytics and statistics.</returns>
    [HttpGet("analytics")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(502)]
    public async Task<IActionResult> GenerateAnalytics()
    {
        _logger.LogInformation("Generating system analytics report");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Fetch data from both services in parallel
            var documentsTask = _documentClient.GetAllDocumentsAsync();
            var indexedIdsTask = _ragClient.GetIndexedDocumentIdsAsync();

            await Task.WhenAll(documentsTask, indexedIdsTask);

            var documents = documentsTask.Result;
            var indexedIds = indexedIdsTask.Result;

            var analytics = new AnalyticsData
            {
                TotalDocuments = documents.Count,
                TotalIndexed = documents.Count(d => d.RagIndexed),
                TotalFailed = documents.Count(d => d.Status.Equals("Failed", StringComparison.OrdinalIgnoreCase)),
                TotalPending = documents.Count(d => d.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase)),
                TotalStorageBytes = documents.Sum(d => d.FileSize),
                TotalDocumentsInVectorDb = indexedIds.Count,

                TypeBreakdown = documents
                    .GroupBy(d => d.ContentType)
                    .Select(g => new DocumentTypeBreakdown(
                        g.Key,
                        g.Count(),
                        g.Sum(d => d.FileSize)))
                    .OrderByDescending(t => t.Count)
                    .ToList(),

                RecentDocuments = documents
                    .OrderByDescending(d => d.UploadedAt)
                    .Take(10)
                    .ToList(),

                GeneratedAt = DateTime.UtcNow
            };

            var pdfBytes = _analyticsGenerator.Generate(analytics);

            stopwatch.Stop();
            _logger.LogInformation(
                "Analytics report generated ({TotalDocs} documents, {Size} bytes, {Elapsed} ms)",
                analytics.TotalDocuments, pdfBytes.Length, stopwatch.ElapsedMilliseconds);

            var fileName = $"analytics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to reach downstream service for analytics report");

            return StatusCode(502, new
            {
                error = "Downstream service unavailable.",
                detail = ex.Message
            });
        }
    }
}
