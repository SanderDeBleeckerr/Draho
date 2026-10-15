#!/usr/bin/env bash
# Waits until GitHub Pages serves the build of a commit (version.txt is written by web-build.yml).
# Usage: Tools/Playtest/wait-deploy.sh <commit-sha> [timeout-minutes]
set -euo pipefail
sha="$1"; limit=$(( ${2:-40} * 60 )); url="https://sanderdebleeckerr.github.io/Draho/version.txt"
start=$(date +%s)
while true; do
  live=$(curl -fsS -m 20 "$url?t=$(date +%s)" 2>/dev/null | tr -d '[:space:]' || true)
  if [ "$live" = "$sha" ]; then echo "deployed $sha after $(( $(date +%s) - start ))s"; exit 0; fi
  if [ $(( $(date +%s) - start )) -gt "$limit" ]; then echo "timeout; live=${live:-none}"; exit 1; fi
  sleep 30
done
