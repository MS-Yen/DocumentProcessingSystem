"""
Service for generating text embeddings using Ollama.

Embeddings convert text into numerical vectors (arrays of floats).
Similar texts produce vectors that are close together in vector space,
which is the foundation of RAG similarity search.
"""

import logging

import ollama
from langchain_text_splitters import RecursiveCharacterTextSplitter

logger = logging.getLogger(__name__)


class EmbeddingService:
    """Handles text chunking and embedding generation via Ollama."""

    def __init__(self, ollama_base_url: str, embedding_model: str) -> None:
        self._client = ollama.Client(host=ollama_base_url)
        self._model = embedding_model

        # Splits text by trying separators in order: paragraphs, newlines,
        # sentences, words, then characters as last resort.
        # chunk_overlap=50 prevents losing context at chunk boundaries.
        self._splitter = RecursiveCharacterTextSplitter(
            chunk_size=500,
            chunk_overlap=50,
            length_function=len,
            separators=["\n\n", "\n", ". ", " ", ""],
        )

    def split_text(self, text: str) -> list[str]:
        """Split document text into chunks for embedding.

        Args:
            text: The full document text.

        Returns:
            List of text chunks, each ~500 characters.
        """
        chunks = self._splitter.split_text(text)
        logger.info("Split text into %d chunks", len(chunks))
        return chunks

    def generate_embedding(self, text: str) -> list[float]:
        """Generate an embedding vector for a single text.

        Args:
            text: Text to embed (a chunk or a user question).

        Returns:
            Embedding vector as a list of floats.
        """
        response = self._client.embeddings(model=self._model, prompt=text)
        return response["embedding"]

    def generate_embeddings(self, texts: list[str]) -> list[list[float]]:
        """Generate embeddings for multiple texts (batch processing).

        Args:
            texts: List of text chunks to embed.

        Returns:
            List of embedding vectors (same order as input texts).
        """
        embeddings: list[list[float]] = []
        for i, text in enumerate(texts):
            embedding = self.generate_embedding(text)
            embeddings.append(embedding)

            if (i + 1) % 10 == 0:
                logger.info("Generated embeddings for %d/%d chunks", i + 1, len(texts))

        logger.info("Generated %d embeddings total", len(embeddings))
        return embeddings

    async def check_connection(self) -> tuple[bool, list[str]]:
        """Check if Ollama is reachable and list available models.

        Returns:
            Tuple of (is_connected, list_of_model_names).
        """
        try:
            models_response = self._client.list()
            model_names = [m.model for m in models_response.models]
            return True, model_names
        except Exception as e:
            logger.error("Failed to connect to Ollama: %s", e)
            return False, []
