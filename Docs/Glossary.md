# Glossary

Status: agreed with the owner (Nov 19 2026). One word per thing, in code comments, docs, in-game text and requests.
Terms not settled yet are listed at the end.

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

## Upgrades

| Term | Means | Code |
|---|---|---|
| **upgrade card** | a card you choose in game | `UpgradeDef`, `Upgrades.All` |
| **upgrade** | the effect or benefit an upgrade card gives | `Stats`, `Player.Taken` |

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

## Bosses

| Term | Means | Code |
|---|---|---|
| **phase** | one part of a boss fight; a **phase change** moves to the next (never "stage") | the boss's `Stage`, `StageShift` (`Enemy.Phase` is per-enemy state, not a boss phase) |

## Not settled yet

- Ability slots: "Space ability", "4th ability", "ultimate"/"ult" (F), and "Q / E / Shift abilities".
- "level": the player's XP level, and sometimes a wave number.
- Helpers: "minion", "summon", "companion", "thrall".
- Rewards: "perk", "relic", "legendary".
