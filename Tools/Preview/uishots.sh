#!/bin/bash
# Renders every UI screen in ui-screens.txt in one go (compiled once, rendered in parallel) and makes a contact sheet.
#   Tools/Preview/uishots.sh                  -> Tools/Preview/out/ui/<name>.png and out/ui/sheet.png
#   Tools/Preview/uishots.sh -r <git-ref>     -> also renders the same screens at that commit (its own code and
#                                                preview tool) and writes out/ui/compare.png: changed screens side by
#                                                side (left = ref, right = now), unchanged ones listed. Renders are
#                                                repeatable (fixed seed and dates), except screens marked NOISY=1 in
#                                                ui-screens.txt (several players over the network): check those by eye
#   Tools/Preview/uishots.sh menu lobby-      -> only screens whose name starts with one of these
# Env: JOBS=n (parallel renders, default the core count), FAST=1 (shot.py's quick flat look).
set -e
cd "$(dirname "$0")"
HERE=$(pwd)
REF=""
if [ "$1" = "-r" ]; then REF="$2"; shift 2; fi
FILTER=("$@")
JOBS=${JOBS:-$(nproc 2>/dev/null || echo 2)}

# The screens to render: "name mode wave seconds players FLAGS..."
list() {
  grep -v '^\s*#' "$HERE/ui-screens.txt" | grep -v '^\s*$' | while read -r name mode rest; do
    if [ ${#FILTER[@]} -gt 0 ]; then
      ok=0; for f in "${FILTER[@]}"; do case "$name" in "$f"*) ok=1;; esac; done
      [ $ok = 1 ] || continue
    fi
    nums=(); flags=()
    for t in $rest; do case "$t" in *=*) flags+=("$t");; *) nums+=("$t");; esac; done
    echo "$name $mode ${nums[0]:-6} ${nums[1]:-12.2} ${nums[2]:-1} ${flags[*]}" | sed "s/ *$//"  # (no trailing blank: xargs -L would join the next line)
  done
}

# render <tool dir> <scripts dir> <out dir>: compile once, then each screen (own temp dir, so saves don't clash)
render() {
  local tool="$1" src="$2" out="$3"
  [ ${#FILTER[@]} -gt 0 ] || rm -rf "$out"  # (a filtered run keeps the other screens)
  mkdir -p "$out"
  ( cd "$tool" && mcs -langversion:7.2 -nowarn:414,169,649,219,618 -out:"$out/shot.exe" Stubs.cs Shot*.cs "$src"/Core/*.cs "$src"/Game/*.cs >/dev/null ) \
    || { echo "compile failed in $tool" >&2; return 1; }
  list | xargs -P "$JOBS" -L 1 bash -c '
    out="$0"; tool="$1"; name="$2"; mode="$3"; wave="$4"; secs="$5"; pl="$6"; shift 6
    mkdir -p "$out/tmp-$name"
    if ( cd "$out" && env TMPDIR="$out/tmp-$name" "$@" mono shot.exe "$name" "$wave" "$secs" "$pl" "$mode" >/dev/null 2>"$out/$name.err" \
         && python3 "$tool/shot.py" "$name" "$name.png" >/dev/null 2>>"$out/$name.err" ); then
      rm -f "$out/$name.err" "$out/$name.tris" "$out/$name.ui"; echo "  $name"
    else echo "  $name FAILED (see $out/$name.err)"; fi
    rm -rf "$out/tmp-$name"' "$out" "$tool"
  rm -f "$out/shot.exe"
}

NOW="$HERE/out/ui"
echo "Rendering the current code:"
render "$HERE" "$HERE/../../Assets/Scripts" "$NOW"
if [ -n "$REF" ]; then
  WT=$(mktemp -d)
  git worktree add --detach "$WT" "$REF" >/dev/null 2>&1 || { echo "can't check out $REF" >&2; exit 1; }
  trap 'git worktree remove --force "$WT" >/dev/null 2>&1' EXIT
  echo "Rendering $REF:"
  # A ref from before the preview was made repeatable: give it the same seed and fixed dates, so only real changes differ.
  if ! grep -q SeedForTests "$WT/Tools/Preview/Shot.cs"; then
    python3 - "$WT/Tools/Preview" <<'PY'
import sys, os
d = sys.argv[1]
for n in ("Shot.cs", "ShotModes.cs", "ShotScenes.cs"):
    p = os.path.join(d, n); s = open(p).read().replace("DateTime.UtcNow", "new DateTime(2026, 11, 7, 18, 0, 0, DateTimeKind.Utc)")
    if n == "Shot.cs":
        s = s.replace("    Shot.args = args;", "    Game.SeedForTests(1);\n    Shot.args = args;", 1)
        s = s.replace("    set = (n, v) => typeof(ArenaApp).GetField(n, f2).SetValue(app, v);",
                      "    set = (n, v) => typeof(ArenaApp).GetField(n, f2).SetValue(app, v);\n    try { set(\"juiceRng\", new System.Random(1)); } catch { }", 1)
    open(p, "w").write(s)
PY
  fi
  render "$WT/Tools/Preview" "$WT/Assets/Scripts" "$HERE/out/ui-ref"
  python3 uisheet.py "$NOW" "$HERE/out/ui-ref" "$REF"
else
  python3 uisheet.py "$NOW"
fi
