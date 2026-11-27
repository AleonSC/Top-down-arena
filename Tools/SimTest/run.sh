#!/bin/sh
# Usage: Tools/SimTest/run.sh [start-wave] [seconds] [suite]   (run.sh list: every suite)
set -e
cd "$(dirname "$0")"
# A build of its own per run (SimTest.<pid>.exe), so runs side by side don't overwrite each other's; it stays in this folder
# because some tests find the repo from the exe's folder (Tests/Classes.cs ScriptsDir). Removed when the run ends.
exe="SimTest.$$.exe"
trap 'rm -f "$exe"' EXIT
trap 'rm -f "$exe"; exit 130' INT TERM
# The music synths are plain C#, so they are tested here too.
mcs -langversion:7.2 -nologo -out:"$exe" Program.cs Content.cs Tests/*.cs ../../Assets/Scripts/Core/*.cs ../../Assets/Scripts/Game/MenuMusic.cs ../../Assets/Scripts/Game/GameMusic.cs ../../Assets/Scripts/Game/MusicLibrary.cs ../../Assets/Scripts/Game/CustomMusic.cs
mono "$exe" "$@"
