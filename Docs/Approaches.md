# Boss approaches: design plan

Status: **step 1 built** (the framework and the Warden's faction, November 2026); the **Lich's faction is built too, out of order, as zombies** (below); the rest planned. Decisions below were made with the owner; numbers are first
guesses to tune in play-testing and the Boss Lab.

> **Campaign update:** the Campaign is now 100 waves with a boss after every **10th** (Docs/RunLength.md), so an approach
> is **ten waves**, not four. "Waves 1-4" and the share/ramp numbers below describe the old 50-wave track; the Campaign
> uses the rolling introduction in "Campaign: ten waves per boss" (proposed, replaces the share/ramp there).

## The idea

The bosses always come in the same order (Warden after wave 5, Hive Queen after 10, ... Eclipse after 50, each its own fight between waves). The five regular waves
before each boss become its **approach**: part of each wave is the boss's own kind of enemy (prisoners and guards
before the Warden, bees before the Hive Queen...). You meet the boss's world, and learn its enemies, on the way to
it. The rest of each wave stays the usual random mix, so runs keep their variety.

How much of an approach is themed is a **difficulty setting**, so the owner can try a light blend and fully themed
waves in play and keep whichever feels better (or leave it to each lobby).

## Decisions (owner)

| Question | Decision |
|---|---|
| Redo waves, or a separate mode? | **Neither, first:** approaches are a layer on the existing 50-wave run. A Chapters mode (below) may come later and reuse them. |
| Blend or fully themed? | **Undecided: a setting.** The lobby's Difficulty tab has Boss approach and Approach ramp, with Blend / Fully themed buttons. They're flavour: the same on every preset, and changing them doesn't make the difficulty Custom. |
| Does the approach change the boss fight? | **No.** A wave only ends when everything in it is dead, so there's nothing to reward; the approach just teaches the boss's enemies. |
| The Warden's searchlight is too easy (it follows at 150 px/s, slower than every player) | **Riot Shield Guards** (below) come into its turn and bash you toward the light. The light does **not** get faster. A change to the Warden fight itself, not tied to the approach setting. |

## Flow

1. Waves 1-4 are the Warden's approach, 6-9 the Hive Queen's, ... 46-49 the Eclipse's. In endless mode the bosses
   come round again (`BossCycle`) and each one's approach comes with it.
2. Each approach wave: `count` is worked out as today (wave, party size, the Enemy count setting). The themed share
   of it (below) comes from the boss's **faction**; the rest is built exactly as today (squads, melee share, the
   unlocked pool).
