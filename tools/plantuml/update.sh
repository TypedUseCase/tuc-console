#!/usr/bin/env bash

set -eu
set -o pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly script_directory
repository_root="$(cd -- "$script_directory/../.." && pwd)"
readonly repository_root
readonly manifest_path="$repository_root/tools/plantuml/manifest.txt"
readonly jar_path="$repository_root/tools/plantuml/plantuml.jar"
readonly releases_url="https://github.com/plantuml/plantuml/releases"
readonly releases_api_url="https://api.github.com/repos/plantuml/plantuml/releases"

usage() {
    printf 'Usage: %s [VERSION]\n' "${0##*/}"
    printf 'Download a PlantUML MIT JAR and update %s.\n' "$manifest_path"
}

resolve_version() {
    curl --fail --silent --show-error --location --output /dev/null --write-out '%{url_effective}' "$releases_url/latest" \
        | sed -n 's|.*/tag/v\([0-9][0-9.]*\)$|\1|p'
}

release_checksum() {
    local version="$1"
    local asset_name="plantuml-mit-$version.jar"

    curl --fail --silent --show-error --location "$releases_api_url/tags/v$version" \
        | jq --raw-output --arg asset_name "$asset_name" '
            .assets[]
            | select(.name == $asset_name)
            | .digest // empty
        ' \
        | sed -n 's/^sha256://p'
}

sha256() {
    if command -v sha256sum >/dev/null 2>&1; then
        sha256sum "$1" | awk '{print $1}'
    else
        shasum -a 256 "$1" | awk '{print $1}'
    fi
}

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
    usage
    exit 0
fi

if ! command -v jq >/dev/null 2>&1; then
    printf 'PlantUML updater requires jq.\n' >&2
    exit 1
fi

version="${1:-$(resolve_version)}"

if [[ ! "$version" =~ ^[0-9]+(\.[0-9]+)+$ ]]; then
    printf 'Could not resolve a valid PlantUML version: %s\n' "$version" >&2
    exit 1
fi

url="$releases_url/download/v$version/plantuml-mit-$version.jar"
checksum="$(release_checksum "$version")"

if [[ ! "$checksum" =~ ^[a-f0-9]{64}$ ]]; then
    printf 'Could not resolve an SHA-256 checksum for PlantUML %s.\n' "$version" >&2
    exit 1
fi

current_version="$(sed -n 's/^version=//p' "$manifest_path" 2>/dev/null || true)"
current_checksum="$(sed -n 's/^sha256=//p' "$manifest_path" 2>/dev/null || true)"

if [[ "$current_version" == "$version" && "$current_checksum" == "$checksum" ]]; then
    printf 'PlantUML %s is already current (%s)\n' "$version" "$checksum"
    exit 0
fi

temporary_jar="$(mktemp)"
temporary_manifest="$(mktemp)"

cleanup() {
    rm -f "$temporary_jar" "$temporary_manifest"
}
trap cleanup EXIT

curl --fail --silent --show-error --location --output "$temporary_jar" "$url"
downloaded_checksum="$(sha256 "$temporary_jar")"

if [[ "$downloaded_checksum" != "$checksum" ]]; then
    printf 'Downloaded PlantUML SHA-256 mismatch: expected %s, got %s.\n' "$checksum" "$downloaded_checksum" >&2
    exit 1
fi

cat > "$temporary_manifest" <<EOF
version=$version
url=$url
sha256=$checksum
license=MIT
EOF

mkdir -p "$(dirname "$jar_path")"
mv "$temporary_jar" "$jar_path"
mv "$temporary_manifest" "$manifest_path"

printf 'Pinned PlantUML %s (%s)\n' "$version" "$checksum"
