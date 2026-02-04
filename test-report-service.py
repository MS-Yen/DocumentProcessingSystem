"""
=============================================================================
Report Service End-to-End Test Script
=============================================================================
Tests all Report Service endpoints, both directly (port 5003)
and through the API Gateway (port 5000).

Pre-requisite: At least one document must be uploaded and indexed.
This script uploads a test document first, then generates all 3 report types.

Test sections:
  1. Health check (direct)
  2. Upload a test document for report generation
  3. Document Summary Report (direct + via gateway)
  4. Q&A Session Report (direct + via gateway) — requires Ollama
  5. Analytics Report (direct + via gateway)
  6. Error handling (404, validation)
  7. Gateway routing (/api/reports/*)
  8. Cleanup

Usage:
  python test-report-service.py
=============================================================================
"""

import requests
import time
import sys
import os
import tempfile

REPORT_URL = "http://localhost:5003"
GATEWAY_URL = "http://localhost:5000"
DOC_URL = "http://localhost:5001"

passed = 0
failed = 0

def test(name, condition, detail=""):
    global passed, failed
    if condition:
        passed += 1
        print(f"  PASS: {name}")
    else:
        failed += 1
        print(f"  FAIL: {name} — {detail}")

def section(title):
    print(f"\n{'='*60}")
    print(f"  {title}")
    print(f"{'='*60}")


# =========================================================================
# 1. HEALTH CHECK
# =========================================================================
section("1. Report Service Health Check")

resp = requests.get(f"{REPORT_URL}/health")
test("GET /health returns 200", resp.status_code == 200)
data = resp.json()
test("Health status is 'healthy'", data.get("status") == "healthy")
test("Service name is 'report-service'", data.get("service") == "report-service")

# Health through gateway
resp = requests.get(f"{GATEWAY_URL}/health/services")
services = resp.json().get("services", {})
test("Gateway sees report-service as healthy",
     services.get("report-service", {}).get("status") == "healthy",
     f"got: {services.get('report-service')}")


# =========================================================================
# 2. UPLOAD TEST DOCUMENT
# =========================================================================
section("2. Upload Test Document")

test_content = (
    "Machine learning is a subset of artificial intelligence that focuses on "
    "building systems that learn from data. Deep learning is a type of machine "
    "learning that uses neural networks with many layers. Common frameworks include "
    "TensorFlow, PyTorch, and scikit-learn. Neural networks are inspired by the "
    "human brain and consist of interconnected nodes organized in layers. "
    "Supervised learning uses labeled data, while unsupervised learning finds "
    "patterns in unlabeled data. Reinforcement learning trains agents through "
    "rewards and penalties. Natural language processing (NLP) is an AI field "
    "that deals with understanding human language. Computer vision enables "
    "machines to interpret visual information from the world."
)

with tempfile.NamedTemporaryFile(mode='w', suffix='.txt', delete=False, prefix='report_test_') as f:
    f.write(test_content)
    temp_path = f.name

doc_id = None
try:
    with open(temp_path, 'rb') as f:
        resp = requests.post(
            f"{DOC_URL}/api/documents/upload",
            files={"file": ("ml-overview.txt", f, "text/plain")}
        )
    test("Upload test document returns 201", resp.status_code == 201,
         f"got {resp.status_code}: {resp.text[:200]}")
    doc_id = resp.json().get("documentId")
    test("Got document ID", doc_id is not None)

    print("  ... waiting for RAG indexing ...")
    time.sleep(4)

except Exception as e:
    test("Upload test document", False, str(e))

os.unlink(temp_path)

if doc_id is None:
    print("\n  FATAL: No document ID — cannot continue tests.")
    sys.exit(1)


# =========================================================================
# 3. DOCUMENT SUMMARY REPORT
# =========================================================================
section("3. Document Summary Report")

# 3a. Direct call to report service
resp = requests.post(
    f"{REPORT_URL}/api/reports/document-summary",
    json={"documentId": doc_id}
)
test("POST /api/reports/document-summary returns 200",
     resp.status_code == 200, f"got {resp.status_code}: {resp.text[:200]}")
test("Response Content-Type is application/pdf",
     "application/pdf" in resp.headers.get("Content-Type", ""),
     f"got: {resp.headers.get('Content-Type')}")
test("PDF is non-empty", len(resp.content) > 100,
     f"size: {len(resp.content)} bytes")
test("PDF starts with %PDF header",
     resp.content[:5] == b'%PDF-',
     f"got: {resp.content[:20]}")
summary_size = len(resp.content)
print(f"  INFO: Document Summary PDF size: {summary_size:,} bytes")

# 3b. Through API Gateway
resp = requests.post(
    f"{GATEWAY_URL}/api/reports/document-summary",
    json={"documentId": doc_id}
)
test("POST via gateway returns 200",
     resp.status_code == 200, f"got {resp.status_code}")
test("Gateway response is valid PDF",
     resp.content[:5] == b'%PDF-')
test("Gateway X-Correlation-ID present",
     resp.headers.get("X-Correlation-ID") is not None)


# =========================================================================
# 4. Q&A SESSION REPORT (requires Ollama to be running)
# =========================================================================
section("4. Q&A Session Report")

# Check if Ollama is available first
ollama_available = False
try:
    rag_health = requests.get(f"{REPORT_URL.replace(':5003', ':5002')}/api/rag/health", timeout=5)
    if rag_health.status_code == 200:
        health_data = rag_health.json()
        ollama_available = health_data.get("ollama_connected", False)
