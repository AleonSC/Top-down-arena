# Map reset: a fresh map after every boss

Status: **step 1 built** (protocol 389: the reset, refund, kits kept, map number synced and saved; suite `mapreset`); steps 2-4 open. Decided with the owner on Nov 17, 2026. Campaign (Expanding arena) only. Builds on
[`VectorBuilding.md`](VectorBuilding.md), [`SurpriseRooms.md`](SurpriseRooms.md), [`MysteryDoors.md`](MysteryDoors.md) and
[`Nodes.md`](Nodes.md).

## Why

By wave 100 the map holds up to 24 bought rooms plus free ones (surprise, Stash, mystery door), up to 6 nodes, 3 doors,
6 gates and 3 portal pairs, and every 4th track wave is a breach wave with up to 6 breaches and a camera tour. That's too
much to follow. Building stays on all the way through; the map resets instead.

## Decisions (owner)

- **The map resets every 10 waves**: after each boss (shown waves 10, 20, ... 90) it goes back to just the hub. That makes
  10 maps per run, each about 10 waves long. Building, breaches, nodes and doors work as today on each map.
- **What the team keeps:**
  - **Vector refund**: each player gets back **half the vectors they spent on that map** (rounded down, up to their cap).
  - **Room kits**: held kits (up to `Expansion.MaxTokens`) carry over.
  - **One keepsake room**: the team votes to keep one room; it's rebuilt next to the hub on the new map, with its shape,
    kind and kit.

## How it plays

1. Boss dies → the usual break opens. A **KEEPSAKE** vote replaces the post-boss surprise room: every room on the map is a
   choice, each player clicks one (the build vote UI and timer), a tie goes to the host and the timer picks the biggest room.
2. As the boss wave is cleared (step 1: at once, before the build round; the boss wave already keeps the rooms down, so the team is in the hub), the map **folds away**: rooms collapse toward the hub
   (about 1.5 s), coins and pickups fly to their nearest player, the camera pulls in.
3. The new map: the hub, plus the keepsake on its best hub wall (`Expansion.Placement`, loops scored as for surprise rooms).
   The toast reads **MAP n/10 · +x VECTORS BACK**. The next wave's breaches, nodes and doors roll from a new `expandSeed`.

## Rules

| Thing | On reset |
|---|---|
| Bought, surprise, Stash and door rooms | gone (except the keepsake) |
| Nodes, chests, mystery doors, gates, portals, openings, traps (`PlacedBuild`) | gone |
| Room kits | kept |
| Player vectors | kept, plus 50% of `Player.MapSpent` (reset to 0 per map), on top of the boss refill and above the cap |
| Coins on the floor | collected by the nearest living player |
| Shop / vendor / Gatekeeper rooms | gone; the next regular wave places the new boss cycle's floating ones as usual |
| `expandSeed` | new (from `sideRng`, as in `ResetExpansion`) |

## Steps

1. **The reset (core + wire)** *(built)*. `Game.ResetMap()` in a new `MapReset.cs`: keeps kits, refunds `MapSpent`, clears the rest
   (shares `ResetExpansion`'s clearing), moves players to the hub, runs after a boss in place of `GiveSurpriseRoom` in the
   Campaign. `Player.MapSpent` counted in `PayVectors` (every vector spend: rooms, doors, pushes) and saved in
   `RunSave`. `Game.MapNumber` in `Msg.ExpandState`. Protocol bump. Test suite `mapreset` (in the feature run): the map
   resets on each boss, the refund is right, kits stay, a save in the middle of a map restores `MapSpent`.
2. **The keepsake vote.** The vote has to happen on the old map before it goes: the reset moves to the end of a short vote
   (about 15 s) at the start of the break. `Msg.KeepsakeVote`, host tally, bots vote for the biggest room, the kept room rebuilt by the hub
   (`Expansion.Placement`; a room that fits nowhere shrinks to the largest surprise shape that does). Protocol bump. Tests.
3. **The look.** The vote's room highlight and tally (DevKit widgets), the fold-away, the MAP n/10 toast, the keepsake's
   gold rim. Previews (`shot.sh`) before/after.
4. **Balance.** Vector income vs a 10-wave map (how many rooms a team ends a map with), breach counts on small maps
   (`Breaches.CountFor` already scales with rooms), session reports from the owner's runs.

## Open

- Should the Campaign tab show the map number (MAP 4/10) next to the boss list?
