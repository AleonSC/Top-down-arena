# Wall push: grow a room from inside it

Status: **planned, nothing built.** Decided with the owner on Nov 14, 2026. Mockup: `Docs/Mockups/wall-push.html`
(playable: walk, pick a section, click to push; browser look, approximate). Builds on [`VectorBuilding.md`](VectorBuilding.md)
and [`RoomBuilding2.md`](RoomBuilding2.md).

## Why

- The build map is good for new rooms, but small changes (deepen a room, add a pocket, widen a corridor) mean opening
  the map and drawing a whole new room.
- Pushing is in the world: you stand where you build, see it at full size, and spend a few vectors at a time.
- It **adds to** the build map; it doesn't replace it. New rooms, openings, portals, gates, traps and room kits stay on the map.

## Decisions (owner, Nov 14)

| Question | Decision |
|---|---|
| Cost | **Floor gained**: 1 vector per new floor tile, so a push costs the section's width |
| Features | **All six**, one rolled per section (seeded): Fins, Baffles, Pincers, T-fins, Divider, Cover |
| When | **Between waves only**, like the build map |
| Which walls | **Any room wall**, the hub included |

Picked by Claude (change any of these):

- Free building only. With **Team vote** on, pushing is off (it doesn't fit proposals and votes).
- Floating rooms (shop, vendor, Gatekeeper) and surprise rooms can't be pushed until they're part of the map. The map's outer limit can't be crossed.
- A pushed section stops **1 tile off** other rooms. Joining another room to make a loop could come later.

## How it plays

1. Between waves, walk within 3 tiles of a wall: a key chip "**C** Push wall" shows on it (new rebindable action
   *Push wall*, default **C**; F is the Ultimate, G the chest item, B the gadget).
2. Press C to start pushing. The mouse picks a **section**: a straight run of plain wall (no door, gate or breach in it),
   **2-10 tiles** wide, snapped to the tile grid and centred on the cursor. The **mouse wheel** sets the width (camera zoom is off while pushing).
3. A ghost row shows where the next tile row goes, with a label: `PUSH +1 · 4 ◆` and `4 wide · 3 / 12 out · Baffles`.
   The vectors chip lights the slice the push would spend. It turns red and says why when you can't push:
   *Need 4 vectors*, *Too close to the Vendor room*, *Map edge*, *12 tiles out is the limit*, *Walk closer*.
4. **Left click** pushes the section out 1 tile. **Hold** to keep pushing (one push every 0.2 s after a 0.38 s wait).
   The selection rides the new face, so clicking again pushes further without aiming again. Moving the mouse picks a new section.
5. **Reach** is 7 tiles from you to the section, so you walk into the new space to push further.
6. **Right click** or C again stops. The wave starting stops it too.

Limits: a section goes at most **12 tiles** out. Pushing a section's side wall starts a new section from that wall.

## Room features

- The first push into a section **rolls one feature** for it (seeded from the section's room, side, start and width,
  so every machine gets the same one, and it never changes as the section grows). A label shows it: `BAFFLES ROLLED`.
- Each push decorates the row **2 tiles behind the face**, so the spot you push into stays clear.
- They need room: **4+ tiles** wide for Fins, T-fins and Cover, 5+ for Baffles and Divider, 6+ for Pincers.
  A narrower section gets none (`TOO NARROW FOR FEATURES`). No feature closes the lane.

| Feature | What grows |
|---|---|
| Fins | Stubs from alternating side walls every 4 rows (2 tiles long, 1.5 when narrow) |
| Baffles | Long walls from alternating sides every 3 rows, leaving a 2-tile lane: a zig-zag |
| Pincers | A pair facing each other every 5 rows, leaving a 3-tile gap |
| T-fins | A stub with a crossbar at its tip every 4 rows |
| Divider | A wall down the middle that grows 1 tile per push: 5 tiles of wall, a 2-tile doorway, repeat |
| Cover | A crate or pillar every 3 rows (two when 7+ wide) |

- Nothing spawns on a player. If you stand on the divider's line it waits and catches up on the next push.
- The look: each piece grows out from where it's attached, with a white build-in flash and sparks. Walls use the
  room walls' hatched style; crates are boxes with a cross.

## How it fits the code

- **A section is one `ExpansionRoom` box**: `Parent` is the room it was pushed from (0 for the hub), the box covers the
  section's width and its depth so far, and it opens full width onto its parent like an outline piece. It's marked by a
  new `Leg` value, `Expansion.PushLeg` (15, below `PieceBase`), so saves and `Msg.ExpandState` carry it with no new field.
- **A push grows that box** by one `Step` (100 px) instead of adding a box, so `MaxRooms` counts sections, not pushes.
  A section is drawn as part of its parent's room (`World.Rim`/`Seam` groups), so the minimap, the draw screen and
  "who drew what" show one room.
- **Features are rebuilt from the box**, like `AddFins`: rows 1 to depth-2 get the rolled feature, so every machine
  builds the same map from the room list. New solids since the last build use the existing rise animation (`LayoutAge`).
- **Wire**: a client sends `Msg.WallPush` (area, side, start tile, width). The host checks it (between waves, free building,
  reach with some slack, rules, cost), grows or makes the box, charges the pusher (the Team purse applies) and sends the map with
  `BroadcastExpansion`, at most 10 times a second while someone holds the button. **Protocol bump** in step 1.
- Breaches use the new outer wall automatically. A map node in the new space is claimed the same way as when a drawn room
  covers it (`TickNodes`). The parent's side walls (`AddFins`) skip the open join like any door.

## Steps

1. **Core**: `Expansion.PushLeg`; the rules and cost (`Expansion.CanPush`, `Push`); the host side (`Msg.WallPush`, charge,
   broadcast); bump the protocol. Tests in suite `expand` (`ExpandingPushTest`): cost = width, a push grows one box, reach,
   12-tile limit, the gap to other rooms, the hub, Team vote off, between waves only, a save round trip.
2. **In the world**: the C key (Keybinds), the prompt chip, push mode (section pick, wheel width, ghost row, label, the
   vectors chip's spend slice, hold to repeat), sounds. Preview flags `PUSHSEL=` and `PUSHAT=`; README *Expanding arena* section.
3. **Features**: the six rolled features built from a section's depth, the build-in look, the test that every machine
   rolls the same; preview `PUSHFEAT=<style>,<depth>`.
4. **Balance**: a bot report run (`vecbal`) to check vectors still go to rooms, and tune the 12-tile limit and feature spacing.
