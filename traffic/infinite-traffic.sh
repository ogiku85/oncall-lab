#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:8080}"
REQUESTS_PER_SECOND="${REQUESTS_PER_SECOND:-10}"

echo "Generating traffic against ${BASE_URL}. Press Ctrl+C to stop."
while true; do
  for _ in $(seq 1 "$REQUESTS_PER_SECOND"); do
    curl -fsS "${BASE_URL}/orders/$RANDOM" > /dev/null || true &
  done
  wait
  sleep 1
done
