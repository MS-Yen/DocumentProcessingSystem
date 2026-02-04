"""
Service for managing the ChromaDB vector store.

ChromaDB is a lightweight vector database that stores embedding vectors
and supports fast approximate nearest-neighbor search using HNSW algorithm.
Data is persisted to disk and survives container restarts.
"""

import logging

import chromadb

logger = logging.getLogger(__name__)

COLLECTION_NAME = "documents"


class VectorStoreService:
    """Manages document chunk storage and retrieval in ChromaDB."""

    def __init__(self, persist_directory: str) -> None:
        self._client = chromadb.PersistentClient(path=persist_directory)

        # Cosine similarity: 0 distance = identical, 2 = opposite
        self._collection = self._client.get_or_create_collection(
            name=COLLECTION_NAME,
            metadata={"hnsw:space": "cosine"},
        )
        logger.info(
            "ChromaDB initialized at '%s', collection '%s' has %d entries",
            persist_directory,
            COLLECTION_NAME,
            self._collection.count(),
        )

    def add_chunks(
        self,
        document_id: str,
        chunks: list[str],
        embeddings: list[list[float]],
        metadata: dict | None = None,
    ) -> int:
        """Store document chunks with their embeddings in the vector store.

        Args:
            document_id: Unique identifier for the source document.
            chunks: List of text chunks (the actual text content).
            embeddings: Corresponding embedding vectors (same length as chunks).
            metadata: Optional metadata to attach to each chunk.

        Returns:
            Number of chunks stored.
        """
        ids = [f"{document_id}_chunk_{i}" for i in range(len(chunks))]

        metadatas = [
            {
                "document_id": document_id,
                "chunk_index": i,
                **(metadata or {}),
            }
            for i in range(len(chunks))
        ]

        self._collection.add(
            ids=ids,
            embeddings=embeddings,
            documents=chunks,
            metadatas=metadatas,
        )

        logger.info("Stored %d chunks for document '%s'", len(chunks), document_id)
        return len(chunks)

    def query(
        self,
        query_embedding: list[float],
        top_k: int = 3,
        document_ids: list[str] | None = None,
    ) -> dict:
        """Query the vector store for the most similar chunks.

        Args:
            query_embedding: The embedding vector of the user's question.
            top_k: Number of results to return (default 3).
            document_ids: Optional filter to search within specific documents only.

        Returns:
            ChromaDB results dict with keys: 'ids', 'documents', 'metadatas', 'distances'.
        """
        where_filter = None
        if document_ids:
            if len(document_ids) == 1:
                where_filter = {"document_id": document_ids[0]}
            else:
                where_filter = {"document_id": {"$in": document_ids}}

        results = self._collection.query(
            query_embeddings=[query_embedding],
            n_results=top_k,
            where=where_filter,
            include=["documents", "metadatas", "distances"],
        )

        logger.info(
            "Query returned %d results (top_k=%d, filter=%s)",
            len(results["ids"][0]) if results["ids"] else 0,
            top_k,
            document_ids,
        )
        return results

    def delete_document(self, document_id: str) -> int:
        """Delete all chunks belonging to a document.

        Args:
            document_id: ID of the document to delete.

        Returns:
            Number of chunks deleted.
        """
        existing = self._collection.get(where={"document_id": document_id})
        count = len(existing["ids"])

        if count > 0:
            self._collection.delete(ids=existing["ids"])
            logger.info("Deleted %d chunks for document '%s'", count, document_id)
        else:
            logger.info("No chunks found for document '%s'", document_id)

        return count

    def get_document_ids(self) -> list[str]:
        """Get all unique document IDs stored in the vector store.

        Returns:
            Sorted list of unique document IDs.
        """
        all_metadata = self._collection.get(include=["metadatas"])
        doc_ids = {m["document_id"] for m in all_metadata["metadatas"] if m}
        return sorted(doc_ids)

    def check_connection(self) -> bool:
        """Check if ChromaDB is operational.

        Returns:
            True if the collection is accessible.
        """
        try:
            self._collection.count()
            return True
        except Exception as e:
            logger.error("ChromaDB health check failed: %s", e)
            return False
