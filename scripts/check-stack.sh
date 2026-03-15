#!/usr/bin/env bash
set -euo pipefail

echo '--- Docker compose services'
docker compose ps

echo
echo '--- API health'
curl -fsS http://localhost:8080/health && echo
curl -fsS http://localhost:8081/health && echo

echo
echo '--- Prometheus targets'
curl -fsS http://localhost:9090/api/v1/targets | jq '.data.activeTargets[] | {labels: .labels, health: .health}'
