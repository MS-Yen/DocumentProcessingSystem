using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ReportService.Core.Interfaces;
using ReportService.Core.Models;

namespace ReportService.Infrastructure.Reports;

public class AnalyticsReportGenerator : IAnalyticsReportGenerator
{
    public byte[] Generate(AnalyticsData analytics)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(ReportStyles.PageMargin, Unit.Point);
                page.DefaultTextStyle(x => x.FontSize(ReportStyles.BodySize)
                    .FontColor(ReportStyles.BodyText));

                page.Header().Element(header =>
                {
                    header
                        .Background(ReportStyles.SectionHeader)
                        .Padding(12)
                        .Row(row =>
                        {
                            row.RelativeItem()
                                .Text("System Analytics Report")
                                .FontSize(ReportStyles.SubSectionSize)
                                .FontColor(ReportStyles.SectionHeaderText)
                                .Bold();

                            row.ConstantItem(150)
                                .AlignRight()
                                .Text(analytics.GeneratedAt.ToString("yyyy-MM-dd HH:mm UTC"))
                                .FontSize(ReportStyles.SmallSize)
                                .FontColor(ReportStyles.SectionHeaderText);
                        });
                });

                page.Content().PaddingVertical(15).Column(content =>
                {
                    content.Item().Element(e => ComposeCover(e, analytics));

                    content.Item().PaddingVertical(10).LineHorizontal(1)
                        .LineColor(ReportStyles.TableBorder);

                    content.Item().Element(e => ComposeExecutiveSummary(e, analytics));

                    content.Item().PaddingVertical(10).LineHorizontal(1)
                        .LineColor(ReportStyles.TableBorder);

                    content.Item().Element(e => ComposeTypeBreakdown(e, analytics));

                    content.Item().PaddingVertical(10).LineHorizontal(1)
                        .LineColor(ReportStyles.TableBorder);

                    content.Item().Element(e => ComposeRagCoverage(e, analytics));

                    if (analytics.RecentDocuments.Count > 0)
                    {
                        content.Item().PaddingVertical(10).LineHorizontal(1)
                            .LineColor(ReportStyles.TableBorder);

                        content.Item().Element(e => ComposeRecentDocuments(e, analytics));
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(ReportStyles.FooterSize)
                        .FontColor(ReportStyles.LightText));
                    text.Span("Document Processing System — Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        })
        .GeneratePdf();
    }

    private void ComposeCover(IContainer container, AnalyticsData analytics)
    {
        container
            .Background(ReportStyles.CoverBackground)
            .Padding(30)
            .Column(col =>
            {
                col.Item().Text("System Analytics")
                    .FontSize(ReportStyles.TitleSize)
                    .FontColor(ReportStyles.CoverText)
                    .Bold();

                col.Item().PaddingTop(10).Text("Executive Summary — Document Processing System")
                    .FontSize(ReportStyles.SubtitleSize)
                    .FontColor(ReportStyles.CoverText);

                col.Item().PaddingTop(5)
                    .Text($"Generated: {analytics.GeneratedAt:yyyy-MM-dd HH:mm:ss UTC}")
                    .FontSize(ReportStyles.SmallSize)
                    .FontColor(ReportStyles.CoverText)
                    .Light();
            });
    }

    private void ComposeExecutiveSummary(IContainer container, AnalyticsData analytics)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(8).Text("Executive Summary")
                .FontSize(ReportStyles.SectionHeaderSize)
                .FontColor(ReportStyles.SectionHeader)
                .Bold();

            // Row of metric cards
            col.Item().Row(row =>
            {
                void AddMetricCard(string value, string label, string color)
                {
                    row.RelativeItem()
                        .Border(1).BorderColor(ReportStyles.TableBorder)
                        .Padding(12)
                        .Column(card =>
                        {
                            card.Item().AlignCenter()
                                .Text(value)
                                .FontSize(24)
                                .FontColor(color)
                                .Bold();

                            card.Item().AlignCenter()
                                .PaddingTop(4)
                                .Text(label)
                                .FontSize(ReportStyles.SmallSize)
                                .FontColor(ReportStyles.LightText);
                        });

                    row.ConstantItem(8); // Spacer between cards
                }

                AddMetricCard(
                    analytics.TotalDocuments.ToString(),
                    "Total Documents",
                    ReportStyles.SectionHeader);

                AddMetricCard(
                    analytics.TotalIndexed.ToString(),
                    "Indexed in RAG",
                    ReportStyles.StatusHealthy);

                AddMetricCard(
                    ReportStyles.FormatFileSize(analytics.TotalStorageBytes),
                    "Total Storage",
                    ReportStyles.SectionHeader);

                AddMetricCard(
                    analytics.TotalDocumentsInVectorDb.ToString(),
                    "In Vector DB",
                    ReportStyles.SectionHeader);
            });

            // Status breakdown row
            col.Item().PaddingTop(10).Row(row =>
            {
                void AddStatusPill(int count, string label, string color)
                {
                    row.RelativeItem()
                        .Background(color + "20") // 20 = 12% opacity in hex
                        .Border(1).BorderColor(color)
                        .Padding(8)
                        .Row(pill =>
                        {
                            pill.ConstantItem(8).Height(8).Background(color);
                            pill.ConstantItem(6); // Spacer
                            pill.RelativeItem().Text($"{count} {label}")
                                .FontSize(ReportStyles.SmallSize)
                                .FontColor(ReportStyles.BodyText);
                        });

                    row.ConstantItem(8);
                }

                AddStatusPill(analytics.TotalIndexed, "Indexed", ReportStyles.StatusHealthy);
                AddStatusPill(analytics.TotalPending, "Pending", ReportStyles.StatusWarning);
                AddStatusPill(analytics.TotalFailed, "Failed", ReportStyles.StatusError);
            });
        });
    }

    private void ComposeTypeBreakdown(IContainer container, AnalyticsData analytics)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(8).Text("Document Type Distribution")
                .FontSize(ReportStyles.SectionHeaderSize)
                .FontColor(ReportStyles.SectionHeader)
                .Bold();

            if (analytics.TypeBreakdown.Count == 0)
            {
                col.Item().Padding(10)
                    .Text("No documents uploaded yet.")
                    .FontColor(ReportStyles.LightText)
                    .Italic();
                return;
            }

            var maxCount = analytics.TypeBreakdown.Max(t => t.Count);

            foreach (var type in analytics.TypeBreakdown.OrderByDescending(t => t.Count))
            {
                col.Item().PaddingVertical(3).Row(row =>
                {
                    row.ConstantItem(100)
                        .Text(SimplifyContentType(type.ContentType))
                        .FontSize(ReportStyles.SmallSize)
                        .Bold();

                    // Bar width proportional to count (max 250 points)
                    var barWidth = maxCount > 0
                        ? (float)type.Count / maxCount * 250
                        : 0;

                    row.ConstantItem(260).Column(barCol =>
                    {
                        barCol.Item().Row(barRow =>
                        {
                            barRow.ConstantItem(barWidth)
                                .Height(16)
                                .Background(ReportStyles.SectionHeader);
                            barRow.RelativeItem();
                        });
                    });

                    row.ConstantItem(10); // Spacer

                    row.RelativeItem()
                        .AlignLeft()
                        .Text($"{type.Count} file{(type.Count != 1 ? "s" : "")} ({ReportStyles.FormatFileSize(type.TotalSize)})")
                        .FontSize(ReportStyles.SmallSize);
                });
            }
        });
    }

    private void ComposeRagCoverage(IContainer container, AnalyticsData analytics)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(8).Text("RAG Indexing Coverage")
                .FontSize(ReportStyles.SectionHeaderSize)
                .FontColor(ReportStyles.SectionHeader)
                .Bold();

            var coveragePercent = analytics.TotalDocuments > 0
                ? (double)analytics.TotalIndexed / analytics.TotalDocuments * 100
                : 0;

            var coverageColor = coveragePercent switch
            {
                >= 80 => ReportStyles.StatusHealthy,
                >= 50 => ReportStyles.StatusWarning,
                _ => ReportStyles.StatusError
            };

            col.Item().Row(row =>
            {
                row.ConstantItem(120)
                    .AlignCenter()
                    .Text($"{coveragePercent:F0}%")
                    .FontSize(36)
                    .FontColor(coverageColor)
                    .Bold();

                row.RelativeItem()
                    .PaddingLeft(15)
                    .AlignMiddle()
                    .Column(details =>
                    {
                        details.Item().Text($"{analytics.TotalIndexed} of {analytics.TotalDocuments} documents indexed")
                            .FontSize(ReportStyles.BodySize);

                        details.Item().PaddingTop(4)
                            .Text($"{analytics.TotalDocumentsInVectorDb} documents in vector database")
                            .FontSize(ReportStyles.SmallSize)
                            .FontColor(ReportStyles.LightText);

                        if (analytics.TotalFailed > 0)
                        {
                            details.Item().PaddingTop(4)
                                .Text($"{analytics.TotalFailed} document(s) failed indexing")
                                .FontSize(ReportStyles.SmallSize)
                                .FontColor(ReportStyles.StatusError);
                        }
                    });
            });

            // Progress bar
            col.Item().PaddingTop(10).Row(row =>
            {
                var fillWidth = (float)(coveragePercent / 100.0 * 400);
                var emptyWidth = 400 - fillWidth;

                row.ConstantItem(fillWidth)
                    .Height(8)
                    .Background(coverageColor);

                row.ConstantItem(emptyWidth)
                    .Height(8)
                    .Background(ReportStyles.TableBorder);

                row.RelativeItem();
            });
        });
    }

    private void ComposeRecentDocuments(IContainer container, AnalyticsData analytics)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(8).Text("Recent Documents")
                .FontSize(ReportStyles.SectionHeaderSize)
                .FontColor(ReportStyles.SectionHeader)
                .Bold();

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);   // File name
                    columns.RelativeColumn(1.5f); // Type
                    columns.RelativeColumn(1);    // Size
                    columns.RelativeColumn(1.5f); // Status
                    columns.RelativeColumn(2);    // Upload date
                });

                void AddHeaderCell(string text, uint column)
                {
                    table.Cell().Row(1).Column(column)
                        .Background(ReportStyles.TableHeader)
                        .Border(0.5f).BorderColor(ReportStyles.TableBorder)
                        .Padding(6)
                        .Text(text)
                        .FontSize(ReportStyles.SmallSize)
                        .FontColor(ReportStyles.TableHeaderText)
                        .Bold();
                }

                AddHeaderCell("File Name", 1);
                AddHeaderCell("Type", 2);
                AddHeaderCell("Size", 3);
                AddHeaderCell("Status", 4);
                AddHeaderCell("Uploaded", 5);

                var recentDocs = analytics.RecentDocuments.Take(10).ToList();
                for (int i = 0; i < recentDocs.Count; i++)
                {
                    var doc = recentDocs[i];
                    uint row = (uint)(i + 2);
                    var bg = i % 2 == 0
                        ? ReportStyles.TableRowOdd
                        : ReportStyles.TableRowEven;

                    void AddCell(string text, uint column, string? fontColor = null)
                    {
                        var cell = table.Cell().Row(row).Column(column)
                            .Background(bg)
                            .Border(0.5f).BorderColor(ReportStyles.TableBorder)
                            .Padding(6);

                        if (fontColor != null)
                            cell.Text(text)
                                .FontSize(ReportStyles.SmallSize)
                                .FontColor(fontColor);
                        else
                            cell.Text(text)
                                .FontSize(ReportStyles.SmallSize);
                    }

                    AddCell(TruncateText(doc.FileName, 30), 1);
                    AddCell(SimplifyContentType(doc.ContentType), 2);
                    AddCell(ReportStyles.FormatFileSize(doc.FileSize), 3);
                    AddCell(doc.Status, 4, ReportStyles.GetStatusColor(doc.Status));
                    AddCell(doc.UploadedAt.ToString("yyyy-MM-dd HH:mm"), 5);
                }
            });
        });
    }

    /// <summary>
    /// Converts MIME types to human-friendly labels.
    /// </summary>
    private static string SimplifyContentType(string contentType)
    {
        return contentType.ToLower() switch
        {
            "application/pdf" => "PDF",
            "text/plain" => "TXT",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => "DOCX",
            "application/msword" => "DOC",
            _ => contentType
        };
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..maxLength] + "...";
    }
}
