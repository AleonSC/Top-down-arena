# Upgrade pools: class and weapon cards

**Status: steps 1-9 built (Nov 2026, protocol 316).** Next: weapon cards for the other classes, one class at a time. Owner decisions are below; build one step at a time ("start on step N").

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

### Step 4: per-class card files (built, protocol 311)

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

### Step 5: fill the ability gaps (built, protocol 312)

Owner decision: the first batch gives a Rare card to every Space ability that has none, so each class has one per
Space ability like the Knight, Ninja and Blood Mage do (9 each). Style matches the existing ones (Bastion, Earthshaker):
Rare, `RequiresAbility`, `MaxStacks = 2`, two numbers per card. Each needs a `Stats` field read by the ability's code
and a sim check that the number changes. They go in the class's own list (`Game.ElementalistCards`, ...); protocol bump.

**Elementalist** (0 today → 9)

| Ability | Card | Effect (per stack) |
|---|---|---|
| Aeroaegis | Eye of the Storm | The vortex lasts 1.5 s longer and reflected bullets hit 50% harder |
| Frost Nova | Permafrost | Freezes 1 s longer and reaches 30% farther |
| Flame Wall | Inferno | The wall stands 2 s longer and burns 50% hotter |
| Healing Rain | Monsoon | The rain is 40% wider and heals 40% more |
| Energy Ward | Overcharged Ward | The bubble soaks 50% more, and gives back 25% more of what it soaked as healing (50% → 75%) |
| Lumen Ward | Dawnlight | The cone lasts 2 s longer and heals 30% more |
| Meteor | Cataclysm | Hits 50% harder and 30% wider |
| Blizzard | Whiteout | The storm is 40% bigger and lasts 2 s longer |
| Tsunami | Riptide | The wave is 40% wider and enemies stay wet 4 s longer |

**Summoner** (1 today: Soul Lantern → 9)

| Ability | Card | Effect (per stack) |
|---|---|---|
| Golem Wall | Basalt Hide | Golems have 50% more health |
| Earthquake | Deep Tremor | The Burrower tunnels 1.5 s longer and hits 50% harder |
| Force Pact | Kindred Bond | Your pact enemy has 50% more health and comes back 5 s sooner |
| Spirit Swarm | Sprite Choir | Sprites heal 40% more and last 2 s longer |
| Phoenix | Rebirth Flame | The phoenix stays 4 s longer, and the people it revives get 25% more health |
| Bat Swarm | Blood Bats | Bats bite 40% harder and hunt 2 s longer |
| Hydra | Venom Glands | The hydra's venom slows its target 30% for 1.5 s (60% with two), and the hydra stays 2 s longer |
| Alpha Call | Pack Leader | Blood Scent lasts 3 s longer and your minions start it 1 stack higher (up to one below a lunge) |

**Soldier** (8 today → 9)

| Ability | Card | Effect (per stack) |
|---|---|---|
| Medkit | Field Surgeon | 3 more orbs, and each heals 5 more |

Effect of the batch on the pool: an Elementalist's Rare pool goes from 8 general cards to 9 (8 + its own one); a card
for your Space ability is then about 1 in 9 of your Rare cards, the same as for the other classes.

Owner change (Nov 2026): Fourth Head, and the extra sprites and bats, repeated what mastering those abilities already
gives (a fourth head, 8 sprites, 14 bats), so they became Venom Glands, healing sprites and harder-biting bats.

Built: the cards sit in `Game.ElementalistCards`, `Game.SummonerCards` and `Game.SoldierCards`; their numbers are
`Stats` fields (`Upgrades.cs`, "Elementalist abilities" / "Summoner abilities") read where each ability is cast
(`Minions.cs` `KitAbility`, `LumenWard.cs`, `Tsunami.cs` (each wave carries its `HalfWidth`), `Burrower.cs`,
`ForcePact.cs` (`PactRespawn`), `Hydra.cs` (venom bolts carry `SlowAmt`), `Medkit.cs`, `EnergyPayback`,
`ScentStartFor`). A golem's health bar now reads against its own `MaxHp`, so Basalt Hide's extra health shows.
Test: suite `abilitycards` (`Tools/SimTest/Tests/AbilityCards.cs`).

