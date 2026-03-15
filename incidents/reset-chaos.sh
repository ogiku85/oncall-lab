#!/usr/bin/env bash
set -euo pipefail
ORDER_API_URL="${ORDER_API_URL:-http://localhost:8080}"
INVENTORY_API_URL="${INVENTORY_API_URL:-http://localhost:8081}"

payload='{"mode":"off","failRate":0.0,"delayMs":0}'
curl -fsS -X POST "${ORDER_API_URL}/chaos" -H 'Content-Type: application/json' -d "$payload" > /dev/null || true
curl -fsS -X POST "${INVENTORY_API_URL}/chaos" -H 'Content-Type: application/json' -d "$payload" > /dev/null || true

echo "Chaos reset on order-api and inventory-api"
