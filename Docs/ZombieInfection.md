# Zombie Infection modifier (design)

Status: **built** (Nov 2026): the hordes, cap, XP split and the dead rising (steps 1-2), and **seven zombie types**
(step 3, designed in `Docs/Mockups/zombie-enemies.html`): Shambler, Runner, Spitter, Crawler, Bloater, Shrieker,
Mender (`Core/Zombies.cs`, `Game/ZombieLook.cs`), green with green effects, on a dark green arena. The Lich's faction uses all seven plus his own **Grave Caller** (cyan, graves round you; not in the modifier's horde; `Docs/Mockups/lich-faction.html`). Changes from the
plan below: the types are the mockup's (no "Brute" zombie; the Bloater's puddle became a spore cloud); the Crawler is
broken free by shooting it (no dash). Still to do: explosions stopping a rise, infected co-op players, a Death Throes
variant, Bloater vision blocking. A run modifier (one more switch in the lobby's Modifiers tab), tagged
**Harder**.

> Every regular enemy is a zombie: no guns, they shamble at you, and there are more of them every wave. The dead
> don't always stay dead.

## Core rules
- **Regular enemies are replaced by zombies.** Each wave's enemy budget is turned into zombies instead of the
  usual mix. Bosses and their adds are unchanged (the Lich fits the theme as it is).
- **More of them, growing.** Count = the wave's normal count x a multiplier rising from **x1.5 at wave 1** to
  **x3 by wave 20**, then flat.
- **Hard cap** on zombies alive at once (start at ~60; tune by profiling). Past the cap, the difficulty
  grows through tougher and faster types, not more bodies. Every enemy is simulated by the host and sent in each
  snapshot, so the cap protects both frame time and co-op bandwidth.
- **No enemy bullets** from zombies, so the bullet budget is free for bosses.

## Zombie types
Built from the existing melee enemies (Swarmer, Brute, Lunger) with new looks, not from scratch.

| Type | Base | Role | Appears |
|---|---|---|---|
| **Shambler** | Brute (slower, less HP) | Slow, tough, the bulk of every wave | Wave 1+ |
| **Runner** | Lunger | Fast, fragile, comes in packs; stops kiting from being free | Wave 3+ |
| **Bloater** | Swarmer (slower, more HP) | Pops on death into a puddle that slows (like the Hive Queen's honey) | Wave 6+ |
| **Brute** | Brute (bigger) | Rare heavy hitter, shoves you | Wave 11+ |

Mix shifts toward runners and bloaters as waves go on.

## Infection mechanics
- **The dead rise.** A killed zombie has a **25%** chance to get up again after 3 s, at half health, with a
  visible twitch/warning on the corpse. It does **not** rise if the killing blow was **fire, an explosion or a
  melee weapon**. That gives Incendiary/Explosive Rounds, grenades and melee classes a new job. Risen
  zombies give no XP the second time.
- **Infected (co-op).** A downed player rises as a zombie version of themselves (controlled by the host's AI,
  slow, weak) until revived. Reviving means getting close to your infected friend. Their zombie can't kill, only
  slow/knock back, so it's tension, not griefing. Solo: no change to going down.
- **Infection stacks (optional, playtest first).** Each zombie hit adds a stack that cuts your regeneration by
  10% (max 5), cleared at the end of each wave.

## Interactions with other modifiers
| Modifier | With Zombie Infection |
|---|---|
| **Nightfall** | The intended combo: lantern beam, shapes in the dark. No change needed. |
| **Death Throes** | Clash (zombies would burst into bullet rings). Proposal: zombies burst into a slowing goo puddle instead of shots. |
| **Swift Foes** | Stacks with runners, gets brutal; keep, it's a Harder pick. |
| **Famine / Bloodlust** | Work as normal. |
| **Hunted** | Fine: the horde goes for the hunted player. |
| Paths that change the enemy mix (Swarm Fields, Elite Gauntlet...) | The path's flavour applies to zombies (Elite Gauntlet: elite zombies), but they stay zombies. |

## Balance watch-outs
- **Weapons swing.** Without enemy bullets, parry and blockers (Barrier, Shield Wall...) lose their job, while
  piercing, area damage and chain weapons get very strong. Runners and bloaters need to be enough of a threat
  that a long-range build can still get cornered.
- **Tank and parry kits** need something to do: zombies hitting a blocker take knockback, a perfect parry against
  a lunging runner staggers it.
- **XP and gold**: more enemies means more XP; scale per-zombie XP down so levels match a normal run
  (check with a SimTest report comparing level at wave 10/20 with and without the modifier).

## Build order
1. **Basic version**: the modifier switch, all-zombie waves from the existing melee enemies, the growing count and
   the cap, per-zombie XP scaling. Playtest.
2. **The dead rise** + fire/explosion/melee exception.
3. **Zombie types** (looks and tuning).
4. **Infected co-op players**, Death Throes variant, optional infection stacks.

Each step: SimTest checks in `Tests/RuleModifiers.cs` (count growth, cap held, rising rules) and a short report
(wave reached / level by wave for a bot duo).
