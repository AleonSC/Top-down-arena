# The Storm Herald's faction: the Squall

**Status: step 1 built** (protocol 333): Dust Devil, Squall Gunner, Stormcaller, squads Squall line and Storm cell. Code: `Core/Squall.cs`,
`Core/Approaches.cs` (the Storm Herald `FactionDef`), `Core/Defs.cs`, `Core/LateGame.cs` (`HazardCaller` in `TickHazards`), `Game/SquallLook.cs`;
mockup `Docs/Mockups/storm-herald-faction.html`; test suite `squall`; preview `SQUALL=0.4 ZOOM=1.7 Tools/Preview/shot.sh x hud 1`.

A storm front walking ahead of the Herald, the same size as the Warden's faction: eight enemies, four squads, a set piece and a ten-wave cycle.
Like the Infall, each has its own behaviour and previews one of the Herald's attacks. Until now the Herald got the basic faction.

| Enemy | Warden's slot | Previews | Step |
|---|---|---|---|
| Dust Devil | Rioter | the Gale's wind: its touch shoves you 120 px | 1 (built) |
| Squall Gunner | Guard | Crosswind: three shots aimed upwind that curve back onto you | 1 (built) |
| Stormcaller | Gas Grenadier | Strikes: an amber ring under you, then lightning | 1 (built) |
| Grounder | Riot Shield | Thunderclap: its copper shield grounds your shots; full, it lets go a ring with two gaps | 2 |
| Pylon pair | Security Guard | Lightning rods: two posts, an arc that flickers 0.6 s then fires along the line | 2 |
| Lineman | Taser Guard | the Conductor: marks a player; the charge jumps to allies in the ring | 2 |
| Thunderhead | Thug | Downburst: a slow cloud that drops a row of strikes ahead of it | 3 |
| Ball Lightning | Shiv | Ball lightning: the Herald's orb, fixed to work without it | 3 |

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
2. **Grounder, Pylon pair, Lineman**: squads **Grounding wall** (two Grounders, three Stormcallers) and **Power line** (two Pylon pairs).
   The Grounder's charge and the Lineman's mark go in `Enemy.Phase`/`PhaseT`; Pylons pair like Bound Wraiths (`Enemy.LinkId`) and their arc
   reuses the Herald's (`HeraldArc` look).
3. **Thunderhead, Ball Lightning**: squad **Supercell** (Thunderhead, Lineman, three Dust Devils); the Ball Lightning fixes.
4. **The Storm front** set piece (six Stormcallers and two Grounders; a wall of strikes sweeps across with a two-strike gap that drifts) and
   the ten lessons in the Warden's order (`FactionDef.Lessons`), as in the mockup's table.
