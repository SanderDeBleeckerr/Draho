#!/usr/bin/env bash
# Runs INSIDE the unityci/editor container. Activates a Unity Personal seat with
# email + password (no .ulf file), builds WebGL, and always returns the seat.
# Based on game-ci/cli PR #246. The licensing-client flags are undocumented by Unity.
set -euo pipefail

: "${UNITY_EMAIL:?missing}" "${UNITY_PASSWORD:?missing}"
WORKSPACE="${WORKSPACE:-/github/workspace}"

CLIENT="$(find /opt/unity -type f -name Unity.Licensing.Client 2>/dev/null | head -n 1)"
if [ -z "$CLIENT" ]; then
  echo "::error::Unity.Licensing.Client not found in the image."
  exit 1
fi
echo "Licensing client: $CLIENT"

return_seat() {
  # A Personal seat is an entitlement, not a .ulf file, so the client's --return-ulf
  # usually answers "Ulf license file not found". The editor route returns it
  # (same fallback as game-ci/cli return_license.sh).
  echo "Returning the Unity Personal seat..."
  local log
  log="$(mktemp)"
  "$CLIENT" --return-ulf 2>&1 | tee "$log" || true
  if ! grep -qE "Successfully returned|License has been returned" "$log"; then
    mkdir -p /tmp/return-project
    unity-editor -batchmode -nographics -quit -logFile - -returnlicense \
      -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" -projectPath /tmp/return-project 2>&1 \
      | grep -E "Licens|license|returned" || true
  fi
}
trap return_seat EXIT

if ! "$CLIENT" --activate-all --include-personal --username "$UNITY_EMAIL" --password "$UNITY_PASSWORD"; then
  echo "::error::Personal activation failed. Check: no 2FA on the Unity account, correct password," \
       "and the account has accepted the Unity terms (sign in once on unity.com)."
  exit 1
fi

# Pass 1 creates the scene and saves project settings (Input System on). Pass 2 builds.
# Two editor starts are needed: scripts compile with the input setting that was active
# when the editor started, and a mismatch stops the player build
# ("script class layout is incompatible between the editor and the player").
for method in Prepare BuildWebGL; do
  echo "=== Unity pass: CiBuild.$method"
  unity-editor -batchmode -nographics -quit -logFile - \
    -projectPath "$WORKSPACE/Draho" \
    -buildTarget WebGL \
    -executeMethod "Draho.EditorTools.CiBuild.$method" \
    -customBuildPath "$WORKSPACE/build/WebGL/WebGL"
done
