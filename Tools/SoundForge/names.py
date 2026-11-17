#!/usr/bin/env python3
"""Refreshes Sound Forge's list of game sound names from the Sfx enum (Assets/Scripts/Core/Entities.cs)."""
import json, os, re
root = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
src = open(os.path.join(root, "Assets/Scripts/Core/Entities.cs"), encoding="utf-8").read()
i = src.index("enum Sfx"); b = src.index("{", i); e = src.index("}", b)
body = re.sub(r"//.*", "", src[b + 1:e])
names = [n.strip().split("=")[0].strip() for n in body.split(",") if n.strip()]
names = [n for n in names if n and n != "None"]
page = os.path.join(os.path.dirname(os.path.abspath(__file__)), "index.html")
html = open(page, encoding="utf-8").read()
html = re.sub(r"/\*SFX-NAMES\*/.*?/\*END\*/", "/*SFX-NAMES*/" + json.dumps(names, separators=(",", ":")) + "/*END*/", html, flags=re.S)
open(page, "w", encoding="utf-8").write(html)
print(f"{len(names)} sound names")
