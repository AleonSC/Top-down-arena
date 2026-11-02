# Boss approaches: design plan

Status: **step 1 built** (the framework and the Warden's faction, November 2026); steps 2-6 planned. Decisions below were made with the owner; numbers are first
guesses to tune in play-testing and the Boss Lab.

## The idea

The bosses always come in the same order (Warden at 5, Hive Queen at 10, ... Eclipse at 50). The four regular waves
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
4. The boss wave itself is unchanged.

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
| **Twin Reapers** (11-14) | **Bound Wraiths** (new, always a pair) · **Scythe Wraith** (new) | Bound Wraiths: two wraiths joined by a harmful tether that sweeps as they circle; kill one and the other revives it after 3 s unless both are dead (the Soul Bond, small). Scythe Wraith: marks a player, then dashes and swings along the mark (the Sundered reaps). Reaper Pillars don't work without the fusion: left out. |
| **Singularity** (16-19) | **Gravity Well** (ready) · **Cage Well** (rework) · **Slingshot** (new) | Gravity Wells already bend shots and drift players with no boss; at most one per wave (they're 260 health). Cage Wells: a trio that rings a player in a shrinking cage (move the cage tick out of the boss-only `TickSingularityHazards`). Slingshot: fires orbs that curve round it and fling out. **Not** the Singularity Shard: it can't be killed without its core, so the wave would never end. |
| **Colossus** (21-24) | **Stomper** (new) · **Rock Hurler** (new) · Juggernaut (regular) | Stomper: heavy melee whose stomp sends out a small ring of push blocks with one gap (the Colossus's rings, small). Rock Hurler: lobs boulders where you're heading (the Leader's lead aim) that leave rubble for a moment. |
| **Duelist** (26-29) | **Blade Dancer** (new) · **Decoy** (rework) · Shielder, Reaver (regular) | Blade Dancer: telegraphed lunges that leave a hanging blade firing sideways; when hit it can blink away and leave a Decoy. The Decoy with no Duelist is a timed bomb today; it needs to give credit and look like a Blade Dancer, not the boss. Shielders (front guard) and Reavers (cone then rake) already duel. |
| **Lich** (31-34) | **Skeleton** (new, from the Grunt) · **Grave Priest** (new) · **Wailing Spirit** (new) | Today the Lich's dead are plain Grunts; a Skeleton type looks the part and climbs out of a grave. Grave Priest: raises Skeletons from graves (at most 4 alive per priest; kill the priest and the graves close), a Nest with a theme. Wailing Spirit: drifts in, wails, then dives at you (the Lich's spirits). Phylacteries mean nothing without the Lich: left out. |
| **Storm Herald** (36-39) | **Ball Lightning** (fix) · **Lightning Rod** (new, in pairs) · **Stormcaller** (new) | Ball Lightning works with no Herald; fix that slows and freezes don't touch it and that its pop shots are marked as boss shots. Lightning Rods: two posts with an arc between them, like the Stitcher's thread. Stormcaller: telegraphs strikes under you. |
| **Devourer** (41-44) | **Bile Spitter** (new) · **Burrower** (new) · Swarmer (regular) | Bile Spitter: lobs bile that leaves a pool (the Devourer's `BileShot`). Burrower: digs under, chases you, erupts in a ring. Morsels only matter to the Devourer: left out. |
| **Eclipse** (46-49) | **Echoes**: one or two members from each earlier faction · Phantom, Hexer (regular) | The Eclipse's own Echoes stage borrows earlier bosses' turns, so its approach does the same with their factions: the run's greatest hits. It already summons Phantoms and Hexers. No new enemies. |

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

Each step: Bestiary entries (`node Docs/Bestiary/build_data.js`), the Practice Range picker, previews, README,
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

## The Riot Line (built, November 2026)

The owner's set piece for the last wave before the Warden: `FactionDef.Finale` is a squad that always comes in the
fourth approach wave (whenever the approach is on), counting toward its share. The Warden's is the **Riot Line**
(`SquadForm.RiotLine`, `RiotLineMove` in WardenFaction.cs): four Riot Shields march at you shoulder to shoulder
(58 px apart, a little over half their pace) with five Guards 80 px behind; the Guards break out and rush you once the
wall is within 190 px, half its shields are down, or it's been marching 18 s. Tested in `approaches`; preview
`RIOTLINE=<seconds> Tools/Preview/shot.sh <name> hud 3 4 1`.
