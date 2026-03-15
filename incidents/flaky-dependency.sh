#!/usr/bin/env bash
set -euo pipefail
API_URL="${API_URL:-http://localhost:8081}"
FAIL_RATE="${FAIL_RATE:-0.25}"
curl -fsS -X POST "${API_URL}/chaos" -H 'Content-Type: application/json' -d "{\"mode\":\"off\",\"failRate\":${FAIL_RATE},\"delayMs\":0}"
echo
echo "Injected flaky dependency with failRate=${FAIL_RATE}"
