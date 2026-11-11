#!/bin/bash
# A short animated GIF of a scene, drawn by the real game code (no Unity), to judge motion and timing.
#   Tools/Preview/clip.sh <name> <mode> [wave secs players]                -> out/<name>.gif: the match runs on from the shot
#   Tools/Preview/clip.sh <name> <mode> [wave secs players] VAR=from:to:step -> one frame per value of a scene's time flag
# Live: FRAMES=30, CLIPSTEP=3 (fixed steps a frame: 20 fps), CLIPHURT=1 (players can be hurt). Env: CROP=x,y,w,h, SCALE
# (default 0.5, or 1 with CROP), FPS (default 20). Also writes out/<name>-strip.png (six frames in a grid).
# e.g. Tools/Preview/clip.sh boss hud 10 20       CAST="Blood Wall" BLOOD=1 Tools/Preview/clip.sh wall hud CASTT=0:1.2:0.05
exec python3 "$(dirname "$0")/sweep.py" clip "$@"
