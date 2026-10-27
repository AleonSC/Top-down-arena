#!/bin/sh
# Usage: Tools/SimTest/run.sh [start-wave] [seconds]
set -e
cd "$(dirname "$0")"
# The music synths are plain C#, so they are tested here too.
mcs -langversion:7.2 -nologo -out:SimTest.exe Program.cs ../../Assets/Scripts/Core/*.cs ../../Assets/Scripts/Game/MenuMusic.cs ../../Assets/Scripts/Game/GameMusic.cs
mono SimTest.exe "$@"
