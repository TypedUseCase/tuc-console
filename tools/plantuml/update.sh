#!/usr/bin/env bash

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly script_directory
repository_root="$(cd -- "$script_directory/../.." && pwd)"
readonly repository_root
readonly manifest_path="$repository_root/tools/plantuml/manifest.txt"
readonly provenance_path="$repository_root/tools/plantuml/source-provenance.txt"
readonly license_path="$repository_root/tools/plantuml/licenses/COPYING"
readonly notice_path="$repository_root/tools/plantuml/licenses/NOTICE"
readonly releases_url="https://github.com/plantuml/plantuml/releases"
readonly releases_api_url="https://api.github.com/repos/plantuml/plantuml/releases"

usage() {
    printf 'Usage: %s [VERSION]\n' "${0##*/}"
    printf 'Update PlantUML native archive metadata, GPLv3 text, notice, and source provenance.\n'
}

resolve_version() {
    curl --fail --silent --show-error --location --output /dev/null --write-out '%{url_effective}' "$releases_url/latest" \
        | sed -n 's|.*/tag/v\([0-9][0-9.]*\)$|\1|p'
}

asset_name() {
    case "$1" in
        linux-x64) printf 'native-plantuml-linux-amd64-%s.zip' "$2" ;;
        win-x64) printf 'native-plantuml-windows-amd64-%s.zip' "$2" ;;
        osx-arm64) printf 'native-plantuml-macos-arm64-%s.zip' "$2" ;;
    esac
}

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
    usage
    exit 0
fi

for command in curl jq; do
    if ! command -v "$command" >/dev/null 2>&1; then
        printf 'PlantUML updater requires %s.\n' "$command" >&2
        exit 1
    fi
done

version="${1:-$(resolve_version)}"

if [[ ! "$version" =~ ^[0-9]+(\.[0-9]+)+$ ]]; then
    printf 'Could not resolve a valid PlantUML version: %s\n' "$version" >&2
    exit 1
fi

release_json="$(mktemp)"

cleanup() {
    rm -f "$release_json"
}
trap cleanup EXIT

curl --fail --silent --show-error --location "$releases_api_url/tags/v$version" --output "$release_json"

asset_metadata() {
    jq --raw-output --arg name "$1" '
        .assets[]
        | select(.name == $name)
        | [.name, .browser_download_url, (.digest // "" | sub("^sha256:"; ""))]
        | @tsv
    ' "$release_json"
}

assets=()
for rid in linux-x64 win-x64 osx-arm64; do
    name="$(asset_name "$rid" "$version")"
    IFS=$'\t' read -r name url checksum <<< "$(asset_metadata "$name")"

    if [[ -z "$url" || ! "$checksum" =~ ^[a-f0-9]{64}$ ]]; then
        printf 'Could not resolve an SHA-256 checksum for %s.\n' "$name" >&2
        exit 1
    fi

    executable="plantuml"
    [[ "$rid" == "win-x64" ]] && executable="plantuml.exe"
    assets+=("$rid|$name|$url|$checksum|$executable")
done

mkdir -p "$(dirname "$license_path")"
curl --fail --silent --show-error --location "https://raw.githubusercontent.com/plantuml/plantuml/v$version/LICENSE" --output "$license_path"

{
    printf 'version=%s\n' "$version"
    printf 'license=GPL-3.0-only\n'
    printf 'release=%s/tag/v%s\n' "$releases_url" "$version"
    printf 'license-text=tools/plantuml/licenses/COPYING\n'
    printf 'notice=tools/plantuml/licenses/NOTICE\n'
    printf 'source-provenance=tools/plantuml/source-provenance.txt\n\n'

    for index in "${!assets[@]}"; do
        [[ "$index" -gt 0 ]] && printf '\n'
        asset="${assets[$index]}"
        IFS='|' read -r rid name url checksum executable <<< "$asset"
        printf 'rid.%s.archive=%s\n' "$rid" "$name"
        printf 'rid.%s.url=%s\n' "$rid" "$url"
        printf 'rid.%s.sha256=%s\n' "$rid" "$checksum"
        printf 'rid.%s.executable=%s\n' "$rid" "$executable"
    done
} > "$manifest_path"

{
    printf 'component=PlantUML native runtime\n'
    printf 'version=%s\n' "$version"
    printf 'license=GPL-3.0-only\n'
    printf 'source_repository=https://github.com/plantuml/plantuml\n'
    printf 'source_release=https://github.com/plantuml/plantuml/tree/v%s\n' "$version"
    printf 'source_archive=https://github.com/plantuml/plantuml/archive/refs/tags/v%s.tar.gz\n' "$version"
    printf 'release=%s/tag/v%s\n' "$releases_url" "$version"
    printf 'license_text=tools/plantuml/licenses/COPYING\n'
    printf 'notice=tools/plantuml/licenses/NOTICE\n'
} > "$provenance_path"

{
    printf 'PlantUML\n'
    printf 'Copyright 2009-2026 PlantUML contributors.\n\n'
    printf 'This distribution embeds an unmodified official PlantUML native runtime archive.\n'
    printf 'The archive is licensed under the GNU General Public License, version 3.\n\n'
    printf 'Upstream project: https://github.com/plantuml/plantuml\n'
    printf 'Pinned release: v%s\n' "$version"
    printf 'Release page: %s/tag/v%s\n' "$releases_url" "$version"
} > "$notice_path"

printf 'Pinned PlantUML native archives for %s.\n' "$version"
