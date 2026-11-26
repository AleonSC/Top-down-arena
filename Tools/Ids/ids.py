#!/usr/bin/env python3
"""Keeps the game's growing id lists in name order, one id per line, so sessions adding ids in parallel don't clash.

The ids that grow with content (AbilityId, EnemyType, FxKind, MinionKind, Sfx, WeaponId, ZoneKind) used to take new
values at the end, so two sessions adding one each always edited the same line and git couldn't merge them. In name
order, one per line, two new ids almost always land in different places. Their numbers follow from the order
(nothing stores them: saves aren't kept across builds), and the tables of definitions are keyed by id, not by place.

  ids.py           checks every list is in name order, one per line (run by CI)
  ids.py --fix     puts them in order (a new id can go anywhere in its list; this sorts it into place)
"""
import os, re, sys

CORE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Scripts", "Core")
ENUMS = [("AbilityId", "Upgrades.cs"), ("EnemyType", "Defs.cs"), ("FxKind", "Entities.cs"), ("MinionKind", "Kits.cs"),
         ("Sfx", "Entities.cs"), ("WeaponId", "Defs.cs"), ("ZoneKind", "Kits.cs")]
FIRST = {"Sfx": ["None"]}  # ids pinned to the top (Sfx.None is 0, the default)
NOTE = "// In name order, one per line: Tools/Ids/ids.py (CI checks it; --fix sorts). Numbers follow from the order."


def find_enum(src, name):
    """(start of the declaration line, index after its closing brace (and any comment after it on that line), header, body)."""
    m = re.search(r"^([ \t]*)public enum " + name + r"\b([^{\n]*)", src, re.M)
    if not m:
        raise SystemExit("no enum " + name)
    i = src.index("{", m.end())
    depth, j = 0, i
    while True:
        if src[j] == "{":
            depth += 1
        elif src[j] == "}":
            depth -= 1
            if depth == 0:
                break
        j += 1
    end = src.find("\n", j)
    end = len(src) if end < 0 else end
    return m.start(), end, m.group(1), m.group(2).rstrip(), src[i + 1:j], src[j + 1:end]


def parse(body, after):
    """[(name, value or None, comment)] for each id; full-line comments that only name a group are dropped."""
    entries, pending = [], []
    for line in body.split("\n"):
        block = re.findall(r"/\*(.*?)\*/", line)  # a /* note */ beside an id becomes its comment
        line = re.sub(r"/\*.*?\*/", "", line)
        code, _, com = line.partition("//")
        code, com = code.strip(), "; ".join([com.strip()] * bool(com.strip()) + [b.strip() for b in block])
        if not code:
            if com and com != NOTE[3:]:
                pending.append(com)
            continue
        names = [v.strip() for v in code.split(",") if v.strip()]
        for k, v in enumerate(names):
            n, _, val = v.partition("=")
            note = []
            if k == 0 and pending:
                note = [p for p in pending if ":" in p or len(p) > 60]  # a real note, not a bare group label
            if k == len(names) - 1 and com:
                note.append(com)
            entries.append([n.strip(), val.strip() or None, "; ".join(note)])
        pending = []
    # A one-line enum's docs ride after its closing brace: "// Name: doc  // Other: doc ..."
    for seg in re.split(r"\s+//\s+", " " + after.strip()):
        seg = seg.strip().lstrip("/").strip()
        m = re.match(r"(\w+)(?: \([^)]*\))?:\s", seg)
        if not m:
            continue
        for e in entries:
            if e[0] == m.group(1):
                e[2] = (e[2] + "; " if e[2] else "") + seg
    return entries


def order(name, entries):
    pinned = [e for n in FIRST.get(name, []) for e in entries if e[0] == n]
    explicit = [e for e in entries if e[1] is not None]
    rest = sorted((e for e in entries if e not in pinned and e not in explicit), key=lambda e: e[0].lower())
    return pinned + rest + explicit


def render(indent, name, header, entries):
    lines = [indent + "public enum " + name + header, indent + "{", indent + "    " + NOTE]
    for n, val, com in entries:
        lines.append(indent + "    " + n + (" = " + val if val else "") + "," + ("  // " + com if com else ""))
    lines.append(indent + "}")
    return "\n".join(lines)


def run(fix):
    bad = []
    for name, f in ENUMS:
        path = os.path.join(CORE, f)
        src = open(path).read()
        start, end, indent, header, body, after = find_enum(src, name)
        entries = parse(body, after)
        want = render(indent, name, header, order(name, entries))
        if src[start:end] != want:
            if fix:
                src = src[:start] + want + src[end:]
                open(path, "w").write(src)
            else:
                names = [e[0] for e in entries]
                sortd = [e[0] for e in order(name, entries)]
                where = next((i for i, (a, b) in enumerate(zip(names, sortd)) if a != b), None)
                bad.append(name + " (" + f + ")" + (": " + names[where] + " is out of name order" if where is not None else ": one id per line"))
    for b in bad:
        print("not in name order: " + b + " (python3 Tools/Ids/ids.py --fix)")
    print("ids: %d lists, %s" % (len(ENUMS), "fixed" if fix else ("OK" if not bad else "%d out of order" % len(bad))))
    return 0 if fix or not bad else 1


if __name__ == "__main__":
    sys.exit(run("--fix" in sys.argv))
