#!/bin/bash
# Rebuilds the Wave Almanac and the Build Planner from the game's code:
#   extract.py reads the numbers into game-data.json, the Armory's build_icons.py reads the upgrade icons,
#   and both are injected into each page's source (at the /*DATA*/null and /*ICONS*/null placeholders).
# Output: Docs/Almanac/out/wave-almanac.html and Docs/Planner/out/build-planner.html (publish them over the URLs in Docs/README.md).
set -e
cd "$(dirname "$0")/.."
python3 Data/extract.py
(cd Armory && python3 build_icons.py)
python3 - <<'PY'
import json, os
data = json.dumps(json.load(open("Data/game-data.json")), separators=(",", ":"), ensure_ascii=False)
icons = json.dumps(json.load(open("Armory/icons.json")), separators=(",", ":"))
for src, out in (("Almanac/almanac.src.html", "Almanac/out/wave-almanac.html"), ("Planner/planner.src.html", "Planner/out/build-planner.html")):
    if not os.path.exists(src):
        continue
    s = open(src, encoding="utf-8").read().replace("/*DATA*/null", data, 1).replace("/*ICONS*/null", icons, 1)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    open(out, "w", encoding="utf-8").write(s)
    print("Docs/" + out)
PY
