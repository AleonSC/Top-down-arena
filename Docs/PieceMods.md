# Piece card modifiers

Status: **built (protocol 424; Loot added Nov 20, no wire change; One-way protocol 447)**. Decided with the owner on Nov 20, 2026.

- **What**: a shape piece card carries a *set* of modifiers (floor, combat, ...). Service cards and the Builder Shop carry none.
- **Rolls**: every time a shape card is offered (start hand, the round's card, wave pick drops, chests, boss picks, Builder Shop shelf) each modifier rolls on its own
  chance (30% each today), so a card gets none, one or several. The Builder Shop's last card always carries one (`PieceMods.Roll(..., atLeastOne: true)`).
- **Floor**: the placed rooms get that rule floor's ground (Burning Grounds, ...). Rolls among the floors unlocked at the wave.
- **Combat**: enemies wait in the placed rooms (dormant groups, `DormantGroups.cs`, at least one group). A plain piece spawns none.
- **Loot**: a chest of one kind is guaranteed in the piece (Upgrade, Gold, Vector, Relic, Piece, Buildable or Gadget, rolled by rarity like a room chest, `RoomChestWeights`), in the middle of one of its cells. A Loot piece skips the random 30% room chest. Rolls 20%.
- **One-way** (owner Nov 20): every door joining the piece to the rest of the map lets you through one way only, in or out (a seeded coin per door,
  `Expansion.OneWayInward`), except one: the first (the door it was built through) always works both ways; a piece with a single door is both ways, and a door
  that is any One-way piece's both-ways door stays both ways. Opened walls stay both ways. Players, bots and enemies obey them (bosses ignore walls anyway);
  enemy routes cross them only the open way; shots fly through. Rolls 20%. Code `Core/OneWayDoors.cs`, look `Game/OneWayDoorLook.cs` (amber chevrons; an arrow each on the build map, and on a One-way card's ghost before you place it); bumping one from the wrong side plays `Sfx.OneWayBump` with a small amber ring (`OneWayDoorSoundBank.cs`, local only).
- **Price**: the Builder Shop charges 6 vectors a cell plus 8 a modifier. Playing a piece is free.
- **Add one**: subclass `PieceMod` in `Core/PieceMods.cs` (name, blurb, chance, `RollValue`, `ApplyRoom` for the room's look/kind, `OnPlaced` for extras), give it the
  next free `Shift`/`Bits`, and add it to `PieceMods.All`. **Remove one**: delete it from `All`; cards still carrying it read as plain (`PieceMods.Clean`).
- **Card code**: `PieceCodeBase (32) + shape + 16 * mods`, `mods` packing each modifier in its own bits (floor bits 0-3, combat bit 4, loot bits 5-7, one-way bit 8, up to `PieceMods.Bits` 10). Floor-only cards
  keep the codes they always had. The hand is sent as 2 bytes a card; floor kit offer codes moved up to `BossCards.KitBase` 20000.
- **UI**: the hand card shows its modifiers under the name; the tip lists what each does; a combat-only card is red.
