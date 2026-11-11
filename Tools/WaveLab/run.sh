#!/bin/sh
# Usage: Tools/WaveLab/run.sh [port]   Wave Lab: play and bot-test wave designs in a browser (real game sim, any device).
set -e
cd "$(dirname "$0")"
mcs -langversion:7.2 -nologo -out:WaveLab.exe Server.cs ../../Assets/Scripts/Core/*.cs
exec mono WaveLab.exe "$@"
