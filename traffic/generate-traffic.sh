#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:8080}"
DURATION_SECONDS="${DURATION_SECONDS:-300}"
BATCH_SIZE="${BATCH_SIZE:-20}"
PARALLELISM="${PARALLELISM:-10}"
SLEEP_SECONDS="${SLEEP_SECONDS:-1}"

end=$((SECONDS + DURATION_SECONDS))
echo "Generating burst traffic against ${BASE_URL} for ${DURATION_SECONDS}s"

while [ "$SECONDS" -lt "$end" ]; do
  seq 1 "$BATCH_SIZE" | xargs -P"$PARALLELISM" -I{} sh -c 'curl -fsS "'$BASE_URL'/orders/$RANDOM" > /dev/null || true'
  sleep "$SLEEP_SECONDS"
done

echo "Done"
