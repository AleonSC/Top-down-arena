#!/bin/bash
# Usage: Tools/RenderCheck/check.sh [git-ref]   (default: HEAD)
# Proves a refactor didn't change what the arena looks like: renders 9 fixed, seeded scenes (every enemy and
# minion type, each class's abilities and ultimate) with DrawWorld, in this working tree and at <git-ref>, and
# compares every GL triangle. "IDENTICAL" means nothing on screen changed. Needs mono.
set -e
cd "$(dirname "$0")/../.."
ref=${1:-HEAD}
tmp=$(mktemp -d)
trap 'git worktree remove --force "$tmp/ref" >/dev/null 2>&1; rm -rf "$tmp"' EXIT
git worktree add -q --detach "$tmp/ref" "$ref"
render() {  # <source root> <output file>
    mcs -langversion:7.2 -nowarn:414,169,649,219,618 -out:"$tmp/h.exe" Tools/RenderCheck/Harness.cs Tools/Preview/Stubs.cs \
        "$1"/Assets/Scripts/Core/*.cs "$1"/Assets/Scripts/Game/*.cs >/dev/null
    mono "$tmp/h.exe" "$2"
}
render "$tmp/ref" "$tmp/before.bin" >/dev/null
render . "$tmp/after.bin"
if cmp -s "$tmp/before.bin" "$tmp/after.bin"; then echo "IDENTICAL to $ref"; else echo "DIFFERENT from $ref"; exit 1; fi
