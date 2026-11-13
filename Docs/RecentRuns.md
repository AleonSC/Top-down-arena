# Recent runs (Records tab with graphs)

Status: **phase 1 built (Nov 2026, protocol unchanged, 167)**; phases 2 to 4 are still plans. Mockup: [`Mockups/recent-runs.html`](Mockups/recent-runs.html)
(https://claude.ai/artifact/UE1ttduAmFgTuTaE7ZLeGr). Protocol version when written: 167.

A fifth Records row, **Recent runs**: a strip of your last runs, and for the one you pick, stat tiles and graphs of
what happened during it (damage over time, what hurt you, health, accuracy, team comparison, trends across runs).

> Nov 2026: a **Boss Hunt** now keeps its curve too (`RunTimeline.cs` samples it, `RunSaves.cs` fills the log line with class, `Mode = "Hunt"`, length and the file), because the results screen graphs the run it just ended (`HudSummary.cs`, `SummaryTimeline`). A hunt you quit is still not logged. Practice isn't graphed.

> Nov 2026: **Quick Play** is fully tracked: a Quick Play you quit is logged as a quit Quick Play with its curve (`LogQuitRun`), and an endless stretch after the win gets its own card and curve too, whether the party falls (`QuickRecord`) or you quit (`LogQuitRun`; mode "Quick Play · Endless").

## What exists today

| Piece | Where | Limit for this feature |
|---|---|---|
| `Profile.Recent` (`RankLog`: wave, result, XP, multiplier, mods), 6 kept, saved as `rlog` lines | `Core/Profile.cs` | No class, mode, time, damage or curves. 6 is few for a trend. |
| `RunStats` per player: damage dealt / taken, healing, biggest hit, kills (all / elite / boss), downs, revives, shots fired / hit, abilities used | `Core/RunStats.cs` | Only end-of-run totals, no time axis, no breakdown. |
| `Msg.RunStatsLive`: the host sends every player's tallies about once a second, but **only with 2+ players** | `RunStats.cs` (`TickLiveStats`) | Solo has the numbers locally; party clients get everyone's. |
| `Player.Hp` is in every snapshot | | Health over time can be sampled by any client. |
| The run's end is recorded in `RunSaves.cs` (`RecordKind.End`), gated on `me != null`, not dev, no bot, once (`runRecorded`) | `Core/RunSaves.cs` | This is the place to finish and keep a run's history. |
| `.replay` and the session JSON recorder | `Replay.cs`, `SessionRecorder.cs` | Too heavy and developer-only; not used here. |

## Design

### Idea: sample cumulative tallies, graph the differences

Every 10 s of play (pauses excluded, like `RunTime`) each game appends one **sample** to a `RunTimeline`: the
cumulative `RunStats` of each player plus each player's current HP%. A rate chart is the difference between two
samples; "damage per second" is `ΔDamageDealt / 10`. This needs no new network message, because every game already
has every player's tallies (live message in a party, local in solo) and every player's HP (snapshot).

Samples, per player: `DamageDealt, DamageTaken, HealingDone, Kills, Downs, Revives, ShotsFired, ShotsHit,
AbilitiesUsed, Hp%`. Per sample: time, wave number, boss alive (bool).

### Two tiers of data

**Tier 1 (no protocol change): everything derivable from the tallies we already send.**
Damage per second, healing per second, damage taken per second, health over time with downs, accuracy per wave,
kills over time, abilities used per minute, team comparison, "run shape" radar, trend across runs, wave bands,
boss marker.

**Tier 2 (protocol bump): breakdowns we do not track yet.** Each adds a counter to `RunStats` and a field in
`RunStatsMessage` / `ReadRunStats` (and so `Session.ProtocolVersion` 167 to 168):
- Damage dealt **by source** (weapon, abilities, minions, passives, other): a `float[5]` credited where damage is
  applied (`Combat.cs`). Needed for the stacked chart.
- Kills **by enemy type**: counts per `EnemyType` (`Combat.cs` near the `killer.Run.Kills++` line). Needed for
  "kills by enemy".
- Damage taken **from** (enemy bullets, touch, boss attacks, zones, other): same idea at `Combat.cs:60`.
- **Ability use times**: a list of (time, ability id) per player. Host-only until sent; adds a message size per run.
- **Hit positions** for the heat map: a 12 x 8 grid of damage taken, so 96 floats per player, sent once at the end
  only (not live).

Tier 2 can go in one bump. Each piece ships only if the owner wants that chart.

### Storage

- Keep the profile small. A finished run's history goes in its **own file** beside the profile
  (`runs/<unix time>.run`, text lines like the saves, using `RunSave.Line`), and `rlog` gets a `file` field so each
  strip card finds its graphs. A run with no file (older, or deleted) shows its tiles and "no graph data".
- Size budget: 20 min at 10 s is 120 samples; 3 players x 10 channels is about 4 k numbers, around 25 KB as text.
  Gzip is not needed. Keep the last **20** runs (new `Profile.RecentKept`, was 6) and delete the oldest file when a
  new one is written.
- Only **your** profile is written, the same as `RankLog` today: the file holds the whole party's samples as
  seen by you.
- Not saved for: dev runs, bot-touched runs, hunts (maybe later, separate tab), practice, hangout. These follow the
  same gate as `RecordKind.End` in `RunSaves.cs`.
- Format version on the first line (`runlog 1`) so Tier 2 fields can be added without breaking older files.

### Core / front-end split

- `Core/RunTimeline.cs` (new, no Unity): `RunTimeline` (the samples; `Add`, `Rates(channel)`, text save / load) and
  `RunTimelineStore` (folder, list, trim). Unit-testable with `Tools/SimTest`.
- Sampler: `Game.TickRunTimeline(dt)` called from `FixedStep` next to `TickLiveStats`, same gate as the End record.
  Finished and handed to the profile in `RecordKind.End`.
- `Core/Profile.cs`: `RankLog` gets class, mode name, duration, damage totals and the file name; `rlog` line grows
  extra fields at the end (old lines still load).
- `Game/RunGraphs.cs` (new): the chart drawing, IMGUI. Line / area, stacked area, bars, donut, heat map, radar,
  timeline strip. Built on a small `Plot` helper (axes, ticks, wave bands, hover readout). IMGUI draws no
  anti-aliased lines, so lines are drawn as thin quads and polylines are simplified (about 120 points per series).
- `Game/MenuDrawer.cs` `DrawRecordsSection`: row 5 "Recent runs" (`recordsSection` 0 to 4 are taken, the Rank page
  is already section 5 in `ProfileUi.cs`, so it takes the next free number).

### UI (from the mockup)

- Strip: one card per run (name, class, wave, length, result, a damage sparkline). Click selects; arrow keys and
  gamepad shoulder buttons step through.
- Tiles: result, time, wave, damage, taken, healing, downs, XP against your average.
- Graph tabs: Overview, Damage, Survival, Team, Trends. Hover readout on every time chart; a click on the
  damage chart jumps the other charts' guide to the same moment.
- Party runs: a player filter (All, or each member) on the time charts. Teammates' colours are their party colours.
- Empty and old states: "Play a run to see it here" and "No graph data for this run".
- Colours stay readable with the CRT / reduced-effects settings; no glow needed.

## Phases

1. **Timeline + Overview** (no protocol change). `RunTimeline`, sampler, per-run file, `RankLog` fields, the
   page with strip, tiles and the Overview tab (damage per second, health over time, accuracy per wave) and
   Trends. Tests: sampler differences, save / load round trip, trimming to 20, old `rlog` lines still load.
2. **Tier 2 breakdowns** (protocol 168). Damage by source, kills by enemy, damage taken from, ability timeline.
   Tests: tallies add up to the totals (sum of sources equals `DamageDealt`).
3. **Heat map and team tab.** Hit grid, per-player filter, radar.
4. **Polish.** Keyboard / gamepad stepping, run delete, hunts and challenge rooms if wanted.

Each phase ships on its own (code, tests, README, commit, push). Phase 1 is the one that makes the tab real.

## Risks

- **Totals vs. samples:** the live message only arrives with 2+ players, so a solo game samples the host's own
  `RunStats` directly. Both paths must give the same series; a test replays a run both ways.
- **Pause and host migration:** samples follow `RunTime`, which excludes pauses. A client who joins mid-run starts
  its timeline at the join (earlier part is flat zeros, drawn as "joined here").
- **Accuracy of "damage by source" credit:** damage applied through minions, zones and reflected hits is credited in
  many places in `Combat.cs`; the first cut will have a large "Other" until each source is routed.
- **IMGUI cost:** about 10 charts of 120 points each drawn per frame is fine, but the stacked area and heat map
  should be cached to a texture when the selected run does not change.
- **Reference pages:** nothing here touches the Bestiary or Armory.

## Questions for the owner

1. Keep how many runs: 20 (about 0.5 MB at most), or more?
2. Do hunts and challenge rooms get graphs too, or only normal runs at first?
3. Show teammates' graphs (everyone's samples in the file), or only your own?
4. Which charts matter most? The proposal for phase 1 is damage per second, health over time, accuracy per wave
   and the trend; everything in Tier 2 waits on your answer.
