# Surprise rooms: free, well-shaped rooms the map gets after a boss or a breach event

Status: **steps 1-2 built** (shapes and placement; the boss trigger, `Surprise` flag on the wire (protocol 282), gold look and chip). The breach event (step 3) is not built. Decided with the owner on Nov 11, 2026. Builds on [`VectorBuilding.md`](VectorBuilding.md)
and [`RoomBuilding2.md`](RoomBuilding2.md) (breaches). Expanding arena only.

## Decisions (owner)

| Question | Decision |
|---|---|
| When | **After every boss**, and after a **breach event** (new: see below). |
| Cost / vote | **Free, no vote.** A gift: no vectors, no purse, never replaces the team's own drawn room for the round. |
| Shapes | Ring (donut), Cross (plus), L and T halls, Chamfered box (cut corners). |
| Placement | **Closes a loop**: prefer spots touching 2+ rooms, else the deadest end. It repairs the map's shape. |

## Why

Bots die at wave 14-21 in Expanding because of map shape (long hallways, dead ends). Drawn boxes are a size choice, not a
tactical one. A surprise room is a fixed, tested shape that adds a tactical feature (a lap to kite round, a crossing, corners
that break line of sight) exactly where the map is weakest.

## The shapes

All tile-aligned (100 px grid), every passage at least 3 tiles (300 px) wide, so each is a legal `Expansion.Outline`. Sizes are
the footprint at the base scale (one size each for now).

| Shape | Footprint (tiles of 100 px) | Why it plays well |
|---|---|---|
| **Ring** | 11 x 11, a 5 x 5 block in the middle, a 3 tile lane all round (96 floor tiles, the area cap) | A loop to run round; enemies split or chase in circles. |
| **Cross** | arms 3 wide, 15 across, a 3 x 3 crossing (81) | Four sightlines, one fight point, an exit at the end of each arm. |
| **L hall** | a 12 tile leg and a 9 tile leg, 3 wide (54) | Corner breaks line of sight; ranged enemies must walk into melee. |
| **T hall** | a 15 tile bar with a 9 tile stem down the middle, 3 wide (63) | Two exits, one choke. |
| **Chamfer** | 9 x 7 with the four corners notched 2 x 2 (47) | Rounded corners: no dead corner to be pinned in. |

Each is tried in all 8 turns and mirrors (duplicates dropped). `Expansion.SurpriseBase`/`SurpriseMask` give the floor as a tile mask.

All five go through the same code as a drawn outline (`FromTiles`: cut into boxes, `Leg = PieceBase + n`, joined open), so the ring's
hole is simply unbuilt floor. A ring is 4 boxes; where two of its boxes meet away from their parent seam they get an ordinary door
(not open full width), so the lane has a door at the loop's closing point.

## Placement ("close a loop")

`Expansion.SurpriseOptions(built, shape)` lists every spot best first and `Expansion.Surprise(seed, wave, built, shape, out why)` picks one. It tries every anchor (each outer wall of each room, sliding in `Step`s, every
rotation and mirror of the shape) and scores it with `Placement` (so it obeys the normal rules: no overlap, 300 px of shared
wall per door, `MaxRooms`):

- **+3 per extra loop** it closes (`loops` out of `Placement`).
- **+2** if it touches a dead-end room (one door).
- **-1 per tile** the footprint pushes the bounding box out, so the map stays compact.
- Ties and the choice among the top 3 use `DetRandom(seed * 7919 + wave * 104729 + built.Count + 13)`: the same on every
  machine, **no wire change for the choice**.

If nothing scores (the map is full or no spot fits), a smaller shape is tried, then nothing: no gift, no error.

## Triggers

1. **After a boss** (right after the boss vote resolves): one surprise room, always. The shape is the one not yet built
   (rotating), else seeded.
2. **Breach event** (new, rare): roughly one regular wave in 8 from wave 10, never two in a row, never the wave after a boss.
   A breach flashes gold instead of red; after 20 s (or at the wave's end, whichever is first) the cracked wall opens onto the
   surprise room, which was already placed behind it. The same room goes in either way; the event only adds the reveal and
   one extra spawn from that breach for the wave.
3. Neither fires if the map is within 2 rooms of `MaxRooms`.

## How it is built and shown

- **Host** picks the spot and shape (deterministic), adds the pieces to `Expanded` as `RoomKind.Combat`, `Price = 0`, and sends
  them with the normal `Msg.ExpandState` / `ExpandApply` (a new `Surprise` flag on `ExpansionRoom` so the look can tag them).
- **Look**: a gold scan-line build-in over 1.5 s (reusing the breach barrier art), a centre banner "SURPRISE ROOM: Ring",
  the room outlined gold on the minimap for the rest of the wave. Preview-renderable via `Tools/Preview` (`EXPANDMAP=6`
  plus a new `EXPANDSURPRISE=ring|cross|l|t|chamfer`).
- **Cover**: `AddCover` is skipped for ring and cross (their geometry is the point); chamfer and halls get it as usual.
- **Vectors**: unchanged. Surprise rooms don't count against a player's proposals.

## Protocol

One bump: the `Surprise` flag in the room list (snapshot/`ExpandState`) and the event state for the gold breach reveal.

## Step 2 (built, protocol 282)

- `GiveSurpriseRoom` (`ExpansionGame.cs`) runs when a boss is cleared (`Waves.cs`, where the Classic arena offers paths), before the build round opens. The shape
  rotates: a seeded start (`expandSeed % 5`) plus the surprise rooms already built. Skipped in Practice and Boss Hunts, after the last wave, and when the map is within
  2 rooms of `MaxRooms`; no spot just logs "no surprise room".
- `ExpansionRoom.Surprise` (1 + `SurpriseShape`, on every box, 0: none) goes over the wire with the rooms (`WriteRooms`, so `Msg.ExpandState` and proposals) and into saves
  (`room` line, an optional 12th number); `RoomArea.Surprise` carries it into the built world.
- Clients notice a new surprise room in `ReadExpandState` (the rooms grew while it is the intermission after a boss) and start the same clock the host does:
  `SurpriseAge`, `SurpriseWave`, `SurpriseKind` (`SurpriseFresh`: gold until the next wave starts). A joiner mid-run gets the rooms without the chip.
- Look (`Game/SurpriseLook.cs`, `RoomsLook.cs`): a gold flash and a scan line down each box over 1.5 s, a gold rim round the whole room (fainter once the wave is over),
  gold on the minimap while fresh, and the chip (SURPRISE ROOM, the shape's name, FREE) for 4.5 s. Cover is not added: every box is a 3-wide lane, so none qualified already.
- Preview: `EXPANDMAP=3 EXPANDSURPRISE=ring|cross|l|t|chamfer [SURPRISEAGE=<s>] ZOOM=0.4 Tools/Preview/shot.sh x hud`. Tests: `ExpandingSurpriseTest`, `ExpandingSurpriseGameTest`.

## Build steps

1. (built) Shapes + `Surprise` + tests (each shape legal, 3-tile minimum, deterministic, never overlaps, closes loops when it can).
2. (built) Boss trigger + sync + gold look + banner; previews for all five shapes.
3. Breach event trigger and reveal; balance pass with `vecbal` bots (does it lift the wave where bots die?).

## Open questions (defaults in bold)

- Sizes: **one size per shape**, or S and L picked by how many rooms exist?
- Should a surprise room be able to carry a **path floor** (Burning Grounds, ...) sometimes? Default **no**, plain combat.
- Breach event frequency: **~1 in 8 waves** from wave 10; tune after the balance pass.
