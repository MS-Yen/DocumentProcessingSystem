"""
API routes for the RAG service.

Each function decorated with @router.post/get becomes an endpoint.
Request/response bodies are automatically validated via Pydantic type hints.
"""

import logging

from fastapi import APIRouter, HTTPException

from app.models import (
    DeleteRequest,
    DeleteResponse,
    HealthResponse,
    IndexRequest,
    IndexResponse,
    QueryRequest,
    QueryResponse,
)
from app.services.embedding_service import EmbeddingService
from app.services.rag_service import RagService
from app.services.vector_store_service import VectorStoreService

logger = logging.getLogger(__name__)

router = APIRouter()

# Service references (set during app startup via init_router)
_rag_service: RagService | None = None
_embedding_service: EmbeddingService | None = None
_vector_store: VectorStoreService | None = None


def init_router(
    rag_service: RagService,
    embedding_service: EmbeddingService,
    vector_store: VectorStoreService,
) -> None:
    """Initialize the router with service instances.

    Called once during app startup (from main.py's lifespan function).
    """
    global _rag_service, _embedding_service, _vector_store
    _rag_service = rag_service
    _embedding_service = embedding_service
    _vector_store = vector_store


def _get_rag_service() -> RagService:
    """Get the RAG service instance, or return 503 if not ready."""
    if _rag_service is None:
        raise HTTPException(status_code=503, detail="RAG service not initialized")
    return _rag_service


@router.post("/index", response_model=IndexResponse)
async def index_document(request: IndexRequest) -> IndexResponse:
    """Index a document for RAG retrieval.

    Splits the document into chunks, generates embeddings, and stores them
    in the vector database. Re-indexing the same document_id replaces the
    previous version.
    """
    service = _get_rag_service()

    try:
        chunks_indexed = service.index_document(
            document_id=request.document_id,
            content=request.content,
            metadata={"filename": request.filename, **(request.metadata or {})},
        )

        return IndexResponse(
            document_id=request.document_id,
            chunks_indexed=chunks_indexed,
            message=f"Successfully indexed {chunks_indexed} chunks",
        )
    except Exception as e:
        logger.exception("Failed to index document '%s'", request.document_id)
        raise HTTPException(status_code=500, detail=f"Indexing failed: {e}") from e


@router.post("/query", response_model=QueryResponse)
async def query_documents(request: QueryRequest) -> QueryResponse:
    """Ask a question and get an answer based on indexed documents.

    Converts the question to a vector, finds similar chunks in the vector DB,
    sends chunks + question to the LLM, and returns the answer with sources.
    """
    service = _get_rag_service()

    try:
        response = service.query(
            question=request.question,
            document_ids=request.document_ids,
            top_k=request.top_k,
        )
        return response
    except Exception as e:
        logger.exception("Query failed for question: '%s'", request.question)
        raise HTTPException(status_code=500, detail=f"Query failed: {e}") from e


@router.post("/delete", response_model=DeleteResponse)
async def delete_document(request: DeleteRequest) -> DeleteResponse:
    """Delete a document and all its chunks from the vector store."""
    service = _get_rag_service()

    try:
        chunks_deleted = service.delete_document(document_id=request.document_id)

        return DeleteResponse(
            document_id=request.document_id,
            chunks_deleted=chunks_deleted,
            message=f"Deleted {chunks_deleted} chunks"
            if chunks_deleted > 0
            else "No chunks found for this document",
        )
    except Exception as e:
        logger.exception("Failed to delete document '%s'", request.document_id)
        raise HTTPException(status_code=500, detail=f"Delete failed: {e}") from e


@router.get("/documents", response_model=list[str])
async def list_documents() -> list[str]:
    """List all document IDs currently stored in the vector database."""
    if _vector_store is None:
        raise HTTPException(status_code=503, detail="Service not initialized")

    try:
        return _vector_store.get_document_ids()
    except Exception as e:
        logger.exception("Failed to list documents")
        raise HTTPException(status_code=500, detail=f"Failed to list documents: {e}") from e


@router.get("/health", response_model=HealthResponse)
async def health_check() -> HealthResponse:
    """Check the health of the RAG service and its dependencies.

    Verifies that both Ollama and ChromaDB are reachable.
    Returns "healthy", "degraded", or "unhealthy".
    """
    if _embedding_service is None or _vector_store is None:
        return HealthResponse(
            status="unhealthy",
            ollama_connected=False,
            chromadb_connected=False,
            models_available=[],
        )

    ollama_ok, models = await _embedding_service.check_connection()
    chroma_ok = _vector_store.check_connection()

    status = "healthy" if (ollama_ok and chroma_ok) else "degraded"

    return HealthResponse(
        status=status,
        ollama_connected=ollama_ok,
        chromadb_connected=chroma_ok,
        models_available=models,
    )
