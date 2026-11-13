# The Singularity's faction: the Infall

**Status: steps 1-2 built** (protocol 319): Debris, Acolyte, Pulsar; Graviton, Orbiter, Collapsar. Code: `Core/Infall.cs`, `Core/Approaches.cs` (the Singularity `FactionDef`),
`Core/Defs.cs`, `Core/Variety.cs` (`CapBeams`: at most 3 Pulsars a wave), `Game/InfallLook.cs`; mockup `Docs/Mockups/singularity-faction.html`;
test suite `infall`; previews `INFALL=0.4 ZOOM=1.7 Tools/Preview/shot.sh x hud 1` (step 1) and `INFALL2=0.4 ZOOM=1.7 ...` (step 2).

Matter falling into the black hole, the same size as the Warden's faction: eight enemies, four squads, a set piece and a ten-wave cycle. Unlike the
Hive's bees, these have their own behaviour: each one previews one thing the Singularity tests.

| Enemy | Warden's slot | Previews | Step |
|---|---|---|---|
| Debris | Rioter | the Pull: an inward spiral | 1 (built) |
| Acolyte | Guard | bent shots: three orbs that meet on you | 1 (built) |
| Pulsar | Security Guard | Lasers: two beams that flicker, then sweep a quarter turn | 1 (built) |
| Graviton | Gas Grenadier | Pull and Gravity Wells: lobs a small well that drags you and swallows shots | 2 (built) |
| Orbiter | Taser Guard | the disk and Slingshot: three orbs round it, slung one at a time | 2 (built) |
| Collapsar | Thug | Collapse and Hawking: a slow heavy star that implodes into a ring when killed | 2 (built) |
| Lens | Riot Shield | Gravity Wells: bends your shots round its face, so flank it | 3 |
| White Dwarf | Shiv | White hole: flashes, shoves you and turns your shots round | 3 |

## Steps

1. **Debris, Acolyte, Pulsar** (built): the faction with these three, squads **Infall** (an Acolyte and six Debris) and **Binary** (two Pulsars).
   No set piece or lesson cycle yet, so the Campaign builds its waves by reach, as for the Lich.
2. **Graviton, Orbiter, Collapsar** (built): the Graviton's Small Well (`EnemyType.MiniWell`) uses the boss's corner-well rules, closer and weaker
   (`WellDriftAt`, `PullIntoWells`); the Orbiter slings its orbs; a killed Collapsar leaves an `Implosion` (no XP, so shots pass through it) that
   pulls, then bursts. Squad **Gravity trap** (Graviton, Collapsar, three Debris).
3. **Lens, White Dwarf**: the two new rules, shots bending round a body's face and an enemy turning bullets round (the boss's White hole has one).
   Squad **Lens array** (two Lenses, three Acolytes).
4. **The set piece and the cycle**: the **Accretion ring** (eight Debris and four Orbiters in a ring round the players, turning and closing, two gaps)
   on the last wave, and the ten lessons in the mockup's order.

## Numbers (step 1)

- Debris: 22 health, 125 px/s, radius 10, contact 9. Up to 1.05 rad off your line when 420 px away, straight at you within 90 px; it keeps its side.
- Acolyte: 45 health, 85 px/s, keeps 280 px. Every 2.4 s, three small shots at 150 px/s: one straight, two 0.5 rad out that curve to meet it
  where you stood (they vanish a little after, so they don't loop round the arena). Its tiny black hole swells while it winds up.
- Pulsar: 70 health, 75 px/s, keeps 300 px. Plants within 390 px; 1 s flicker starting 0.5 rad behind you; a 90° sweep over 4 s (16 a touch, the
  Beam Sentry's damage); a 2.5 s rest. A Zoner. At most 3 a wave (spare ones become Acolytes).

## Numbers (step 2)

- Graviton: 50 health, 80 px/s, keeps 320 px, a Zoner. Every 6 s a 0.6 s wind-up (it stands still), then a Small Well lands within 50 px of you
  0.8 s later. Small Well: 30 health, lasts 4 s, drags players 80 px/s at its middle falling to 0 at 240 px, bends shots within 150 px. One a Graviton.
- Orbiter: 60 health, 95 px/s, circles 260 px out. Every 1.8 s, a 0.55 s flare, then a fast orb (330 px/s, 17).
- Collapsar: 220 health, 58 px/s, radius 22, contact 20. Its Implosion pulls 100 px/s at the middle (0 at 260 px) for 0.8 s, then a ring of 12
  small shots at 150 px/s.
