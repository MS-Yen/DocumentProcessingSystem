# Example Questions for the Sample Document

These questions work well with the Acme Corporation Remote Work Policy (`example-document.txt`). Upload the document first, then try these queries to see the RAG system in action.

## Getting Started

```bash
# 1. Upload the sample document
curl -X POST http://localhost:5000/api/documents/upload \
  -F "file=@samples/example-document.txt"

# 2. Wait a few seconds for indexing
sleep 3

# 3. Try any of the questions below
curl -X POST http://localhost:5000/api/rag/query \
  -H "Content-Type: application/json" \
  -d '{"question": "Who is eligible for remote work?"}'
```

## Suggested Questions

### Factual Questions (Best results - answer is directly in the text)

| # | Question | Expected Answer Summary |
|---|----------|------------------------|
| 1 | Who is eligible for remote work at Acme Corporation? | Full-time employees who completed 90-day probation; part-time/contractors case-by-case |
| 2 | What equipment does the company provide to remote workers? | Laptop, external monitor, keyboard, and mouse |
| 3 | What is the internet speed requirement? | 50 Mbps download, 10 Mbps upload |
| 4 | How much is the home office setup stipend? | $500 one-time stipend |
| 5 | What are the core business hours? | 10:00 AM to 3:00 PM Eastern Time |

### Analytical Questions (Good results - requires synthesizing information)

| # | Question | Expected Answer Summary |
|---|----------|------------------------|
| 6 | What security measures are required for remote work? | VPN required, no public Wi-Fi without VPN, locked computer, encrypted storage, MDM for personal devices |
| 7 | What expenses can remote employees claim? | Internet ($75/month), office supplies ($50/quarter), phone charges, approved software |
| 8 | How is remote employee performance evaluated? | Monthly manager check-ins, output-based evaluation, not hours logged |

### Open-Ended Questions (Varied results - tests LLM reasoning)

| # | Question | Notes |
|---|----------|-------|
| 9 | What happens if a remote employee performs poorly? | Tests ability to find escalation path: increased check-ins, temporary return to office, privilege revocation |
| 10 | Summarize the key responsibilities of a remote employee | Tests synthesis across multiple sections |

## Generating a Q&A Report

Use the Report Service to generate a PDF with multiple questions at once:

```bash
curl -X POST http://localhost:5000/api/reports/qa-session \
  -H "Content-Type: application/json" \
  -d '{
    "documentId": "<your-document-id>",
    "questions": [
      "Who is eligible for remote work?",
      "What equipment does the company provide?",
      "What are the security requirements?",
      "How much is the home office stipend?",
      "What happens if performance is poor?"
    ],
    "sessionTitle": "Remote Work Policy Analysis"
  }' --output qa-report.pdf
```

## Tips for Good Questions

- **Be specific** — "What is the internet reimbursement amount?" works better than "Tell me about internet"
- **Reference concepts in the document** — The RAG system matches your question to relevant text chunks
- **Ask one thing at a time** — Compound questions (A and B?) may get partial answers
- **Factual questions work best** — The RAG system is designed to retrieve and cite specific passages