After this batch: one card per class (`Kit`), then weapon cards class by class.

### Step 6: one card per class (built, protocol 313)

One `Kit` card per class, offered to every player of that class (a Wildcard: the classes its gear comes from). Rare,
`MaxStacks = 2`. Each leans on what the class already is (its lobby blurb) and doesn't repeat a mastery or a general card.

| Class | Card | Effect (per stack) | Why |
|---|---|---|---|
| Soldier | Quartermaster | Health orbs and medkit orbs you pick up heal 30% more, and your Q/E/Shift abilities recharge 10% faster | Guns and gear: the class that lives off pickups and kit |
| Elementalist | Arcane Surge | Casting your Space ability makes your weapon fire 25% faster for 3 s | Spells feed the staff |
| Summoner | Pack Bond | Every minion of yours (weapon and ability) takes 25% more before it breaks or falls, and hits 15% harder | The class is its minions |
| Knight | Plate Armour | Take 8% less damage, and your swings cut bullets out of the air 20% farther out | Armoured melee that cuts bullets |
| Ninja | Ghost Step | +6% move speed, and for 1 s after you dodge you take 30% less damage (a second copy: 2 s) | Fast, slippery |
| Blood Mage | Bloodthirst | Abilities cost 20% less health, and each kill heals you 2 | Health is its resource |

Built (owner approved all six): each sits in its class's list with `Kit` set. `Stats`: `OrbHealMult`, `BonusCdMult`
(`Game.Cooldown`, slots 1-3), `SurgeRate` (`Player.SurgeT`, set in `CastAbility` on a Space cast, read by
`FireRate`, sent in the snapshot so your own firing speeds up), `MinionDmgMult` / `MinionToughMult` (`SpawnMinion`:
`Power` and `BlockHp`, a staggered minion's recovery, thralls' health), `CutReachMult` (`Knight.cs` bullet cutting),
`GhostCut` / `GhostTime` (`Player.GhostT`, set on a dodge in `Dodge.cs`, read in `Damage`), `HealthCostMult`
(`PayHealth`) plus `KillHeal`. Test: suite `abilitycards`, its class card checks.

### Step 7: Knight weapon cards (built, protocol 314)

One Rare card per Knight weapon (`Weapons = { that weapon }`, `MaxStacks = 2`), offered only while you hold it. Each
sharpens what the weapon already does and doesn't repeat a general card (damage, swing speed, reach via Piercing).

| Weapon | Card | Effect (per stack) |
|---|---|---|
| Warhammer | Quake Hammer | The slam circle is 20% wider and stuns 0.3 s longer |
| Lance | Breacher | Breaching Thrust shoves 30% farther, and Impact damage is 50% higher |
| Halberd | Long Haft | The blade reaches 15% farther and sweeps 20° wider |
| Quarterstaff | Pilgrim's Staff | Each hit heals 1 more, and you can store 1 more Jab stack |
| Lightbringer | Daybreak | The light gauge fills 30% faster and the Sunlance heals 30% more |
| Censer | Thick Incense | Incense clouds are 30% bigger and last 2 s longer |
| Twin Blades | Whirling Sabres | The thrown sabres fly 25% farther and hit 30% harder |
| Greatsword | Tempered Blade | Rend hits 30% harder, and guarding builds Temper 25% faster |
| Rapier | Duelist's Edge | Each thrust skewers 1 more bullet, and Tempo holds 0.5 s longer |
| Arc Knuckles | Live Wire | The Rocket Dash reaches 25% farther, and the Twin Hammer stuns 0.3 s longer |

Built (owner approved all ten): in `Game.KnightCards`, each with `Weapons = { its weapon }`. Hooks: `Game.HammerR`
(the slam and its charge outline, `WorldShots.cs`), the Breaching Thrust's `Knock` and `LanceBump` / `LanceWallImpact`,
the Halberd's reach and `sweep`, the Quarterstaff's `HealPerHit` and `StaffCap`, `LightLanded` (`Player.LightCarry`
keeps the fraction) and `LandSunlance`, `AddZone` for every `ZoneKind.Incense` cloud, `HostTwinFang` and `FangHurt`,
the guard's Rend and `GreatGuardBlock` (`Player.TemperCarry`), `TickRapier`'s grace and the Rapier's skewer cap,
`KnuckleLockTarget` / `HostRocketDash` (and the reticle in `ArcKnucklesLook.cs`) and the Twin Hammer's stun.
Test: suite `abilitycards`, its Knight weapon checks.

