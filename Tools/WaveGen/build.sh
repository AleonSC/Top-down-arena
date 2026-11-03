#!/bin/bash
# Builds the designed wave sets into the game: reads Data/Waves/*.waves (the F1 wave editor's files) with the game's
# own parser and writes Assets/Scripts/Core/DesignedWaves.Generated.cs. Stops, writing nothing, if a file has a
# problem (it names the file, the line and what's wrong). Run it after adding or changing a .waves file.
#   Tools/WaveGen/build.sh
# Needs mono (mcs). The SimTest suite "wavegen" fails if the generated file is out of date.
set -e
cd "$(dirname "$0")/../.."
out=$(mktemp -d)
trap 'rm -rf "$out"' EXIT
# The generated file itself is left out, so a stale or broken one can't stop the script from replacing it.
core=$(ls Assets/Scripts/Core/*.cs | grep -v 'DesignedWaves.Generated.cs')
mcs -langversion:7.2 -nologo -nowarn:414,169,649,219,618 -out:"$out/gen.exe" Tools/WaveGen/Gen.cs Tools/WaveGen/Stub.cs $core >/dev/null
mono "$out/gen.exe" .
