#!/bin/bash
# Usage: Tools/RenderCheck/juice.sh   Checks the visual effects (Game/Juice.cs) frame by frame, without Unity. Needs mono.
set -e
cd "$(dirname "$0")/../.."
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
mcs -langversion:7.2 -nowarn:414,169,649,219,618 -out:"$tmp/j.exe" Tools/RenderCheck/JuiceCheck.cs Tools/Preview/Stubs.cs Assets/Scripts/Core/*.cs Assets/Scripts/Game/*.cs >/dev/null
mono "$tmp/j.exe"