3. The wave before the boss shows a small banner as it starts: "The Warden is near" (the boss's name), and the wave
   panel's chip says "Approach · The Warden".
4. The boss fight itself is unchanged. (Steps 1-4 are waves 1-4 of a cycle; wave 5 is step 4 again and brings the set piece.)

## The setting: Boss approach

Two new knobs in `Difficulty.cs` (shown together as one row in the Difficulty tab):

| Knob | What it sets | Range | Presets (Easy / Normal / Hard / Nightmare) |
|---|---|---|---|
| **Approach share** | the themed share of the **last** wave before the boss | 0-100% (0 = off, today's waves) | 85% on all four (was 60%: the owner wanted more prison enemies) |
| **Approach ramp** | on: the four waves rise to it (25%, 50%, 75%, 100% of the share); off: every wave uses the full share | on / off | on |

So **Blend** = 85% with the ramp (40%, 60%, 80%, 100% of it: 34%, 51%, 68%, 85%) and **Fully themed** = 100% without it (every approach wave is
all faction). The presets don't differ by difficulty: this is flavour, not challenge. The tab could offer the two as
quick buttons ("Blend" / "Fully themed") beside the sliders.

## Factions

A faction is the enemies that belong to a boss's world, with a weight each, the wave they're allowed from (some
are too tough for wave 1), and optional faction squads (which count toward the themed share). They are regular
enemies: XP, score, drops, elites (from wave 12), bullet budget and Practice Range like any other.

Legend: **ready** works as a regular enemy today · **fix** exists, needs a small change to work with no boss ·
**rework** exists but means nothing without its boss, needs new logic · **new** a new enemy type · **regular** an
existing regular enemy that fits the theme (a higher weight during the approach).

| Boss (waves) | Faction | Notes |
|---|---|---|
| **The Warden** (1-4) | **Rioter** (ready) · **Guard** (ready) · **Riot Shield Guard** (new, below) · **Gas Grenadier** (new) | Rioters already chase players when there's no Warden; Guards are plain fast chasers (their baton only hits rioters inside the boss fight). Riot Shield Guards are the front line: shield walls you go round or break. Gas Grenadier: lobs tear-gas canisters that leave a short gas patch (the Warden's `PathPatch` gas), so you learn to step out of gas. Squads: "Breakout" (6 Rioters with a Guard behind) and "Cell block" (2 Riot Shield Guards with Rioters pouring past them). All weak enough for waves 1-4. |
| **Hive Queen** (6-9) | **Hive Bomber** (fix) · **Hive Egg** (rework) · **Nectar Spitter** (new) · Shard (regular) | Bombers need a target: launch them at a player's position when they spawn (`LaunchBomber` takes any launcher), or they blow up where they appear. Eggs hatch into two Shards on their own timer (5 s), so break them first. Nectar Spitter sprays honey that leaves slowing patches (the Queen's `PatchKind.Honey`). Her "drones" are Shards already. |
| **Twin Reapers** (11-14) | **Bound Wraiths** (new, always a pair) · **Scythe Wraith** (new) · **BUILT** (Nov 2026) as 8: also Wisp (Swarmer), Acolyte (Swordsman with a sickle), Lamplighter, Mourner (new), Gravedigger (Brute), Shade (Phantom); squads Vigil, Procession, Twin graves, Harvest; finale Dusk and Dawn (`ReaperFaction.cs`, `Docs/Mockups/reaper-faction.html`) | Bound Wraiths: two wraiths joined by a harmful tether that sweeps as they circle; kill one and the other revives it after 3 s unless both are dead (the Soul Bond, small). Scythe Wraith: marks a player, then dashes and swings along the mark (the Sundered reaps). Reaper Pillars don't work without the fusion: left out. |
| **Singularity** (16-19) | **Gravity Well** (ready) · **Cage Well** (rework) · **Slingshot** (new) | Gravity Wells already bend shots and drift players with no boss; at most one per wave (they're 260 health). Cage Wells: a trio that rings a player in a shrinking cage (move the cage tick out of the boss-only `TickSingularityHazards`). Slingshot: fires orbs that curve round it and fling out. **Not** the Singularity Shard: it can't be killed without its core, so the wave would never end. |
| **Colossus** (21-24) | **The Quarry, BUILT** (all eight, squads, the Avalanche, the Long Campaign's ten lessons; owner's call, Nov 2026; section below, mockup `Docs/Mockups/colossus-faction.html`): eight enemies, one per Warden enemy: Rubble, Quarryman, Stomper, Driller, Slab Bearer, Rock Hurler, Geomancer, Cinder Priest | Crew (like the guards) and stoneborn (like the prisoners). Teaches push-ring gaps, cracks, rubble cover and lava before the fight. |
| **Duelist** (26-29) | **The Masked Court, BUILT** (all four steps: the Ring of Honour set piece and the Long Campaign's ten lessons too; all eight: Fencer, Blade Dancer, Parrier, Second, Marshal, Gallant, Harlequin, Mummer; squads Salon, Seconds, Guard line and Masked ball; `CourtFaction.cs`; it replaces the basic placeholder faction). Mockup (Nov 2026, `Docs/Mockups/duelist-faction.html`): the Masked Court, 8 members (Fencer, Blade Dancer, Parrier, Marshal, Gallant, Second, Harlequin, Mummer), squads Salon, Seconds, Guard line, Masked ball, set piece Ring of Honour. Steps: 1 Fencer, Blade Dancer, Parrier (built); 2 Second, Marshal, Gallant (built); 3 Harlequin, Mummer (built); 4 the Ring of Honour and the ten lessons (built). *Earlier plan:* **Blade Dancer** (new) · **Decoy** (rework) · Shielder, Reaver (regular) | Blade Dancer: telegraphed lunges that leave a hanging blade firing sideways; when hit it can blink away and leave a Decoy. The Decoy with no Duelist is a timed bomb today; it needs to give credit and look like a Blade Dancer, not the boss. Shielders (front guard) and Reavers (cone then rake) already duel. |
| **Lich** (31-34) | **BUILT, as zombies, all eight** (owner's call, Nov 2026; `Docs/Mockups/lich-faction.html`): the seven Zombie Infection kinds and the new **Grave Caller** (Riot Shield's slot; owner picked it over a Phylactery Bearer, Bone Binder and Wailing Spirit): Shamblers first, then Runners (packs of three) and Spitters (wave 32), Crawlers, Bloaters, Shriekers (33), Menders, Grave Callers (34); squads Graveyard shift, Grave rush, Gravediggers, Plague cart, Wake, Wake the dead; wave 34 always brings the Dead march (now with a Shrieker and two Callers); ten Campaign lessons. No rising outside the modifier; the Lich fight is untouched (the owner will redesign it). *Original plan, now dropped:* **Skeleton** (new, from the Grunt) · **Grave Priest** (new) · **Wailing Spirit** (new) | Today the Lich's dead are plain Grunts; a Skeleton type looks the part and climbs out of a grave. Grave Priest: raises Skeletons from graves (at most 4 alive per priest; kill the priest and the graves close), a Nest with a theme. Wailing Spirit: drifts in, wails, then dives at you (the Lich's spirits). Phylacteries mean nothing without the Lich: left out. |
| **Storm Herald** (36-39) | **Ball Lightning** (fix) · **Lightning Rod** (new, in pairs) · **Stormcaller** (new) | Ball Lightning works with no Herald; fix that slows and freezes don't touch it and that its pop shots are marked as boss shots. Lightning Rods: two posts with an arc between them, like the Stitcher's thread. Stormcaller: telegraphs strikes under you. |
| **Devourer** (41-44) | **The Larder, steps 1-2 BUILT** (owner's call, Nov 2026; section below, mockup `Docs/Mockups/devourer-faction.html`): eight enemies, one per Warden enemy: Gnawling, Butcher, Gulper, Gutworm, Carcass Bearer, Bile Spitter, Hook Thrower, Maw Priest | Brood (its spawn) and Larder (the butchers who feed it). Each teaches a turn of the fight: morsels, Snap, Inhale, Burrow, Bile, the Maw. Replaces the earlier Bile Spitter / Burrower / Swarmer plan. |
| **Eclipse** (46-49) | **Penumbral Court** (steps 1-3 built, `Docs/EclipseFaction.md`), with **Echoes** (one or two members from each earlier faction) as one squad · Phantom, Hexer (regular) | The Eclipse's own Echoes stage borrows earlier bosses' turns, so its approach does the same with their factions: the run's greatest hits. It already summons Phantoms and Hexers. No new enemies. |

In all: **5 existing enemies** to use or fix, **3 to rework** and **16 new ones**, spread over the build order below so
each boss can be played as soon as its faction is done.

## The Riot Shield Guard (owner's idea)

A guard behind a tall riot shield who **bashes you around**. In the approach waves and in the Warden's searchlight turn.

- **Shield:** blocks shots and swings from the front (like the Shielder, whose shield code it reuses: it wears down
  in four crack stages, then shatters). It's **breakable but slow to break**: about 4x the guard's own health
  (first guess: shield 240, guard 60 at wave 1, both growing with the wave like any enemy). Flanking still works:
  hits from the sides and back skip the shield.
- **Once the shield breaks, it dies fast:** the guard is staggered for a second, drops to a fragile body (60
  health, takes +50% damage), and only flails with its baton.
- **Bash:** within about 70 px the shield flashes for 0.4 s, then it lunges a short way. A hit does about 10 damage and
  knocks you **about 160 px** in the direction it faces. A player can't be bashed again for 0.8 s, so two guards bounce
  you around without locking you down. After a bash it holds its shield low for about a second: hits from the front
  land then.
- **In the Warden fight:** during the searchlight turn he sends in **two** Riot Shield Guards. Each one works its way
  round to your side away from the light, so its bash knocks you into it. Outrunning the light is no longer enough;
  you have to watch the guards too. Caught means the stun shots and the shackle ring, as now.
- **Engineering:** players move on their own game, so the knockback is applied there, like touching a boss
  (`shoveDir`/`shoveT` in `LocalControl`) and the Warden's gates (`WardenGatePush`): each game sees the guard's bash
  (its phase and facing are already in the snapshot) and shoves its own player; the host deals the damage. Bots get a
  rule: keep out of the front of a winding-up shield.

## The Quarry: the Colossus's faction (built; the fight hooks below are still a proposal)

Mockup: `Docs/Mockups/colossus-faction.html`. Eight enemies, each mirroring a Warden enemy's job so the lessons carry
over. **Crew** (quarry workers and cultists who woke it; squads with the crew) and **stoneborn** (chunks of the
Colossus; squads with the stone). Numbers are first guesses at wave 21, growing like any enemy.

| Enemy | Side | From | Mirrors | What it does |
|---|---|---|---|---|
| **Rubble** | stone | 21 | Rioter | Rolls at you in pairs, weaving (contact 10, XP 7) |
| **Quarryman** | crew | 21 | Guard | Hard hat, headlamp beam ahead, pickaxe; straight at you (contact 14, XP 15) |
| **Stomper** | stone | 22 | Thug | 180 health, contact 20. Every 4 s lifts a foot (0.8 s) and stomps a small push-block ring (the Colossus's ring code, small) with one green ~100 px gap beside you |
| **Driller** | crew | 23 | Shiv | Circles close, shows the lunge lane, drills 200 px (26); the crack it leaves erupts 0.4 s later (12) |
| **Slab Bearer** | crew | 22 | Riot Shield | Riot Shield logic with a stone slab (4x health, bash 10, 160 px knockback); a broken slab drops as 3 s of cover that blocks both sides; the bearer staggers and takes +50% |
| **Rock Hurler** | stone | 22 | Gas Grenadier | Lobs a boulder at your lead every 3.6 s (ring fills over the wind-up); lands for 18, 6 shards, rubble pile for 2 s that stops shots |
| **Geomancer** | crew | 23 | Taser Guard (moves) | Holds 320 px out, plants its staff 0.6 s, sends a crack at where you stood; glows, then erupts (12) (the Colossus's Fissure, small) |
| **Cinder Priest** | crew | 23 | Taser Guard | Charges 0.7 s on a shooting line, one magma bolt (240 px/s, 9, parryable); wherever it stops (hit or miss) it leaves a 2.5 s lava pool (20/s) instead of the planned burn |

**Squads:** Landslip (six Rubble, a Quarryman; mirrors Breakout), Dig crew (two Rock Hurlers behind two Quarrymen;
Patrol), Quarry (a Stomper, three Rubble, a Driller; Yard), Wall-in (two Slab Bearers, Rubble pouring past; Cell
block), Fault line (three Geomancers spread wide so their cracks cross; new). **Last wave: Avalanche** (mirrors the
Riot Line): four Slab Bearers as a wall, two Stompers behind, two Rock Hurlers lobbing over; within ~190 px or half
the slabs broken, the Stompers break out and stomp.

**In the Colossus fight (proposal, not decided):** Crumbling's Charge turn sends two Slab Bearers (a broken slab is a
fifth cover plate); Molten Core's Cave-in sends two Cinder Priests through the side doors; in the last stand two Rubble
roll off each stomp (at most six up).

**Build order:** (1) ✅ Rubble, Quarryman (kin in `HiveKin.cs`'s table), Stomper (`QuarryFaction.cs`; a stand-in **Rockfall** squad, a Stomper and three Rubble, until the Driller makes it Quarry); (2) ✅ Slab Bearer (Riot Shield kin; its slab drops as a `PatchKind.Slab`),
Rock Hurler (Gas Grenadier kin, every 3.6 s; the pile is a `PatchKind.Rubble`); (3) ✅ Driller (Shiv kin), Geomancer and Cinder Priest (Taser Guard kin: the Geomancer was planned as a Security Guard mirror, but the Taser Guard's charge-then-one-shot fits a crack; the Cinder Priest comes from wave 23, `From = 3` like every faction's last members); the Quarry and Fault line squads replace the stand-in Rockfall; (4) ✅ the Avalanche (the Riot line formation now takes any Riot Shield kin as its wall) and the Long Campaign's
ten lessons (waves 41-50, the Warden's order; the Driller is starred in the recap wave). New `EnemyType`s at the end, protocol bump per step.

## The Larder: the Devourer's faction (steps 1-2 built; steps 3-4 planned)

Mockup: `Docs/Mockups/devourer-faction.html` (https://claude.ai/artifact/UDdBdEF8Pfp4rsrVQAbPTw). Eight enemies, each
mirroring a Warden enemy's job like the Quarry. **Brood** (the Devourer's spawn: mouths, no eyes; squads with the
brood) and **Larder** (hooded butchers who keep it fed; squads with the butchers). Each teaches a Devourer turn.
Numbers are first guesses at wave 41. **Gutworm**, not Burrower: that name is the Earthquake minion (`Burrower.cs`).

| Enemy | Side | From | Mirrors | What it does | Teaches |
|---|---|---|---|---|---|
| **Gnawling** | brood | 41 | Rioter | 24 HP; weaves at you in pairs (contact 10, XP 7); runs for any morsel on the floor and grows when it eats one (+50% health) | Hungry's morsels |
| **Butcher** | larder | 41 | Guard | 60 HP; straight at you; within 70 px chops a 110 px half-circle in front after a 0.4 s wind-up (14, XP 15) | Snap's bite |
| **Gulper** | brood | 42 | Thug | 180 HP, contact 20; every 4 s its cheeks swell 0.6 s, then it inhales a 70° cone 200 px long for 1 s (pull 120 px/s, bite 20 at the mouth) | Inhale |
| **Gutworm** | brood | 43 | Shiv | 70 HP; dives (can't be hurt), a ripple chases you 1.8 s at 110 px/s, stops, marks 50 px for 0.6 s, erupts (16) with a ring of 8 | Burrow |
| **Carcass Bearer** | larder | 42 | Riot Shield | Riot Shield logic with a carcass slab (4x health, bash 10); a broken slab drops a morsel (60 HP) the brood eats to grow; the bearer staggers and takes +50% | Hungry's morsels |
| **Bile Spitter** | brood | 42 | Gas Grenadier | 50 HP; holds 300 px out, every 3.6 s (the Gas Grenadier's) lobs a glob at your lead (spot marked in flight); a 70 px acid pool for 4 s, 10/s (the Devourer's `BileShot`, small) | Bile |
| **Hook Thrower** | larder | 43 | Taser Guard | 55 HP; shows a line for 0.7 s, throws a hook down it (500 px/s); a hit does 8 and reels you 90 px toward it | Snap / Frenzy lunge lines |
| **Maw Priest** | larder | 43 | Taser Guard (moves) | 90 HP; a 90° maw in front: shots into it heal it for half, ×1.25 from behind; turns at 30°/s; a fan of 3 every 2 s | the Maw (Ravenous) |

**Squads:** Feeding Frenzy (six Gnawlings and a Butcher; mirrors Breakout), Meat Hooks (two Hook Throwers behind two
Butchers; Patrol), Gullet (a Gulper, three Gnawlings, a Gutworm; Yard), Larder Wall (two Carcass Bearers, Gnawlings
pouring past; Cell block), Bile Rain (three Bile Spitters spread wide so their pools overlap; new). **Last wave: the
Feast** (mirrors the Riot Line): four Carcass Bearers as a wall, two Gulpers behind, two Bile Spitters lobbing over;
within ~190 px or half the slabs broken, the Gulpers push through the gaps and inhale.

**In the Devourer fight (proposal, not decided):** Hungry: two Gnawlings crawl out with the morsels and race it to
them; Inhale: Gnawlings in the cone are pulled in too and heal it 1% each; Ravenous: a Maw Priest at each side door.

**Build order:** (1) ✅ Gnawling, Butcher, Gulper (`LarderFaction.cs`, kin in `HiveKin.cs`'s table; the Gulper's pull goes through `DevourPull`; a stand-in **Gullet** squad of a Gulper and three Gnawlings until the Gutworm joins; the Gnawling's morsel eating waits for step 2's morsels; protocol 333 with step 2); (2) ✅ Carcass Bearer (Riot Shield kin; the slab drops a
morsel), Bile Spitter (Gas Grenadier kin, the Devourer's `PatchKind.Bile`), Gnawlings eat morsels, the Larder Wall squad; (3) Gutworm, Hook Thrower, Maw Priest, and the squads;
(4) the Feast and the Long Campaign's lessons. New `EnemyType`s at the end, protocol bump per step.

## With paths

Paths and approaches both live in the four waves before a boss, and they stack:

- **Floors, arena rules and twists** (Burning Grounds, Fogbound, Narrow Pass, Frozen Lake, Blood Moon, Sanctuary,
  Bazaar): no conflict, the faction fights you on the path's ground.
- **Enemy-mix paths** take what's left after the themed share: Swarm Fields and Sniper Ridge build their own mix from
  the rest; Elite Gauntlet halves the whole wave and makes everything elite, faction included.
- Waves 1-4 have no path, so the Warden's approach is the first thing a run shows.

## Screens

- **Wave panel chip:** "Approach · The Warden" during an approach (stacked with the path chip when there is one).
- **Banner** at the start of the last approach wave: "The Warden is near".
- **Difficulty tab:** the Boss approach row (share slider, ramp toggle, Blend / Fully themed buttons). Saved runs show
  it with the other difficulty numbers.
- **Field guide / Bestiary:** each faction enemy gets an entry, linked from its boss ("Its approach: ...").
- **Practice Range:** faction enemies appear in the picker under their boss.

## Engineering

- `Core/Approaches.cs`: a `FactionDef` per boss (`Boss`, members with weight and `From` wave, squads),
  `ApproachShare(wave)` (from the two knobs, the wave's place before its boss, and 0 on boss waves), and
  `BuildApproach(n, ref count)` called from `BuildWave` **before** `BuildPathWave` and `BuildSquads`: it queues the
  themed enemies (as faction squads where it can) and takes them off `count`.
- Boss-only enemies that become faction members need to work with **no boss alive**: see the notes per faction.
  Anything that reads the boss (its state object, its position) gets a "no boss" branch; that's most of the work for
  the existing ones.
- **Messages:** the two knobs travel with the difficulty in the lobby message (protocol bump). Waves are built on the
  host, so nothing else is new on the wire; new enemy types bump the protocol as usual.
- **Bots:** faction enemies use the existing danger reading (bullets, contact, lines); bots already treat Gravity
  Wells and Ball Lightning as priority targets and give Hive Bombers a wide berth. New attacks get a rule in
  `BotBrain` like the shooting-pattern enemies did.
- **Session recordings** note each wave's approach (boss and share); `report.py` groups damage and downs by approach,
  so Blend and Fully themed runs can be compared.
- **Tests:** the share comes out right for each wave and both ramp settings; 0% builds exactly today's waves (same
  seed, same queue); faction enemies spawn, fight and die with no boss alive; boss waves are unchanged; it stacks with
  each enemy-mix path; recordings list it.

## Build order (each step playable on its own)

1. ✅ **Framework + the Warden:** the two knobs, `Approaches.cs`, `BuildApproach`, the chip and banner, recordings and
   `report.py`, tests. The Warden's faction with Rioters and Guards (both ready), the **Riot Shield Guard** (also sent
   into the Warden's searchlight turn) and the Gas Grenadier; the Breakout and Cell block squads. The owner plays waves 1-4 at Blend and Fully themed and picks a default.
2. **Hive Queen:** the Bomber fix, Eggs that hatch on their own, the Nectar Spitter.
3. **Singularity and Storm Herald:** Gravity Wells and Ball Lightning (ready or nearly), Cage Wells reworked, then
   the Slingshot, Lightning Rods and Stormcaller.
4. **Lich and Duelist:** Skeleton, Grave Priest, Wailing Spirit; Blade Dancer and the Decoy rework.
5. **Twin Reapers, Colossus, Devourer:** their new enemies (all new, so the most work).
6. **Eclipse:** Echoes from the finished factions.

Each step: Bestiary entries (`BestiaryData.Entries.cs`), the Practice Range picker, previews, README,
protocol bump when enemies are added.

## Later: Chapters mode

Once the factions exist, a lobby mode where you pick a boss you've beaten and play its approach as a short run:
nine themed waves and the boss. The hard part is power: the 50-wave run expects you to arrive with a level, upgrades
and legendaries, so a chapter would start you at the level, picks and legendaries a typical run has at that point
(from session recordings), with enemy damage set for that wave. Its own design doc when we get there.

## Still open

- Names, numbers and faction members are first drafts: change freely, especially the 16 new enemies.
- Should the ramp be on by default, or the whole stretch even? (Decide after playing step 1.)
- Waves 1-4 now have a theme: does that settle `Docs/Paths.md`'s question about a path choice for them?
- Do faction enemies stay out of regular waves entirely, or show up rarely elsewhere once you've met them?

## Campaign: ten waves per boss (owner's idea; the Warden's table, Trickle and the fade are built, callbacks and the rest of the schedule are not)

Instead of a themed share that ramps up, each faction enemy is **introduced one at a time**: it first **trickles in
as stragglers in the second half** of its intro wave (alone, so you learn it), then **joins the squads** the next
wave while the next new enemy trickles in. The rest of each wave stays the usual random mix. Ten waves leave room
for the Warden's whole roster, then a few waves of everything together.

**Squads stay one side.** Prisoner squads are Rioters, Thugs and Shivs; guard squads are Guards, Security Guards and
Shields. The two sides only share a wave through the random mix or by one squad each.

### The Warden (waves 1-10)

**Built as this table** (November 2026, `Approaches.cs` lessons; `run.sh 1 1 chartcheck` prints the real waves next to it). The table's "Rioter pack" and "Guard pair" are `FactionDef.LessonSquads` (only the lessons use them). Wave 10 is the Riot line plus a big Breakout (`WaveLesson.Also`). A wave's new enemy comes last, after its squads (`stragglers`, `SpawnNextSquad`). One gap: the Yard squad carries a Shiv, which has no intro wave.

| Wave | Squads (from earlier waves) | New: stragglers in the second half |
|---|---|---|
| 1 | none | **Rioter** |
| 2 | Rioter pack | **Guard** |
| 3 | Rioter pack, Guard pair | **Thug** |
| 4 | Yard (Rioters + Thugs), Guard pair | **Security Guard** |
| 5 | Yard, Patrol (Security Guards + Guards); recap | none |
| 6 | Yard, Patrol | **Riot Shield** |
| 7 | Yard, Cell block (Shields + Guards) | **Gas Grenadier** |
| 8 | Yard, Cell block | **Taser Guard** |
| 9 | every Warden squad, tighter | none |
| 10 | the exam: Riot Line (guards) + a big Breakout (prisoners) | none |

(The Shiv, a lunger reskin, is in the pool and the Yard squad but has no intro wave yet: wave 6 or 7 is the slot.)

**Other bosses (rule):** with N faction enemies, introduce one at waves 1, 3, 5... spread over waves 1-6 (one per
wave when N is 5-6, every second wave when N is 3-4); wave 7 is all of them in squads, 8 the named squads, 9 everything
tighter, 10 the `Finale`. Rosters under 3 (Twin Reapers) borrow a regular enemy, as in the faction table; the Lich keeps
its zombie kinds; the Eclipse's Echoes introduce one earlier faction per wave.

### The handover between factions (waves 11-14, the Warden to the Hive Queen)

The old faction **trickles out over about three waves** after its boss while the new faction starts **at once**. B1-B5
are placeholders for the Hive Queen's enemies.

| Wave | Hive squads | Hive stragglers | Warden leftovers (singles, no squads) |
|---|---|---|---|
| 10 | none | none | the full roster, Riot Line + Breakout |
| **Warden fight** | | | boss |
| 11 | none | **B1**, from the start of the wave | about 40% of the wave: Rioter, Guard, Thug, Security Guard |
| 12 | B1 pack | **B2** | about 20%: Rioter, Guard, Thug |
| 13 | B1 + B2 | **B3** | about 8%: Rioter, Guard |
| 14 | B1, B2, B3 | **B4** | none |
| 15 | recap | none | none |
| 16 | all Hive squads | **B5** | none |
| 17 | all Hive squads | none | none |
| 18 | named Hive squads | none | none |
| 19 | everything, tighter | none | none |
| 20 | Hive exam (set piece) | none | none |

- The old faction's **tricky enemies leave first** (Shield, Gas, Taser), so wave 11 isn't new and hard at once; the
  basics (Rioter, Guard) linger longest. Old squads stop at the boss, so a Rioter pack doesn't look like the approach
  restarting. The leftover share counts toward the wave's size, like the old themed share.
- The new faction's banner ("The Hive Queen is near") shows at wave 11.

### Callback squads (later waves) (built as the existing comeback squad, made big)

Once a faction's boss is beaten, its **named squads can come back as random events** in later waves, bigger than in
their own approach: say a Breakout of 14 Rioters on wave 43. They fight on top of the current faction's waves, so a
late run keeps meeting old friends.

- **Built:** `Approaches.Comeback` (Docs/Campaign.md) already picks one squad from an earlier faction on one wave in five
  from wave 11, seeded by the wave; `Approaches.Callback` now makes that squad big (members x2 plus the faction's
  toughest as an anchor). Not built: the banner line, and the chance beyond one wave in five.
- **When (original idea):** from three waves after the boss on, any non-boss wave rolls about 20% for one callback (never two in a
  wave, never in the 10th wave of a cycle, which is the exam). The roll is seeded by the wave, so every player in a
  party sees the same one.
- **Which:** one of the beaten bosses' squads (or its `Finale`), picked by weight; the latest factions are slightly
  more likely.
- **How big:** the squad's members repeated about 2x, plus one tougher member (a Thug or Shield) as an anchor, scaled
  by wave and party size like the rest of the wave. It takes its size off the wave's count, so waves don't grow.
- **Tell:** a short banner line ("Breakout!") as it spawns, so it reads as an event.

### Engineering

`WaveLesson` gains `Trickle` (the `Star`'s spawns are held to the wave's second half instead of queued up front) and
`SquadsFrom` (which earlier members squads may use). The faction handover needs `FactionDef.Fade` (the share and the
order its members leave) read from the previous boss's faction in `Approaches.Campaign(n)`; callbacks are a new
`BuildCallback(n, ref count)` next to `BuildApproach`. The share/ramp knobs stay for the old 50-wave track and are
ignored in the Campaign. Waves are built on the host, so no wire change; a banner line is a new string only. Tests
(`approaches`): each intro enemy appears only after its wave's halfway point and only as its intro wave's star; squads
use only already-introduced members; the leftover share is 40/20/8% on waves 11-13 and 0 after; callbacks only come
from beaten bosses, never on boss or exam waves, at most one a wave, and the same on every game; wave 10 is unchanged.

**Open:** the Warden's order (Taser last, as the trickiest, vs. shields earlier); whether wave 5 stays a pure recap;
the Shiv's intro wave; the callback chance (20%) and size (2x).

## The Riot Line (built, November 2026)

The owner's set piece for the last wave before the Warden: `FactionDef.Finale` is a squad that always comes in the
fourth approach wave (whenever the approach is on), counting toward its share. The Warden's is the **Riot Line**
(`SquadForm.RiotLine`, `RiotLineMove` in WardenFaction.cs): four Riot Shields march at you shoulder to shoulder
(58 px apart, a little over half their pace) with five Guards 80 px behind; the Guards break out and rush you once the
wall is within 190 px, half its shields are down, or it's been marching 18 s. Tested in `approaches`; preview
`RIOTLINE=<seconds> Tools/Preview/shot.sh <name> hud 3 4 1`.
