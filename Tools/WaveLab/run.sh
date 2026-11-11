#!/bin/sh
# Usage: Tools/WaveLab/run.sh [port]   Wave Lab: play and bot-test wave designs in a browser (real game sim, any device).
# Installs mono if it's missing (apt or brew), rebuilds only when the source changed, and opens the browser.
cd "$(dirname "$0")" || exit 1
PORT="${1:-8080}"
if ! command -v mcs >/dev/null 2>&1 || ! command -v mono >/dev/null 2>&1; then
  echo "Mono is missing, installing it..."
  if command -v apt-get >/dev/null 2>&1; then
    (sudo -n true 2>/dev/null && S=sudo || S=; $S apt-get update -qq; $S apt-get install -y mono-mcs mono-runtime libmono-system-core4.0-cil libmono-system4.0-cil)
  elif command -v brew >/dev/null 2>&1; then
    brew install mono
  else
    echo "Install Mono from https://www.mono-project.com/download/stable/ and run this again."; exit 2
  fi
fi
if [ ! -f WaveLab.exe ] || [ -n "$(find Server.cs ../../Assets/Scripts/Core -name '*.cs' -newer WaveLab.exe 2>/dev/null | head -1)" ]; then
  echo "Building (first run takes a few seconds)..."
  mcs -langversion:7.2 -nologo -out:WaveLab.exe Server.cs ../../Assets/Scripts/Core/*.cs 2>&1 | grep -v 'warning' ; [ -f WaveLab.exe ] || exit 1
fi
( sleep 2; (xdg-open "http://localhost:$PORT" || open "http://localhost:$PORT") >/dev/null 2>&1 ) &
exec mono WaveLab.exe "$PORT"
