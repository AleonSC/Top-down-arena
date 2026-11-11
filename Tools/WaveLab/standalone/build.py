#!/usr/bin/env python3
"""Builds the standalone Wave Lab page: app.html + meta.json (enemy stats, squads, built-in sets) -> wavelab.html.
Usage: build.py [--fragment out.html]   (a fragment has no <html>/<head>, for publishing as a claude.ai artifact)
Refresh meta.json after the enemies or wave sets change: run Tools/WaveLab/run.sh, then curl localhost:8080/api/meta > standalone/meta.json."""
import json, os, sys
here = os.path.dirname(os.path.abspath(__file__))
meta = json.load(open(os.path.join(here, "meta.json")))
meta = {k: meta[k] for k in ("enemies", "squads", "sets")}
app = open(os.path.join(here, "app.html")).read().replace("__META__", json.dumps(meta, separators=(",", ":")))
if "--fragment" in sys.argv:
    open(sys.argv[sys.argv.index("--fragment") + 1], "w").write(app)
else:
    head = ('<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n'
            '<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">\n'
            '<style>[hidden]{display:none!important}body{margin:0}</style>\n</head>\n<body>\n')
    open(os.path.join(here, "wavelab.html"), "w").write(head + app + "\n</body>\n</html>\n")
    print("Built", os.path.join(here, "wavelab.html"))
