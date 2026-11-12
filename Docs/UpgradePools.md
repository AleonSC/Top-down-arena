# Upgrade pools: class and weapon cards

**Status: planned (Nov 2026). No step built yet.** Owner decisions are below; build one step at a time ("start on step N").

## Why

The owner wants more upgrade cards, many of them only for one class or weapon, without flooding everyone's pool.
The Nov 2026 audit of `Core/Upgrades.cs` found:

- **Filters mostly exist.** A card can need a role (`Role`), an ability you have (`RequiresAbility`), one exact weapon
  (`RequiresWeapon`) or the Expanding arena (`NeedsExpanding`). A card that fails its filter is never offered, so it
  can't crowd anyone else's pool. Only one weapon card exists (Warding Ink, Spirit Chalk) and no class-only card.
- **255-card cap.** A card's id is its index in `Upgrades.All`, sent as one byte (`Msg.Upgrades`, the snapshot's
  `Offer`, shop shelves, chest loot). About 158 are used (104 written cards + 54 class-ability cards). With 59
  weapons, two cards each would pass the cap.
- **Append-only list.** Inserting a card renumbers every card after it, so new groups are chained onto the end
  (`WithClassAbilities`, `WithMilestones`, `WithBuilders`), and `Tests/Expansion.cs:1359` fails as soon as any card
  comes after the vector cards. Saves store card names (`RunSaves.cs`), so only the network and replays care about ids.
- **No class filter, no weapon list.** A "Knight only" or "any Knight melee weapon" card can't be written today.
- **Mods, legendaries and milestones ignore your weapon.** `ModOffer`, `LegendaryOffer` and `MilestoneOffer` don't
  take a weapon, so a weapon-specific card of those rarities would never be offered (`RequiresWeapon` reads "no weapon
  passed" as "not yours"). The optional `weapon`/`expanding` parameters make this easy to miss.

## Decisions (owner, Nov 2026)

| Question | Decision |
|---|---|
| Card id size | **2 bytes** (protocol bump) |
| Where cards live | **Per-class files**, gathered like `Upgrades.Abilities` |
| New filters | **`Kit`** (class only) and **`Weapons`** (any of a list) |
| Mod / legendary / milestone offers | **Weapon-aware** |
| Odds of "your" cards vs general ones | **By count, as now**: more cards for a weapon means they turn up more often for it |

## Steps

### Step 1: 2-byte card ids

- Every place a card id crosses the wire goes from `byte` to `ushort`: `BroadcastUpgrades` / its reader in
  `GameNet.cs`, the snapshot's `Offer`, the shop shelf, chest loot, `Msg.Pick` payloads, dev spawn. Find them with
  `grep -rn 'Upgrades.All\[' Assets/Scripts/Core` plus the `(byte)` writes in `Game.cs` / `GameNet.cs`.
- An empty offer slot (`-1`) gets an explicit "none" value instead of wrapping to 255.
- Bump `Session.ProtocolVersion` and `Replay.FormatVersion`; line in `Docs/ProtocolHistory.md`.
- Test: a sim-test check that `Upgrades.All.Length < ushort.MaxValue` and a snapshot round trip with a card id > 255.

### Step 2: one offer context, weapon-aware offers

- `OfferCtx` (role, kit, Space ability, weapon, expanding, taken) built once from a `Player` (`OfferCtx.Of(p, game)`),
  replacing the 4-6 loose parameters on `Available`, `RollOffer`, `PrizeOffer`, `RandomOfRarity`, `ModOffer`,
  `LegendaryOffer`, `MilestoneOffer`. No optional parameters left to forget.
- Callers: `Game.cs` (`NextOffer`), `Shop.cs`, `Props.cs`, `BossLab.cs`, `BuildPower.cs` (it simulates a build with
  no `Player`: give it a ctx with the weapon it plays).
- Test: a weapon-only mod is offered to that weapon and never to another.

### Step 3: `Kit` and `Weapons` filters

- `UpgradeDef.Kit` (class only) and `UpgradeDef.Weapons` (any of these; `RequiresWeapon` becomes a one-entry list).
- **Wildcard:** counts as the class of its weapon and the class of its second ability (both can match).
- Card text and the picker need nothing new; the Dev dock's spawn sheet shows the filter (e.g. "Knight", "Lance").
- Test: each filter offers the card only to the right class / weapons, for every role and the Wildcard.

### Step 4: per-class card files

- Each class file gets its cards beside its abilities (`Game.KnightCards`, ...), like `Game.KnightAbilities`.
  `Upgrades.All` = general cards, legendaries, mods, class-ability cards, milestones, vector cards, then each class's
  list in `Kit` order. The `With...` chain goes.
- **Order rule:** ids are positions. Host and client must share a protocol version and saves use names, so a card
  can go anywhere as long as the change bumps `Session.ProtocolVersion` and `Replay.FormatVersion` (old replays store
  ids). No more "add it at the very end".
- Replace the "vector cards are last" check (`Tests/Expansion.cs:1359`) with a check that every card name is unique
  (saves look cards up by name).
- `CODEMAP.md`: `Upgrades.cs` line names the per-class lists; `HANDOFF.md` "Adding a class" gets a cards line.

### Step 5 (later): the first class and weapon cards

Proposed per class in a follow-up, after steps 1-4. With odds by count, a weapon with several cards sees its own
cards more often than a weapon with one. Keep the numbers per weapon close (e.g. 1-2 each) so classes stay even.
