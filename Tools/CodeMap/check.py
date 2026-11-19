#!/usr/bin/env python3
"""Checks CODEMAP.md against the source: every file it names exists, no file has two lines, and every source file
under Assets/Scripts/Core and Assets/Scripts/Game has a line. Usage: Tools/CodeMap/check.py (exit 1 on a problem)."""
import os, re, sys
from collections import Counter

root = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
src = os.path.join(root, "Assets", "Scripts")
text = open(os.path.join(root, "CODEMAP.md"), encoding="utf-8").read()

def resolve(name):
    """A backticked name in a file line: `X.cs` is Core/X.cs (or Game/, Tests/), `Game/X.cs` is explicit."""
    for base in ("", "Core", "Game"):
        p = os.path.join(src, base, name)
        if os.path.isfile(p):
            return os.path.normpath(p)
    return None

lead = Counter()
where = {}         # file -> the line numbers of its entries
lead = Counter()   # files that head a line (their own entry): the run of names the line starts with
listed = set()     # files named anywhere on a line
missing = set()
joiner = re.compile(r"^[\s,]*(?:and|&)?[\s,]*(?:\((?:Core|Game)\))?[\s,]*(?:and|&)?[\s,]*$")
lines = text.splitlines()
for no, line in enumerate(lines, 1):
    if not line.lstrip().startswith("- "):
        continue
    k = no
    while k < len(lines) and lines[k].startswith("  ") and not lines[k].lstrip().startswith("- "):
        line += " " + lines[k].strip()  # an entry's wrapped lines
        k += 1
    hits = list(re.finditer(r"`((?:Core/|Game/)?[\w.]+\.cs)`", line))
    for m in hits:
        p = resolve(m.group(1))
        if p:
            listed.add(p)
    run = []
    if line.startswith("- `") and hits and hits[0].start() == 2:  # an entry: "- `A.cs`, `Game/B.cs`: what they hold" (other lines only mention files)
        for i, m in enumerate(hits):
            if i > 0 and not joiner.match(line[hits[i - 1].end():m.start()]):
                break
            run.append(m)
        if not run or not re.match(r"\s*(\([^)]*\))?\s*:", line[run[-1].end():]):
            run = []
        run = [m.group(1) for m in run]
    for n in run:
        p = resolve(n)
        if p is None:
            missing.add(n)
        else:
            lead[p] += 1
            where.setdefault(p, []).append(no)

problems = []
problems += [f"names a file that doesn't exist: {n}" for n in sorted(missing)]
problems += [f"listed {c} times (lines {', '.join(map(str, where[p]))}): {os.path.relpath(p, src)}" for p, c in sorted(lead.items()) if c > 1]
for d in ("Core", "Game"):
    for f in sorted(os.listdir(os.path.join(src, d))):
        p = os.path.normpath(os.path.join(src, d, f))
        if f.endswith(".cs") and p not in listed:
            problems.append(f"no line: {d}/{f}")
for p in problems:
    print("CODEMAP.md " + p)
print("CODEMAP.md: OK" if not problems else f"CODEMAP.md: {len(problems)} problem(s)")
sys.exit(1 if problems else 0)
