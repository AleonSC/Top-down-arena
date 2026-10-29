#!/bin/sh
# Usage: Tools/SimTest/run.sh [start-wave] [seconds] [suite]   (run.sh list: every suite)
set -e
cd "$(dirname "$0")"
# The music synths are plain C#, so they are tested here too.
mcs -langversion:7.2 -nologo -out:SimTest.exe Program.cs Tests/*.cs ../../Assets/Scripts/Core/*.cs ../../Assets/Scripts/Game/MenuMusic.cs ../../Assets/Scripts/Game/GameMusic.cs
mono SimTest.exe "$@"
