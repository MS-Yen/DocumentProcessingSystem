"""
End-to-end test script for the RAG Service API.

Tests the full RAG pipeline:
1. Health check — verifies Ollama + ChromaDB are reachable
2. Document indexing — uploads and indexes two documents
3. Question answering — asks questions and verifies the RAG pipeline
4. Vector search accuracy — confirms the correct chunks are retrieved
5. Document filtering — tests scoping queries to specific documents
6. Cleanup — deletes documents and verifies they're gone

Run with: python test-rag-service.py
Requires: pip install httpx (or requests)
"""

import sys
import httpx

BASE_URL = "http://localhost:5002/api/rag"

# Track test results
passed = 0
failed = 0


def test(name: str, condition: bool, detail: str = "") -> None:
    """Record a test result and print it."""
    global passed, failed
    if condition:
        passed += 1
        print(f"  PASS: {name}")
    else:
        failed += 1
        print(f"  FAIL: {name}")
    if detail:
        print(f"        {detail}")


# Use a long timeout because Ollama's first inference can be slow
# (the model needs to load into memory)
client = httpx.Client(base_url=BASE_URL, timeout=120.0)


# =============================================================================
# TEST 1: Health Check
# =============================================================================
print("\n" + "=" * 60)
print("TEST 1: Health Check")
print("=" * 60)

resp = client.get("/health")
health = resp.json()

test("Health endpoint returns 200", resp.status_code == 200)
test("Status is healthy", health["status"] == "healthy")
test("Ollama is connected", health["ollama_connected"] is True)
test("ChromaDB is connected", health["chromadb_connected"] is True)
test(
    "Required models available",
    any("nomic-embed-text" in m for m in health["models_available"])
    and any("llama3.2" in m for m in health["models_available"]),
    f"Models: {health['models_available']}",
)


# =============================================================================
# TEST 2: Document Indexing
# =============================================================================
print("\n" + "=" * 60)
print("TEST 2: Document Indexing")
print("=" * 60)

# Document 1: Insurance policy
doc1_content = """
Insurance Policy Overview

This comprehensive insurance policy covers the following areas:

Fire and Flood Protection:
All residential properties are covered against fire damage up to $500,000.
Flood damage coverage is available for properties in designated flood zones.
Claims must be filed within 30 days of the incident.

Auto Insurance:
Vehicle coverage includes collision and comprehensive protection.
The deductible for collision claims is $500.
Rental car coverage is included for up to 14 days during repairs.

Premium Payments:
Premium payments can be made monthly, quarterly, or annually.
A 5% discount is applied for annual upfront payments.
Late payments incur a $25 fee after a 15-day grace period.
"""

resp = client.post("/index", json={
    "document_id": "doc-insurance-001",
    "content": doc1_content,
    "filename": "insurance_policy.pdf",
    "metadata": {"category": "insurance", "year": "2024"},
})
index1 = resp.json()

test("Index doc1 returns 200", resp.status_code == 200)
test("Doc1 has chunks indexed", index1["chunks_indexed"] > 0, f"Chunks: {index1['chunks_indexed']}")
test("Doc1 ID matches", index1["document_id"] == "doc-insurance-001")

# Document 2: Employee handbook
doc2_content = """
Employee Handbook - Technology Company

Work Schedule:
Standard working hours are Monday through Friday, 9 AM to 5 PM.
Flexible working arrangements are available with manager approval.
Remote work is permitted up to 3 days per week.

Paid Time Off:
New employees receive 15 days of paid time off per year.
PTO increases by 2 days per year of service, up to a maximum of 25 days.
Unused PTO can be carried over, up to a maximum of 5 days.

Benefits:
Health insurance is provided through BlueCross BlueShield.
The company matches 401(k) contributions up to 4% of salary.
Tuition reimbursement is available up to $5,000 per year.

Performance Reviews:
Annual performance reviews are conducted in December.
Mid-year check-ins occur in June.
Promotions are based on performance ratings and available positions.
"""

resp = client.post("/index", json={
    "document_id": "doc-handbook-001",
    "content": doc2_content,
    "filename": "employee_handbook.pdf",
    "metadata": {"category": "hr", "year": "2024"},
})
index2 = resp.json()

test("Index doc2 returns 200", resp.status_code == 200)
test("Doc2 has chunks indexed", index2["chunks_indexed"] > 0, f"Chunks: {index2['chunks_indexed']}")


# =============================================================================
# TEST 3: List Indexed Documents
# =============================================================================
print("\n" + "=" * 60)
print("TEST 3: List Indexed Documents")
print("=" * 60)

resp = client.get("/documents")
docs = resp.json()

test("List documents returns 200", resp.status_code == 200)
test("Both documents are listed", len(docs) == 2, f"Documents: {docs}")
test("Doc IDs are correct", "doc-insurance-001" in docs and "doc-handbook-001" in docs)


