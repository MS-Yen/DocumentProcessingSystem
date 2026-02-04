# API Reference

All endpoints are accessible through the **API Gateway** at `http://localhost:5000`.

You can also call services directly during development:
- Document Service: `http://localhost:5001`
- RAG Service: `http://localhost:5002`
- Report Service: `http://localhost:5003`

> **Authentication:** This project does not implement authentication. It's a proof-of-concept focused on the document processing and RAG pipeline.

---

## API Gateway

### `GET /health`

Gateway self-check. Returns immediately if the gateway process is running.

**Response** `200 OK`
```json
{
  "status": "healthy",
  "service": "api-gateway",
  "timestamp": "2026-02-03T05:40:00Z"
}
```

**curl**
```bash
curl http://localhost:5000/health
```

---

### `GET /health/services`

Checks health of all downstream services. Calls each service's health endpoint and reports aggregate status.

**Response** `200 OK` (all healthy) or `503 Service Unavailable` (any unhealthy)
```json
{
  "status": "healthy",
  "timestamp": "2026-02-03T05:40:00Z",
  "services": {
    "document-service": {
      "status": "healthy",
      "statusCode": 200
    },
    "rag-service": {
      "status": "healthy",
      "statusCode": 200
    },
    "report-service": {
      "status": "healthy",
      "statusCode": 200
    }
  }
}
```

**curl**
```bash
curl http://localhost:5000/health/services
```

---

### Cross-Cutting Features

Every request through the gateway receives:

| Header | Description |
|--------|-------------|
| `X-Correlation-ID` | Auto-generated UUID for distributed tracing. Send your own to override. |
| `Access-Control-Allow-Origin` | CORS header — allows all origins in development |

---

## Document Service

### `POST /api/documents/upload`

Upload a document file (PDF, DOCX, or TXT). The file is stored in MongoDB (GridFS), metadata saved to PostgreSQL, text extracted, and sent to the RAG Service for indexing.

**Request** `multipart/form-data`

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | File | Yes | The document file (PDF, DOCX, or TXT) |

**Response** `201 Created`
```json
{
  "documentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "fileName": "research-paper.pdf",
  "fileSize": 245760,
  "status": "Indexed",
  "message": "Document uploaded and indexed successfully."
}
```

**Status Codes**

| Code | Meaning |
|------|---------|
| `201` | Document uploaded and indexed successfully |
| `400` | No file provided or unsupported file type |
| `500` | Internal server error during upload |

**curl**
```bash
curl -X POST http://localhost:5000/api/documents/upload \
  -F "file=@my-document.pdf"
```

---

### `GET /api/documents`

List all uploaded documents with their metadata.

**Response** `200 OK`
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "fileName": "research-paper.pdf",
    "contentType": "application/pdf",
    "fileSize": 245760,
    "uploadedAt": "2026-02-03T04:30:00Z",
    "status": "Indexed",
    "ragIndexed": true,
    "ragError": null
  }
]
```

**curl**
```bash
curl http://localhost:5000/api/documents
```

---

### `GET /api/documents/{id}`

Get metadata for a single document by ID.

**Path Parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | GUID | Document ID |

**Response** `200 OK`
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "fileName": "research-paper.pdf",
  "contentType": "application/pdf",
  "fileSize": 245760,
  "uploadedAt": "2026-02-03T04:30:00Z",
  "status": "Indexed",
  "ragIndexed": true,
  "ragError": null
}
```

**Status Codes**

| Code | Meaning |
|------|---------|
| `200` | Document found |
| `404` | Document not found |

