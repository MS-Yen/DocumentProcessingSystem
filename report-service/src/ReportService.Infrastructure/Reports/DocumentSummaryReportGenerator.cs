using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ReportService.Core.Interfaces;
using ReportService.Core.Models;

// Alias to resolve naming collision with QuestPDF.Infrastructure.DocumentMetadata
using DocumentMetadata = ReportService.Core.Models.DocumentMetadata;

namespace ReportService.Infrastructure.Reports;

public class DocumentSummaryReportGenerator : IDocumentSummaryReportGenerator
{
    /// <summary>
    /// Generates a Document Summary PDF and returns the raw bytes.
    /// </summary>
    public byte[] Generate(DocumentMetadata document)
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
                                .Text("Document Summary Report")
                                .FontSize(ReportStyles.SubSectionSize)
                                .FontColor(ReportStyles.SectionHeaderText)
                                .Bold();

                            row.ConstantItem(150)
                                .AlignRight()
                                .Text(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC"))
                                .FontSize(ReportStyles.SmallSize)
                                .FontColor(ReportStyles.SectionHeaderText);
                        });
                });

                page.Content().PaddingVertical(15).Column(content =>
                {
                    content.Item().Element(e => ComposeCoverSection(e, document));

                    content.Item().PaddingVertical(10).LineHorizontal(1)
                        .LineColor(ReportStyles.TableBorder);

                    content.Item().Element(e => ComposeMetadataTable(e, document));

                    content.Item().PaddingVertical(10).LineHorizontal(1)
                        .LineColor(ReportStyles.TableBorder);

                    content.Item().Element(e => ComposeRagSection(e, document));
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

    private void ComposeCoverSection(IContainer container, DocumentMetadata document)
    {
        container
            .Background(ReportStyles.CoverBackground)
            .Padding(30)
            .Column(col =>
            {
                col.Item().Text("Document Summary")
                    .FontSize(ReportStyles.TitleSize)
                    .FontColor(ReportStyles.CoverText)
                    .Bold();

                col.Item().PaddingTop(10).Text(document.FileName)
                    .FontSize(ReportStyles.SubtitleSize)
                    .FontColor(ReportStyles.CoverText);

                col.Item().PaddingTop(5).Text($"ID: {document.Id}")
                    .FontSize(ReportStyles.SmallSize)
                    .FontColor(ReportStyles.CoverText)
                    .Light();
            });
    }

    private void ComposeMetadataTable(IContainer container, DocumentMetadata document)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(8).Text("Document Metadata")
                .FontSize(ReportStyles.SectionHeaderSize)
                .FontColor(ReportStyles.SectionHeader)
                .Bold();

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(160);
                    columns.RelativeColumn();
                });

                uint rowIndex = 0;
                void AddRow(string label, string value)
                {
                    rowIndex++;
                    var bgColor = rowIndex % 2 == 0
                        ? ReportStyles.TableRowEven
                        : ReportStyles.TableRowOdd;

                    table.Cell().Row(rowIndex).Column(1)
                        .Background(bgColor)
                        .Border(0.5f).BorderColor(ReportStyles.TableBorder)
                        .Padding(8)
                        .Text(label).Bold();

                    table.Cell().Row(rowIndex).Column(2)
                        .Background(bgColor)
                        .Border(0.5f).BorderColor(ReportStyles.TableBorder)
                        .Padding(8)
                        .Text(value);
                }

                AddRow("File Name", document.FileName);
                AddRow("Content Type", document.ContentType);
                AddRow("File Size", ReportStyles.FormatFileSize(document.FileSize));
                AddRow("Uploaded At", document.UploadedAt.ToString("yyyy-MM-dd HH:mm:ss UTC"));
                AddRow("Document ID", document.Id.ToString());
                AddRow("Status", document.Status);
            });
        });
    }

    private void ComposeRagSection(IContainer container, DocumentMetadata document)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(8).Text("RAG Analysis")
                .FontSize(ReportStyles.SectionHeaderSize)
                .FontColor(ReportStyles.SectionHeader)
                .Bold();

            var statusColor = ReportStyles.GetStatusColor(document.Status);
            col.Item()
                .Border(1).BorderColor(ReportStyles.TableBorder)
                .Padding(15)
                .Row(row =>
                {
                    row.ConstantItem(12)
                        .Height(12)
                        .Background(statusColor);

                    row.ConstantItem(10); // Spacer

                    row.RelativeItem().Column(statusCol =>
                    {
                        statusCol.Item().Text(text =>
                        {
                            text.Span("Indexing Status: ").Bold();
                            text.Span(document.RagIndexed ? "Indexed" : "Not Indexed")
                                .FontColor(statusColor);
                        });

                        if (!string.IsNullOrEmpty(document.RagError))
                        {
                            statusCol.Item().PaddingTop(5)
                                .Text($"Error: {document.RagError}")
                                .FontColor(ReportStyles.StatusError)
                                .FontSize(ReportStyles.SmallSize);
                        }

                        statusCol.Item().PaddingTop(8).Text(text =>
                        {
                            text.Span("Document Status: ").Bold();
                            text.Span(document.Status);
                        });
                    });
                });

            col.Item().PaddingTop(10)
                .Background(ReportStyles.CitationBackground)
                .Border(0.5f).BorderColor(ReportStyles.CitationBorder)
                .Padding(10)
                .Text("This report was generated by the Document Processing System's " +
                      "Report Service. The RAG indexing status indicates whether the " +
                      "document has been chunked, embedded, and stored in the vector " +
                      "database for AI-powered question answering.")
                .FontSize(ReportStyles.SmallSize)
                .FontColor(ReportStyles.LightText);
        });
    }
}
