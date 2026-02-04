namespace ReportService.Core.Models;

/// <summary>
/// Breakdown of documents by file type (PDF, DOCX, TXT, etc.).
/// </summary>
public record DocumentTypeBreakdown(
    string ContentType,
    int Count,
    long TotalSize
);

/// <summary>
/// Aggregated statistics from Document Service + RAG Service.
/// Feeds the executive summary Analytics Report.
/// </summary>
public class AnalyticsData
{
    public int TotalDocuments { get; set; }
    public int TotalIndexed { get; set; }
    public int TotalFailed { get; set; }
    public int TotalPending { get; set; }
    public long TotalStorageBytes { get; set; }
    public List<DocumentTypeBreakdown> TypeBreakdown { get; set; } = new();
    public List<DocumentMetadata> RecentDocuments { get; set; } = new();

    public int TotalDocumentsInVectorDb { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
