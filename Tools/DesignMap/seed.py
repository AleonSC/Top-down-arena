#!/usr/bin/env python3
"""Builds seed.js (starting design map) from Docs/Reference.md. Run: python3 Tools/DesignMap/seed.py"""
import json, os, re
root = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
lines = open(os.path.join(root, "Docs", "Reference.md"), encoding="utf-8").read().splitlines()
nodes, edges, seen = [], [], set()
def add(t, name, notes=""):
    i = t + ":" + name
    if i not in seen:
        seen.add(i); nodes.append({"id": i, "type": t, "name": name, "notes": notes, "status": "built"})
    return i
sec = sub = ""
hdr = None
for ln in lines:
    if ln.startswith("## "): sec, sub, hdr = ln[3:].strip(), "", None; continue
    if ln.startswith("### "): sub, hdr = ln[4:].strip(), None; continue
    if not ln.startswith("|"): continue
    c = [x.strip() for x in ln.strip("|").split("|")]
    if hdr is None: hdr = c; continue
    if set(c[0]) <= set("-"): continue
    r = dict(zip(hdr, c))
    if sec == "Roles": add("Role", c[0], f"HP {r['Health']}, speed {r['Speed']}, ability {r['Ability']} ({r['Cooldown']})")
    elif sec == "Classes": add("Class", c[0], r["About"])
    elif sec in ("Weapons", "Abilities") and sub:
        t = "Weapon" if sec == "Weapons" else "Ability"
        n = f"{r['Role']} · " + r.get("What it does", "")
        i = add(t, c[0], n[:300])
        edges.append([i, "Class:" + sub]); 
        if r["Role"] in ("Tank", "Healer", "DPS"): edges.append([i, "Role:" + r["Role"]])
    elif sec == "Enemies":
        boss = r["Group"] == "Boss"
        add("Boss" if boss else "Enemy", c[0], f"{r['Group']} · HP {r['Health']} · {r['What it does']}"[:300])
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "curated.py"), encoding="utf-8").read())
open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "seed.js"), "w", encoding="utf-8").write(
    "window.SEED=" + json.dumps({"seedVer": 2, "nodes": nodes, "edges": [e for e in edges if e[1] in seen]}, separators=(",", ":")) + ";\n")
print(len(nodes), "nodes", len(edges), "edges")
