"""FastAPI application entry point for the RAG service."""

import logging
from contextlib import asynccontextmanager
from typing import AsyncIterator

from fastapi import FastAPI

from app.config import Settings
from app.routers import rag_router
from app.services.embedding_service import EmbeddingService
from app.services.rag_service import RagService
from app.services.vector_store_service import VectorStoreService


def _setup_logging(level: str) -> None:
    """Configure the root logger with a standard format."""
    logging.basicConfig(
        level=level.upper(),
        format="%(asctime)s | %(levelname)-8s | %(name)s | %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S",
    )


@asynccontextmanager
async def lifespan(app: FastAPI) -> AsyncIterator[None]:
    """Initialize services on startup and clean up on shutdown."""
    settings = Settings()
    _setup_logging(settings.log_level)

    logger = logging.getLogger(__name__)
    logger.info("Starting RAG service...")
    logger.info("Ollama URL: %s", settings.ollama_base_url)
    logger.info("Embedding model: %s", settings.embedding_model)
    logger.info("LLM model: %s", settings.llm_model)
    logger.info("ChromaDB path: %s", settings.chroma_persist_dir)

    # Compose services (composition root)
    embedding_service = EmbeddingService(
        ollama_base_url=settings.ollama_base_url,
        embedding_model=settings.embedding_model,
    )

    vector_store = VectorStoreService(persist_directory=settings.chroma_persist_dir)

    rag_service = RagService(
        embedding_service=embedding_service,
        vector_store=vector_store,
        ollama_base_url=settings.ollama_base_url,
        llm_model=settings.llm_model,
    )

    rag_router.init_router(rag_service, embedding_service, vector_store)

    logger.info("RAG service ready")

    yield

    logger.info("RAG service shutting down")


app = FastAPI(
    title="RAG Service",
    description="Retrieval-Augmented Generation service for document Q&A",
    version="1.0.0",
    lifespan=lifespan,
)

app.include_router(rag_router.router, prefix="/api/rag", tags=["RAG"])
