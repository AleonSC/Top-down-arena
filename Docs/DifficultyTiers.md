# Difficulty tiers: mechanics that change with difficulty

**Status: planned** (audit Nov 25, protocol 522 when written). No code yet; build one step at a time ("start on step N").

Today a difficulty only changes numbers (`Core/Difficulty.cs`: 27 knobs, 5 retired). No gameplay code reads the level,
so a harder mode can't add a boss phase, swap an enemy's behaviour or bring in an enemy type. This plan adds a
**tier** that mechanics read, and turns the places that decide bosses, enemies and waves into tables with a tier column.

## The owner's picks

1. **Custom gets its own knob**: a new `Mechanics` knob (Easy / Normal / Hard / Nightmare). Presets set it; Custom lets the host pick it.
2. **Both ways**: Hard and Nightmare can add mechanics, and Easy can remove some (e.g. a boss with no Last Stand). Normal plays exactly as today.
3. **First test case**: the Broken Bottle Rioter, which lunges like a Shiv. Hard 50% of Rioters, Nightmare 100%.
4. **Build everything**: steps 1 to 6 below.

## What the audit found

| Area | Today | Blocks |
|---|---|---|
| Difficulty | Float knobs only; Custom has no level | No "Hard or above?" check |
| Boss phases | 9 of 10 bosses copy one pattern: `XStage` enum, `const` HP thresholds (33), `EnterXStage` + `StartXTransition` (18 near-copies), chained `if (Stage == A && frac <= AAt)`. Reapers: two bodies, int change codes | Thresholds and stage count fixed at compile time |
| Boss attacks | Turn order is already a table (`XTurns[stage][step]`, `XTimes`); each turn is a `switch` case with inline counts (about 900 float literals vs about 150 consts) | "More adds on Nightmare" needs a named number per turn |
| Boss HUD | `HudBossBars.cs:271` copies the Warden's thresholds; Eclipse shows "STAGE n / 5" but has 6 stages | Two sources of truth |
| Boss tests | About 47 inline HP fractions in `Tools/SimTest/Tests` (`Bosses.cs`, `BossesLate.cs`, `BossMechanics.cs`, `Duelist.cs`); `WikiPhases.cs` text says "100–65%" | Normal must stay identical; other tiers get their own tests |
| Boss variants | `RollVariant` skips bosses; `BossAt` returns a type only; `BossWaves` escort rows have no tier | No "Nightmare Warden" |
| Enemy variants | `VariantTable` has MinWave/MaxWave, one variant (Ball and Chain) | No tier column; behaviour is fixed per type (`BrainOf(type)`) |
| Wave pool | `WavePool` and the melee list are `if (n >= 3)` lines (`EnemyAI.cs:81`, `:147`) | A hard-only enemy needs code |
| Factions | `FactionMember` has From, Weight, Debut | No tier column |
| Elites | Start wave 12 (written twice, `Variety.cs:219/221`), 2.5x health, 2 traits from wave 35, all inline; `EliteTrait` is a byte with 7 of 8 bits used | One more trait fits |
| Run modifiers | 33 `Has(RunMod...)` checks already switch mechanics | Not linked to difficulty |

The boss phase number already goes over the network as one byte (`Enemy.Phase`) and enemy variants come from the enemy's id, so new phases and variants mostly fit the current format.

## Steps

### Step 1: the tier
- `DiffKnob.Mechanics` at the end of the knob list (Count unit, 0-3; presets 0/1/2/3, not a flavour knob). Lobby slider in `DifficultyUi.cs` with the tier names.
- `enum Tier : byte { Easy, Normal, Hard, Nightmare }`, `Game.Tier` and `Game.TierAtLeast(t)`, read from the knob.
- Rows filter with `MinTier`/`MaxTier` (default Easy..Nightmare = always). One helper `TierOk(min, max)`.
- Old saves and recordings with no Mechanics value read as Normal. Protocol bump (the lobby message's difficulty gets one more value).
- Tests: a preset sets the tier; Custom keeps it; parsing old text gives Normal.

### Step 2: boss phase tables
- `BossStageDef { byte Stage; float At; Transition How (Timed, Instant, Stagger); }`, and a per-boss `XStages(Tier)` that returns the list. Normal returns today's thresholds, so nothing changes on Normal.
- One shared `TickBossStages(e, fight, defs)` replaces the chained ifs; `IBossFight` (Stage, Step, TransT, TransTo) for the 9 single-body bosses, an adapter for the Reapers' two-body health.
- New stages go at the **end** of each `XStage` enum; turn tables are indexed through the def, not `(int)Stage`.
- The HUD reads the same list: stage ticks, "n / N" and names for every boss. This also fixes Eclipse's "/ 5".
- Tests: `Game.StageAt(type, stage, tier)` replaces the inline fractions; RenderCheck IDENTICAL on Normal; one test per boss that Easy skips its Last Stand and Hard reaches its extra stage, once one exists.

### Step 3: per-tier boss rules
- `BossRules` per boss, filled at spawn from the tier: named counts and beats that are inline now (Queen drones `3 + players`, Lich graves 3 / 2 each / cap 6 and Spirits 6, Colossus meteor every 3 s, ...), plus one shared enrage multiplier on turn times.
- Boss variants: `BossAt` returns a type and a variant; `RollVariant` allows a boss variant picked by tier (not random). Escort rows get `MinTier` and an extra-escort count.
- First content: one Hard-only stage on one boss, picked with the owner.

### Step 4: tier filters on enemies and waves
- `EnemyVariantDef` gets `MinTier`/`MaxTier`, a per-tier chance (`float[] ChanceByTier`) and an optional `Brain` (an override for `BrainOf`: `BrainOf(e)` checks the variant first).
- **Broken Bottle Rioter**: `EnemyVariant.BrokenBottle` at the end; the Shiv's lunge brain with the Rioter's body; chance 0 / 0 / 50% / 100% by tier; its own look (a jagged bottle in hand). Test suite entry plus a preview.
- `WavePool` and the melee list become a table (`{ Type, From, MinTier, MaxTier }`), so a hard-only enemy is one row.
- `FactionMember`, `MutationDef` and the `BossWaves` escorts get `MinTier`/`MaxTier`.

### Step 5: elites as a table
- `EliteRules` per tier: start wave (12 on Normal), health (2.5x), traits per elite and the wave that adds a second one (35), chance curve. Normal keeps today's numbers.
- `EliteTrait` and `Enemy.Traits` go from byte to ushort (protocol bump) so new traits fit; a trait row gets `MinTier`.

### Step 6: tiers switch on modifiers
- `TierMods[tier]`: run modifiers a tier switches on by itself (shown in the lobby as locked chips). Starts empty; the owner picks which.

## Rules for every step
- Normal plays exactly as today: same rolls, same numbers, same tests.
- New enum values go at the end; bump `Session.ProtocolVersion` when the lobby message or a snapshot changes.
- README's Difficulty section lists what each tier adds or removes.
