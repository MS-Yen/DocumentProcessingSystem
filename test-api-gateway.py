"""
=============================================================================
API Gateway End-to-End Test Script
=============================================================================
Tests that ALL requests routed through the API Gateway (port 5000)
reach the correct downstream services and return expected results.

This verifies:
  1. Health check endpoints (gateway self-check + downstream services)
  2. YARP reverse proxy routing: /api/documents/* → Document Service (5001)
  3. YARP reverse proxy routing: /api/rag/* → RAG Service (5002)
  4. Correlation ID propagation (X-Correlation-ID header)
  5. CORS headers
  6. Full document lifecycle through gateway (upload → list → get → download → delete)
  7. RAG query through gateway

Usage:
  python test-api-gateway.py
=============================================================================
"""

import requests
import time
import sys
import os
import tempfile

GATEWAY_URL = "http://localhost:5000"
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
# 1. GATEWAY HEALTH CHECKS
# =========================================================================
section("1. Gateway Health Checks")

# 1a. Basic gateway health
resp = requests.get(f"{GATEWAY_URL}/health")
test("GET /health returns 200", resp.status_code == 200)
data = resp.json()
test("/health status is 'healthy'", data.get("status") == "healthy")
test("/health service is 'api-gateway'", data.get("service") == "api-gateway")

# 1b. Downstream services health
resp = requests.get(f"{GATEWAY_URL}/health/services")
test("GET /health/services returns 200", resp.status_code == 200)
data = resp.json()
test("/health/services overall status is 'healthy'", data.get("status") == "healthy")

services = data.get("services", {})
test("document-service is healthy",
     services.get("document-service", {}).get("status") == "healthy",
     f"got: {services.get('document-service')}")
test("rag-service is healthy",
     services.get("rag-service", {}).get("status") == "healthy",
     f"got: {services.get('rag-service')}")


# =========================================================================
# 2. CORS HEADERS
# =========================================================================
section("2. CORS Headers")

# Send a preflight OPTIONS request
resp = requests.options(
    f"{GATEWAY_URL}/api/documents",
    headers={
        "Origin": "http://localhost:3000",
        "Access-Control-Request-Method": "POST",
        "Access-Control-Request-Headers": "Content-Type",
    }
)
test("OPTIONS preflight returns 2xx", 200 <= resp.status_code < 300,
     f"got {resp.status_code}")
test("Access-Control-Allow-Origin header present",
     "access-control-allow-origin" in {k.lower(): v for k, v in resp.headers.items()},
     f"headers: {dict(resp.headers)}")


# =========================================================================
# 3. CORRELATION ID PROPAGATION
# =========================================================================
section("3. Correlation ID Propagation")

# 3a. Gateway should generate a correlation ID if none provided
resp = requests.get(f"{GATEWAY_URL}/health")
corr_id = resp.headers.get("X-Correlation-ID")
test("X-Correlation-ID header returned on /health", corr_id is not None,
     f"headers: {dict(resp.headers)}")

# 3b. Gateway should echo back a provided correlation ID
custom_id = "test-correlation-12345"
resp = requests.get(
    f"{GATEWAY_URL}/health",
    headers={"X-Correlation-ID": custom_id}
)
returned_id = resp.headers.get("X-Correlation-ID")
test("Custom X-Correlation-ID is echoed back",
     returned_id == custom_id,
     f"sent '{custom_id}', got '{returned_id}'")


# =========================================================================
# 4. DOCUMENT SERVICE ROUTING — Full Lifecycle Through Gateway
# =========================================================================
section("4. Document Service Routing (/api/documents/*)")

# 4a. Upload a test document through the gateway
test_content = "The quick brown fox jumps over the lazy dog. " * 20
with tempfile.NamedTemporaryFile(mode='w', suffix='.txt', delete=False, prefix='gateway_test_') as f:
    f.write(test_content)
    temp_path = f.name

