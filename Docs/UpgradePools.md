# Upgrade pools: class and weapon cards

**Status: steps 1-4 built (Nov 2026, protocol 310); step 5 (the first class and weapon cards) next.** Owner decisions are below; build one step at a time ("start on step N").

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

### Step 1: 2-byte card ids (built, protocol 309)

- Card ids cross the wire as `Int16` everywhere: `Msg.Upgrades` (`Game.BroadcastUpgrades`, read in `GameNet.cs`: the
  taken count and each id) and the snapshot's `Offer` (`GameNet.cs`) moved from one byte; the shop shelf
  (`Shop.SyncShop`) and chest loot (`Props.SyncLoot`) already used `Short`. `Msg.Pick` sends an offer index, the shop
  and dev spawn send `Int32`: unchanged. Limit: 32,767 cards.
- An empty offer slot (`-1`) now arrives as `-1` (one byte wrapped it to 255).
- Replays need no format bump: since `Replay.FormatVersion` 7 a replay from another protocol isn't listed.
- Test: suite `cardids` (`CardIdWireTest`, `Tools/SimTest/Tests/Upgrades.cs`).

### Step 2: one offer context, weapon-aware offers (built, no wire change)

- `OfferCtx` (`Core/Upgrades.cs`: role, class, Space ability, picks, weapon, Expanding arena) is the one argument every
  offer takes: `Available`, `RollOffer`, `PrizeOffer`, `RandomOfRarity`, `ModOffer`, `LegendaryOffer`,
  `MilestoneOffer`. The old loose parameters are gone.
- In a match, `Game.OfferFor(p)` builds it (`OfferCtx.Of(p, ExpandingOn)`): `Game.cs` (`NextOffer`), `Shop.cs`,
  `Props.cs`, `DevTools.cs`, `BossLab.cs`, `DevDock.cs`. Mod, legendary and milestone offers, a chest's legendary and
  the dev spawn sheet now also see your weapon and the arena.
- `BuildPower` (boss health scaling) builds `new OfferCtx(role, ability, taken)` with no weapon: a typical build of any
  weapon, so weapon-only cards never count toward it.
- Tests use the constructor. Test: suite `offers` (`OfferWeaponTest`, `Tools/SimTest/Tests/Upgrades.cs`).

### Step 3: `Kit` and `Weapons` filters (built, no wire change)

- `UpgradeDef.Kit`: only offered to that class. `UpgradeDef.Weapons`: only offered if your weapon is on the list
  (`RequiresWeapon` is gone; Warding Ink is `Weapons = { SpiritChalk }`). Both are checked in `Upgrades.Available`.
- `OfferCtx.Kits` is a bit per class (`HasKit`). Anyone but a Wildcard has one: their class (the constructor defaults
  it to the class of the Space ability). **A Wildcard** has no class of its own, so it counts as the class of its
  Space ability, its weapon and its second ability (`Player.StartBonus`): any of those classes' cards can turn up.
- The Dev dock's Spawn-an-upgrade sheet tags a filtered card with whose it is (`Upgrades.FilterNote`: "Knight",
  "Spirit Chalk", "Knight · 2 weapons"), and "... only" when it isn't for the chosen player. Preview:
  `DEVSHEET=4 UPRARITY=2 UPSCROLL=2000 Tools/Preview/shot.sh <name> dev`.
- Test: suite `cardfilters` (`CardFilterTest`, `Tools/SimTest/Tests/Upgrades.cs`).

### Step 4: per-class card files (built, protocol 310)

- Each class keeps its own cards beside its abilities: `Game.SoldierCards` (`Abilities.cs`), `ElementalistCards` and
  `SummonerCards` (`Minions.cs`), `KnightCards` (`Knight.cs`), `NinjaCards` (`Ninja.cs`), `HemomancerCards`
  (`BloodMage.cs`). The 37 Rare ability cards (and Warding Ink, a Summoner weapon card) moved there.
- `Upgrades.All = Gather(general cards, ClassAbilityCards(), Milestones(), VectorCards(), each class's list in Kit
  order)`. The `With...` chain is gone. `Gather` stops the game at start-up if two cards share a name.
- **Order rule:** ids are positions. Host and client share a protocol and saves name their cards, so a card can go
  anywhere: add it to its class's list (or the general list) and bump `Session.ProtocolVersion`.
- Cards per class today: Soldier 8, Elementalist 0, Summoner 2, Knight 9, Ninja 9, Blood Mage 9.
- Test: suite `classcardlists` (`ClassCardListsTest`: each list holds only its class's cards, nothing class-specific is
  left in the general list, names are unique). `Tests/Expansion.cs` no longer needs the vector cards last.

### Step 5 (later): the first class and weapon cards

Proposed per class in a follow-up, after steps 1-4. With odds by count, a weapon with several cards sees its own
cards more often than a weapon with one. Keep the numbers per weapon close (e.g. 1-2 each) so classes stay even.
