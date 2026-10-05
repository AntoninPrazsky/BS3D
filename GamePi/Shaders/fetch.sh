#!/usr/bin/env bash
# Brings the GL effects gamepi-shaders.yml compiled for a commit (#789) into GamePi/Shaders/Compiled, on a machine that
# cannot compile them (Linux, the Pi): edit an effect, push, let the workflow run (it fails, the committed files being
# stale), run this, commit what it wrote. Needs gh, logged in. The commit is HEAD unless one is named.
#
#   GamePi/Shaders/fetch.sh [commit]
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
sha="$(git -C "$here" rev-parse "${1:-HEAD}")"

run="$(gh run list --workflow gamepi-shaders.yml --commit "$sha" --json databaseId,status \
    --jq 'map(select(.status == "completed")) | .[0].databaseId // empty')"
if [ -z "$run" ]; then
    echo "No finished gamepi-shaders.yml run for $sha yet (is it pushed, and has the run ended?)" >&2
    exit 1
fi

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
gh run download "$run" -n gamepi-gl-shaders -D "$tmp"

mkdir -p "$here/Compiled"
cp "$tmp"/*.xnb "$here/Compiled/"
echo "Run $run's effects for ${sha:0:8} are in GamePi/Shaders/Compiled:"
ls -l "$here/Compiled"
