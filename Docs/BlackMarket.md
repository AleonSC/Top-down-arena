# Black Market

Status: **built (protocol 291)**: buying, stash, placing and the UI. Only the teleporter does anything; the traps are placeholders.

The vendor room is now the **Black Market** (`RoomKind.Vendor` keeps its name in code). Decided with the owner on Nov 11, 2026.

- **Screen**: the shop carousel with two tabs, **Buildables** (Teleporter pair 150, Spike trap 60, Landmine 80, Slow field 90 gold) and **Services**
  (remove upgrade, heal, revive, vector pack, Floor kit). Add stock stays in the shop room. `ShopUi.DrawBuildCard`.
- **Stash**: `Player.Stash[BuildKind]`, at most 5 of a kind, bought with `ShopAction.BuyBuild`, synced with the shop fields, saved as a `stash` line per seat.
- **Teleporters**: the draw screen's Portal tool is now **Teleporter**: it spends a stash item and builds at once (no vote, no vectors). Bots don't build them.
- **Traps**: the draw screen's **Trap** tool places one from the stash with a click (`Msg.PlaceBuild`, `Expansion.PlaceSpot`); the list is `Game.Placed`,
  synced in `Msg.ExpandState`, saved as `build` lines. They have no effect yet.
- **Next**: give the traps their effects, draw them in the world, more buildables.
