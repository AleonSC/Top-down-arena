# Divergent paths: design plan

Status: **planned, not built.** Decisions below were made with the owner (October 2026); numbers are first
guesses to tune in play-testing and the Boss Lab.

## The idea

After each boss, the host picks one of **two paths** for the next stretch of the run. A path changes the four
regular waves before the next boss: the arena's rules, the enemy mix, or the floor, and it pays off with a
bonus while you're on it and a prize when you reach the boss. Harder paths pay more. Every path has its own
look, so the run feels like travelling somewhere.

## Decisions (owner)

| Question | Decision |
|---|---|
| Does a path apply to the boss fight? | **No: regular waves only.** The path ends before the boss; boss fights stay standard, so boss balance and Boss Lab data hold. |
| Who picks? | **The host decides.** Everyone sees both cards; only the host's click counts. |
| Which kinds of path? | **All four:** enemy mixes, arena rules, hazard floors, twists on you. |
| How do harder paths pay off? | **Both:** a bonus while on the path and a prize at the end. |

## Flow

1. Waves 1-4 are the plain arena (no path yet).
2. After a boss dies and its legendary / weapon-mod picks are done, the intermission shows **Choose your path**:
   two cards side by side. The host clicks one; the others see the cards and "Waiting for <host>". The next
   wave doesn't start until the host has picked.
3. The chosen path runs the next four waves (e.g. after wave 5: waves 6-9). Its floor tint and rules are on;
   a chip by the wave plate shows its name and waves left.
4. The wave before the boss ends the path: its **prize** is given (before the boss), the arena returns to
   normal, and the boss wave plays as usual.
5. Repeats after every boss (9 path choices in a 50-wave run). In endless mode it keeps going.

The two offers are drawn from the pool so that (usually) one is safer and one harder, and the path you just
finished isn't offered again straight away.

## Danger, bonus and prize

Each path has a **danger rating** of 1-3 (shown as 1-3 skulls). The bonus and prize follow from it, so they're
easy to compare and balance:

| Danger | While on the path | Prize at the end |
|---|---|---|
| 1 (safe) | no bonus | full heal for everyone + 2 rerolls |
| 2 | +25% XP and score | +1 upgrade pick each |
| 3 (hard) | +50% XP and score | an extra legendary pick each |

Run modifiers (the lobby's Modifiers tab) stack with paths.

## The first pool (9 paths)

| Path | Kind | Danger | What changes |
|---|---|---|---|
| **Swarm Fields** | Enemy mix | 2 | Twice as many melee rushers (Swarmers, Brutes, Lungers) at 60% health. Floor: trampled grass. |
| **Sniper Ridge** | Enemy mix | 2 | Mostly Snipers, Mortars and Grunts; few melee. Floor: rocky ridges. |
| **Elite Gauntlet** | Enemy mix | 3 | Half as many enemies, every one elite. Floor: dark gold flagstones. |
| **Fogbound** | Arena rule | 2 | You only see about 350 px around each player; enemies and bullets beyond fade out. Floor: misty grey. |
| **Narrow Pass** | Arena rule | 2 | The arena shrinks to about 70% between glowing walls; no wrapping around the edges. Floor: canyon stone. |
| **Burning Grounds** | Hazard floor | 3 | Fire patches flash a warning, ignite and slowly spread; they burn enemies too. Floor: scorched earth. |
| **Sanctuary** | Hazard floor | 1 | Healing pools appear around the arena; 20% fewer enemies. A recovery path. Floor: soft blue light. |
| **Frozen Lake** | Twist | 2 | You slide with momentum (speed builds and carries); enemies are chilled 10% slower. Floor: cracked ice. |
| **Blood Moon** | Twist | 3 | No health orbs and no regeneration, but every kill heals its killer 2 HP. Floor: red-lit. |

## Screens

- **Choose your path:** two big cards, each with a live animated preview of its floor and a few enemies from its
  mix, the name and skulls, its rules as short lines, the bonus, and the prize. Host: "Take this path" button on
  each. Others: the same cards and "Waiting for <host>".
- **During a path:** the floor tint and pattern, a chip under the wave plate ("Sniper Ridge · 3 waves left"), and
  the path's own effects (fog, walls, fire patches, healing pools).
- **Tab menu:** a small route strip of the paths taken so far this run.
- **Run summary:** the route taken.

## Engineering

- `Core/Paths.cs`: a `PathDef` registry (id, name, kind, danger, floor look, enemy-mix rule, arena rule,
  hazard rule, twist) and `Game.CurrentPath` / `PathWavesLeft`. Hooks in `BuildWave` (mix), `TickWaves`
  (offer, start, end, prize), XP and score gain (bonus), and per-path tick functions for hazards and twists.
  This is self-contained and doesn't need the ability/enemy registries from the audit plan.
- **Messages** (protocol bump): `PathOffer` (host → clients: the two ids), `PathChosen` (the id and when it
  started). Fire patches and healing pools reuse the existing replicated zones/hazards. Fog is drawn by each
  client from the path id. Frozen Lake affects local movement, which clients already control.
- **Narrow Pass** is the riskiest: the arena is a fixed 1600×900 used in ~30 places, and players wrap around
  the edges. It needs a "playable rect" that movement, spawns, bullets and bots respect. Build it last.
- **Bots** (Boss Lab, "Bot plays for you"): avoid fire patches like other hazards, use healing pools, cope with
  fog (they read the real positions, so fog only matters to people).
- **Session recordings** note the path for each wave; `Tools/SessionReport` groups damage and downs by path.
- **Practice Range:** later, an optional path per practice wave, to drill a path on its own.
- **Tests:** offer and choice sync; the host's pick is the one used; mixes apply; bonus and prize given once;
  hazards hurt and heal; the arena returns to normal for the boss; recordings list the route.

## Build order (each step shippable on its own)

1. **Framework + enemy-mix paths:** the registry, offers, the Choose-your-path screen, bonus and prize, HUD chip,
   recordings, tests. Paths: Swarm Fields, Sniper Ridge, Elite Gauntlet.
2. **Hazard floors:** Burning Grounds, Sanctuary (and bots handling them).
3. **Twists:** Frozen Lake, Blood Moon.
4. **Arena rules:** Fogbound, then Narrow Pass (the playable-rect refactor).

## Still open

- Names, floor looks and numbers are first drafts: change freely.
- Should the very first stretch (waves 1-4) also get a choice, e.g. picked in the lobby?
- Should the host be able to pick "no path" (the plain arena) as a third option?
