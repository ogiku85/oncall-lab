#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:8080}"
DURATION_SECONDS="${DURATION_SECONDS:-300}"
REQUESTS_PER_SECOND="${REQUESTS_PER_SECOND:-5}"

end=$((SECONDS + DURATION_SECONDS))
echo "Generating steady traffic against ${BASE_URL} for ${DURATION_SECONDS}s at ~${REQUESTS_PER_SECOND} rps"

while [ "$SECONDS" -lt "$end" ]; do
  for _ in $(seq 1 "$REQUESTS_PER_SECOND"); do
    curl -fsS "${BASE_URL}/orders/$RANDOM" > /dev/null || true &
  done
  wait
  sleep 1
done

echo "Done"
