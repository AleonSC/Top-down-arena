#!/bin/sh
# Usage: Tools/EnemyForge/run.sh [port]   Builds and runs Enemy Forge from source (serves web/index.html live; no window, no auto-quit).
set -e
cd "$(dirname "$0")"
mkdir -p out
mcs -langversion:7.2 -nologo -optimize+ -out:out/EnemyForgeDev.exe Server.cs ../../Assets/Scripts/Core/*.cs 2>&1 | grep -v warning || true
exec mono out/EnemyForgeDev.exe "${1:-8190}" --serve --web="$(pwd)/web"
