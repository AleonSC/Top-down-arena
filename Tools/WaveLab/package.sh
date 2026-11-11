#!/bin/sh
# Usage: Tools/WaveLab/package.sh [out-dir]   Builds the single-file Wave Lab app: WaveLab.exe (the page is inside it).
# Double-clicking it opens Wave Lab in its own window and quits when the window is closed. On Windows it runs as is
# (the built-in .NET Framework, no console window); Mac/Linux run it with mono.
set -e
cd "$(dirname "$0")"
OUT="${1:-out}"
mkdir -p "$OUT"
OUT="$(cd "$OUT" && pwd)"
rm -f "$OUT/WaveLab.exe"
mcs -langversion:7.2 -nologo -optimize+ -target:winexe -resource:web/index.html,index.html -out:"$OUT/WaveLab.exe" Server.cs ../../Assets/Scripts/Core/*.cs 2>&1 | grep -v warning || true
[ -f "$OUT/WaveLab.exe" ] || exit 1
echo "Built $OUT/WaveLab.exe"
