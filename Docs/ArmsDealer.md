# Arms Dealer and the boss card pick

Status: **steps 1-3 built** (protocols 394-396): service cards, the Arms Dealer room, the boss card pick. Step 4 open. Planned Nov 17, 2026. Expanding arena with room cards on only. Builds on [`RoomCards.md`](RoomCards.md) (the hand, now [`GridPieces.md`](GridPieces.md)),
[`RoomModifiers.md`](RoomModifiers.md) (floor kits), [`MysteryDoors.md`](MysteryDoors.md) and [`Nodes.md`](Nodes.md) (the tiny shop / Black Market rooms).

## Decisions (owner)

| Question | Decision |
|---|---|
| Boss reward | The legendary stays. The **weapon mod pick** after it becomes a **card pick**: **5 random** cards from room cards, shape cards and floor kits. |
| Free boss drops | **Replaced** by the pick: no free shape card, no Floor kit, and the **Mini-boss card is no longer free** anywhere (only from the pick or bought). |
| Pick rules | **Rerollable** (normal rerolls). **Mini-boss cards** can be offered. |
| Weapon mods | Move to a new **Arms Dealer** room: the **full mod list**, paid in **gold**, 3 stacks each as now. |
| Service cards | New **shape cards** that build a tiny 4 x 4 service room: **Shop**, **Black Market**, **Arms Dealer**. Earned like the other shape cards. Not floor kits. |
| Arms Dealer on the map | Played from its card, or rolled behind a **mystery door** (as a tiny room, like the shop and vendor). |
| Other arenas (Survival, Simple arena) and Free draw | **Later** (owner). Until then they keep today's boss mod grid. |

## Plan

1. (built) **Service cards.** `PieceCard.Shop`, `Market`, `Arms` (added at the end of the enum); each plays a tiny 4 x 4 room (`SurpriseShape.Tiny`)
   of `RoomKind.Shop` / `Vendor` / `Arms` (new, at the end), already open. Card art in `PiecesUi.cs`. Sources as shape cards: the
   1 in 4 wave drop and chest nodes can roll them, and the boss pick. Tests in suite `roomcards` (since removed; now suites `gridrooms` and `armsdealer`).
   Built: `Expansion.ServiceCards`, `IsServiceCard`, `CardRoomKind`, `DropCard` (1 in `ServiceCardChance` 5); the tiny room gets the floating rooms' mark
   (`RoomsLook.TinyService`, also mystery-door shops), the card a coin / plus / crosshair (`PiecesUi.DrawServiceIcon`). Preview `CARDROOM=arms`.
2. (built) **Arms Dealer room.** `RoomKind.Arms`, `MysteryKind.Arms` (in the shop / Black Market slot of the door table), a stall look (`NodeLook.cs`),
   its screen: every weapon mod as a CRT card with its stacks and price, `ShopAction.BuyMod` (host checks gold, room, stack cap). Saves.
   Built: `ArmsDealer.cs` (`ModIds`, `ModPrices` 150/250/400, `RoomTypePrice`, `HasArmsRoom`, `InArmsRoom`, `BuyMod`), the shop screen's Arms Dealer
   (`ShopUi.DrawModItem`, items 40+), `Game/ArmsDealerLook.cs` (the stall), small mystery rooms: Arms Dealer 11 of 100. Bought mods are upgrades,
   so saves already keep them. Previews `EXPANDSHOP=4 [SHOPCAT=0] [MODSHELD=a,b]`, `EXPANDMAP=0 CARDROOM=arms CARDDY=260`. Suite `armsdealer`.
3. (built) **Boss card pick.** In the Expanding arena with room cards on, the boss's `PendingMods` becomes `PendingBossCards`: 5 cards rolled per
   player from Box, Hall, the five shapes, Mini-boss, the service cards and Floor kits (seeded). A card goes to your hand, a kit to the
   team's kits; a full hand or full kit list greys those cards. Remove the free boss drops. Picker look: the room card art in the upgrade
   picker (`HudPicker.cs`). Elsewhere the mod grid stays.
   Built: `BossCards.cs` (`Offer`: codes, a Piece or `KitBase` + a PathId; `Game.BossCardPicks`, `BossCardUsable`, `GrantBossCard`, `BotBossCard`),
   `Player.PendingCards` / `OfferCards` (snapshot), the picker's BOSS SPOILS! cards (`Game/BossCardsUi.cs`), rerolls, the free boss card, Mini-boss
   card and Floor kit gone with cards on; a pick nobody can take is skipped. Preview mode `bosscards` (`BOSSFULL=1`). Suite `armsdealer`.
4. **Bots and balance.** Bots pick cards (prefer a service card the map lacks, else a shape), buy mods at the Arms Dealer, play service
   cards near the hub; `vecbal` / sim runs to check bots still get mods; README, previews.

Each step bumps `Session.ProtocolVersion`.

## First guesses (tune later)

- Mod prices: **150 / 250 / 400 gold** for your 1st / 2nd / 3rd stack of a mod.
- Pool weights for the boss pick: shapes 40, Box / Hall 15, service cards 20, Floor kits 15, Mini-boss 10; no duplicates in one offer.
- Service cards in the wave drop and chests: 1 in 5 of shape-card rolls.
- The Black Market sells no cards (its card shelf was retired Nov 18). The Arms Dealer card comes only from the usual sources (wave drops, chests, the boss card pick, mystery doors); the per-cycle guarantee was removed Nov 19. Heals and revives moved to the Medic (`Medic.cs`).
