# Callings: design plan

Status: **planned, nothing built** (Nov 2026). Summoner first; the other classes later, each with its own Callings.
Numbers are first guesses to tune in play-testing.

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
- **Guarantee**: if a Summoner reaches level 5 with no Calling and none was offered, that level's offer swaps one
  card for a Calling (the role's Calling is twice as likely). Offered once and skipped still counts as offered, so
  you can turn Callings down.
- **Calling cards**: offered only once you hold that Calling. There are 2 tier 2 cards (Rare, 2 stacks each), plus
  1 capstone (Very Rare, 1 stack) that is offered once you hold both tier 2 cards.
- **Minion buffs**: Pack Bond and the other minion-wide buffs work on the Calling minion too.

## The three Callings (first guesses)

| Calling | Minion | Tier 2 cards | Capstone |
|---|---|---|---|
| **Spider** (Tank) | Follows you and leaps onto the enemy nearest you. Its bite (14) webs the enemy, slowing it 40% for 1.5 s | **Silk Snare**: webs also slow enemy shots that pass through them · **Thick Carapace**: +50% health, and it taunts what it bites | **Spider Queen**: every 6 s the spider lays 3 spiderlings that bite once and pop |
| **Fairy** (Healer) | Orbits the most hurt ally, healing them 4 health/s within 120 px, and is untargetable | **Pixie Dust**: its trail blinds enemies (their shots miss 30%) · **Second Wind**: it heals 50% more an ally under 30% health | **Fairy Ring**: once every 45 s, an ally who goes down stands up again at 40% health |
| **Demon** (DPS) | A hovering imp-lord that hurls fireballs (20, small burst) at the target you last hit | **Soul Feast**: each kill near it grows it +5% damage, stacking to +50% until it is hit · **Hellfire**: its fireballs leave burning ground for 2 s | **Infernal Pact**: Alt sacrifices 15% of your health to make it go berserk for 5 s (×2 fire rate) |

Each new minion gets a `MinionKind` (Spider, Fairy, Demon, in name order) and a look in `Game/` drawn with the real
draw code, with previews for the owner to choose from.

## Code plan

- `UpgradeDef` gets two new filters, checked in `Upgrades.Available`:
  - `Calling` (a `CallingId`): which Calling this card is, or which Calling it belongs to.
  - `CallingTier` (1 = the Calling itself, 2, 3 = capstone).
- `Upgrades.CallingOf(taken)` returns the run's Calling from `Player.Taken`, so no new player state goes over the wire.
- The guarantee lives in `RollOffer`, which needs to know whether a Calling has been offered. That state belongs on
  the host's `Player` (host only, nothing replicated).
- Calling minions are `Minion`s with `Companion = true`. They ride the minion snapshot as it is, so the protocol
  needs a bump only if a minion needs a new replicated field.
- New files: `Core/Callings.cs` (the `CallingId` enum, cards, minion ticks), `Game/CallingsLook.cs`, a `callings`
  test suite, and `CODEMAP.md` lines for each.

## Steps

1. **Framework**: `CallingId`, the two card filters, lock-in, the guarantee, `CallingOf`, and the `callings` test
   suite (offers, lock-in, guarantee, tier gating). Placeholder cards with no minion yet.
2. **Spider**: the minion, its 3 cards, look and preview.
3. **Fairy**: the same.
4. **Demon**: the same, and README entries for all three.
5. **Other classes**: a Calling plan per class, each needing the owner's decisions first.
