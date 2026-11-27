# Callings: design plan

Status: **Summoner Callings built** (Nov 2026: steps 1-4, `Core/Callings.cs`, `Game/CallingsLook.cs`, `callings` test suite).
Not built: step 5, Callings for the other classes. Numbers are first guesses to tune in play-testing.

## The idea

A **Calling** is an upgrade card that commits your run to one path. For the Summoner, each Calling summons a new
minion that stays with you all run. Once you pick a Calling, the other Callings stop being offered, and that minion's own
cards (its **Calling cards**) join your card pool. So two Summoner runs with the same weapon and abilities can still
play differently, depending on which Calling the run offered and which one you took.

```
Calling offered (random, at least one by level 5)
   └─ pick Spider ─▶ other Callings leave the pool ─▶ Spider cards drawn as normal (tier 2, tier 3)
                                                        └─ capstone (Very Rare) once both tier 2 cards are taken
```

## Decisions (owner)

| Question | Decision |
|---|---|
| Scope of the first step | The Summoner only; other classes get Callings later |
| How Callings are offered | In normal card draws, plus a guaranteed one by about level 5 |
| How many | 3, one per role: Spider (Tank), Fairy (Healer), Demon (DPS) |
| Name | **Calling** (the Glossary's word; "path" already means the route between bosses) |

## Rules

- **The Calling card**: Rare, 1 stack, `Kit = Summoner`. Any Summoner gets all three, whatever their role. The role
  only sets which Calling is drawn more often (weight 2 for your role's Calling, 1 for the other two).
- **Lock-in**: once you take a Calling, no other Calling is offered for the rest of the run. Wildcards offered
  Summoner cards follow the same rule.
- **Guarantee**: a Summoner's level-5 offer with no Calling yet swaps its last card for a Calling (the role's
  Calling is twice as likely). Only level 5: skip it and Callings come up in normal draws again.
- **Calling cards**: offered only once you hold that Calling. There are 2 tier 2 cards (Rare, 2 stacks each), plus
  1 capstone (Very Rare, 1 stack) that is offered once you hold both tier 2 cards.
- **Minion buffs**: Pack Bond and the other minion-wide buffs work on the Calling minion too.

## The three Callings (first guesses)

| Calling | Minion | Tier 2 cards | Capstone |
|---|---|---|---|
| **Spider** (Tank) | Fights at your side, leaping onto enemies within 260 px of you. Its bite (14 every 0.8 s) webs the enemy, slowing it 40% for 1.5 s. 120 health + 12 a wave, blocks bullets, back 8 s after it falls | **Silk Snare**: webs hold 1 s longer and slow 15% more · **Thick Carapace**: +50% health, back 2 s sooner | **Spider Queen**: every 6 s the spider lays 3 spiderlings that bite once and are gone |
| **Fairy** (Healer) | Orbits the most hurt ally, healing them 4 health/s within 120 px, and is untargetable | **Pixie Dust**: enemies within 90 px of the ally it tends burn for 10/s · **Second Wind**: heals 30% more, and 50% more again an ally under 30% health | **Fairy Ring**: once every 45 s it flies to a downed ally and stands them up at 40% health |
| **Demon** (DPS) | Hovers at your shoulder hurling fireballs (20 every 1.2 s, 46 px burst) at the nearest enemy within 460 px; untargetable | **Soul Feast**: each kill within 300 px grows it +5%, up to +40%, fading 6 s after its last kill · **Hellfire**: its fireballs leave burning ground for 2 s | **Infernal Pact**: every 12 s it goes berserk for 4 s (×2 throw rate, 50% wider bursts) |

Changed from the first plan while building: Silk Snare slows enemies rather than their shots, Thick Carapace comes back
sooner instead of taunting, Pixie Dust burns instead of blinding, Soul Feast fades with time (the demon can't be hit),
and Infernal Pact is a timer rather than an Alt (Alt belongs to the weapons).

Each new minion gets a `MinionKind` (Spider, Fairy, Demon, in name order) and a look in `Game/` drawn with the real
draw code, with previews for the owner to choose from.

## Code plan

- `UpgradeDef` has two filters, checked in `Upgrades.Available`:
  - `Calling` (a `CallingId`): which Calling this card is, or which Calling it belongs to.
  - `CallingTier` (1 = the Calling itself, 2, 3 = capstone).
- `Upgrades.CallingOf(taken)` returns the run's Calling from `Player.Taken`, so no new player state goes over the wire.
- The guarantee lives in `RollOffer` (`WithCalling`): only at level 5, so no extra state.
- Calling minions are `Minion`s with `Companion = true`. They ride the minion snapshot as it is, so the protocol
  needs a bump only if a minion needs a new replicated field.
- Files: `Core/Callings.cs` (the `CallingId` enum, minion ticks; the cards are in `SummonerCards`, Summoner.cs),
  `Game/CallingsLook.cs`, `Tools/Preview/ShotCallings.cs` and the `callings` test suite.

## Steps

1. **Framework** (built): `CallingId`, the two card filters, lock-in, the guarantee, `CallingOf`, and the `callings` test
   suite (offers, lock-in, guarantee, tier gating). Placeholder cards with no minion yet.
2. **Spider** (built): the minion, its 3 cards, look and preview.
3. **Fairy** (built): the same.
4. **Demon** (built): the same, and README entries for all three.
5. **Other classes**: a Calling plan per class, each needing the owner's decisions first.