except:
    pass

if ollama_available:
    print("  INFO: Ollama is available — running Q&A report tests")

    resp = requests.post(
        f"{REPORT_URL}/api/reports/qa-session",
        json={
            "documentId": doc_id,
            "questions": [
                "What is machine learning?",
                "What frameworks are mentioned?"
            ],
            "sessionTitle": "ML Overview Q&A"
        },
        timeout=300  # LLM queries can be slow
    )
    test("POST /api/reports/qa-session returns 200",
         resp.status_code == 200, f"got {resp.status_code}: {resp.text[:300]}")

    if resp.status_code == 200:
        test("Response is valid PDF",
             resp.content[:5] == b'%PDF-')
        test("Q&A PDF is non-empty", len(resp.content) > 100,
             f"size: {len(resp.content)} bytes")
        qa_size = len(resp.content)
        print(f"  INFO: Q&A Session PDF size: {qa_size:,} bytes")

        # Through gateway
        resp = requests.post(
            f"{GATEWAY_URL}/api/reports/qa-session",
            json={
                "documentId": doc_id,
                "questions": ["What is deep learning?"],
                "sessionTitle": "Gateway Q&A Test"
            },
            timeout=300
        )
        test("Q&A via gateway returns 200",
             resp.status_code == 200, f"got {resp.status_code}")
        if resp.status_code == 200:
            test("Gateway Q&A response is valid PDF",
                 resp.content[:5] == b'%PDF-')
else:
    print("  SKIP: Ollama is not available — skipping Q&A report tests")
    print("  INFO: Start Ollama with a model loaded to test Q&A reports")


# =========================================================================
# 5. ANALYTICS REPORT
# =========================================================================
section("5. Analytics Report")

# 5a. Direct call
resp = requests.get(f"{REPORT_URL}/api/reports/analytics")
test("GET /api/reports/analytics returns 200",
     resp.status_code == 200, f"got {resp.status_code}: {resp.text[:200]}")
test("Response Content-Type is application/pdf",
     "application/pdf" in resp.headers.get("Content-Type", ""),
     f"got: {resp.headers.get('Content-Type')}")
test("Analytics PDF starts with %PDF header",
     resp.content[:5] == b'%PDF-')
analytics_size = len(resp.content)
print(f"  INFO: Analytics Report PDF size: {analytics_size:,} bytes")

# 5b. Through gateway
resp = requests.get(f"{GATEWAY_URL}/api/reports/analytics")
test("GET analytics via gateway returns 200",
     resp.status_code == 200, f"got {resp.status_code}")
test("Gateway analytics response is valid PDF",
     resp.content[:5] == b'%PDF-')


# =========================================================================
# 6. ERROR HANDLING
# =========================================================================
section("6. Error Handling")

# 6a. Document not found → 404
fake_id = "00000000-0000-0000-0000-000000000000"
resp = requests.post(
    f"{REPORT_URL}/api/reports/document-summary",
    json={"documentId": fake_id}
)
test("Non-existent document returns 404",
     resp.status_code == 404, f"got {resp.status_code}")

# 6b. Missing documentId → Guid defaults to empty (value type), returns 404
# Note: Guid is a value type in C# — it can't be null. When omitted from JSON,
# it defaults to Guid.Empty (00000000-...). The [Required] attribute doesn't
# trigger for value types, so the controller receives Guid.Empty and correctly
# returns 404 (document not found). This is expected ASP.NET behavior.
resp = requests.post(
    f"{REPORT_URL}/api/reports/document-summary",
    json={}
)
test("Missing documentId defaults to empty Guid, returns 404",
     resp.status_code == 404, f"got {resp.status_code}")

# 6c. Q&A with no questions → 400
resp = requests.post(
    f"{REPORT_URL}/api/reports/qa-session",
    json={"documentId": doc_id, "questions": []}
)
test("Empty questions list returns 400",
     resp.status_code == 400, f"got {resp.status_code}")

# 6d. Non-existent document for Q&A → 404
resp = requests.post(
    f"{REPORT_URL}/api/reports/qa-session",
    json={"documentId": fake_id, "questions": ["test?"]}
)
test("Q&A with non-existent document returns 404",
     resp.status_code == 404, f"got {resp.status_code}")


# =========================================================================
# 7. SWAGGER UI
# =========================================================================
section("7. Swagger UI")

resp = requests.get(f"{REPORT_URL}/swagger/index.html")
test("GET /swagger/index.html returns 200",
     resp.status_code == 200, f"got {resp.status_code}")
test("Swagger page contains API documentation",
     "swagger" in resp.text.lower())


# =========================================================================
# 8. CLEANUP
# =========================================================================
section("8. Cleanup")

if doc_id:
    resp = requests.delete(f"{DOC_URL}/api/documents/{doc_id}")
    test("Delete test document returns 204",
         resp.status_code == 204, f"got {resp.status_code}")


# =========================================================================
# SUMMARY
# =========================================================================
print(f"\n{'='*60}")
print(f"  RESULTS: {passed} passed, {failed} failed, {passed + failed} total")
print(f"{'='*60}")

if failed > 0:
    print("\n  Some tests FAILED. Review output above.")
    sys.exit(1)
else:
    print("\n  All tests PASSED!")
    sys.exit(0)
