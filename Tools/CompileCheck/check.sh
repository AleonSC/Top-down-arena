#!/bin/bash
# Fast compile check of the game scripts without Unity. The Unity APIs come from the preview's stubs
# (Tools/Preview/Stubs.cs, shared with RenderCheck and SoundCheck): when the game starts using a Unity member they lack, add it there.
# check.sh -w also lists the warnings that are normally hidden (unused locals and fields, never-assigned fields).
cd "$(dirname "$0")/../.."
if ! command -v mcs >/dev/null 2>&1; then
    echo "FAILED: mcs (mono) isn't installed, so nothing was checked. See HANDOFF.md section 3 to install it."
    exit 2
fi
nowarn="-nowarn:414,169,649,219"
[ "$1" = "-w" ] && nowarn=""
tmp=$(mktemp -d)  # (its own output: two checks at once don't overwrite each other)
trap 'rm -rf "$tmp"' EXIT
out=$(mcs -langversion:7.2 $nowarn -target:library -out:"$tmp/arena-check.dll" Tools/Preview/Stubs.cs Assets/Scripts/Core/*.cs Assets/Scripts/Game/*.cs Assets/Editor/*.cs 2>&1)
status=$?
errors=$(echo "$out" | grep -E "error CS[0-9]+")  # (a compiler error, not any line that happens to contain "error")
[ "$1" = "-w" ] && echo "$out" | grep -E "warning CS[0-9]+"
if [ $status -ne 0 ] || [ -n "$errors" ]; then echo "${errors:-$out}"; exit 1; fi
echo "OK: compiles"
