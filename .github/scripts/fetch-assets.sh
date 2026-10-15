#!/usr/bin/env bash
# Downloads third-party assets into the repository. Runs in GitHub Actions
# (see .github/workflows/fetch-assets.yml), because the Claude sandbox cannot
# reach kenney.nl or sketchfab.com.
#
#   KENNEY_PACKS        space-separated Kenney slugs, e.g. "furniture-kit"
#   SKETCHFAB_MODELS    space-separated "folder=uid" pairs
#   SKETCHFAB_API_TOKEN Sketchfab account token (only needed for Sketchfab)
set -euo pipefail

ROOT="$(pwd)"
UNITY_DIR="$ROOT/Draho/Assets/_Draho/ThirdParty"
RAW_DIR="$ROOT/ThirdParty"
TMP="$(mktemp -d)"
UA="Mozilla/5.0 (DrahoAssetFetch; +https://github.com/${GITHUB_REPOSITORY:-draho})"
mkdir -p "$UNITY_DIR" "$RAW_DIR"

# ---------------------------------------------------------------- Kenney (CC0)
for slug in ${KENNEY_PACKS:-}; do
  echo "::group::Kenney $slug"
  page="$TMP/$slug.html"
  curl -fsSL -A "$UA" "https://kenney.nl/assets/$slug" -o "$page"
  zip_url="$(grep -oE "https://kenney\.nl/media/pages/assets/$slug/[^\"' ]+\.zip" "$page" | head -1 || true)"
  if [ -z "$zip_url" ]; then
    echo "::error::No zip link found on https://kenney.nl/assets/$slug"; exit 1
  fi
  echo "Zip: $zip_url"
  curl -fsSL -A "$UA" "$zip_url" -o "$TMP/$slug.zip"
  rm -rf "$TMP/$slug" && mkdir -p "$TMP/$slug"
  unzip -q "$TMP/$slug.zip" -d "$TMP/$slug"

  name="$(echo "$slug" | sed -E 's/(^|-)([a-z])/\U\2/g')"   # furniture-kit -> FurnitureKit
  dest="$UNITY_DIR/Kenney/$name"
  rm -rf "$dest" && mkdir -p "$dest"
  # Keep only the FBX models and their textures; skip OBJ/GLB/previews to keep the repo small.
  fbx_dir="$(find "$TMP/$slug" -type f -iname '*.fbx' -printf '%h\n' | sort | uniq -c | sort -rn | head -1 | awk '{ $1=""; sub(/^ /,""); print }')"
  if [ -z "$fbx_dir" ]; then echo "::error::No FBX files in the $slug zip"; find "$TMP/$slug" | head -50; exit 1; fi
  cp -r "$fbx_dir"/. "$dest/"
  find "$dest" -type f ! -iname '*.fbx' ! -iname '*.png' -delete
  find "$TMP/$slug" -maxdepth 2 -iname 'license*.txt' -exec cp {} "$dest/License.txt" \; -quit
  [ -f "$dest/License.txt" ] || printf 'Kenney %s - CC0 1.0 Universal\nhttps://kenney.nl/assets/%s\n' "$slug" "$slug" > "$dest/License.txt"
  printf 'source: https://kenney.nl/assets/%s\nzip: %s\nfetched: %s\nlicense: CC0 1.0\n' \
    "$slug" "$zip_url" "$(date -u +%FT%TZ)" > "$dest/SOURCE.txt"
  echo "$(find "$dest" -iname '*.fbx' | wc -l) FBX models -> ${dest#$ROOT/}"
  echo "::endgroup::"
done

# ----------------------------------------------------------------- Sketchfab
for pair in ${SKETCHFAB_MODELS:-}; do
  folder="${pair%%=*}"; uid="${pair#*=}"
  echo "::group::Sketchfab $folder ($uid)"
  info="$TMP/$uid.json"
  curl -fsSL "https://api.sketchfab.com/v3/models/$uid" -o "$info"
  python3 - "$info" <<'PY'
import json, sys
m = json.load(open(sys.argv[1]))
lic = m.get("license") or {}
print(f"Name:      {m.get('name')}")
print(f"Author:    {m['user'].get('displayName')} (@{m['user'].get('username')})")
print(f"License:   {lic.get('label')} [{lic.get('slug')}]")
print(f"Download:  {m.get('isDownloadable')}")
print(f"Faces:     {m.get('faceCount')}  Vertices: {m.get('vertexCount')}  Animations: {m.get('animationCount')}")
PY
  slug="$(python3 -c 'import json,sys; print((json.load(open(sys.argv[1])).get("license") or {}).get("slug",""))' "$info")"
  case "$slug" in
    cc0|by|st) ;;  # public domain, attribution, or a bought Standard license: OK for a paid app
    *) echo "::error::License '$slug' is not safe for a commercial App Store game (need CC0, CC BY or Standard)."; exit 1 ;;
  esac
  if [ -z "${SKETCHFAB_API_TOKEN:-}" ]; then
    echo "::error::Add the repository secret SKETCHFAB_API_TOKEN (sketchfab.com -> Settings -> Password & API)."; exit 1
  fi
  dl="$TMP/$uid.dl.json"
  curl -fsSL -H "Authorization: Token $SKETCHFAB_API_TOKEN" "https://api.sketchfab.com/v3/models/$uid/download" -o "$dl"
  read -r kind url < <(python3 -c '
import json, sys
d = json.load(open(sys.argv[1]))
for k in ("glb", "gltf", "source"):
    if k in d and d[k].get("url"):
        print(k, d[k]["url"]); break
' "$dl")
  dest="$RAW_DIR/Sketchfab/$folder"
  rm -rf "$dest" && mkdir -p "$dest"
  case "$kind" in
    glb)  curl -fsSL "$url" -o "$dest/model.glb" ;;
    *)    curl -fsSL "$url" -o "$TMP/$uid.zip"; unzip -q "$TMP/$uid.zip" -d "$dest" ;;
  esac
  python3 - "$info" "$dest" <<'PY'
import json, sys, datetime
m = json.load(open(sys.argv[1])); dest = sys.argv[2]
lic = m.get("license") or {}
u = m["user"]
open(f"{dest}/LICENSE.txt", "w").write(
    f"\"{m['name']}\" by {u.get('displayName')} ({u.get('profileUrl')})\n"
    f"Source:  {m.get('viewerUrl')}\n"
    f"License: {lic.get('label')} {lic.get('url') or ''}\n"
    f"Fetched: {datetime.datetime.utcnow():%Y-%m-%dT%H:%M:%SZ}\n\n"
    "This model is NOT covered by the Draho code license. Do not reuse it outside this project\n"
    "except as its own license allows. CC BY requires credit to the author (see CREDITS.md).\n")
json.dump({k: m.get(k) for k in ("uid", "name", "viewerUrl", "faceCount", "vertexCount", "animationCount", "license", "user")},
          open(f"{dest}/sketchfab.json", "w"), indent=2, default=str)
PY
  echo "Saved $kind -> ${dest#$ROOT/}"; ls -la "$dest"
  echo "::endgroup::"
done

rm -rf "$TMP"
