# Boss Hunts and Perks (design)

Status: **the Warden Hunt is built** (Nov 2026: `Core/Hunts.cs`, `Game/HuntUi.cs`, `hunts` test suite): ten waves,
nine of only the boss's own faction (tougher members and squads joining as it goes, its set piece on waves 5 and 9),
then the boss on wave 10 at wave-10 strength. Later bosses need a faction first (only the Warden has one) and a
starting build that fits their strength. **Perks: the framework and the first perk, Riot Gear, are built** (Nov 2026: `Core/Perks.cs`, `Core/RiotGear.cs`, `Game/PerksUi.cs`, `Game/RiotGearLook.cs`, `perks` test suite; see [`RiotGear.md`](RiotGear.md)). Not built yet: the other perks, the second and third perk unlocks per boss, and the no-perks record variant. Two features that make one loop:

```
Full run ──beat a boss──▶ unlocks its Boss Hunt
Boss Hunt ──win it──────▶ unlocks that boss's Perks
Perks ──equip in lobby──▶ next full run (or hunt) ──▶ reach / beat new bosses ──▶ …
```

Each part has a job: the full run is the long game and the source of unlocks, a hunt is a short hard test against
one boss, and perks carry what you earned back into the long game.

---

## 1. Boss Hunt

### What it is
A short run (10 waves, ~8-10 minutes) that ends with one chosen boss at wave 10. Win it and the run ends in
victory. It is a **third mode** in the lobby's mode switch: **Run / Boss Hunt / Practice Range**.

Why a mode and not a modifier: modifiers are on/off switches that stack. A hunt picks exactly one boss and
changes the run's length, so it doesn't fit a switch. Other modifiers and the difficulty still apply on top
(e.g. Warden Hunt + Elite Gauntlet).

### Unlocking
- A boss's hunt unlocks the first time this computer's player beats that boss in a full run (Records already
  track "each boss beaten"; the unlock is an entry in `Unlocks.All`, `Profile.cs`).
- Hunts, like the rest of Records, don't count runs with dev tools (F1) or the bot.
- In co-op the **host's** unlocked hunts are offered (the host picks the mode). Everyone's records/unlocks grow from
  the result, as they do today.

### Lobby
Choosing Boss Hunt shows a row of 10 boss portraits (live models, as the Bestiary/Field guide draws them).
Locked ones are dark with "Beat it in a run to unlock". Each unlocked one shows best hunt time and wins.

### The run
| Waves | What |
|---|---|
| 1-9 | Regular waves, built from the boss's **home faction/era** where it has one (the Warden's prison troops, the Hive Queen's swarmers and bombers, late-game enemies for late bosses). Wave strength follows the boss's home wave, not 1-9. |
| 10 | The boss, at **its real strength for its home wave** (Warden = wave-5 Warden, Storm Herald = wave-40 Storm Herald, including the post-20 health growth). |

No paths. At most one challenge room (or none; to playtest). No portal before wave 3.

### Power: scale the player up, not the boss down
A wave-10 build can't fight the wave-40 boss at its real strength. Instead of weakening the boss (then it isn't
the real fight and the win means less), the hunt gives the party a **compressed build**:

- **XP multiplier** so the party reaches roughly the level a full run has at the boss's home wave
  (tune with `run.sh` reports: the typical build at wave N already exists for boss power scaling).
- **Legendary picks** after waves 3, 6 and 9, one per earlier boss in the cycle, from those bosses' pools
  (late hunts get more: a wave-40 hunt should hand out about as many legendaries as a full run has by wave 40,
  spread across the 9 breaks).
- **Weapon mods** on the same schedule a full run would have them by the home wave.
- Boss power scaling (`BossPowerScale`) stays **on**, so an over-built party still gets a fair fight.

Target: a "typical" hunt party arrives at the boss with roughly the typical build for its home wave.
Verify with a SimTest report (`run.sh 1 1 hunts`) that prints the build score at wave 10 vs. the typical build
at the home wave, per boss.

### Ending
- Win: "HUNT COMPLETE", record time, unlock that boss's perk(s) (section 2).
- Loss: normal game over. Second Wind, revive tokens etc. work as usual.
- No endless; hunts aren't saved mid-run (short enough not to need it). Open question: allow saving anyway?

### Records
Separate from full runs: hunts started/won per boss, best time per boss, and (section 2) whether it was won with
no perks. Full-run records are unchanged.

### Session recording
`mode: "hunt"`, plus `huntBoss`, so balance logs can be filtered.

---

## 2. Perks

