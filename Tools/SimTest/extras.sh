#!/bin/bash
# Usage: Tools/SimTest/extras.sh [shard/shards] [jobs]   e.g. extras.sh 1/2
# The pass/fail suites that neither `core` nor `features` runs (Program.cs CiExtras), each in its own process (a fresh game
# state, the same seeds as running it alone), built once. CI runs them in a job beside the other two; shards split the list
# round-robin when one job gets too long. Suites run `jobs` at a time (default: one per CPU): the test network is in memory
# (NetPeer.Loopback), so suites share no ports and can't disturb each other. Prints each suite's time and result, then the failures.
# A suite that fails is run once more, alone. A pass on the second try is printed as FLAKY (with the first try's failures),
# never hidden: a flaky suite is a bug to fix, not to retry forever.
cd "$(dirname "$0")"
shard=${1:-1/1}; k=${shard%/*}; n=${shard#*/}
jobs=${2:-$(nproc 2>/dev/null || echo 2)}
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
mcs -langversion:7.2 -nologo -out:"$tmp/SimTest.exe" Program.cs Content.cs Tests/*.cs ../../Assets/Scripts/Core/*.cs ../../Assets/Scripts/Game/MenuMusic.cs ../../Assets/Scripts/Game/GameMusic.cs ../../Assets/Scripts/Game/MusicLibrary.cs ../../Assets/Scripts/Game/CustomMusic.cs \
    | grep -v "warning CS" || true
[ -f "$tmp/SimTest.exe" ] || { echo "FAILED: the tests don't compile"; exit 1; }
start=$(date +%s); i=0; mine=()
for suite in $(mono "$tmp/SimTest.exe" extras-list); do
    i=$((i + 1))
    [ $(( (i - 1) % n + 1 )) -eq "$k" ] && mine+=("$suite")
done

# One suite: its output to $tmp/<suite>.out, then "<suite> <seconds> <exit code>" to $tmp/<suite>.res.
run_one() {
    t0=$(date +%s)
    mono "$tmp/SimTest.exe" 1 30 "$1" > "$tmp/$1.out" 2>&1; rc=$?
    echo "$1 $(( $(date +%s) - t0 )) $rc" > "$tmp/$1.res"
}
export -f run_one; export tmp
printf '%s\n' "${mine[@]}" | xargs -P "$jobs" -I{} bash -c 'run_one {}'

failed=(); flaky=()
for suite in "${mine[@]}"; do
    read -r _ secs rc < "$tmp/$suite.res"
    r=ok
    if [ "$rc" != 0 ]; then
        first=$(grep -E "^FAIL|Exception" "$tmp/$suite.out" | head -20)
        if mono "$tmp/SimTest.exe" 1 30 "$suite" > "$tmp/$suite.again" 2>&1; then r="FLAKY (passed on the second try)"; flaky+=("$suite")
        else r=FAILED; failed+=("$suite"); first=$(grep -E "^FAIL|Exception" "$tmp/$suite.again" | head -20); fi
    fi
    printf '%-16s %4ss  %s\n' "$suite" "$secs" "$r"
    [ "$r" = ok ] || echo "$first" | sed 's/^/    /'
done
echo "extras $shard: ${#mine[@]} suites, $jobs at a time, $(( $(date +%s) - start ))s"
[ ${#flaky[@]} -gt 0 ] && echo "flaky (failed once, then passed): ${flaky[*]}"
if [ ${#failed[@]} -gt 0 ]; then echo "${#failed[@]} FAILED: ${failed[*]}"; exit 1; fi
echo "ALL PASSED"
