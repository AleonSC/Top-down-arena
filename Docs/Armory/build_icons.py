#!/usr/bin/env python3
"""Builds icons.json for the Armory page straight from the game's pixel art (Assets/Scripts/Game/PixelArt.cs),
so the page always shows the same icons as the game.

Output: {"pal": {char: "#rrggbb" or None}, "icons": {key: [12 rows]}, "names": {upgrade name: icon key}}
Usage:  python3 Docs/Armory/build_icons.py   (from anywhere; writes Docs/Armory/icons.json)
"""
import json, os, re

here = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(here, '..', '..', 'Assets', 'Scripts', 'Game', 'PixelArt.cs'), encoding='utf-8').read()

def block(name):
    """The text of the C# initializer `name = new Dictionary<...> { ... };`."""
    start = src.index(name + ' = new Dictionary')
    open_brace = src.index('{', start)
    depth, i = 0, open_brace
    while True:
        if src[i] == '{': depth += 1
        elif src[i] == '}':
            depth -= 1
            if depth == 0: return src[open_brace + 1:i]
        i += 1

pal = {'.': None}
for ch, r, g, b in re.findall(r"\{\s*'(.)',\s*new Color32\((\d+),\s*(\d+),\s*(\d+),\s*\d+\)\s*\}", block('Palette')):
    pal[ch] = '#%02x%02x%02x' % (int(r), int(g), int(b))

names = dict(re.findall(r'\{\s*"((?:[^"\\]|\\.)*)",\s*"([^"]+)"\s*\}', block('ByUpgrade')))

icons = {}
for key, body in re.findall(r'\["([^"]+)"\]\s*=\s*new\[\]\s*\{([^}]*)\}', block('Icons')):
    icons[key] = re.findall(r'"([^"]*)"', body)

problems = [k for k, rows in icons.items() if len(rows) != 12 or any(len(r) != 12 for r in rows)]
problems += ['%s -> missing icon %s' % (n, k) for n, k in names.items() if k not in icons]
problems += ['%s uses unknown colour %r' % (k, ch) for k, rows in icons.items() for row in rows for ch in row if ch not in pal]
out = os.path.join(here, 'icons.json')
with open(out, 'w') as f:
    json.dump({'pal': pal, 'icons': icons, 'names': names}, f, separators=(',', ':'))
print('%d icons, %d upgrades mapped -> %s' % (len(icons), len(names), out))
if problems:
    print('problems:', problems)
