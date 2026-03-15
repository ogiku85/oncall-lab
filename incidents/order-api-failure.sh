#!/usr/bin/env bash
set -euo pipefail
API_URL="${API_URL:-http://localhost:8080}"
FAIL_RATE="${FAIL_RATE:-1.0}"
curl -fsS -X POST "${API_URL}/chaos" -H 'Content-Type: application/json' -d "{\"mode\":\"fail\",\"failRate\":${FAIL_RATE},\"delayMs\":0}"
echo
echo "Injected order-api failures with failRate=${FAIL_RATE}"
