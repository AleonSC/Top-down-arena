# Top-Down Arena

A 1-3 player co-op top-down bullet hell in **Unity 6** (`6000.6.7f1`, Unity 6.6). The game logic is plain C# in
`Assets/Scripts/Core/` (namespace `Arena`, no Unity), and the Unity front end is in `Assets/Scripts/Game/`. There's
no Unity in the cloud container: check changes with the tools below.

## Where to look (read only what the task needs)

- `CODEMAP.md`: **start here**. One line per source file grouped by system, plus a "Where to look for..." table.
  It's the one file map to keep up to date: give every new source file a line there.
- `HANDOFF.md`: §1 the game in one page · §2 key mechanisms (networking, snapshots, ...) and "Adding a class" ·
  §3 every test and preview tool · §6 open ideas from the code audits.
- `README.md`: player-facing reference for every weapon, ability, enemy, boss, mode and control. Use `grep -n '^## '
  README.md` to jump to a section.
- `Docs/AfterReset.md`: **the owner's to-do list** (open items, ideas left for later, unstarted plans). Check it at
  the start of a session.
- `Docs/History.md`: background only (finished to-do entries, the audit notes, the old changelog); `git log` usually
  does. `Docs/CodeNotes.md`: older, detailed per-file notes, no longer maintained.
- `Docs/*.md`: design docs (Paths, Approaches, BossHuntsAndPerks, ZombieInfection, ...); each starts with its status.
  `Docs/README.md` covers the Bestiary and Armory pages.
- Big generated or reference files are in `.ignore` (mockups, Armory/Bestiary HTML, History, `*.Generated.cs`), so a
  repo-wide search skips them. Search them by naming the path.

## Rules

- Work and push on the session's branch, then **merge it into `main` as the last step** (merge `main` in first if
  it moved, and rerun the checks). Commit messages end with the session's attribution lines; no model ids in commits.
- Do the whole job: code, tests, README, a `CODEMAP.md` line for new files, then commit, push and merge. End with a
  short report that names the protocol version and what's next.
- The **very last line** of every session's final message is `Branch used: <branch>`, e.g. `Branch used: main` or
  `Branch used: claude/xyz (not merged to main)`, so the owner knows whether to ask for a merge.
- **Bump `Session.ProtocolVersion`** (`Core/Session.cs`) whenever a network message or snapshot format changes. New
  enum values (`Msg`, `Kit`, `WeaponId`, `AbilityId`, `EnemyType`, ...) go at the **end**, before `None`.
- Ask with a question box before deleting code.
- **Never update the reference pages unless the owner asks in that prompt**: the Bestiary (`Docs/Bestiary/`), the Armory
  (`Docs/Armory/`, its icons and `build.sh`), `BestiaryData.Generated.cs`, and the published artifacts. Code changes
  don't carry them along; at most, say in the report that they are now out of date. When asked to edit a Bestiary
  entry, run `node Docs/Bestiary/build_data.js` after; never edit `*.Generated.cs` by hand.
- To-do list: add an entry for anything built that needs a try in Unity ("check in Unity"). Once the owner has
  checked it, move the entry to the top of `Docs/History.md` rather than leaving it ticked.

## How the owner works

- Requests are short and outcome-focused ("make the lance a rectangle", "do 1, 2"). Implement them fully.
- They play-test in Unity and send **screenshots** and **session JSON uploads** (`Tools/SessionReport/report.py`
  summarises them). Analyses give concrete numbers and a short list of proposed fixes, then ask which to do.
- They want things to look **polished** ("make it really nice"), with live previews over names.
- For visual choices, offer **mockups to choose from** (`Docs/Mockups/`, published as artifacts).
- For big features, **plan first** in a design doc with their decisions (e.g. `Docs/Paths.md`), then build one step
  at a time when they say "start on step N": code, tests, previews, README and doc status, push, short report.

## Checking changes

Mono may be missing: `apt-get update && apt-get install -y mono-mcs mono-runtime libmono-system-core4.0-cil`.

```bash
Tools/CompileCheck/check.sh            # fast compile with Unity stubs
Tools/SimTest/run.sh                   # core suite (defaults; `run.sh 1 1 core` is too short)
Tools/SimTest/run.sh 1 30 features     # feature suite; run core + features before committing
Tools/SimTest/run.sh list              # every suite (single systems: parry, knight, bosses, warden, ...)
Tools/RenderCheck/check.sh [git-ref]   # IDENTICAL = drawing unchanged (use for render refactors)
Tools/Preview/shot.sh <name> <mode>    # PNG screenshot without Unity, to Tools/Preview/out/
Tools/Preview/uishots.sh [-r git-ref]  # every UI screen + contact sheet; -r: what changed since that commit
Tools/WaveGen/build.sh                 # after adding or changing Data/Waves/*.waves (builds them into the game)
```

If the game uses a Unity member the stubs lack, add it to `Tools/Preview/Stubs.cs` (the compile check, previews,
RenderCheck and SoundCheck all build against it).
