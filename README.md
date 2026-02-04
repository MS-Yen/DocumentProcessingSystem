# Document Processing System

A proof-of-concept microservices application that handles document uploads, AI-powered question answering (RAG), and PDF report generation. Built to explore how these patterns work together in a containerized environment.

**What it does:**
- Upload documents (PDF, DOCX, TXT) and store them across PostgreSQL + MongoDB
- Ask questions about document content using a local LLM (Ollama) with source citations
- Generate PDF reports (document summaries, Q&A sessions, system analytics)
- Route everything through an API gateway

## Architecture

```
┌───────────────────────────────────────────────────────────┐
│                 API Gateway (YARP) :5000                   │
├─────────────────┬─────────────────┬───────────────────────┤
│                 │                 │                        │
▼                 ▼                 ▼                        │
┌──────────┐ ┌──────────┐ ┌──────────────┐                  │
│ Document │ │   RAG    │ │   Report     │                  │
│ Service  │ │ Service  │ │   Service    │                  │
│ .NET 8   │ │ FastAPI  │ │ .NET 8       │                  │
│ :5001    │ │ :5002    │ │ QuestPDF     │                  │
│          │ │          │ │ :5003        │                  │
└────┬─────┘ └────┬─────┘ └──────────────┘                  │
     │            │                                          │
     ▼            ▼                                          │
┌─────────┐ ┌──────────┐ ┌──────────┐                       │
│PostgreSQL│ │ ChromaDB │ │  Ollama  │                       │
│  :5432   │ │(embedded)│ │  :11434  │                       │
└─────────┘ └──────────┘ └──────────┘                       │
┌─────────┐                                                  │
│ MongoDB │                                                  │
│ :27017  │                                                  │
└─────────┘                                                  │
```

More detailed diagrams: [docs/architecture.md](docs/architecture.md)

| Service | Port | Stack | Role |
|---------|------|-------|------|
| API Gateway | 5000 | .NET 8 + YARP | Reverse proxy, CORS, correlation IDs, logging |
| Document Service | 5001 | .NET 8 + EF Core | File upload/download, metadata in PostgreSQL, files in MongoDB GridFS |
| RAG Service | 5002 | Python + FastAPI | Chunking, embedding, vector search, LLM answer generation |
| Report Service | 5003 | .NET 8 + QuestPDF | PDF report generation from document and RAG data |

## Tech Stack

**Backend:** .NET 8, Python 3.12 + FastAPI, YARP, Entity Framework Core, QuestPDF

**AI/ML:** Ollama (local LLM), llama3.2:1b, nomic-embed-text, ChromaDB, LangChain

**Databases:** PostgreSQL 16, MongoDB 7, ChromaDB (embedded)

**Infrastructure:** Docker Compose, Serilog, Swagger/OpenAPI

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (4.x+)
- [Ollama](https://ollama.com/)
- 8 GB RAM minimum

## Setup

### 1. Pull the Ollama models

```bash
ollama pull llama3.2:1b
ollama pull nomic-embed-text
```

### 2. Build and start everything

```bash
docker compose up -d --build
```

First build takes a few minutes. Subsequent starts are faster.

### 3. Check that services are running

```bash
curl http://localhost:5000/health/services
```

```json
{
  "status": "healthy",
  "services": {
    "document-service": { "status": "healthy" },
    "rag-service": { "status": "healthy" },
    "report-service": { "status": "healthy" }
  }
}
```

### 4. Try it out

```bash
# Upload a document
curl -X POST http://localhost:5000/api/documents/upload \
  -F "file=@samples/example-document.txt"

# Wait a few seconds for indexing, then ask a question
curl -X POST http://localhost:5000/api/rag/query \
  -H "Content-Type: application/json" \
  -d '{"question": "What is the document about?"}'

# Generate an analytics report
curl http://localhost:5000/api/reports/analytics --output analytics.pdf
```

Swagger UI is available at:
- http://localhost:5000/swagger (Gateway)
- http://localhost:5001/swagger (Document Service)
- http://localhost:5002/docs (RAG Service)
- http://localhost:5003/swagger (Report Service)

### Stopping

```bash
docker compose down       # stop containers, keep data
docker compose down -v    # stop containers and delete all data
```

## API Endpoints

All endpoints go through the gateway on port 5000.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/documents/upload` | Upload a document (PDF, DOCX, TXT) |
| `GET` | `/api/documents` | List all documents |
| `GET` | `/api/documents/{id}` | Get document metadata |
| `GET` | `/api/documents/{id}/download` | Download original file |
| `DELETE` | `/api/documents/{id}` | Delete document from all stores |
| `POST` | `/api/rag/query` | Ask a question about documents |
| `GET` | `/api/rag/health` | RAG service health + Ollama status |
| `GET` | `/api/rag/documents` | List indexed document IDs |
| `POST` | `/api/reports/document-summary` | Generate document summary PDF |
| `POST` | `/api/reports/qa-session` | Generate Q&A session PDF with source citations |
| `GET` | `/api/reports/analytics` | Generate system analytics PDF |
| `GET` | `/health` | Gateway self-check |
| `GET` | `/health/services` | All downstream service health |

Full API reference with request/response examples: [docs/API.md](docs/API.md)

## Project Structure

```
DocumentProcessingSystem/
├── api-gateway/                  # YARP reverse proxy
├── document-service/             # .NET 8 — Clean Architecture
│   └── src/
│       ├── DocumentService.API/
│       ├── DocumentService.Core/
│       └── DocumentService.Infrastructure/
├── rag-service/                  # Python FastAPI
│   └── app/
├── report-service/               # .NET 8 + QuestPDF — Clean Architecture
│   └── src/
│       ├── ReportService.API/
│       ├── ReportService.Core/
│       └── ReportService.Infrastructure/
├── docs/
│   ├── architecture.md
│   └── API.md
├── samples/
├── docker-compose.yml
├── test-document-service.py      # 31 tests
├── test-api-gateway.py           # 31 tests
└── test-report-service.py        # 30 tests
```

The .NET services use Clean Architecture (Core / Infrastructure / API layers).

## Testing

92 integration tests across 3 test suites.

```bash
pip install requests
python test-document-service.py
python test-api-gateway.py
python test-report-service.py
```

Tests cover: document lifecycle (upload through delete), RAG indexing and query, PDF generation for all report types, gateway proxy routing, CORS, correlation IDs, error handling, and Swagger availability.

## Troubleshooting

**Ollama not connecting:**
```bash
curl http://localhost:11434/api/tags   # check if running
ollama serve                           # start if not
ollama list                            # verify models
```

**Port already in use:** Change the host port in `docker-compose.yml` (e.g., `"5050:5000"`)

**Out of memory:** Docker Desktop needs at least 6 GB allocated. Settings > Resources > Memory.

**Container failing:** Check logs with `docker compose logs <service-name>`, then rebuild with `docker compose up -d --build`

## What This Project Covers

This is a learning/portfolio project. It touches on:

- Microservices with an API gateway
- RAG pipeline (chunk, embed, retrieve, generate)
- Clean Architecture in .NET
- Docker Compose orchestration
- Cross-service HTTP communication (IHttpClientFactory)
- PDF generation with QuestPDF
- Dual database strategy (PostgreSQL + MongoDB)
- Structured logging with Serilog
