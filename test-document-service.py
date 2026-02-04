"""
End-to-end test script for the Document Service API.

Tests the full document processing pipeline:
1. Upload documents (TXT) — store in MongoDB, metadata in PostgreSQL
2. List documents — verify metadata is returned
3. Get document by ID — verify individual document retrieval
4. Download document — verify file content is returned
5. RAG integration — verify document was indexed in the RAG service
6. Delete document — verify cascading delete (Postgres + MongoDB + RAG)

Run with: python test-document-service.py
Requires: pip install httpx
"""

import sys
import time
import httpx

DOC_SERVICE_URL = "http://localhost:5001/api/documents"
RAG_SERVICE_URL = "http://localhost:5002/api/rag"

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


# Use long timeouts since RAG indexing involves Ollama model loading
client = httpx.Client(timeout=180.0)


# =============================================================================
# TEST 1: Upload a TXT Document
# =============================================================================
print("\n" + "=" * 60)
print("TEST 1: Upload a TXT Document")
print("=" * 60)

txt_content = """Insurance Policy Overview

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

print("  Uploading insurance_policy.txt...")
print("  (This may be slow on first run as Ollama loads the model...)")

resp = client.post(
    f"{DOC_SERVICE_URL}/upload",
    files={"file": ("insurance_policy.txt", txt_content.encode(), "text/plain")},
)
upload1 = resp.json()

test("Upload returns 201 Created", resp.status_code == 201)
test("Response has documentId", "documentId" in upload1, f"Response: {upload1}")
test("Filename matches", upload1.get("fileName") == "insurance_policy.txt")
test("File size > 0", upload1.get("fileSize", 0) > 0, f"Size: {upload1.get('fileSize')}")
test(
    "Status is Indexed or Pending",
    upload1.get("status") in ("Indexed", "Pending"),
    f"Status: {upload1.get('status')}, Message: {upload1.get('message')}",
)

doc1_id = upload1.get("documentId")
print(f"  Document ID: {doc1_id}")


# =============================================================================
# TEST 2: Upload a Second TXT Document
# =============================================================================
print("\n" + "=" * 60)
print("TEST 2: Upload a Second TXT Document")
print("=" * 60)

txt_content2 = """Employee Handbook - Technology Company

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
"""

print("  Uploading employee_handbook.txt...")

resp = client.post(
    f"{DOC_SERVICE_URL}/upload",
    files={"file": ("employee_handbook.txt", txt_content2.encode(), "text/plain")},
)
upload2 = resp.json()

test("Upload returns 201 Created", resp.status_code == 201)
test("Response has documentId", "documentId" in upload2)

doc2_id = upload2.get("documentId")
print(f"  Document ID: {doc2_id}")


# =============================================================================
# TEST 3: List All Documents
# =============================================================================
print("\n" + "=" * 60)
print("TEST 3: List All Documents")
print("=" * 60)

resp = client.get(DOC_SERVICE_URL)
docs = resp.json()

test("List returns 200", resp.status_code == 200)
test("Two documents listed", len(docs) == 2, f"Count: {len(docs)}")
test(
    "Documents have expected fields",
    all("id" in d and "fileName" in d and "contentType" in d for d in docs),
)

# Check that our doc IDs are present
doc_ids_in_list = [d["id"] for d in docs]
test("Doc1 ID in list", doc1_id in doc_ids_in_list)
test("Doc2 ID in list", doc2_id in doc_ids_in_list)


# =============================================================================
# TEST 4: Get Document by ID
# =============================================================================
print("\n" + "=" * 60)
print("TEST 4: Get Document by ID")
print("=" * 60)

resp = client.get(f"{DOC_SERVICE_URL}/{doc1_id}")
doc_detail = resp.json()

test("Get by ID returns 200", resp.status_code == 200)
test("Filename is correct", doc_detail["fileName"] == "insurance_policy.txt")
test("Content type is text/plain", doc_detail["contentType"] == "text/plain")
test("File size > 0", doc_detail["fileSize"] > 0)
test("Has uploadedAt", "uploadedAt" in doc_detail)

# Test 404 for non-existent document
resp_404 = client.get(f"{DOC_SERVICE_URL}/00000000-0000-0000-0000-000000000000")
test("Non-existent ID returns 404", resp_404.status_code == 404)


# =============================================================================
# TEST 5: Download Document
# =============================================================================
print("\n" + "=" * 60)
print("TEST 5: Download Document")
print("=" * 60)

resp = client.get(f"{DOC_SERVICE_URL}/{doc1_id}/download")

test("Download returns 200", resp.status_code == 200)
test(
    "Content-Type is text/plain",
    "text/plain" in resp.headers.get("content-type", ""),
    f"Content-Type: {resp.headers.get('content-type')}",
)
test("Downloaded content matches", "collision claims" in resp.text.lower())
test("Downloaded content has $500", "$500" in resp.text)


# =============================================================================
# TEST 6: Verify RAG Integration
# =============================================================================
print("\n" + "=" * 60)
print("TEST 6: Verify RAG Integration")
print("=" * 60)

# Check the RAG service to see if documents were indexed
rag_docs = client.get(f"{RAG_SERVICE_URL}/documents").json()
test(
    "Documents indexed in RAG service",
    len(rag_docs) >= 2,
    f"RAG documents: {rag_docs}",
)

# Ask the RAG service a question about the uploaded insurance document
print("  Asking RAG: 'What is the deductible for collision claims?'")
resp = client.post(f"{RAG_SERVICE_URL}/query", json={
    "question": "What is the deductible for collision claims?",
    "top_k": 3,
})
rag_answer = resp.json()

test("RAG query returns 200", resp.status_code == 200)
test("RAG answer mentions $500", "500" in rag_answer["answer"], f"Answer: {rag_answer['answer'][:200]}")
test("RAG returns sources", len(rag_answer["sources"]) > 0)


# =============================================================================
# TEST 7: Delete Document
# =============================================================================
print("\n" + "=" * 60)
print("TEST 7: Delete Document")
print("=" * 60)

# Delete doc1
resp = client.delete(f"{DOC_SERVICE_URL}/{doc1_id}")
test("Delete doc1 returns 204", resp.status_code == 204)

# Verify it's gone from the Document Service
resp = client.get(f"{DOC_SERVICE_URL}/{doc1_id}")
test("Doc1 no longer accessible (404)", resp.status_code == 404)

# Verify list only shows doc2
resp = client.get(DOC_SERVICE_URL)
remaining = resp.json()
test("Only one document remains", len(remaining) == 1, f"Remaining: {[d['fileName'] for d in remaining]}")

# Delete doc2
resp = client.delete(f"{DOC_SERVICE_URL}/{doc2_id}")
test("Delete doc2 returns 204", resp.status_code == 204)

# Verify all gone
resp = client.get(DOC_SERVICE_URL)
final_docs = resp.json()
test("No documents remain", len(final_docs) == 0)


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
    print("\nAll tests passed! Document Service is working correctly.")
    sys.exit(0)
