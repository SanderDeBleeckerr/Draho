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
  echo "Returning the Unity Personal seat..."
  "$CLIENT" --return-ulf --username "$UNITY_EMAIL" --password "$UNITY_PASSWORD" || true
}
trap return_seat EXIT

if ! "$CLIENT" --activate-all --include-personal --username "$UNITY_EMAIL" --password "$UNITY_PASSWORD"; then
  echo "::error::Personal activation failed. Check: no 2FA on the Unity account, correct password," \
       "and the account has accepted the Unity terms (sign in once on unity.com)."
  exit 1
fi

unity-editor -batchmode -nographics -quit -logFile - \
  -projectPath "$WORKSPACE/Draho" \
  -buildTarget WebGL \
  -executeMethod Draho.EditorTools.CiBuild.BuildWebGL \
  -customBuildPath "$WORKSPACE/build/WebGL/WebGL"
