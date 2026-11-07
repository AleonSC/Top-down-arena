# Top-Down Arena

A 1-3 player co-op top-down bullet hell in **Unity 6** (`6000.6.7f1`, Unity 6.6). The game logic is plain C# in
`Assets/Scripts/Core/` (namespace `Arena`, no Unity), and the Unity front end is in `Assets/Scripts/Game/`. There's
no Unity in the cloud container: check changes with the tools below.

## Where to look (read only what the task needs)

- `CODEMAP.md`: **start here**. One line per source file grouped by system, plus a "Where to look for..." table.
- `HANDOFF.md` §2: **code map**, one table row per source file, plus key mechanisms and "Adding a class".
  §3: every test and preview tool. §4: how the owner works. §6: audit notes and the work plan.
- `README.md`: player-facing reference for every weapon, ability, enemy, boss, mode and control. Use `grep -n '^## '
  README.md` to jump to a section.
- `Docs/AfterReset.md`: **the owner's to-do list**. Check it at the start of a session.
- `Docs/History.md`: the old changelog (newest first). Read it only for background; `git log` usually does.
- `Docs/*.md`: design docs (Paths, Approaches, BossHuntsAndPerks, ZombieInfection, ...). `Docs/README.md` covers
  the Bestiary and Armory pages.
- Big generated or reference files are in `.ignore` (mockups, Armory/Bestiary HTML, `*.Generated.cs`), so a
  repo-wide search skips them. Search them by naming the path.

## Rules

- Work on and push to **`main`** (`git push -u origin main`). Commit messages end with the session's attribution
  lines; no model ids in commits.
- Do the whole job: code, tests, README, then commit and push. End with a short report
  that names the protocol version and what's next.
- **Bump `Session.ProtocolVersion`** (`Core/Session.cs`) whenever a network message or snapshot format changes. New
  enum values (`Msg`, `Kit`, `WeaponId`, `AbilityId`, `EnemyType`, ...) go at the **end**, before `None`.
- Ask with a question box before deleting code. For visual choices, offer mockups (`Docs/Mockups/`). For big
  features, plan first in a design doc.
- **Never update the reference pages unless the owner asks in that prompt**: the Bestiary (`Docs/Bestiary/`), the Armory
  (`Docs/Armory/`, its icons and `build.sh`), `BestiaryData.Generated.cs`, and the published artifacts. Code changes
  don't carry them along; at most, say in the report that they are now out of date. When asked to edit a Bestiary
  entry, run `node Docs/Bestiary/build_data.js` after; never edit `*.Generated.cs` by hand.

## Checking changes

Mono may be missing: `apt-get install -y mono-mcs mono-runtime libmono-system-core4.0-cil`.

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

If the game uses a Unity member the stubs lack, add it to both `Tools/CompileCheck/UnityStubs.cs` and
`Tools/Preview/Stubs.cs`.