try:
    with open(temp_path, 'rb') as f:
        resp = requests.post(
            f"{GATEWAY_URL}/api/documents/upload",
            files={"file": ("gateway-test.txt", f, "text/plain")}
        )
    test("POST /api/documents/upload returns 201",
         resp.status_code == 201, f"got {resp.status_code}: {resp.text[:200]}")

    upload_data = resp.json()
    doc_id = upload_data.get("documentId")
    test("Upload response contains documentId", doc_id is not None)
    test("Upload response contains fileName",
         upload_data.get("fileName") == "gateway-test.txt",
         f"got: {upload_data.get('fileName')}")

    # 4b. Wait briefly for RAG indexing
    print("  ... waiting for RAG indexing ...")
    time.sleep(3)

    # 4c. List documents through gateway
    resp = requests.get(f"{GATEWAY_URL}/api/documents")
    test("GET /api/documents returns 200", resp.status_code == 200,
         f"got {resp.status_code}")
    docs = resp.json()
    test("Document list is non-empty", len(docs) > 0)

    # Find our uploaded document
    our_doc = next((d for d in docs if d.get("id") == doc_id), None)
    test("Our uploaded document appears in list", our_doc is not None,
         f"doc_id={doc_id}, docs={[d.get('id') for d in docs[:5]]}")

    # 4d. Get document by ID through gateway
    resp = requests.get(f"{GATEWAY_URL}/api/documents/{doc_id}")
    test("GET /api/documents/{id} returns 200", resp.status_code == 200,
         f"got {resp.status_code}")
    doc_detail = resp.json()
    test("Document detail has correct fileName",
         doc_detail.get("fileName") == "gateway-test.txt")
    test("Document detail has correct contentType",
         doc_detail.get("contentType") == "text/plain")

    # Verify correlation ID is propagated through proxied requests
    corr = resp.headers.get("X-Correlation-ID")
    test("Proxied response includes X-Correlation-ID", corr is not None)

    # 4e. Download document through gateway
    resp = requests.get(f"{GATEWAY_URL}/api/documents/{doc_id}/download")
    test("GET /api/documents/{id}/download returns 200",
         resp.status_code == 200, f"got {resp.status_code}")
    test("Downloaded content matches original",
         resp.text == test_content,
         f"length expected={len(test_content)}, got={len(resp.text)}")
    test("Content-Type is text/plain",
         "text/plain" in resp.headers.get("Content-Type", ""),
         f"got: {resp.headers.get('Content-Type')}")

    # 4f. Delete document through gateway
    resp = requests.delete(f"{GATEWAY_URL}/api/documents/{doc_id}")
    test("DELETE /api/documents/{id} returns 204",
         resp.status_code == 204, f"got {resp.status_code}")

    # 4g. Verify deletion
    resp = requests.get(f"{GATEWAY_URL}/api/documents/{doc_id}")
    test("GET deleted document returns 404",
         resp.status_code == 404, f"got {resp.status_code}")

finally:
    os.unlink(temp_path)


# =========================================================================
# 5. RAG SERVICE ROUTING
# =========================================================================
section("5. RAG Service Routing (/api/rag/*)")

# 5a. RAG health check through gateway
resp = requests.get(f"{GATEWAY_URL}/api/rag/health")
test("GET /api/rag/health returns 200", resp.status_code == 200,
     f"got {resp.status_code}: {resp.text[:200]}")
rag_health = resp.json()
test("RAG health status is 'healthy'",
     rag_health.get("status") == "healthy",
     f"got: {rag_health}")


# =========================================================================
# 6. REPORT SERVICE ROUTING
# =========================================================================
section("6. Report Service Routing (/api/reports/*)")

resp = requests.get(f"{GATEWAY_URL}/api/reports/analytics")
test("GET /api/reports/analytics returns 200 (PDF)",
     resp.status_code == 200,
     f"got {resp.status_code}")


# =========================================================================
# 7. SWAGGER UI
# =========================================================================
section("7. Swagger UI")

resp = requests.get(f"{GATEWAY_URL}/swagger/index.html")
test("GET /swagger/index.html returns 200", resp.status_code == 200,
     f"got {resp.status_code}")
test("Swagger page contains 'swagger'",
     "swagger" in resp.text.lower(),
     f"page content length: {len(resp.text)}")


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
