"""
Application configuration loaded from environment variables.

Uses pydantic-settings to read from env vars / .env files
and validate values automatically.
"""

from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    """RAG service configuration.

    Values are loaded from environment variables (or a .env file).
    Field names map to env vars: e.g. 'ollama_base_url' reads OLLAMA_BASE_URL.
    """

    # Ollama runs on the HOST machine; "host.docker.internal" resolves to the host from Docker
    ollama_base_url: str = "http://host.docker.internal:11434"

    # Model for text-to-vector conversion (768-dimensional vectors)
    embedding_model: str = "nomic-embed-text"

    # LLM for generating natural language answers
    llm_model: str = "llama3.2:1b"

    # ChromaDB persistence directory (mapped to a Docker volume)
    chroma_persist_dir: str = "/app/chroma_data"

    log_level: str = "INFO"

    model_config = {"env_file": ".env", "extra": "ignore"}
