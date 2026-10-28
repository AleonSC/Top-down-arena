#!/bin/bash
# Fast compile check of the game scripts without Unity (Unity APIs are stubbed in UnityStubs.cs).
# When the game starts using a Unity member the stubs lack, add it here (and to Tools/Preview/Stubs.cs).
cd "$(dirname "$0")/../.."
if ! command -v mcs >/dev/null 2>&1; then
    echo "FAILED: mcs (mono) isn't installed, so nothing was checked. See HANDOFF.md section 3 to install it."
    exit 2
fi
out=$(mcs -langversion:7.2 -nowarn:414,169,649,219 -target:library -out:/tmp/arena-check.dll Tools/CompileCheck/UnityStubs.cs Assets/Scripts/Core/*.cs Assets/Scripts/Game/*.cs Assets/Editor/*.cs 2>&1)
status=$?
errors=$(echo "$out" | grep "error")
if [ $status -ne 0 ] || [ -n "$errors" ]; then echo "${errors:-$out}"; exit 1; fi
echo "OK: compiles"