### Step 8: Ninja weapon cards (built, protocol 315)

One Rare card per Ninja weapon (`Weapons = { that weapon }`, `MaxStacks = 2`), offered only while you hold it.

| Weapon | Card | Effect (per stack) |
|---|---|---|
| Kusarigama | Heavy Weight | The thrown weight flies 25% farther and stuns 0.4 s longer |
| Bo Staff | Iron Bo | The staff shove knocks 40% farther, and the full spin hits 30% harder |
| Fuma Shuriken | Long Loop | The thrown loop reaches 20% farther, and the Orbit circle is 20% wider |
| Medicine Darts | Potent Dose | Each dart's poison and healing stack is 30% stronger |
| Spirit Fan | Gale Fan | The gust reaches 25% farther and heals 30% more |
| Ofuda | Blessed Paper | Charm bursts are 25% wider and heal 30% more |
| Shuriken | Barbed Stars | Stuck stars come back 1.5 s sooner, and their return hit is 40% harder |
| Katana | Honed Edge | The Momentum Burst shoves 30% farther, and caltrops last 2 s longer |
| Kunai | Shadow Mark | The blink's hit deals 40% more, and marks last 2 s longer |

Built (owner approved all nine): in `Game.NinjaCards`. Hooks: `StartKusariThrow` (reach) and `KusariImpact` (stun),
the Bo Staff's shove push (`BoStaff.cs`) and full spin, the Fuma throw's starting speed (the oval scales with it) and
`Game.FumaR` (the Orbit circle, Reversal ring and its look in `FumaLook.cs`), `DoseAlly` / `PoisonHit`
(`DoseMultOf`), the Spirit Fan's reach and `HealAlly`, `BurstWard` (radius, heal reach, heal), `StarStuck` /
`ReturnStar`, the Momentum Burst's push and the tossed caltrops' life, `ShadowKunaiLanded` and `NinjaHitMult`
(`Player.BlinkBoost`, set by the blink and spent with the ambush). Test: suite `abilitycards`, its Ninja checks.

### Step 9: Blood Mage weapon cards (built, protocol 316)

One Rare card per Blood Mage weapon (`Weapons = { that weapon }`, `MaxStacks = 2`), offered only while you hold it.

| Weapon | Card | Effect (per stack) |
|---|---|---|
| Blood Lash | Crimson Whip | The whip reaches 20% farther, and each Clot stack cuts 1% more damage |
| Clot Cannon | Deep Pool | The pool it leaves is 30% bigger and lasts 2 s longer |
| Leech Tendrils | Hungry Tendrils | The tendrils drain 30% faster, and 15% more of the drain heals you (25% → 40%) |
| Transfusion Darts | Generous Blood | Each dart gives an ally 30% more health for the same cost |
| Sanguine Orb | Rich Blood | The heal reserve fills 30% faster, and the healing coat heals 30% more |
| Vein Tether | Lifeline | 15% more of the beam's damage heals (40% → 55%) |
| Blood Bolts | Hemophilia | Hemorrhage takes one bolt fewer (5 → 4), and its burst is 30% wider |
| Implosion | Event Horizon | Pulls enemies in from 30% farther, and the crush hits 40% harder |
| Bone Spear | Marrow | The spear pierces 1 more enemy, and picking it up gives 3 more health |

Built (owner approved all nine): in `Game.HemomancerCards`. Hooks (all in `BloodMage.cs`): the lash's reach and
`ClotCut`, the clot's `Pool` / `PoolTime`, the grasp's `Dmg` and `LeechSteal`, the dart's `HealAlly`, `ReserveGain`
and the coat's healing, `VeinSteal`, `Hemorrhage` (hits needed, radius), `Collapse` (the crush and its pull radius),
the spear's `Pierce` and `TickSpearPickups`. Test: suite `abilitycards`, its Blood Mage checks.