**curl**
```bash
curl http://localhost:5000/api/documents/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

---

### `GET /api/documents/{id}/download`

Download the original file. Returns the binary content with the original content type.

**Response** `200 OK` — Binary file stream

**Response Headers**
```
Content-Type: application/pdf
Content-Disposition: attachment; filename="research-paper.pdf"
```

**Status Codes**

| Code | Meaning |
|------|---------|
| `200` | File returned |
| `404` | Document not found |

**curl**
```bash
curl http://localhost:5000/api/documents/3fa85f64-.../download --output paper.pdf
```

---

### `DELETE /api/documents/{id}`

Delete a document. Removes the file from MongoDB, metadata from PostgreSQL, and vectors from ChromaDB (via RAG Service).

**Response** `204 No Content`

**Status Codes**

| Code | Meaning |
|------|---------|
| `204` | Document deleted successfully |
| `404` | Document not found |

**curl**
```bash
curl -X DELETE http://localhost:5000/api/documents/3fa85f64-...
```

---

## RAG Service

### `POST /api/rag/query`

Ask a question about uploaded documents. The RAG pipeline retrieves relevant text chunks from ChromaDB, then uses Ollama to generate an answer grounded in the document content.

**Request** `application/json`
```json
{
  "question": "What are the key findings of the research?",
  "document_ids": ["3fa85f64-5717-4562-b3fc-2c963f66afa6"],
  "top_k": 3
}
```

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `question` | string | Yes | — | Natural language question |
| `document_ids` | string[] | No | all | Limit search to specific documents |
| `top_k` | integer | No | 3 | Number of chunks to retrieve (1-10) |

**Response** `200 OK`
```json
{
  "answer": "The research found that deep learning models outperform traditional machine learning approaches by 15% on the benchmark dataset...",
  "sources": [
    {
      "document_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "chunk_text": "Our experiments demonstrate that deep learning models achieve a 15% improvement over baseline methods...",
      "relevance_score": 0.87,
      "metadata": {
        "filename": "research-paper.pdf",
        "chunk_index": 3
      }
    },
    {
      "document_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "chunk_text": "The benchmark dataset consists of 10,000 labeled examples spanning five categories...",
      "relevance_score": 0.72,
      "metadata": {
        "filename": "research-paper.pdf",
        "chunk_index": 1
      }
    }
  ],
  "document_ids_searched": ["3fa85f64-5717-4562-b3fc-2c963f66afa6"]
}
```

**Status Codes**

| Code | Meaning |
|------|---------|
| `200` | Answer generated successfully |
| `422` | Validation error (empty question, invalid top_k) |
| `500` | Ollama connection failed or model error |

**curl**
```bash
curl -X POST http://localhost:5000/api/rag/query \
  -H "Content-Type: application/json" \
  -d '{
    "question": "What are the key findings?",
    "document_ids": ["3fa85f64-5717-4562-b3fc-2c963f66afa6"],
    "top_k": 5
  }'
