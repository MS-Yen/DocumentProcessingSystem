using ReportService.Core.Models;

namespace ReportService.Core.Interfaces;

/// <summary>
/// Generates a Document Summary PDF from document metadata.
/// </summary>
public interface IDocumentSummaryReportGenerator
{
    byte[] Generate(DocumentMetadata document);
}

/// <summary>
/// Generates a Q&A Session PDF with questions, answers, and source citations.
/// </summary>
public interface IQaSessionReportGenerator
{
    byte[] Generate(QaSessionData session);
}

/// <summary>
/// Generates an Analytics/Executive Summary PDF with system-wide statistics.
/// </summary>
public interface IAnalyticsReportGenerator
{
    byte[] Generate(AnalyticsData analytics);
}
