# Campaign: themed enemies that build up and fade (design)

Status: **step 1 built** (Nov 2026: `Approaches.Campaign`, the *Campaign buildup* switch in the Difficulty tab, default on, `campaign` test suite); **step 2 built** (`Approaches.Comeback`); **step 3 built** (the hunt: whole faction from wave 1, 1.2x to 1.8x enemies, a squad per wave, two from wave 6; elites still open); the HUD chip (5) is not. Numbers are first
guesses. It builds on [`Approaches.md`](Approaches.md) (boss factions, `Core/Approaches.cs`) and the Boss Hunt
([`BossHuntsAndPerks.md`](BossHuntsAndPerks.md), `Core/Hunts.cs`).

## The idea (owner)

- The campaign is a **buildup of the current boss's themed enemies**. Today a faction only shows in the four waves
  before its boss (the *approach*); this makes it a slow build that carries on past the fight.
- After a boss is defeated its enemies **phase out slowly** (not a hard cut), replaced by the **next boss's faction**.
- **Some squads or single enemies can show up again later**, so earlier worlds are never fully gone.
- **Boss Hunt = an enhanced version of the themed mobs**: more types, more of them, tougher squads, in the same world.

## Today (what exists)

| Piece | Now |
|---|---|
| Approach | waves before a boss: a themed share (default 85%, ramped 34/51/68/85%) from `FactionDef` |
| Faction | weighted members with a `From` wave, plus faction squads (`Approaches.All`) |
| After a boss | back to the plain random mix; the dead boss's faction is gone |
| Boss Hunt | ten waves of only that faction, tougher as it goes, set piece on waves 5 and 9, then the boss |

## Proposal

### 1. Every wave is 100% themed (*owner*)

No random filler: **every enemy in a campaign wave belongs to a faction.** A wave's mix is split between the factions
below; within a faction the members, `From` waves and squads work as today.

For the wave that is step `p` (1-5) of the cycle leading to boss *k*:

| Slice | Share | Notes |
|---|---|---|
| Fading (boss *k-1*) | 35 / 25 / 15 / 5 / 0 % for `p` = 1-5 | the last boss's world phases out slowly |
| Older pool | 10% from wave 11 on | members of all earlier factions, plus comeback squads (below) |
| Current (boss *k*) | the rest: 65 → 100% (55 → 90% with the pool) | builds up to the boss |

Waves 1-5 are 100% the Warden's faction. Where the pool is empty (waves 6-10) the current faction takes its 10%.
The boss wave itself is unchanged. Bosses with no faction yet keep today's random mix until their faction is built.

### 2. Comebacks

- **Pool:** the 10% older slice picks single members of earlier factions (weights as in their `FactionDef`).
- **Comeback squad (built):** one wave in every five from wave 11 (a different step each cycle, never step 5) has one
  squad from an *earlier* faction. It is picked from the wave number, so every game agrees, and **takes over that
  wave's older-pool slice** (the squad is bigger than the slice, so the extra comes off the unthemed mix). Squads are the faction's existing set pieces (e.g. the Warden's wall of Riot Shields).
- Limits: at most one comeback squad per wave, never on step 5 of a cycle (keep the approach clean), members still
  respect their `From` wave and the bullet budget.

### 3. Boss Hunt as the enhanced faction

The hunt uses the same faction data, turned up:

| Knob | Campaign | Hunt |
|---|---|---|
| Types in the pool | those allowed by the `From` wave | **all** members from the first wave |
| Count | normal wave count | 1.2x rising to 1.8x |
| Squads | occasional | every wave has one; tougher variants (more members, elites) |
| Share | 100% (current faction 55-100%, rest fading/older) | 100% of the boss's own faction only |

Because the hunt reads the same `FactionDef`, adding a member to a faction improves both the campaign and the hunt.

## Build plan

1. **Share curve**: the slice table above (fading / pool / current), no random filler; `Approaches` returns shares for all factions on a wave
   (generalising `ShareFor`). Wave spawner picks by share. Tests in the `features` suite: shares sum, ramp and fade
   shape, determinism.
2. **Comebacks**: seeded comeback picker, with the limits above. Test: same seed gives the same comeback waves.
3. **Hunt upgrade**: hunt pool/count/squad rules from the table. Update the `hunts` suite.
4. **Setting** (built with step 1): the Difficulty tab's *Campaign buildup* switch (off = the *Boss approach* row's
   approach-only waves).
5. README, `CODEMAP.md`, preview screenshot of the wave panel chip showing two factions ("Fading · Warden / Approach
   · Hive Queen").

No network format changes expected (the wave plan is derived from the seed), so **no protocol bump**, unless the wave
panel chip needs a new snapshot field.

## Open questions

- Fade length: 5 waves, or until the next boss? Should the older pool ever reach 0?
- Do comebacks scale with the original wave number or the current one? (Current, so they stay a threat.)
- Factions that don't exist yet (only the Warden and the Lich's zombies are built): the buildup needs each faction
  built first; unbuilt bosses keep the plain mix until then.
- Endless mode (`BossCycle`): do factions fade and rebuild every cycle, or stay in the older pool after the first lap?
- Should the hunt unlock its enhanced faction pool one member at a time as perks do?
