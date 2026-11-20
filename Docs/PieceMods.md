# Piece card modifiers

Status: **built (protocol 424)**. Decided with the owner on Nov 20, 2026.

- **What**: a shape piece card carries a *set* of modifiers (floor, combat, ...). Service cards and the Builder Shop carry none.
- **Rolls**: every time a shape card is offered (start hand, the round's card, wave pick drops, chests, boss picks, Builder Shop shelf) each modifier rolls on its own
  chance (30% each today), so a card gets none, one or several. The Builder Shop's last card always carries one (`PieceMods.Roll(..., atLeastOne: true)`).
- **Floor**: the placed rooms get that rule floor's ground (Burning Grounds, ...). Rolls among the floors unlocked at the wave.
- **Combat**: enemies wait in the placed rooms (dormant groups, `DormantGroups.cs`, at least one group). A plain piece spawns none.
- **Price**: the Builder Shop charges 6 vectors a cell plus 8 a modifier. Playing a piece is free.
- **Add one**: subclass `PieceMod` in `Core/PieceMods.cs` (name, blurb, chance, `RollValue`, `ApplyRoom` for the room's look/kind, `OnPlaced` for extras), give it the
  next free `Shift`/`Bits`, and add it to `PieceMods.All`. **Remove one**: delete it from `All`; cards still carrying it read as plain (`PieceMods.Clean`).
- **Card code**: `PieceCodeBase (32) + shape + 16 * mods`, `mods` packing each modifier in its own bits (floor bits 0-3, combat bit 4, up to `PieceMods.Bits` 10). Floor-only cards
  keep the codes they always had. The hand is sent as 2 bytes a card; floor kit offer codes moved up to `BossCards.KitBase` 20000.
- **UI**: the hand card shows its modifiers under the name; the tip lists what each does; a combat-only card is red.
