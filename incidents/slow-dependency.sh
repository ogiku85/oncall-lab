#!/usr/bin/env bash
set -euo pipefail
API_URL="${API_URL:-http://localhost:8081}"
DELAY_MS="${DELAY_MS:-2000}"
curl -fsS -X POST "${API_URL}/chaos" -H 'Content-Type: application/json' -d "{\"mode\":\"delay\",\"failRate\":0.0,\"delayMs\":${DELAY_MS}}"
echo
echo "Injected slow dependency with ${DELAY_MS}ms latency"
