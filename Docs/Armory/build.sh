#!/bin/bash
# Rebuilds the publishable Armory page: icons from the game's PixelArt.cs, injected into armory.src.html.
# Output: Docs/Armory/out/arena-armory.html (publish it over https://claude.ai/artifact/Vx6Rkk2y7TyRd47ADoSnKz).
set -e
cd "$(dirname "$0")"
python3 build_icons.py
mkdir -p out
python3 -c "import json; s=open('armory.src.html').read(); open('out/arena-armory.html','w').write(s.replace('/*ICONS*/null', json.dumps(json.load(open('icons.json')), separators=(',', ':')), 1))"
echo "Docs/Armory/out/arena-armory.html"
