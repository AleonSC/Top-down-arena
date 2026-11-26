#!/usr/bin/env python3
"""The suites CI runs, worked out from Program.cs, so a new suite needs no list edit.

CI runs `core` (CoreMatchTest), `features` (every test in FeatureTests) and the extras: every pass/fail suite made with
Test("name", ...) that `features` doesn't already cover (some test method it calls isn't called from FeatureTests).
Reports (Report(...)) aren't pass/fail and are skipped.
  ci_coverage.py           checks the file parses into suites and prints the count (run by CI)
  ci_coverage.py --extras  prints the extras, one per line, in the order they're declared (extras.sh runs them)"""
import re, sys, os

src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "Program.cs")).read()

def block(start_pat):
    i = src.index(start_pat)
    j = src.index("};", i)
    return src[i:j]

IDENT = r"\b([A-Z]\w*)\b(?!\.)"  # a method called or passed (not Dt, a type before a dot, or a constant)
SKIP = {"Dt", "Arg"}
features = set(re.findall(IDENT, block("static readonly Action[] FeatureTests"))) - SKIP

suites, extras = [], []
for m in re.finditer(r'^\s*Test\("(\w+)",\s*"(?:[^"\\]|\\.)*"(.*)$', src, re.M):
    name, rest = m.group(1), m.group(2)
    calls = set(re.findall(IDENT, rest)) - SKIP
    suites.append(name)
    if not (calls and calls <= features) and name not in extras:
        extras.append(name)

if "--extras" in sys.argv:
    print("\n".join(extras))
    sys.exit(0)
dupes = sorted({n for n in suites if suites.count(n) > 1})
for n in dupes:
    print("two suites are named %s" % n)
ok = len(extras) > 0 and not dupes
print("CI coverage: %d suites (%d in features, %d extras), %s" % (len(suites), len(suites) - len(extras), len(extras), "OK" if ok else "problem"))
sys.exit(0 if ok else 1)
