"""
Pydantic models for the RAG Service API.

These define the request/response shapes, validate data automatically,
and serialize to/from JSON.
"""

from pydantic import BaseModel, Field


# Request models

class IndexRequest(BaseModel):
    """Request to index a document for RAG processing."""

    document_id: str = Field(..., description="Unique identifier for the document")
    content: str = Field(..., min_length=1, description="Document text content to index")
    filename: str = Field(..., description="Original filename of the document")
    metadata: dict | None = Field(default=None, description="Optional metadata about the document")


class IndexResponse(BaseModel):
    """Response after indexing a document."""

    document_id: str
    chunks_indexed: int
    message: str


class QueryRequest(BaseModel):
    """Request to query documents using RAG."""

    question: str = Field(..., min_length=1, description="Question to ask about the documents")
    document_ids: list[str] | None = Field(
        default=None,
        description="Optional list of document IDs to search within. Searches all if not provided.",
    )
    top_k: int = Field(default=3, ge=1, le=10, description="Number of relevant chunks to retrieve")


# Response models

class QueryResponse(BaseModel):
    """Response from a RAG query with the answer and source citations."""

    answer: str
    sources: list["SourceChunk"]
    document_ids_searched: list[str]


class SourceChunk(BaseModel):
    """A retrieved chunk used as context for the answer."""

    document_id: str
    chunk_text: str
    relevance_score: float  # 0.0 to 1.0
    metadata: dict | None = None


class DeleteRequest(BaseModel):
    """Request to delete a document from the vector store."""

    document_id: str = Field(..., description="ID of the document to delete")


class DeleteResponse(BaseModel):
    """Response after deleting a document."""

    document_id: str
    chunks_deleted: int
    message: str


class HealthResponse(BaseModel):
    """Health check response for Docker healthchecks and monitoring."""

    status: str  # "healthy", "degraded", or "unhealthy"
    ollama_connected: bool
    chromadb_connected: bool
    models_available: list[str]
