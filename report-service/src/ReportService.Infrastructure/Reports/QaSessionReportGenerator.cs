using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ReportService.Core.Interfaces;
using ReportService.Core.Models;

namespace ReportService.Infrastructure.Reports;

public class QaSessionReportGenerator : IQaSessionReportGenerator
{
    public byte[] Generate(QaSessionData session)
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
                                .Text("Q&A Session Report")
                                .FontSize(ReportStyles.SubSectionSize)
                                .FontColor(ReportStyles.SectionHeaderText)
                                .Bold();

                            row.ConstantItem(150)
                                .AlignRight()
                                .Text(session.GeneratedAt.ToString("yyyy-MM-dd HH:mm UTC"))
                                .FontSize(ReportStyles.SmallSize)
                                .FontColor(ReportStyles.SectionHeaderText);
                        });
                });

                page.Content().PaddingVertical(15).Column(content =>
                {
                    content.Item().Element(e => ComposeCover(e, session));

                    content.Item().PaddingVertical(10).LineHorizontal(1)
                        .LineColor(ReportStyles.TableBorder);

                    content.Item().Element(e => ComposeTableOfContents(e, session));

                    content.Item().PaddingVertical(10).LineHorizontal(1)
                        .LineColor(ReportStyles.TableBorder);

                    for (int i = 0; i < session.Entries.Count; i++)
                    {
                        if (i > 0)
                            content.Item().PageBreak();

                        var index = i; // Capture for closure
                        content.Item().Element(e =>
                            ComposeQaEntry(e, session.Entries[index], index + 1));
                    }

                    content.Item().PageBreak();
                    content.Item().Element(e => ComposeSummary(e, session));
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

    private void ComposeCover(IContainer container, QaSessionData session)
    {
        container
            .Background(ReportStyles.CoverBackground)
            .Padding(30)
            .Column(col =>
            {
                col.Item().Text(session.SessionTitle)
                    .FontSize(ReportStyles.TitleSize)
                    .FontColor(ReportStyles.CoverText)
                    .Bold();

                col.Item().PaddingTop(10).Text($"Document: {session.Document.FileName}")
                    .FontSize(ReportStyles.SubtitleSize)
                    .FontColor(ReportStyles.CoverText);

                col.Item().PaddingTop(5).Text($"{session.Entries.Count} questions asked")
                    .FontSize(ReportStyles.BodySize)
                    .FontColor(ReportStyles.CoverText)
                    .Light();
            });
    }

    private void ComposeTableOfContents(IContainer container, QaSessionData session)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(8).Text("Questions Overview")
                .FontSize(ReportStyles.SectionHeaderSize)
                .FontColor(ReportStyles.SectionHeader)
                .Bold();

            for (int i = 0; i < session.Entries.Count; i++)
            {
                var entry = session.Entries[i];
                var bgColor = i % 2 == 0
                    ? ReportStyles.TableRowOdd
                    : ReportStyles.TableRowEven;

                col.Item()
                    .Background(bgColor)
                    .Padding(8)
                    .Text($"{i + 1}. {entry.Question}")
                    .FontSize(ReportStyles.BodySize);
            }
        });
    }

    private void ComposeQaEntry(IContainer container, QaEntry entry, int number)
    {
        container.Column(col =>
        {
            // Question header
            col.Item()
                .Background(ReportStyles.SectionHeader)
                .Padding(12)
                .Text($"Question {number}")
                .FontSize(ReportStyles.SubSectionSize)
                .FontColor(ReportStyles.SectionHeaderText)
                .Bold();

            // Question text
            col.Item()
                .PaddingVertical(10)
                .Text(entry.Question)
                .FontSize(ReportStyles.SubSectionSize)
                .Bold();

            // Answer section
            col.Item().PaddingBottom(5).Text("Answer")
                .FontSize(ReportStyles.BodySize)
                .FontColor(ReportStyles.SectionHeader)
                .Bold();

            col.Item()
                .PaddingBottom(10)
                .PaddingLeft(10)
                .Text(entry.Answer)
                .FontSize(ReportStyles.BodySize)
                .LineHeight(1.4f);

            // Source Citations
            if (entry.Sources.Count > 0)
            {
                col.Item().PaddingBottom(5).Text($"Source Citations ({entry.Sources.Count})")
                    .FontSize(ReportStyles.BodySize)
                    .FontColor(ReportStyles.SectionHeader)
                    .Bold();

                for (int i = 0; i < entry.Sources.Count; i++)
                {
                    var source = entry.Sources[i];
                    var sourceIndex = i;
                    col.Item().PaddingBottom(5).Element(e =>
                        ComposeSourceCitation(e, source, sourceIndex + 1));
                }
            }
            else
            {
                col.Item()
                    .Background(ReportStyles.CitationBackground)
                    .Padding(10)
                    .Text("No source citations available for this answer.")
                    .FontSize(ReportStyles.SmallSize)
                    .FontColor(ReportStyles.LightText)
                    .Italic();
            }
        });
    }

    private void ComposeSourceCitation(IContainer container, SourceChunk source, int number)
    {
        var scoreColor = ReportStyles.GetScoreColor(source.RelevanceScore);
        var scorePercent = (source.RelevanceScore * 100).ToString("F0");

        container
            .Background(ReportStyles.CitationBackground)
            .Border(0.5f).BorderColor(ReportStyles.CitationBorder)
            .Padding(10)
            .Column(col =>
            {
                // Source header with relevance score
                col.Item().Row(row =>
                {
                    row.RelativeItem()
                        .Text($"Source {number}")
                        .FontSize(ReportStyles.SmallSize)
                        .Bold();

                    row.ConstantItem(100)
                        .AlignRight()
                        .Text(text =>
                        {
                            text.Span("Relevance: ")
                                .FontSize(ReportStyles.SmallSize);
                            text.Span($"{scorePercent}%")
                                .FontSize(ReportStyles.SmallSize)
                                .FontColor(scoreColor)
                                .Bold();
                        });
                });

                // Visual score bar
                col.Item().PaddingVertical(4).Row(row =>
                {
                    var fillWidth = (float)(source.RelevanceScore * 200);
                    row.ConstantItem(fillWidth)
                        .Height(4)
                        .Background(scoreColor);

                    var emptyWidth = (float)((1 - source.RelevanceScore) * 200);
                    row.ConstantItem(emptyWidth)
                        .Height(4)
                        .Background(ReportStyles.TableBorder);

                    row.RelativeItem();
                });

                // Chunk text
                col.Item().PaddingTop(5)
                    .Text($"\"{TruncateText(source.ChunkText, 500)}\"")
                    .FontSize(ReportStyles.SmallSize)
                    .Italic()
                    .LineHeight(1.3f);

                // Document ID reference
                col.Item().PaddingTop(4)
                    .Text($"Document: {source.DocumentId}")
                    .FontSize(ReportStyles.FooterSize)
                    .FontColor(ReportStyles.LightText);
            });
    }

    private void ComposeSummary(IContainer container, QaSessionData session)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(8).Text("Session Summary")
                .FontSize(ReportStyles.SectionHeaderSize)
                .FontColor(ReportStyles.SectionHeader)
                .Bold();

            var totalSources = session.Entries.Sum(e => e.Sources.Count);
            var allScores = session.Entries
                .SelectMany(e => e.Sources)
                .Select(s => s.RelevanceScore)
                .ToList();
            var avgScore = allScores.Count > 0 ? allScores.Average() : 0;

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(200);
                    columns.RelativeColumn();
                });

                uint rowIndex = 0;
                void AddRow(string label, string value)
                {
                    rowIndex++;
                    var bg = rowIndex % 2 == 0
                        ? ReportStyles.TableRowEven
                        : ReportStyles.TableRowOdd;

                    table.Cell().Row(rowIndex).Column(1)
                        .Background(bg)
                        .Border(0.5f).BorderColor(ReportStyles.TableBorder)
                        .Padding(8)
                        .Text(label).Bold();

                    table.Cell().Row(rowIndex).Column(2)
                        .Background(bg)
                        .Border(0.5f).BorderColor(ReportStyles.TableBorder)
                        .Padding(8)
                        .Text(value);
                }

                AddRow("Document", session.Document.FileName);
                AddRow("Total Questions", session.Entries.Count.ToString());
                AddRow("Total Source Citations", totalSources.ToString());
                AddRow("Average Relevance Score", $"{(avgScore * 100):F1}%");
                AddRow("Session Generated", session.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss UTC"));
            });

            // Score distribution note
            col.Item().PaddingTop(15)
                .Background(ReportStyles.CitationBackground)
                .Border(0.5f).BorderColor(ReportStyles.CitationBorder)
                .Padding(10)
                .Column(noteCol =>
                {
                    noteCol.Item().Text("Relevance Score Guide")
                        .FontSize(ReportStyles.SmallSize).Bold();
                    noteCol.Item().PaddingTop(4).Text(text =>
                    {
                        text.DefaultTextStyle(x => x.FontSize(ReportStyles.SmallSize));
                        text.Span("High (>70%): ").FontColor(ReportStyles.ScoreHigh).Bold();
                        text.Span("Strong match — answer is well-supported by source text. ");
                        text.Span("Medium (40-70%): ").FontColor(ReportStyles.ScoreMedium).Bold();
                        text.Span("Partial match — answer may include interpretation. ");
                        text.Span("Low (<40%): ").FontColor(ReportStyles.ScoreLow).Bold();
                        text.Span("Weak match — verify answer against original document.");
                    });
                });
        });
    }

    /// <summary>
    /// Truncates text to a maximum length, adding "..." if truncated.
    /// </summary>
    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text[..maxLength] + "...";
    }
}
