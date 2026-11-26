#!/usr/bin/env python3
"""Fails when a pass/fail test suite in Program.cs isn't run by CI.

CI runs `core` (CoreMatchTest), `features` (every test in FeatureTests) and the suites named in CiExtras
(extras.sh). A suite made with Test("name", ...) is covered when its name is in CiExtras, or every test
method it calls is also called from FeatureTests. Reports (Report(...)) aren't pass/fail and are skipped.
Run by CI (.github/workflows/checks.yml); fix a miss by adding the suite's name to CiExtras in Program.cs."""
import re, sys, os

src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "Program.cs")).read()

def block(start_pat):
    i = src.index(start_pat)
    j = src.index("};", i)
    return src[i:j]

IDENT = r"\b([A-Z]\w*)\b(?!\.)"  # a method called or passed (not Dt, a type before a dot, or a constant)
SKIP = {"Dt", "Arg"}
features = set(re.findall(IDENT, block("static readonly Action[] FeatureTests"))) - SKIP
extras = set(re.findall(r'"(\w+)"', block("static readonly string[] CiExtras")))

suites = {}
for m in re.finditer(r'^\s*Test\("(\w+)",\s*"(?:[^"\\]|\\.)*"(.*)$', src, re.M):
    name, rest = m.group(1), m.group(2)
    suites[name] = set(re.findall(IDENT, rest)) - SKIP

missing = sorted(n for n, calls in suites.items() if n not in extras and not (calls and calls <= features))
stale = sorted(n for n in extras if n not in suites)
for n in missing:
    print("not run by CI: suite %s (add it to CiExtras in Tools/SimTest/Program.cs)" % n)
for n in stale:
    print("CiExtras names a suite that doesn't exist: %s" % n)
print("CI coverage: %d suites, %s" % (len(suites), "OK" if not missing and not stale else "%d problem(s)" % (len(missing) + len(stale))))
sys.exit(1 if missing or stale else 0)
