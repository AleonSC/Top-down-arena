# Glossary

Status: agreed with the owner (Nov 19 2026). One word per thing, in code comments, docs, in-game text and requests.

## Naming rule

- A weapon's, ability's or enemy's **code id is its in-game name** with the spaces taken out and any leading "The"
  dropped: Revolver = `WeaponId.Revolver`, Arterial Spray = `AbilityId.ArterialSpray`, The Warden = `EnemyType.Warden`.
  Files and look classes follow the same name (`BloodBrawler.cs`, `BloodBrawlerLook.cs`).
- Renaming something in game means renaming its id as well. Add the old id to `Core/OldNames.cs` so saves, profiles
  and wave files that use the old name still load. Enum numbers don't change, so the protocol doesn't either.
- A short id is fine where it's just a shortened name: `Medkit` (Deployable Medkit), `Mark` (Mark for Death),
  `Adrenaline` (Adrenaline Shot). `Decoy` and `MummerCopy` show the name of the boss they copy.

## Players

| Term | Means | Code |
|---|---|---|
| **class** | Soldier, Elementalist, Summoner, Knight, Ninja, Hemomancer, Engineer (never "Mage" or "Blood Mage") | `Kit`, `KitDef` |
| **role** | Tank, Healer, DPS | `Role` |
| **alternative fire** | a weapon's secondary attack; "Alt" for short (the key label and weapon text) | `Alt` |
| **level** | only your XP level in a run (level-ups bring upgrade cards); your profile's progress is **rank** | `Player.Level`, `Rank` |
| **difficulty preset** | Normal, Hard, Nightmare... (never "difficulty level") | `DifficultyLevel` |

## Abilities

Abilities are named by slot, not by key (keys are rebindable; the old defaults were Space, Q, E, Shift and F).

| Term | Means | Code |
|---|---|---|
| **1st ability** | picked in the lobby: 3 choices per role and class, sometimes a **4th choice** (Flood, Gale Wall...) | `Player.FirstAbility`, `Slots[0]`, `KeyAction.Ability1`, `AbilityDef.Choice` (`Fourth`) |
| **2nd, 3rd, 4th ability** | filled by very rare upgrade cards ("2nd–4th abilities" together) | `Player.CardAbilities`, `Slots[1..3]`, `KeyAction.Ability2..4` |
| **Wildcard ability** | the Wildcard's extra starting ability, on the 2nd, 3rd or 4th slot | `Player.WildcardAbility` |
| **Ultimate** | picked in the lobby, one per role and class (never "ult" in text; the key cap keeps "ULT") | `AbilityDef.Ult`, `Slots[4]`, `KeyAction.Ultimate` |
| **mastery** | an ability's 1-5 rank from use; **mastery 5** unlocks its capstone (never "ability level") | `Mastery.cs`, `MasteryLevel` |
| **enemy level** | an enemy's strength in a wave file (`level 3`) | `DesignWave.Level` |

## Helpers

| Term | Means | Code |
|---|---|---|
| **minion** | anything that fights on your side; "summon" is only a verb | `Minion`, `MinionKind` |
| **companion** | the minion your weapon keeps (Golem, wolves, the Strider...) | `Minion.Companion` |
| **thrall** | an enemy chained to you by Force Pact | `MinionKind.Thrall` |

## Upgrades

| Term | Means | Code |
|---|---|---|
| **upgrade card** | a card you choose in game | `UpgradeDef`, `Upgrades.All` |
| **upgrade** | the effect or benefit an upgrade card gives | `Stats`, `Player.Taken` |

## Rewards

| Term | Means | Code |
|---|---|---|
| **perk** | picked before a run; unlocked by beating bosses | `Perks.cs`, `PerkId` |
| **relic** | found on the map mid-run; one slot | `Relics.cs`, `RelicId` |
| **legendary card** | a boss's rare upgrade card ("legendary" is its rarity, never a noun on its own) | `Rarity.Legendary` |
| **gadget** | an item you carry and can hand over (the Magnet) | `Gadget` |

## Runs

| Term | Means | Code |
|---|---|---|
| **run** | one game, start to end, in any mode; never "match" | `MatchState`, `Game.State` |
| **Campaign run**, **Quick Play run**, **Boss Hunt run**, **Survival run**, **Practice run** | a run in that mode | `GameMode` |

## Places

| Term | Means | Code |
|---|---|---|
| **arena** | the whole play space | `CurrentWorld` |
| **hub** | the Expanding arena's starting box, where bosses are fought | `Expansion` |
| **room** | a walled section the arena grows into | `ExpansionRoom`, `RoomKind` |
| **rule room** | a room whose ground has a path's rule (was "Floor room") | `RoomToken.Floor` |
| **floor** | only the ground surface (patches, coins on the floor) | |
| **map** | only the Paths and relic node map | `MapNode`, `NodeKind` |

## Building on the grid

| Term | Means | Code |
|---|---|---|
| **cell** | one 600 px square of the lattice | |
| **shape** | the 1-4 cell polyomino: Mono, Domino, ... TetL | `PieceShape` |
| **piece card** | what sits in your hand: a shape plus a room type; the service ones (Shop, Black Market, Arms Dealer, Builder Shop) are one cell. "Piece" on its own is the shape on the map. | `PieceCard`, `GridPieces.cs` |
| **room type** | what a piece card builds: plain, or a rule room's ground (was "modifier") | `RoomTypeOf`, `RoomTypeName` |
| **placement** | one legal spot and turn for a piece card, at a door spot | `Placement`, `PlacementsFor` |
| **piece chest** | the orange chest that gives every player a piece card | `ChestKind.Piece` |
| **Builder Shop** | the only place piece cards are bought, with vectors | `BuilderShop` |
| **Black Market** | sells buildables; never "vendor" | `RoomKind.BlackMarket` |
| **card** | never the bare word for a piece card: upgrade cards and milestone cards only | |

## Bosses

| Term | Means | Code |
|---|---|---|
| **phase** | one part of a boss fight; a **phase change** moves to the next (never "stage") | the boss's `Stage`, `StageShift` (`Enemy.Phase` is per-enemy state, not a boss phase) |
