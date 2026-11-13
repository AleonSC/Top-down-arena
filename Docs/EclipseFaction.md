# The Eclipse's faction: the Penumbral Court

**Status: step 1 built** (protocol 329): Ember, Corona Priest, Heliograph, squads Dawn and Mirror pair. Code: `Core/Penumbral.cs`, `Core/Approaches.cs`
(the Eclipse `FactionDef`), `Core/Defs.cs` (bullet kind `EmberSpark`), `Core/Variety.cs` (`CapBeams`: at most 3 Heliographs a wave), `Game/PenumbralLook.cs`;
mockup `Docs/Mockups/eclipse-faction.html`; test suite `penumbral`; preview `PENUMBRAL=0.4 Tools/Preview/shot.sh x hud 1`. Replaces the Echoes plan in `Docs/Approaches.md`
(Echoes stays as one squad).

Eight enemies, one per Warden slot, the same size as the Warden's and the Singularity's factions. Like the Infall, each has its own behaviour and
previews one thing the Eclipse's stages test, so its cycle (waves 91-99 of the Campaign) rehearses the final fight.

| Enemy | Warden's slot | Job | Previews | Step |
|---|---|---|---|---|
| Ember | Rioter | Hunter | Lunar wake: drops slow sparks that hang 1.5 s | 1 (built) |
| Corona Priest | Guard | Pressure | Corona rings: a ring of 13 with a gap beside you | 1 (built) |
| Heliograph | Security Guard | Zoner | Solar flares: 1 s line, then a 40 px lane of fire | 1 (built) |
| Moonward | Riot Shield | Anchor | Penumbra's moon: an orbiting moon soaks shots | 2 |
| Gnomon | Thug | Hunter | Eclipse flash: safe only in its shadow, in front of it | 2 |
| Dusk Lobber | Gas Grenadier | Zoner | Umbra: a pool of dark that hides shots and drains | 2 |
| Sickle | Taser Guard | Pressure | Crescents: a blade that goes out and comes back | 3 |
| Nova | Shiv | Disruptor | Totality: a 3 s fuse, then a nova; +50% damage taken while lit | 3 |

## Steps

1. **Ember, Corona Priest, Heliograph** (built): the faction (`Approaches.All`, boss `Eclipse`) with these three, squads **Dawn** (a Corona Priest and six
   Embers) and **Mirror pair** (two Heliographs). Ember sparks are a new bullet kind (`Defs.EmberSpark`, 65); the Heliograph's line is its Phase and Facing (no extra snapshot fields).
   The Campaign builds waves by reach until step 4.
2. **Moonward, Gnomon, Dusk Lobber**: a moon that blocks shots (`BlocksShot`, like the Lens's face), the Gnomon's shadow cone (players outside it
   take the flash), and the Dusk Lobber's pool (the Umbra's sight and drain, local). Squad **Night watch** (Dusk Lobber, Gnomon, three Embers).
3. **Sickle, Nova**: the Sickle's crescent (the boss's `Crescents` path, one blade); the Nova's fuse and burst. Squad **Moon court** (two Moonwards,
   two Sickles).
4. **The set piece and the cycle**: **Last Light** (eight Embers and four Heliographs on a square round the players that closes in like the boss's
   burning edge, flaring along its sides; it breaks where you kill them), squad **Echoes** (one member from each earlier faction), and nine lessons:
   Embers; Corona Priests; Heliographs; Moonwards; a recap with Gnomons; Dusk Lobbers; Sickles; Novas; the exam with Last Light.

## Numbers (step 1 as built; the rest from the mockup, to tune)

- Ember: 16 health, 135 px/s, contact 8. Weaves up to 0.55 rad off your line (straighter within 200 px); a spark every 0.2 s within 650 px
  (6 each, drifting back 25 px/s, 1.5 s).
- Corona Priest: 45 health, 85 px/s, keeps 260 px. Every 2.4 s, a 0.7 s halo wind-up, then 13 of 14 slots (130 px/s, 14 each): one at you, the
  slot beside it (its Strafe side) open.
- Heliograph: 70 health, 75 px/s, keeps 340 px; plants within 430. 1 s flickering line, then a 40 px lane of fire for 0.5 s (20), 2.5 s rest.
  At most 3 a wave (spare ones become Corona Priests).
- Moonward: 100 health, 70 px/s, melee. Moon 30 px out, one turn every 2.4 s, blocks every shot it touches.
- Gnomon: 240 health, 55 px/s, radius 22. Lantern swells 1.5 s, then a flash (30) to every player outside the slab's shadow (a cone in front).
- Dusk Lobber: 50 health, keeps 320 px. Every 6 s a pool lands by you: 90 px, 4 s, sight 40 px inside, drains 6/s.
- Sickle: 60 health, keeps 240 px. Every 2.4 s a crescent (16) out past you and back on a wider curve.
- Nova: 40 health, 230 px/s. Darts in, lights a 3 s fuse (+50% damage taken), then a nova 110 px (30) and dies.
