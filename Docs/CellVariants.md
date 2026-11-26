# Cell variants and pits

Status: **all four steps built** (protocol 456, Nov 21, 2026; decided with the owner the same day). Variant chance 40% for a first look. Mockup: `Docs/Mockups/cell-pits.html`
(approximate HTML, not the real draw code). Builds on `CellLayouts.cs` (the 18 cell layouts) and Docs/GridPieces.md.

## Decisions

- **Cell variants are a system, not a one-off.** A cell layout can have variants: the plain one plus others (pits first;
  later ones such as a different floor or extra cover use the same slot). Each Combat lattice cell rolls its layout,
  then its variant, seeded from its place on the lattice like `CellLayouts.Plans`, so no wire data.
- **Pits**: voids in the floor. Look **B, Hazard rim** (marching gold/black stripes, a grid falling away inside).
- **All 8 pit variants** go in: Sinkhole (Open Plaza), Four Wells (Four Pillars), Island Vault (Vault), The Chasm
  (Side Alcoves), Corner Drops (Corner Nooks), Crossfire Gap (Crossfire), Pit Halls (Hall & Alcoves), Stepping Stones
  (Pillar Field).
- **Enemy falls in**: dies, counts as the shover's kill, its coins pop onto the rim. Bosses, mini-bosses and flyers hover.
- **Player falls in**: **15% max HP, never lethal** (stops at 1 HP), respawns on the rim next to where they fell, 1 s shield.
- Falling needs your centre over the void. A dash that lands on solid ground crosses; bullets fly over; steering and
  bots treat pits as walls.

## Changes from the mockup

- The chest always stands in the cell's middle, so every variant keeps it clear and the door lanes open: Sinkhole became
  **Sinkholes** (two round pits off the middle), **The Chasm** two chasms where the bars were, **Corner Drops** a pit in each
  corner, **Crossfire Gap** two pits off the S-lane. Slots were moved off the pits.
- A player drops in: held down in the pit 0.5 s (body hidden, controls held, your own game only: no wire change), then climbs out on the rim. (It was instant at first, which read as bumping the lip.)

## Steps (all built)

1. **Variants in data** (`CellVariants.cs`): variants by the plain layout's name, `CellPlan.Variant`, the seeded roll for
   plain Combat cells, pits (boxes or discs) turned with the cell, `World.Pits`.
2. **Falling** (`Pits.cs`): enemies shoved in die (kill credit, coins on the rim), walking never goes in, steering and bots
   go round; players climb out, 15% max HP never below 1, 1 s shield, `Msg.PitFall` (protocol 456).
3. **Look** (`Game/PitLook.cs`): the Hazard rim, the fall effect, minimap marks; preview `PITCELL=<name> [PITFALL=<s>]`.
4. **Tune**: `VariantChance` 40%; README. Next ideas: more variant kinds (floors, extra cover), a fall sound.
