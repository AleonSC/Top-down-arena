# Campaign: themed enemies that build up and fade (design)

Status: **idea, not built.** Nothing below is decided except what the owner said (marked *owner*). Numbers are first
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

### 1. Themed share per faction, per wave

Replace "only the next boss's faction, only in its approach" with a **share curve per faction** over the whole run.
Each wave asks every faction for a share; shares add up to the themed part of the wave (the rest stays the usual
random mix, as today).

For boss *N* (fights at wave `B`):

| Phase | Waves | Share of that faction |
|---|---|---|
| Build | `B-9 … B-1` | ramps up from ~10% to the approach share (replaces the 4-wave ramp) |
| Peak | the waves just before the fight | full approach share (85%) |
| Fade | `B+1 … B+5` | falls linearly to a **residue** (~5-10%) |
| Return | any later wave | residue, plus occasional **comeback** waves (below) |

So at any wave you see two factions overlapping: the one fading (the boss you just beat) and the one building (the
next). The overlap is the "phase out slowly, replaced by the next" feel.

### 2. Comebacks

- **Residue:** a faction keeps a small weight forever, so single members drift back in.
- **Comeback squad:** every few waves (seeded, so all players agree) one *earlier* faction's squad is added to the
  wave. Squads are the faction's existing set pieces (e.g. the Warden's wall of Riot Shields).
- Limits: at most one comeback per wave, never in the 4 waves before a boss (keep the approach clean), comeback
  members still respect their `From` wave and the bullet budget.

### 3. Boss Hunt as the enhanced faction

The hunt uses the same faction data, turned up:

| Knob | Campaign | Hunt |
|---|---|---|
| Types in the pool | those allowed by the `From` wave | **all** members from the first wave |
| Count | normal wave count | ~1.5x, rising toward the boss |
| Squads | occasional | every wave has one; tougher variants (more members, elites) |
| Share | up to 85% | 100% (as built today) |

Because the hunt reads the same `FactionDef`, adding a member to a faction improves both the campaign and the hunt.

## Build plan

1. **Share curve**: `FactionDef` gets build/fade/residue; `Approaches` returns shares for all factions on a wave
   (generalising `ShareFor`). Wave spawner picks by share. Tests in the `features` suite: shares sum, ramp and fade
   shape, determinism.
2. **Comebacks**: seeded comeback picker, with the limits above. Test: same seed gives the same comeback waves.
3. **Hunt upgrade**: hunt pool/count/squad rules from the table. Update the `hunts` suite.
4. **Setting**: keep the Difficulty tab's *Boss approach* row; add a *Campaign buildup* switch (off = today's
   approach-only waves) so the old feel stays available.
5. README, `CODEMAP.md`, preview screenshot of the wave panel chip showing two factions ("Fading · Warden / Approach
   · Hive Queen").

No network format changes expected (the wave plan is derived from the seed), so **no protocol bump**, unless the wave
panel chip needs a new snapshot field.

## Open questions

- Fade length: 5 waves, or until the next boss? Should the residue ever reach 0?
- Do comebacks scale with the original wave number or the current one? (Current, so they stay a threat.)
- Factions that don't exist yet (only the Warden and the Lich's zombies are built): the buildup needs each faction
  built first; unbuilt bosses keep the plain mix until then.
- Endless mode (`BossCycle`): do factions fade and rebuild every cycle, or stay at residue after the first lap?
- Should the hunt unlock its enhanced faction pool one member at a time as perks do?