# =============================================================================
# TEST 4: RAG Query — Insurance Question
# =============================================================================
print("\n" + "=" * 60)
print("TEST 4: RAG Query — Insurance Question")
print("=" * 60)

print("  Asking: 'What is the deductible for collision claims?'")
print("  (This may be slow on first query as Ollama loads the model...)")

resp = client.post("/query", json={
    "question": "What is the deductible for collision claims?",
    "top_k": 3,
})
query1 = resp.json()

test("Query returns 200", resp.status_code == 200)
test("Answer is not empty", len(query1["answer"]) > 0)
test(
    "Answer mentions $500",
    "500" in query1["answer"],
    f"Answer: {query1['answer'][:200]}",
)
test("Sources are returned", len(query1["sources"]) > 0, f"Source count: {len(query1['sources'])}")
test(
    "Top source is from insurance doc",
    any(s["document_id"] == "doc-insurance-001" for s in query1["sources"]),
)
test(
    "Relevance scores are reasonable (> 0.5)",
    all(s["relevance_score"] > 0.5 for s in query1["sources"]),
    f"Scores: {[s['relevance_score'] for s in query1['sources']]}",
)


# =============================================================================
# TEST 5: RAG Query — Employee Handbook Question
# =============================================================================
print("\n" + "=" * 60)
print("TEST 5: RAG Query — Employee Handbook Question")
print("=" * 60)

print("  Asking: 'How many days of paid time off do new employees get?'")

resp = client.post("/query", json={
    "question": "How many days of paid time off do new employees get?",
    "top_k": 3,
})
query2 = resp.json()

test("Query returns 200", resp.status_code == 200)
test("Answer is not empty", len(query2["answer"]) > 0)
test(
    "Answer mentions 15 days",
    "15" in query2["answer"],
    f"Answer: {query2['answer'][:200]}",
)
test(
    "Top source is from handbook doc",
    any(s["document_id"] == "doc-handbook-001" for s in query2["sources"]),
)


# =============================================================================
# TEST 6: Document-Scoped Query (filtering)
# =============================================================================
print("\n" + "=" * 60)
print("TEST 6: Document-Scoped Query (filtering)")
print("=" * 60)

print("  Asking about insurance but scoping to handbook only...")

resp = client.post("/query", json={
    "question": "What is the deductible for collision claims?",
    "document_ids": ["doc-handbook-001"],
    "top_k": 3,
})
query3 = resp.json()

test("Scoped query returns 200", resp.status_code == 200)
test(
    "Results only from handbook (not insurance)",
    all(s["document_id"] == "doc-handbook-001" for s in query3["sources"]),
    f"Source doc IDs: {[s['document_id'] for s in query3['sources']]}",
)


# =============================================================================
# TEST 7: Re-indexing (idempotent update)
# =============================================================================
print("\n" + "=" * 60)
print("TEST 7: Re-indexing (idempotent update)")
print("=" * 60)

resp = client.post("/index", json={
    "document_id": "doc-insurance-001",
    "content": doc1_content + "\n\nAddendum: Pet insurance is now available.",
    "filename": "insurance_policy_v2.pdf",
})
reindex = resp.json()

test("Re-index returns 200", resp.status_code == 200)
test(
    "Re-index created new chunks",
    reindex["chunks_indexed"] > 0,
    f"Chunks: {reindex['chunks_indexed']}",
)

# Verify no duplicate docs
resp = client.get("/documents")
docs_after = resp.json()
test("Still only 2 documents (no duplicates)", len(docs_after) == 2)


# =============================================================================
# TEST 8: Delete Document
# =============================================================================
print("\n" + "=" * 60)
print("TEST 8: Delete Document")
print("=" * 60)

resp = client.post("/delete", json={"document_id": "doc-insurance-001"})
delete1 = resp.json()

test("Delete returns 200", resp.status_code == 200)
test("Chunks were deleted", delete1["chunks_deleted"] > 0, f"Deleted: {delete1['chunks_deleted']}")

resp = client.post("/delete", json={"document_id": "doc-handbook-001"})
delete2 = resp.json()

test("Delete doc2 returns 200", resp.status_code == 200)

# Verify all documents are gone
resp = client.get("/documents")
docs_final = resp.json()
test("No documents remain after cleanup", len(docs_final) == 0, f"Remaining: {docs_final}")


# =============================================================================
# SUMMARY
# =============================================================================
print("\n" + "=" * 60)
print(f"RESULTS: {passed} passed, {failed} failed, {passed + failed} total")
print("=" * 60)

if failed > 0:
    print("\nSome tests failed. Check the output above for details.")
    sys.exit(1)
else:
    print("\nAll tests passed! RAG service is working correctly.")
    sys.exit(0)