### What they are
Small **rule-benders** you pick in the lobby before a run (or hunt). They are not stat sticks and not weaker
legendaries: legendaries are big, picked mid-run, from a boss's pool. A perk should change *how you play*
("oh, I can be greedier with chests now"), not add "+10% damage".

### Unlocking
- Winning a boss's hunt unlocks that boss's **first perk**.
- Each boss has **3 perks**. The 2nd unlocks by winning its hunt on **Hard or higher**, the 3rd by winning it with
  **no perks equipped**. That gives hunts a reason to be replayed.
- Perks are **per player** (this computer's profile), like Records. In co-op everyone brings their own.

### Slots
- 1 slot to start.
- 2nd slot: first full-run win (beat The Eclipse).
- 3rd slot: win 5 different hunts.
- More unlocks = more choice, not more power. One perk per boss at most (can't stack two Warden perks).

### Lobby
A **Perks** tab beside the loadout: your slots, and the unlocked perks as cards (boss portrait, name, one-line
effect). Locked perks show how to unlock them. Teammates' equipped perks show on their lobby seat.

### Records and fairness
- Run records note the perks used. "Best wave" / "wins" get a **no-perks** variant so a clean win still counts
  for something.
- Perks are off in the Practice Range and Boss Lab (like boss power scaling).
- Saved runs keep their perks (by name, like upgrades), and they can't be changed when continuing.

### First perk ideas (one per boss, to build first; the rest after playtesting)
Themed on each boss's mechanics. Numbers are starting points.

| Boss | Perk | Effect |
|---|---|---|
| Warden | **Riot Gear** | A shield that blocks one hit, then recharges in 30 s (plan: [`RiotGear.md`](RiotGear.md)). |
| Warden | Contraband | Chests at the start of each boss cycle are guaranteed rare or better. |
| Hive Queen | **Royal Jelly** | Health orbs heal 30% more, and you pick them up from further away. |
| Hive Queen | Brood | Start each wave with a small drone that orbits you and chips enemies (weak, dies in a few hits). |
| Twin Reapers | **Death's Door** | Once per run, going down instead leaves you up at 1 HP and invulnerable for 1.5 s. |
| Twin Reapers | Reaping | Killing an enemy below 15% health refunds 0.5 s of your Space ability's cooldown. |
| Singularity | **Event Horizon** | Enemy bullets within 60 px of you slow down by 30%. |
| Colossus | **Bedrock** | Knockback and pushes (gates, stomps, wind) move you 40% less. |
| Duelist | **Riposte** | Your parry window is 25% wider, and a perfect parry gives +25% damage for 2 s. |
| Lich | **Phylactery** | +1 revive token at the start of the run (co-op: the team gets one extra each). |
| Lich | Soul Tithe | Each boss kill gives +1 reroll. |
| Storm Herald | **Tailwind** | +10% move speed while you aren't firing. |
| Devourer | **Second Helping** | Level-up offers show 4 cards instead of 3. |
| Eclipse | **Totality** | Pick your first upgrade offer from rare-or-better cards (like a challenge prize). |

(Bold = the first perk unlocked per boss.) Each perk needs: an entry in a `Perks.All` table (name, boss, desc,
unlock condition), an effect hook, a Field guide line, a SimTest check.

### Watch-outs
- **Late-boss perks** only help players already strong enough to beat that boss. Fine, as long as they're fun
  rather than required: a trophy you get to use.
- **Power creep**: keep every perk weaker than a legendary. If playtests show perk runs winning much more often,
  trim effects before adding slots.
- **Network**: perks are chosen per player in the lobby and sent with the loadout; effects run on the host like
  upgrades and must be in the snapshot/upgrade sync where clients need them (HUD, shields).

---

## Build order
1. **Boss Hunt**: mode switch, boss picker, hunt waves, compressed build, records, SimTest `hunts` report + checks.
2. **Perks framework**: `Perks.All`, profile unlocks, slots, lobby tab, sync, saved runs, records variant.
3. **First perks**: Warden, Hive Queen, Twin Reapers (the first three bosses most players beat).
4. Remaining bosses' perks, then 2nd/3rd perks per boss after playtesting.

## Open questions
- Should hunts have a short challenge-room or path step, or stay pure waves → boss?
- Hunt difficulty: the lobby's difficulty card only, or a separate "hunt tier"?
- Can a hunt be saved and continued?
- Do perks apply in hunts (it makes hunts easier to farm), or only in full runs? Proposal: yes, except the
  "no perks" unlock condition.
