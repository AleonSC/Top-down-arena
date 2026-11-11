#!/bin/bash
# The balance grid: Good bots play every role, class and weapon against the boss of each listed wave (the Boss Lab, real
# game code), in parallel, and the results become a heatmap.
#   Tools/BalanceGrid/grid.sh [waves=10,20,30] [runs=2] [roles=DTH] [skill=2] [classes, e.g. Knight,Ninja]
#   -> Tools/BalanceGrid/out/grid.csv (one line per fight) and grid.png (wins, fight time, DPS per cell; outliers listed)
# Env: GRIDTEAM=auto (default: the tested seat plus a Soldier Tank / Soldier DPS / Elementalist Healer in the other two
#      roles; "" = alone; or seats like T/K/0,H/M/1), METRIC=wins|dps|time (what colours the cells), JOBS=n.
#   Tools/BalanceGrid/grid.sh 10,20,30,40,50 3 D     # every DPS loadout vs five bosses, three runs each
set -e
cd "$(dirname "$0")"
mkdir -p out
JOBS=${JOBS:-$(nproc 2>/dev/null || echo 2)}
export GRIDTEAM=${GRIDTEAM-auto}
( cd ../SimTest && mcs -langversion:7.2 -nologo -nowarn:414,169,649,219,618 -out:SimTest.exe Program.cs Tests/*.cs ../../Assets/Scripts/Core/*.cs ../../Assets/Scripts/Game/MenuMusic.cs ../../Assets/Scripts/Game/GameMusic.cs ../../Assets/Scripts/Game/MusicLibrary.cs ../../Assets/Scripts/Game/CustomMusic.cs >/dev/null ) \
  || { echo "compile failed (run Tools/SimTest/run.sh list to see why)" >&2; exit 1; }
echo "fighting on $JOBS cores..."
for i in $(seq 0 $((JOBS - 1))); do
  ( cd ../SimTest && GRIDSHARD=$i/$JOBS PORTBASE=$((8100 + i * 400)) mono SimTest.exe 1 1 grid "${1:-10,20,30}" "${2:-2}" "${3:-DTH}" "${4:-2}" ${5:+"$5"} > "../BalanceGrid/out/shard$i.txt" ) &
done
wait
grep -h '^GRIDHEAD' out/shard0.txt | head -1 | cut -d, -f2- > out/grid.csv
grep -h '^GRID,' out/shard*.txt | cut -d, -f2- >> out/grid.csv
rm -f out/shard*.txt
echo "$(($(wc -l < out/grid.csv) - 1)) fights -> Tools/BalanceGrid/out/grid.csv"
python3 heatmap.py out/grid.csv out/grid.png
