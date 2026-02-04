using System.ComponentModel.DataAnnotations;

namespace ReportService.Core.DTOs;

/// <summary>
/// Request to generate a Document Summary Report.
/// </summary>
public class DocumentSummaryRequest
{
    [Required]
    public Guid DocumentId { get; set; }
}

/// <summary>
/// Request to generate a Q&A Session Report.
/// </summary>
public class QaSessionRequest
{
    [Required]
    public Guid DocumentId { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one question is required.")]
    public List<string> Questions { get; set; } = new();

    /// <summary>
    /// Optional title for the session. If omitted, a default is generated.
    /// </summary>
    public string? SessionTitle { get; set; }
}
