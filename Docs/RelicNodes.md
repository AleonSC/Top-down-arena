# Relic nodes: faction relics on pedestals

Status: **built (protocol 432)**. Builds on [`Nodes.md`](Nodes.md) (relic nodes) and `Relics.cs`.

- Each boss cycle places **1-3 relic nodes** for that cycle's boss faction (`Game.BossCycle` order; `PlaceRelicNodes`), guaranteed: the waiting-nodes cap doesn't apply and a full map tries nearer tiers, then each side of the hub.
- 10 factions, 20 relics: Warden 1, Hive Queen 3, Reaper of Dusk 2, Singularity 2, Colossus 1, Duelist 2, Lich 3, Storm Herald 2, Devourer 1, Eclipse 3. Each node holds one of its faction's relics (`MapNode.Rule` = relic + 1; `Relics.Held`).
- Build a room round the node and a **pedestal** stands in the cell with the artefact floating above it, in the faction's colour (`RelicLook.cs`). Walk up to take it: one relic slot per player, the old relic is left behind. A taken node stands empty and dim.
- The effects are small stat changes applied in `Game.Recalc` (`Relics.Apply`); the three classic relics (Wall Bounce, Backup, Hallway Runner) are unchanged and still roll on old nodes.
- Preview: `RELICSHEET=0|1` (artefact sheet), `EXPANDMAP=0 RELICPED=<relic id> ZOOM=2` (hud: a built room with that relic on its pedestal).
- Open: bots walking to relic nodes, effects tuning, a pick-up banner.