5. Is the extra damage-source accounting in `Combat.cs` worth a protocol bump now, or only after phase 1 is tried?

## Phase 1 as built

- `Core/RunTimeline.cs`, `Game/RunGraphs.cs`; test suite `Tools/SimTest/run.sh 1 30 recentruns`
  (`Tests/RecentRuns.cs`); preview `SECTION=4 [RTAB=1] [RSEL=n] [MOUSE=x,y] Tools/Preview/shot.sh x records`.
- The page: run strip (four cards, arrows for the rest), six tiles, **Overview** (damage per second with team line,
  health per player, accuracy per wave) and **Trends** (wave reached and damage dealt by run, click to open one).
- Kept 20 runs (`Profile.RecentKept`, was 6); the Rank page still lists the newest 6 (`Profile.RankPageRuns`).
- `rlog` lines grew five-plus fields at the end (class, mode, seconds, dealt, taken, healing, players, file); older
  lines load with blanks, and older builds ignore the extra fields.
- Left out for now (decided without waiting for the answers above, easy to change): hunts and challenge rooms have no
  graphs; a client's and a host's profile each keep the whole party's lines; no player filter on the charts; "kept"
  means 20 runs, oldest files deleted. Not tried in Unity: line thickness, the hover tooltip, the strip's arrow
  buttons, and how it looks with the window-open animation. The preview does not draw panel fills, so the real
  panel background is missing from the screenshots.

## Runs you quit

A run you leave before it ends (pause menu > quit, back to menu, closing the game) is now logged too: `Game.Stop` calls
`LogQuitRun` (`Core/RunSaves.cs`), which adds a `RankLog` with `Result = 4`, no XP, and keeps the curve like a normal
run. The strip and tiles say "QUIT" / "Quit"; the Rank page lists it as "Wave N · quit" with +0 XP. Same gate as the
End record (not dev, bot, hunt, practice), and only if a curve had started. No protocol change. A quit run that you
resume from its save later logs again when that run ends or is quit, so it can appear twice.
