namespace ReportService.Core.Models;

/// <summary>
/// A single question-answer pair with its source citations.
/// </summary>
public class QaEntry
{
    public required string Question { get; set; }
    public required string Answer { get; set; }
    public List<SourceChunk> Sources { get; set; } = new();
    public DateTime AskedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Complete data for a Q&A session report.
/// Built by the controller from Document Service + RAG Service responses.
/// </summary>
public class QaSessionData
{
    public required DocumentMetadata Document { get; set; }
    public required string SessionTitle { get; set; }
    public List<QaEntry> Entries { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
