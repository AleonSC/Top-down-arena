# Mystery doors: sealed doors on the outer walls you pay vectors to open

Status: **steps 1-3 built** (protocol 382: doors rolled, opened, saved, expiring, synced; step 2: every kind behind a door; step 3: the Hologram look, the reveal, minimap and build map marks). Step 4 (bots, balance) open. Decided with the owner on Nov 17, 2026. Expanding arena only. Builds on
[`SurpriseRooms.md`](SurpriseRooms.md) (shapes, placement), [`BreachRooms.md`](BreachRooms.md) (Stash, a door lined up with a
wall spot), [`Nodes.md`](Nodes.md) (tiny shop / vendor rooms) and [`FactionMinis.md`](FactionMinis.md). **Drawing rooms stays
exactly as it is**: doors are a second, cheaper way to grow the map.

## Decisions (owner)

| Question | Decision |
|---|---|
| Replace drawing? | **No, alongside it.** |
| How many | **1-2 new doors each cleared wave**, **cap 3** on the map. |
| Behind a door | **Combat room, loot (Stash / chest), shop / vendor, risk (mini-boss / ambush).** |
| Price | **~60% of drawing** a room of the same size (you don't pick shape or spot). |
| When | **Any time**, mid-wave too; the room joins the fight at once. |
| Show the room? | **No**: nothing tells you what is behind a door. |
| Expiry | Unopened doors **expire at the end of the round** (when the next wave is cleared) **unless a player saves one**: **one save per player**. A saved door stays until opened. |
| Breaches | A door **never acts as a breach**, but a risk room's enemies can **swarm out** once it's opened (step 2). |

## How it plays

- A **sealed door** is a lit shutter in an outer wall with its price over it. The room behind is already rolled (seeded, same on
  every machine) but hidden: only the door and a "? price" tag show.
- Any player stands at it and presses **G** to pay from their own vectors (no vote). Not enough vectors: the price shows red.
- On paying, the shutter lifts with the reveal (the gold build-in of surprise rooms) and the room is added like any built room.
- The room's kind is **never shown**: it's a full gamble.
- **X** saves (one door per player; X on another moves the save, X on your own lets it go).

## What's behind (first-guess weights)

| Kind | Weight | Room |
|---|---|---|
| Combat | 50 | a surprise shape (Ring, Cross, L, T, Chamfer), placed by `SurpriseOptions` behind the door's wall |
| Loot | 20 | a Stash (vector cache) or a small room round a chest |
| Shop / vendor | 15 | the tiny 4 x 4 shop or vendor room, already open |
| Risk | 15 | the faction mini-boss room (`MiniBossNow`), or an **ambush**: a squad spawns in the room as it opens |

Risk is never rolled before wave 5 or on the wave after a boss. A door whose room no longer fits (a drawn room took its space) is
re-rolled to a spot that does, or removed.

## Price

`Door price = round(0.6 * Expansion.Cost(room))`, minimum 1. Loot and shop doors are priced as their small room (cheap); risk
doors at the combat price of a mid shape so they don't stand out.

## Steps

1. **Core**: `MysteryDoor` (wall spot, shape + placement, price), the roll (seeded from map + wave, 1-2 per build
   round, cap 3), `Game.MysteryDoors`, `Msg.MysteryDoor` (player -> host) and the doors in `Msg.ExpandState`; opening adds the room to
   `Expanded` and rebuilds the world; mid-wave opening. Suite `mysterydoor`. Protocol bump.
2. **Contents**: loot / shop / risk rooms (Stash cache, chest, tiny shop / vendor, mini-boss, ambush squad).
3. **Look**: the sealed shutter, price tag, "?" on the minimap and build map, the reveal; previews.
4. **Bots and balance**: bots open doors when they can afford them; measure vectors spent per wave and survival vs drawing only.

## Built (step 1)

- `Core/MysteryDoors.cs`: the roll at a cleared wave (unsaved doors expire first, then 1-2 new up to the cap), each a surprise shape set
  against the map (`SurpriseOptions`, seeded among the 8 best spots, kept 400 px and a tile apart from other doors' rooms), the door at
  the first plain door the planned room would share with the map. When the map changes the plan is checked again (`TickMysteryDoors`):
  re-planned behind the same spot or dropped. Opening builds the rooms as a bought room (the payer's, price on it, not a gift). Mid-wave
  opening needs the rooms up (the first room raises them between waves).
- Not saved with the run (`RunSaves`): a loaded run starts with no doors; the next cleared wave rolls new ones.
- `Game/MysteryDoorLook.cs`: a first look (amber shutter, price tags, key hint); step 3 replaces it.

## Built (step 2)

- `MysteryKind` (host only, never sent) rolled per door by `Mystery.KindFor` with the weights above; no risk before wave 5, round a boss,
  or without a faction mini-boss. Shapes (`Mystery.ShapeOf`): combat and ambush one of the five surprise shapes, Stash its 5 x 3, chest,
  shop and vendor the new `SurpriseShape.Tiny` (4 x 4), mini-boss the new `SurpriseShape.Duel` (8 x 6). A loot, shop or mini-boss door
  that loses its spot is only re-planned in its own shape.
- Opening: a Stash keeps its Stash mark (`TickStashClaims` pays its cache); a chest room gets a chest node at its middle (`TickNodes`
  puts the chest in); shop / vendor rooms are `RoomKind.Shop` / `Vendor` (connected, so gold drops and the room shop start); the mini-boss
  room is `RoomKind.Gatekeeper` with `Mini` = `MiniBossNow()` (the duel as in `GatekeeperRoom.cs`); an ambush springs
  `Mystery.AmbushCount` enemies of `WavePool(Wave)` in the room (`SpringAmbush`), or as the next wave starts if opened between waves
  (`ambushPending`, `SpringPendingAmbushes` in `Waves.cs`).
- Tests: `MysteryContentsTest` (suite `mysterydoor`).

## Built (step 3)

- The owner picked the **Hologram** look (of Shutter, Hologram, Vault) and asked for it to stand out from the cyan walls: a dark plate
  hides the wall, a bright magenta-violet field with drifting scan lines over it, corner brackets that reach past the wall, a halo and a
  white "?"; lime once saved (`DrawMysteryDoor`). Reveal kept as shown: the plate's halves slide into the wall over 0.5 s and a ring goes
  out (`MysteryOpened`, `NoteOpenedDoors` on every game: a door gone with a doorway where it stood; `DrawMysteryReveal`).
- The hint pill sits over you (it hid the door), with *NEED n MORE* when you can't pay; tags are red when unaffordable.
- Minimap: a violet (lime) mark (`DrawMysteryOnMap`); build map: a bar and diamond on the doorway (`DrawHoloMystery`) and *MYSTERY DOOR* in
  the map key.
- Previews: `EXPANDMAP=6 MYSTERY=1 MYSTERYAT=1 ZOOM=2.2 shot.sh x hud 8 3`, `MYSTERYSAVED=1`, `MYSTERYOPEN=<s>` (the reveal),
  `EXPANDVOTE=4 EXPANDFREE=1 MYSTERY=1 EXPANDDRAW=0,0,0,0` (build map).
