"""
Core RAG orchestration service.

RAG (Retrieval-Augmented Generation) pipeline:
1. INDEXING: Split document into chunks, generate embeddings, store in vector DB
2. RETRIEVAL: Convert question to embedding, find similar chunks
3. GENERATION: Build prompt with context + question, send to LLM
"""

import logging

import ollama

from app.models import QueryResponse, SourceChunk
from app.services.embedding_service import EmbeddingService
from app.services.vector_store_service import VectorStoreService

logger = logging.getLogger(__name__)

# System prompt template for the LLM.
# Instructs the model to answer only from provided context to prevent hallucination.
RAG_PROMPT_TEMPLATE = """You are a helpful assistant that answers questions based on the provided context.
Use ONLY the information in the context below to answer the question.
If the context does not contain enough information to answer, say so clearly.
Do not make up information.

Context:
{context}

Question: {question}

Answer:"""


class RagService:
    """Orchestrates the full RAG pipeline: index, query, and delete.

    Coordinates between EmbeddingService, VectorStoreService, and the Ollama LLM.
    """

    def __init__(
        self,
        embedding_service: EmbeddingService,
        vector_store: VectorStoreService,
        ollama_base_url: str,
        llm_model: str,
    ) -> None:
        self._embedding_service = embedding_service
        self._vector_store = vector_store
        self._llm_client = ollama.Client(host=ollama_base_url)
        self._llm_model = llm_model

    def index_document(
        self,
        document_id: str,
        content: str,
        metadata: dict | None = None,
    ) -> int:
        """Index a document: split into chunks, embed, and store.

        Args:
            document_id: Unique identifier for the document.
            content: Full text content of the document.
            metadata: Optional metadata to attach.

        Returns:
            Number of chunks indexed.
        """
        # Delete old chunks first so re-indexing replaces rather than duplicates
        self._vector_store.delete_document(document_id)

        chunks = self._embedding_service.split_text(content)
        if not chunks:
            logger.warning("No chunks generated for document '%s'", document_id)
            return 0

        embeddings = self._embedding_service.generate_embeddings(chunks)

        count = self._vector_store.add_chunks(document_id, chunks, embeddings, metadata)

        logger.info("Indexed document '%s': %d chunks", document_id, count)
        return count

    def query(
        self,
        question: str,
        document_ids: list[str] | None = None,
        top_k: int = 3,
    ) -> QueryResponse:
        """Run the full RAG pipeline: embed question, retrieve context, generate answer.

        Args:
            question: The user's natural language question.
            document_ids: Optional filter to search within specific documents.
            top_k: Number of context chunks to retrieve (default 3).

        Returns:
            QueryResponse with the answer, source chunks, and metadata.
        """
        query_embedding = self._embedding_service.generate_embedding(question)

        results = self._vector_store.query(query_embedding, top_k=top_k, document_ids=document_ids)

        sources: list[SourceChunk] = []
        context_parts: list[str] = []
        searched_doc_ids: set[str] = set()

        if results["ids"] and results["ids"][0]:
            for i, chunk_text in enumerate(results["documents"][0]):
                metadata = results["metadatas"][0][i]
                distance = results["distances"][0][i]

                # Convert cosine distance (0=identical, 2=opposite) to relevance score (1.0=identical, 0.0=opposite)
                relevance = 1.0 - (distance / 2.0)

                sources.append(
                    SourceChunk(
                        document_id=metadata["document_id"],
                        chunk_text=chunk_text,
                        relevance_score=round(relevance, 4),
                        metadata=metadata,
                    )
                )
                context_parts.append(chunk_text)
                searched_doc_ids.add(metadata["document_id"])

        if not context_parts:
            return QueryResponse(
                answer="No relevant documents found to answer this question. Please index some documents first.",
                sources=[],
                document_ids_searched=list(searched_doc_ids),
            )

        context = "\n\n---\n\n".join(context_parts)
        prompt = RAG_PROMPT_TEMPLATE.format(context=context, question=question)

        logger.info("Sending prompt to LLM with %d context chunks", len(context_parts))

        response = self._llm_client.chat(
            model=self._llm_model,
            messages=[{"role": "user", "content": prompt}],
        )
        answer = response["message"]["content"]

        return QueryResponse(
            answer=answer,
            sources=sources,
            document_ids_searched=list(searched_doc_ids),
        )

    def delete_document(self, document_id: str) -> int:
        """Delete a document and all its chunks from the vector store.

        Args:
            document_id: ID of the document to remove.

        Returns:
            Number of chunks deleted.
        """
        return self._vector_store.delete_document(document_id)
