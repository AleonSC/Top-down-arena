# Glossary

Status: agreed with the owner (Nov 19 2026). One word per thing, in code comments, docs, in-game text and requests.

## Naming rule

- A weapon's, ability's or enemy's **code id is its in-game name** with the spaces taken out and any leading "The"
  dropped: Revolver = `WeaponId.Revolver`, Arterial Spray = `AbilityId.ArterialSpray`, The Warden = `EnemyType.Warden`.
  Files and look classes follow the same name (`BloodBrawler.cs`, `BloodBrawlerLook.cs`).
- Renaming something in game means renaming its id as well. Add the old id to `Core/OldNames.cs` so saves, profiles
  and wave files that use the old name still load. Enum numbers don't change, so the protocol doesn't either.
- A short id is fine where it's just a shortened name: `Medkit` (Deployable Medkit),
  `Adrenaline` (Adrenaline Shot). `Decoy` and `MummerCopy` show the name of the boss they copy.

## Players

| Term | Means | Code |
|---|---|---|
| **class** | Soldier, Elementalist, Summoner, Knight, Ninja, Hemomancer, Engineer, Arcanist (never "Mage" or "Blood Mage") | `Kit`, `KitDef` |
| **role** | Tank, Healer, DPS | `Role` |
| **alternative fire** | a weapon's secondary attack; "Alt" for short (the key label and weapon text) | `Alt` |
| **level** | only your XP level in a run (level-ups bring upgrade cards); your profile's progress is **rank** | `Player.Level`, `Rank` |
| **difficulty preset** | Normal, Hard, Nightmare... (never "difficulty level") | `DifficultyLevel` |

## Abilities

Abilities are named by slot, not by key (keys are rebindable; the old defaults were Space, Q, E, Shift and F).

| Term | Means | Code |
|---|---|---|
| **1st ability** | picked in the lobby: 3 choices per role and class, sometimes a **4th choice** (Flood, Gale Wall, Stampede, Land Shark, Senbon Seekers, Deployable Turret, Thorn Bloom...) | `Player.FirstAbility`, `Slots[0]`, `KeyAction.Ability1`, `AbilityDef.Choice` (`Fourth`) |
| **2nd, 3rd, 4th ability** | filled by very rare upgrade cards ("2nd–4th abilities" together) | `Player.CardAbilities`, `Slots[1..3]`, `KeyAction.Ability2..4` |
| **Wildcard ability** | the Wildcard's extra starting ability, on the 2nd, 3rd or 4th slot | `Player.WildcardAbility` |
| **Ultimate** | picked in the lobby, one per role and class (never "ult" in text; the key cap keeps "ULT") | `AbilityDef.Ult`, `Slots[4]`, `KeyAction.Ultimate` |
| **Interact** | the one "use what you stand by" key (F by default): take a chest item, open a mystery door, shut a gate, open a room shop again; never "take item" or "use key" | `KeyAction.Interact` |
| **mastery** | an ability's 1-5 rank from use; **mastery 5** unlocks its capstone (never "ability level") | `Mastery.cs`, `MasteryLevel` |
| **expertise** | a class's 0-10 level from the XP of runs played as it (account-wide, cosmetic); unlocks its **weapon skins** (never "class mastery") | `Expertise.cs`, `Profile.ExpertiseLevel` |
| **weapon skin** | a look for one weapon, one equipped per weapon, earned by expertise | `WeaponSkinDef`, `Profile.SkinOf` |
| **enemy level** | an enemy's strength in a wave file (`level 3`) | `DesignWave.Level` |

## Helpers