```

---

### `POST /api/rag/index`

Index a document's text content into the vector database. This endpoint is called internally by the Document Service after upload — you typically don't call it directly.

**Request** `application/json`
```json
{
  "document_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "content": "Full text content of the document...",
  "filename": "research-paper.pdf",
  "metadata": {}
}
```

**Response** `200 OK`
```json
{
  "document_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "chunks_indexed": 12,
  "message": "Document indexed successfully"
}
```

---

### `POST /api/rag/delete`

Remove a document's vectors from ChromaDB. Called internally by the Document Service during document deletion.

**Request** `application/json`
```json
{
  "document_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

**Response** `200 OK`
```json
{
  "document_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "chunks_deleted": 12,
  "message": "Document deleted from index"
}
```

---

### `GET /api/rag/documents`

List all document IDs currently stored in the vector database.

**Response** `200 OK`
```json
[
  "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "7b2e8f1a-9c3d-4e5f-a6b7-c8d9e0f1a2b3"
]
```

**curl**
```bash
curl http://localhost:5000/api/rag/documents
```

---

### `GET /api/rag/health`

RAG Service health check. Reports Ollama connectivity and model availability.

**Response** `200 OK`
```json
{
  "status": "healthy",
  "ollama_connected": true,
  "chromadb_connected": true,
  "models_available": ["llama3.2:1b", "nomic-embed-text"]
}
```

**curl**
```bash
curl http://localhost:5000/api/rag/health
```

---

## Report Service

### `POST /api/reports/document-summary`

Generate a Document Summary PDF. Fetches metadata from the Document Service and produces a professional report with cover page, metadata table, and RAG indexing status.

**Request** `application/json`
```json
{
  "documentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `documentId` | GUID | Yes | ID of the document to summarize |

**Response** `200 OK` — PDF file download (`application/pdf`)

**Response Headers**
```
Content-Type: application/pdf
Content-Disposition: attachment; filename="summary-research-paper.pdf.pdf"
```

**PDF Contents**
- Cover page with document name and generation date
- Metadata table: filename, content type, file size, upload date, status
- RAG analysis section with indexing status indicator

**Status Codes**

| Code | Meaning |
|------|---------|
| `200` | PDF generated and returned |
| `404` | Document not found |
| `502` | Document Service unreachable |

**curl**
```bash
curl -X POST http://localhost:5000/api/reports/document-summary \
  -H "Content-Type: application/json" \
  -d '{"documentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"}' \
  --output summary.pdf
```

---

### `POST /api/reports/qa-session`

Generate a Q&A Session Report. Queries the RAG Service for each question and compiles the results into a multi-page PDF with answers and source citations.

**Request** `application/json`
```json
{
  "documentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "questions": [
    "What is the main topic?",
    "What methodology was used?",
    "What were the conclusions?"
  ],
  "sessionTitle": "Research Analysis Session"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `documentId` | GUID | Yes | Document to query |
| `questions` | string[] | Yes | List of questions (min 1) |
| `sessionTitle` | string | No | Custom title (auto-generated if omitted) |

**Response** `200 OK` — PDF file download (`application/pdf`)

**PDF Contents**
- Cover page with session title, document name, question count
- Table of contents listing all questions
- For each Q&A pair:
  - Question text (bold)
  - AI-generated answer
  - Source citations with relevance score bars (color-coded: green > 70%, amber > 40%, red < 40%)
  - Quoted chunk text
- Summary page with totals and average relevance score

**Status Codes**

| Code | Meaning |
|------|---------|
| `200` | PDF generated and returned |
| `400` | Validation error (empty questions list) |
| `404` | Document not found |
| `502` | Downstream service unreachable |

**curl**
```bash
curl -X POST http://localhost:5000/api/reports/qa-session \
  -H "Content-Type: application/json" \
  -d '{
    "documentId": "3fa85f64-...",
    "questions": ["What is machine learning?", "What frameworks are mentioned?"],
    "sessionTitle": "ML Research Q&A"
  }' --output qa-report.pdf
```

> **Note:** This endpoint can take 30-120 seconds depending on the number of questions and Ollama response time. Each question requires a full LLM inference pass.

---

### `GET /api/reports/analytics`

Generate a System Analytics Report. Fetches data from both the Document Service and RAG Service, computes aggregate statistics, and generates an executive summary PDF.

**Response** `200 OK` — PDF file download (`application/pdf`)

**PDF Contents**
- Cover page with generation timestamp
- Executive summary cards: total documents, indexed count, storage used, vector DB count
- Status breakdown: indexed / pending / failed counts with color indicators
- Document type distribution bar chart (PDF, DOCX, TXT)
- RAG indexing coverage percentage with progress bar
- Recent documents table (last 10 uploaded)

**Status Codes**

| Code | Meaning |
|------|---------|
| `200` | PDF generated and returned |
| `502` | Downstream service unreachable |

**curl**
```bash
curl http://localhost:5000/api/reports/analytics --output analytics.pdf
```

---

## Error Response Format

All services return errors in a consistent JSON format:

```json
{
  "error": "Error type description",
  "detail": "Specific details about what went wrong",
  "traceId": "0HN8EXAMPLE:00000001"
}
```

| Field | Description |
|-------|-------------|
| `error` | Short error category |
| `detail` | Human-readable explanation |
| `traceId` | ASP.NET Core trace ID for debugging |

### Common Error Codes

| Code | Meaning | Common Causes |
|------|---------|---------------|
| `400` | Bad Request | Missing required fields, validation failure |
| `404` | Not Found | Document ID doesn't exist |
| `422` | Unprocessable Entity | RAG validation error (empty question) |
| `500` | Internal Server Error | Unexpected application error |
| `502` | Bad Gateway | Downstream service unreachable |
| `503` | Service Unavailable | One or more services are down |
