namespace ReportService.Core.Models;

/// <summary>
/// A single chunk of text retrieved from the vector database.
/// Contains the text, its source document, and how relevant it was to the query.
/// </summary>
public record SourceChunk(
    string DocumentId,
    string ChunkText,
    double RelevanceScore,
    Dictionary<string, object>? Metadata
);

/// <summary>
/// Full response from POST /api/rag/query.
/// Contains the AI answer and the supporting source chunks.
/// </summary>
public record RagQueryResult(
    string Answer,
    List<SourceChunk> Sources,
    List<string> DocumentIdsSearched
);