| Term | Means | Code |
|---|---|---|
| **minion** | anything that fights on your side; "summon" is only a verb | `Minion`, `MinionKind` |
| **companion** | the minion your weapon keeps (Golem, wolves, the Nimbus...) | `Minion.Companion` |
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
| **health shrine** | the map node a room claims; one use per player: full health plus health past max | `Shrines.cs`, `NodeKind.Shrine`, `Player.OverHp` |
| **room chest** | a treasure chest a placed piece or mystery room may hold (never a vendor's room) | `RoomChests.cs` |
| **legendary card** | a boss's rare upgrade card ("legendary" is its rarity, never a noun on its own) | `Rarity.Legendary` |
| **gadget** | an item you carry (one in hand, up to 4 in the bag) and can hand over (Magnet, Lodestone, Energy Drink, Shield Generator, Medkit, Cloak, Overclock Chip, EMP, Grenade, Smoke Bomb, Gas Grenade, Incendiary, Land Mine) | `Gadget` |
| **gadget chest** | the red chest that gives every player a gadget | `ChestKind.Gadget` |
| **gadget wheel** | the wheel you hold the Gadget key (or its own Gadget wheel key) to open, to swap the gadget in hand | `OpenGadgetWheel` |
| **Energy Drink** | the gadget that makes you move 60% faster while on (60 s in all) | `GadgetId.EnergyDrink` |
| **Lodestone** | the gadget that carries you to the hub's middle after a 3 s cast (3 uses; was the Recall) | `GadgetId.Lodestone` |
| **Shield Generator** | the gadget whose dome wipes out enemy bullets round you while on (20 s in all) | `GadgetId.ShieldGenerator` |
| **Medkit** (gadget) | the gadget whose 3 uses each clear your wound and heal 30% of your max health (was the Defib Kit) | `GadgetId.Medkit` |
| **throwable** | a gadget whose tap lobs one of its 3 charges: Grenade, Smoke Bomb, Gas Grenade, Incendiary, Land Mine | `Game.Throwable` |
| **Cloak** | the gadget that hides you from enemies while on (15 s in all) | `GadgetId.Cloak` |
| **Overclock Chip** | the gadget that speeds fire rate and cooldowns 40% while on (30 s in all) | `GadgetId.OverclockChip` |
| **EMP** | the gadget whose 2 pulses wipe enemy bullets round you and stun enemies | `GadgetId.Emp` |

## Runs

| Term | Means | Code |
|---|---|---|
| **run** | one game, start to end, in any mode; never "match" | `MatchState`, `Game.State` |
| **Campaign run**, **Quick Play run**, **Boss Hunt run**, **Survival run** | a run in that mode | `GameMode` |
| **Training**, **drill** | the lobby mode of short drills, one mechanic each, started from its page; a drill's best time earns a medal (gold, silver, bronze) | `GameMode.Training`, `Training.cs`, `TrainingUi.cs` |
| **recruit**, **Training Pistol** | everyone in a drill: a plain character with no class or role look, no abilities, ultimate or perks, and the Training Pistol (a weapon in no lobby list) | `Player.Recruit`, `Game.MakeRecruit`, `RecruitLook.cs`, `WeaponId.TrainingPistol`, `Training.Pistol` |
| **Practice** | the lobby's shared room in the loadout window: the real match held between waves, PvP on, enemy spawners (never "Hangout" in text; the code still says Hangout) | `GameMode.Hangout`, `Game.Practicing`, `HangoutGame.cs`, `HangoutUi.cs` |

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
| **cell variant** | another version of a cell layout (the pit variants so far) a plain Combat cell may roll | `CellPlan.Variant`, `CellVariants.cs` |
| **pit** | a void in a cell's floor: enemies shoved in die, players lose 15% HP and climb out | `World.Pits`, `Pits.cs` |
| **shape** | the 1-4 cell polyomino: Mono, Domino, ... TetL | `PieceShape` |
| **piece card** | what sits in your hand: a shape plus its modifiers; the service ones (Shop, Black Market, Arms Dealer, Builder Shop) are one cell. "Piece" on its own is the shape on the map. | `PieceCard`, `GridPieces.cs` |
| **modifier** | something a shape piece card carries, rolled when it is offered: a floor (the room's ground), combat (enemies inside) or loot (a guaranteed chest kind) or one-way (all its doors but one one way); a card can carry several | `PieceMod`, `PieceMods.cs` |
| **placement** | one legal spot and turn for a piece card, at a door spot | `Placement`, `PlacementsFor` |
| **piece chest** | the orange chest that gives every player a piece card | `ChestKind.Piece` |
| **Builder Shop** | the only place piece cards are bought, with vectors | `BuilderShop` |
| **Black Market** | sells buildables; never "vendor" | `RoomKind.BlackMarket` |
| **card** | never the bare word for a piece card: upgrade cards and milestone cards only | |

## Bosses

| Term | Means | Code |
|---|---|---|
| **phase** | one part of a boss fight; a **phase change** moves to the next (never "stage") | the boss's `Stage`, `StageShift` (`Enemy.Phase` is per-enemy state, not a boss phase) |
| **Holding Cell** | the room off the arena the Warden's **net** locks a player in (one grid cell; three side by side); its **padlock** is out in the arena where they were netted | `HoldingCell`, `HoldingCell.cs`, `WardenState.NetId` |
