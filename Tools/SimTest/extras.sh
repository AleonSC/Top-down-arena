#!/bin/bash
# Usage: Tools/SimTest/extras.sh [shard/shards]   e.g. extras.sh 1/2
# The pass/fail suites that neither `core` nor `features` runs (Program.cs CiExtras), each in its own process (a fresh game
# state, the same seeds as running it alone), built once. CI runs them in a job beside the other two; shards split the list
# round-robin when one job gets too long. Prints each suite's time and result, then the failures.
cd "$(dirname "$0")"
shard=${1:-1/1}; k=${shard%/*}; n=${shard#*/}
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
mcs -langversion:7.2 -nologo -out:"$tmp/SimTest.exe" Program.cs Tests/*.cs ../../Assets/Scripts/Core/*.cs ../../Assets/Scripts/Game/MenuMusic.cs ../../Assets/Scripts/Game/GameMusic.cs ../../Assets/Scripts/Game/MusicLibrary.cs ../../Assets/Scripts/Game/CustomMusic.cs \
    | grep -v "warning CS" || true
[ -f "$tmp/SimTest.exe" ] || { echo "FAILED: the tests don't compile"; exit 1; }
failed=(); i=0; start=$(date +%s)
for suite in $(mono "$tmp/SimTest.exe" extras-list); do
    i=$((i + 1))
    [ $(( (i - 1) % n + 1 )) -eq "$k" ] || continue
    t0=$(date +%s)
    if out=$(mono "$tmp/SimTest.exe" 1 30 "$suite" 2>&1); then r=ok; else r=FAILED; failed+=("$suite"); fi
    printf '%-14s %4ss  %s\n' "$suite" $(( $(date +%s) - t0 )) "$r"
    [ $r = ok ] || echo "$out" | grep -E "^FAIL|Exception" | head -20 | sed 's/^/    /'
done
echo "extras $shard: $(( $(date +%s) - start ))s"
if [ ${#failed[@]} -gt 0 ]; then echo "${#failed[@]} FAILED: ${failed[*]}"; exit 1; fi
echo "ALL PASSED"
