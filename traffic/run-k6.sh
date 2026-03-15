#!/usr/bin/env bash
set -euo pipefail
k6 run traffic/traffic.js
