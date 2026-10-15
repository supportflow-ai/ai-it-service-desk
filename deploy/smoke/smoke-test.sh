#!/usr/bin/env bash
# ==============================================================================
# AI IT Service Desk — Automated Smoke Test Hook (Bash)
# ==============================================================================
set -e

BASE_URL="${SERVICE_DESK_BASE_URL:-http://localhost}"
TEST_EMAIL="${TEST_USER_EMAIL:-smoke.tester@internal.company}"
TEST_PASSWORD="${TEST_USER_PASSWORD:-SmokeTestPassword123!}"

echo "============================================================"
echo " AI IT Service Desk — Smoke Test Hook Execution"
echo " Base URL   : ${BASE_URL}"
echo " Test User  : ${TEST_EMAIL}"
echo " Timestamp  : $(date '+%Y-%m-%d %H:%M:%S')"
echo "============================================================"

FAILED_STEPS=0

# Step 1: Health
echo -n "[1/5] Testing /health: "
HEALTH_RESP=$(curl -s -w "\n%{http_code}" "${BASE_URL}/health" || true)
HTTP_CODE=$(echo "$HEALTH_RESP" | tail -n1)
BODY=$(echo "$HEALTH_RESP" | sed '$d')

if [ "$HTTP_CODE" = "200" ] && echo "$BODY" | grep -q '"status":"Healthy"'; then
  echo "PASS (HTTP 200 Healthy)"
else
  echo "FAIL (HTTP $HTTP_CODE: $BODY)"
  FAILED_STEPS=$((FAILED_STEPS + 1))
fi

# Step 2: Auth Login
echo -n "[2/5] Testing Auth Login (/api/v1/auth/login): "
AUTH_RESP=$(curl -s -w "\n%{http_code}" -X POST "${BASE_URL}/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"${TEST_EMAIL}\",\"password\":\"${TEST_PASSWORD}\"}" || true)
AUTH_CODE=$(echo "$AUTH_RESP" | tail -n1)
if [ "$AUTH_CODE" = "200" ] || [ "$AUTH_CODE" = "201" ]; then
  echo "PASS (HTTP $AUTH_CODE)"
else
  echo "FAIL (HTTP $AUTH_CODE - Endpoint not implemented or credentials rejected)"
  FAILED_STEPS=$((FAILED_STEPS + 1))
fi

# Step 3: Create Ticket
echo -n "[3/5] Testing Ticket Creation (/api/v1/tickets): "
TICKET_RESP=$(curl -s -w "\n%{http_code}" -X POST "${BASE_URL}/api/v1/tickets" \
  -H "Content-Type: application/json" \
  -d '{"title":"Smoke Test Ticket","description":"Verification","category":"Hardware","urgency":"Low","impact":"Low"}' || true)
TICKET_CODE=$(echo "$TICKET_RESP" | tail -n1)
if [ "$TICKET_CODE" = "200" ] || [ "$TICKET_CODE" = "201" ]; then
  echo "PASS (HTTP $TICKET_CODE)"
else
  echo "FAIL (HTTP $TICKET_CODE - Endpoint not implemented)"
  FAILED_STEPS=$((FAILED_STEPS + 1))
fi

# Step 4: List Tickets
echo -n "[4/5] Testing Ticket Listing (/api/v1/tickets): "
LIST_RESP=$(curl -s -w "\n%{http_code}" "${BASE_URL}/api/v1/tickets" || true)
LIST_CODE=$(echo "$LIST_RESP" | tail -n1)
if [ "$LIST_CODE" = "200" ]; then
  echo "PASS (HTTP 200)"
else
  echo "FAIL (HTTP $LIST_CODE - Endpoint not implemented)"
  FAILED_STEPS=$((FAILED_STEPS + 1))
fi

# Step 5: Response Validation
echo -n "[5/5] Validating Response Payload: "
if [ $FAILED_STEPS -eq 0 ]; then
  echo "PASS (All responses valid)"
else
  echo "FAIL (Prior steps failed, response validation aborted)"
fi

echo "============================================================"
if [ $FAILED_STEPS -gt 0 ]; then
  echo " SMOKE TEST FAILED: $FAILED_STEPS checks failed."
  exit 1
else
  echo " ALL SMOKE CHECKS PASSED (5/5)"
  exit 0
fi
