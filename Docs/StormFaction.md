# The Storm Herald's faction: the Squall

**Status: steps 1-3 built** (protocol 339): all eight enemies and five squads (Squall line, Storm cell, Power line, Supercell, Grounding
wall). Step 4 (the Storm front set piece and the ten lessons) is next. Code: `Core/Squall.cs`,
`Core/Approaches.cs` (the Storm Herald `FactionDef`), `Core/Defs.cs`, `Core/LateGame.cs` (`HazardCaller` in `TickHazards`), `Game/SquallLook.cs`;
mockup `Docs/Mockups/storm-herald-faction.html`; test suite `squall`; previews `SQUALL=0.4 ZOOM=1.7 Tools/Preview/shot.sh x hud 1` (step 1), `SQUALL2=0.4 ZOOM=1.7 ...` (step 2), `SQUALL3=0.4 ZOOM=1.7 ...` (step 3).

A storm front walking ahead of the Herald, the same size as the Warden's faction: eight enemies, four squads, a set piece and a ten-wave cycle.
Like the Infall, each has its own behaviour and previews one of the Herald's attacks. Until now the Herald got the basic faction.

| Enemy | Warden's slot | Previews | Step |
|---|---|---|---|
| Dust Devil | Rioter | the Gale's wind: its touch shoves you 120 px | 1 (built) |
| Squall Gunner | Guard | Crosswind: three shots aimed upwind that curve back onto you | 1 (built) |
| Stormcaller | Gas Grenadier | Strikes: an amber ring under you, then lightning | 1 (built) |
| Grounder | Riot Shield | Thunderclap: its copper shield grounds your shots; full, it lets go a ring with two gaps | 2 (built) |
| Pylon pair | Security Guard | Lightning rods: two posts, an arc that flickers 0.6 s then fires along the line | 2 (built) |
| Lineman | Taser Guard | the Conductor: marks a player; the charge jumps to allies in the ring | 2 (built) |
| Thunderhead | Thug | Downburst: a slow cloud that drops a row of strikes ahead of it | 3 (built) |
| Ball Lightning | Shiv | Ball lightning: the Herald's orb, fixed to work without it | 3 (built) |

## Decisions

- Names: "Lightning Rod" is already an ability, so the posts are **Pylons**. The faction is **the Squall**.
- No arena-wide wind outside the fight: the Dust Devil's shove and the Squall Gunner's own curve stand in for the Gale.
- The Stormcaller's strike is the Herald's `Hazard` (synced and drawn already) with a new kind, `HazardCaller`: smaller (40 px), 20 damage,
  drawn amber, named "Stormcaller (lightning)". Bots already dodge hazards.
- Ball Lightning reuses `EnemyType.BallLightning` with the fixes `Docs/Approaches.md` lists (works with no Herald, slows and freezes skip it,
  its pop shots count as boss shots), rather than a second type.
- Numbers are first guesses at wave 36, growing like any enemy's.

## Steps

1. **Dust Devil, Squall Gunner, Stormcaller** (built): the faction with these three and squads **Squall line** (a Gunner and six Dust Devils)
   and **Storm cell** (two Stormcallers, three Dust Devils). No set piece or lesson cycle yet, so the Campaign builds its waves by reach.
   - Dust Devil: weaves across your line (`DevilSway`, dying away up close); a touch does 8 and `KnockPlayer` 120 px, then it veers off 0.7 s
     (`Enemy.Phase` 1).
   - Squall Gunner: keeps 260 px; every 2.2 s, three shots (220 px/s, 0.06 rad apart) leave 0.45 rad upwind of you and turn (`Bullet.AngVel`,
     the Acolyte's chord formula) to cross where you stood. Its wind's side is `Enemy.Strafe`, which never flips for it.
   - Stormcaller: keeps 300 px; every 2.8 s, a 40 px strike on you with a 1.1 s warning (20).
2. **Grounder, Pylon pair, Lineman** (built): squads **Power line** (two Pylon pairs, from wave 2) and **Grounding wall** (two Grounders, three
   Stormcallers, from wave 3).
   - Grounder: turns 1.4 rad/s (`GrounderFace`); a shot within 1.15 rad of its front is blocked (`GrounderFaces` in `BlocksShot`) and charges it
     (`GroundShot` from the bullet loop, `Enemy.Phase` is the count, replicated). At 12: `Thunderclap`, 18 directions less two 2-shot gaps
     centred 70° either side of its front (14 shots, 170 px/s). It never shoots otherwise.
   - Pylon pair: `PairPylon` in `SpawnEnemyAt` (`Enemy.LinkId`, like a Bound Wraith); they walk to 150 px either side of you along the line
     between them (`PylonSpot`, so the spots hold still), plant (or after 5 s), then three times quiet 0.9 s, flicker 0.6 s, fire (18 to anyone
     within 7 px of the line, shown 0.35 s), then walk at least 1.6 s. The leader (lower Id) runs it, the partner copies its Phase. Lines over
     620 px don't fire. At most two pairs a wave (`CapBeams`; spare ones become Stormcallers). Bots keep off a line that's flickering or firing.
   - Lineman: its "shot" is the mark (`Enemy.LinkId` is the player's id, Phase 1, `LineMark` 2.6 s; the ring's countdown is timed from
     `SeenT`). Then `Conduct`: with allies within 120 px, everyone in the ring takes 16 and bursts 8 shots; alone, the marked player only bursts.
   - Decided here: the Grounder's gaps sit off to the sides (not in front), so the shooter in front has to step into one; the Lineman's mark is
     instant after its wind-up (no projectile to dodge), as the Herald's Conductor is.
3. **Thunderhead, Ball Lightning** (built): squad **Supercell** (a Thunderhead, a Lineman, three Dust Devils, from wave 3).
   - Thunderhead: 220 health, 55 px/s, heavy (no shoves), contact 20. Within 380 px, every 2.6 s (`TickThunderhead`) it drops three 30 px
     strikes 55 px apart starting 80 px ahead of it toward you, landing 1, 1.2 and 1.4 s later (Hazards of kind `HazardDownburst`, 20 each,
     drawn amber, landing in `CallerStrike`); Phase 1 while they're coming (it flashes).
   - Ball Lightning: still the Herald's `EnemyType.BallLightning` and its code in `TickHerald` (it already ran with no Herald). Fixed: slows
     and freezes hold it (a frozen one stays put, its timer still runs); one the Herald didn't make (`HeraldBall`) pops into its own shots
     (not boss shots) and its arcs are named "Ball Lightning (arc)"; it pays 9 XP (`BallWaveXp`, the Herald's stay at 2). At most four a
     wave (`CapBeams`; spare ones become Dust Devils). Job: Disruptor.
4. **The Storm front** set piece (six Stormcallers and two Grounders; a wall of strikes sweeps across with a two-strike gap that drifts) and
   the ten lessons in the Warden's order (`FactionDef.Lessons`), as in the mockup's table.
