#!/bin/bash
# A labelled grid of the same scene with different settings, drawn by the real game code (no Unity), to pick from.
#   Tools/Preview/variants.sh <name> <mode> [wave secs players] VAR=a,b,c [VAR2=x,y]   -> Tools/Preview/out/<name>.png
# Every combination of the listed values is one cell. VAR is any shot.sh env flag (THRUST, BG, KNIGHT, CASTT, ...)
# or WAVE / SECS / PLAYERS. Values split on '|' if there is one (MOUSE=100,200|300,400). Env: CROP=x,y,w,h, SCALE, COLS.
# e.g. KNIGHT=1 POSE=1 Tools/Preview/variants.sh lance hud THRUST=0,0.25,0.5,0.75 CROP=440,200,400,320
exec python3 "$(dirname "$0")/sweep.py" variants "$@"
