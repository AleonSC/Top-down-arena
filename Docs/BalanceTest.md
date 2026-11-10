# Balance test (F1 > Balance)

Status: built, Nov 2026, no protocol change (host only). Check in Unity (see `AfterReset.md`).
Mockup: [`Mockups/balance-test-run.html`](Mockups/balance-test-run.html).

## What it does
From a normal run you host, F1 > Balance > **Launch test** ends the current recording, starts a new one marked as a test
(`SessionRecorder.MarkTest`), turns the bot on if chosen, resets the DPS meter and jumps to the start wave
(`Game.DevStartBalanceTest`, `Core/BalanceTest.cs`). When wave `start + 4` clears (`TestWaveCleared`, called from
`WaveCleared`), or the run is won or lost, the recording closes and `SessionRecorder.BuildReport` makes the report shown by
`Game/BalanceTestUi.cs`. Files go to `<persistentDataPath>/balance`.

## What is logged (session format 4, on top of format 3)
- `test` block: kind `balance-test`, first wave, waves, bot, sample interval.
- Per player and wave: `damageBySource` and `hitsBySource` (keys: `Weapon: <name>`, `Ability: <name>` for the cast's own hit,
  `Burn, bleed and poison`, `Ability and summon shots`, `Other (zones, minions, delayed hits)`), `shotsFired`, `shotsHit`,
  `castInstantHits`.
- Per enemy type: `avgMaxHp` (scaled health at spawn), plus the existing spawned, killed, lifetime and damage to players.
- Samples every second when "sample every second" is on (health, enemies, bullets, nearest enemy).

## Known limits
- Zones, minions, turrets and delayed ability hits are not tied to a cast and land in "Other". Reading that row as the
  lasting abilities' share is the best the log can do without tagging every damage call.
- The test starts from the current level and upgrades; start at wave 1 of a fresh run for a clean build.

## Ideas
- Difficulty and speed choices on the launch card (the mockup shows them).
- Tag zone and minion damage with the ability that made them.
- Compare two test files side by side in `Tools/SessionReport`.
