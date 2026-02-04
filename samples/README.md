# Samples

This folder contains example files for testing the Document Processing System.

## Files

| File | Description |
|------|-------------|
| `example-document.txt` | A fictional company remote work policy (~4,500 characters). Good for testing document upload, RAG Q&A, and report generation. |
| `example-questions.md` | 10 example questions to ask about the sample document, with expected answers and curl commands. |

## Quick Test

```bash
# Upload the sample document
curl -X POST http://localhost:5000/api/documents/upload \
  -F "file=@samples/example-document.txt"

# Ask a question
curl -X POST http://localhost:5000/api/rag/query \
  -H "Content-Type: application/json" \
  -d '{"question": "What equipment does the company provide to remote workers?"}'

# Generate an analytics report
curl http://localhost:5000/api/reports/analytics --output analytics.pdf
```

## PDF Reports

When you generate reports using the Report Service, the PDFs are returned as file downloads in the HTTP response. They are not stored on disk. Save them using `--output filename.pdf` with curl, or use the browser download dialog from Swagger UI.
