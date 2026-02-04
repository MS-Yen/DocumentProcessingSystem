namespace ReportService.Infrastructure.Reports;

public static class ReportStyles
{
    // Color Palette — Corporate Blue Theme

    // Cover page / header backgrounds
    public const string CoverBackground = "#1B4F72";     // Dark navy blue
    public const string CoverText = "#FFFFFF";            // White text on dark bg

    // Section headers
    public const string SectionHeader = "#2980B9";        // Medium blue
    public const string SectionHeaderText = "#FFFFFF";    // White text

    // Body text
    public const string BodyText = "#2C3E50";             // Dark gray (easier than pure black)
    public const string LightText = "#7F8C8D";            // Muted gray for secondary info

    // Table styling
    public const string TableHeader = "#2980B9";          // Blue header row
    public const string TableHeaderText = "#FFFFFF";      // White text in header
    public const string TableRowEven = "#EBF5FB";         // Light blue for even rows
    public const string TableRowOdd = "#FFFFFF";          // White for odd rows
    public const string TableBorder = "#BDC3C7";          // Light gray borders

    // Status indicators
    public const string StatusHealthy = "#27AE60";        // Green = indexed/healthy
    public const string StatusWarning = "#F39C12";        // Amber = pending/degraded
    public const string StatusError = "#E74C3C";          // Red = failed/error

    // Source citation backgrounds
    public const string CitationBackground = "#F8F9FA";   // Very light gray
    public const string CitationBorder = "#DEE2E6";       // Subtle border

    // Score colors (relevance score gradient)
    public const string ScoreHigh = "#27AE60";            // Green: > 0.7
    public const string ScoreMedium = "#F39C12";          // Amber: 0.4-0.7
    public const string ScoreLow = "#E74C3C";             // Red: < 0.4

    // Typography — Font sizes in points
    public const float TitleSize = 28;
    public const float SubtitleSize = 16;
    public const float SectionHeaderSize = 18;
    public const float SubSectionSize = 14;
    public const float BodySize = 11;
    public const float SmallSize = 9;
    public const float FooterSize = 8;

    // Layout — Margins and spacing in points (1 point = 1/72 inch)
    public const float PageMargin = 50;
    public const float SectionSpacing = 15;
    public const float ItemSpacing = 8;

    /// <summary>
    /// Returns a color based on the relevance score (0.0 to 1.0).
    /// </summary>
    public static string GetScoreColor(double score)
    {
        return score switch
        {
            > 0.7 => ScoreHigh,
            > 0.4 => ScoreMedium,
            _ => ScoreLow
        };
    }

    /// <summary>
    /// Returns a color based on document status string.
    /// </summary>
    public static string GetStatusColor(string status)
    {
        return status.ToLower() switch
        {
            "indexed" => StatusHealthy,
            "pending" => StatusWarning,
            "failed" => StatusError,
            _ => LightText
        };
    }

    /// <summary>
    /// Formats file size in human-readable units (KB, MB, GB).
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
            _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
        };
    }
}
