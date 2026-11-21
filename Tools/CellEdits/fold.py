#!/usr/bin/env python3
"""Writes layouts edited in the game (F1 > LABS > CELL EDITOR) into the code: cell layouts (CellLayoutEdits.txt) into Core/CellLayouts.cs and hub
floor plans (HubLayoutEdits.txt) into Core/HubLayouts.Generated.cs and core floor plans (CoreLayoutEdits.txt) into Core/CoreLayouts.cs.

Each line of the file replaces the `new CellLayout { Name = "...", ... }` entry with that name. Folded lines are removed from
the file (the file is deleted when none is left). A hub line replaces the line of the same name in HubLayouts.Generated.cs.

Usage: python3 Tools/CellEdits/fold.py [--dry-run] [repo root]
"""
import os
import re
import sys

dry = '--dry-run' in sys.argv
args = [a for a in sys.argv[1:] if not a.startswith('--')]
root = args[0] if args else os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
edits_path = os.path.join(root, 'CellLayoutEdits.txt')
code_path = os.path.join(root, 'Assets', 'Scripts', 'Core', 'CellLayouts.cs')


def points(text, n, fn):
    if not text:
        return 'new int[0][]'
    items = [tuple(int(v) for v in p.split(',')) for p in text.split(';')]
    return 'new[] { ' + ', '.join('%s(%s)' % (fn, ', '.join(str(v) for v in it)) for it in items) + ' }'


def entry(name, walls, sleepers, traps, loot):
    return ('new CellLayout { Name = "%s",\n'
            '                Walls = %s,\n'
            '                Sleepers = %s,\n'
            '                Traps = %s,\n'
            '                Loot = %s }') % (name, points(walls, 4, 'W'), points(sleepers, 2, 'P'), points(traps, 3, 'T'), points(loot, 2, 'P'))


hub_path = os.path.join(root, 'HubLayoutEdits.txt')
hub_code = os.path.join(root, 'Assets', 'Scripts', 'Core', 'HubLayouts.Generated.cs')


def fold_hub():
    if not os.path.exists(hub_path):
        return
    text = open(hub_code).read()
    m = re.search(r'Data = "(.*)";', text)
    old = m.group(1).replace('\\t', '\t').replace('\\n', '\n') if m else ''
    lines = {}
    for ln in old.split('\n'):
        if ln:
            lines[ln.split('\t')[0]] = ln
    folded = []
    for raw in open(hub_path).read().split('\n'):
        ln = raw.rstrip('\r')
        if not ln or ln.startswith('#!'):
            continue
        if len(ln.split('\t')) == 4:
            lines[ln.split('\t')[0]] = ln
            folded.append(ln.split('\t')[0])
    data = ''.join(l + '\n' for l in (lines[k] for k in sorted(lines)))
    esc = data.replace('\t', '\\t').replace('\n', '\\n')
    print('Folded hub plans:', ', '.join(folded) if folded else 'nothing')
    if not dry:
        open(hub_code, 'w').write(text[:m.start(1)] + esc + text[m.end(1):])
        os.remove(hub_path)



def fold_core():
    path = os.path.join(root, 'CoreLayoutEdits.txt')
    if not os.path.exists(path):
        return
    cpath = os.path.join(root, 'Assets', 'Scripts', 'Core', 'CoreLayouts.cs')
    code = open(cpath).read()
    keep, folded = [], []
    for raw in open(path).read().split('\n'):
        ln = raw.rstrip('\r')
        if not ln or ln.startswith('#!'):
            continue
        f = ln.split('\t')
        m = re.search(r'Name = "%s",.*?Pieces = new\[\] \{.*?\} \},' % re.escape(f[0]), code, re.S) if len(f) == 2 else None
        if not m:
            keep.append(ln)
            print('No core layout named', f[0])
            continue
        pieces = ', '.join('P(%s)' % ', '.join(p.split(',')) for p in f[1].split(';') if p)
        block = m.group(0)
        head = block[:block.index('Pieces = new[] {')]
        code = code[:m.start()] + head + 'Pieces = new[] { ' + pieces + ' } },' + code[m.end():]
        folded.append(f[0])
    print('Folded core plans:', ', '.join(folded) if folded else 'nothing')
    if not dry:
        open(cpath, 'w').write(code)
        if keep:
            open(path, 'w').write('\n'.join(keep) + '\n')
        else:
            os.remove(path)


fold_hub()
fold_core()
if not os.path.exists(edits_path):
    sys.exit(0)

code = open(code_path).read()
keep, folded = [], []
for raw in open(edits_path).read().split('\n'):
    line = raw.rstrip('\r')
    if not line or line.startswith('#!'):
        continue
    f = line.split('\t')
    if len(f) != 5:
        keep.append(line)
        continue
    pat = re.compile(r'new CellLayout \{ Name = "%s",.*?\} \}' % re.escape(f[0]), re.S)
    m = pat.search(code)
    if not m:
        keep.append(line)
        print('No layout named', f[0])
        continue
    code = code[:m.start()] + entry(*f) + code[m.end():]
    folded.append(f[0])

print('Folded:', ', '.join(folded) if folded else 'nothing')
if not dry:
    open(code_path, 'w').write(code)
    if keep:
        open(edits_path, 'w').write('\n'.join(keep) + '\n')
    else:
        os.remove(edits_path)
