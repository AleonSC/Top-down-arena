#!/bin/sh
# Usage: Tools/SimTest/run.sh [start-wave] [seconds]
set -e
cd "$(dirname "$0")"
mcs -langversion:7.2 -nologo -out:SimTest.exe Program.cs ../../Assets/Scripts/Core/*.cs
mono SimTest.exe "$@"
