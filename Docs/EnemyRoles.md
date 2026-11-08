# Enemy roles: late waves where enemies do more than shoot

Owner (Nov 2026): "I think it's mostly enemy design. Like the enemies just shoot after a certain point."
Follows on from [`LateGamePressure.md`](LateGamePressure.md) (the numbers side). **Design only: nothing here is built.**

## What a wave is made of today

`Tools/SimTest/run.sh 1 1 wavemix`: 40 built waves at each wave number, two players, Normal. Shoot = fires bullets;
melee = contact, charges, lunges; support = Medic, Warder, Nest.

| Wave | Enemies | Shoot | Melee | Support | Kinds | Most common |
|---|---|---|---|---|---|---|
| 6 | 19 | 33% | 67% | 0% | 11 | Swarmer 34%, Grunt 19%, Brute 12% |
| 9 | 24 | 60% | 40% | 0% | 14 | Swarmer 20%, Sniper 12%, Mortar 12% |
| 12 | 28 | 64% | 27% | 8% | 15 | Brute 18%, Grunt 15%, Sniper 13% |
| 14 | 32 | **81%** | 12% | 6% | 11 | **Grunt 25%**, Sniper 14%, Brute 12% |
| 19 | 32 | **91%** | 6% | 4% | 14 | Grunt 15%, Phantom 13%, Sniper 12% |
| 22 | 33 | **91%** | 5% | 3% | 16 | **Grunt 22%**, Sniper 10%, Leader 8% |
| 29 | 36 | 83% | 9% | 8% | 20 | Grunt 18%, Sniper 9%, Medic 8% |
| 33 | 38 | 28% | 69% | 3% | 25 | Shambler 36%, Runner 29% (the Lich's approach) |
| 39 | 40 | 79% | 10% | 12% | 22 | **Grunt 20%**, Sniper 7%, Splitter 6% |

Three problems:

1. **Every new enemy from wave 14 to 26 is a new bullet pattern** (Pauser, Phantom, Beam Sentry, Leader, Hexer,
   Stitcher, Scorcher, Juggernaut). They're good patterns, but they all ask for the same thing: weave. Only the Reaver
   (wave 28), Nest (31) and Warder (36) ask for something else, and they arrive late and rarely.
2. **The melee stops at wave 13** (`MeleeShare`). Nothing makes you move for a reason other than a bullet.
3. **The pool is flat.** Each single enemy is drawn evenly from every type unlocked so far, and the early squads
   (Firing line: three Grunts and a Sniper) stay in the draw. So the wave-1 Grunt is a fifth of wave 39. With 20 kinds
   spread thin, a wave has no identity. It's a soup of everything you've seen, and the new enemy gets lost in it.

## The idea: every enemy has a job

A late wave should mix enemies that each ask for a **different action**, so the right move changes from moment to
moment:

| Job | Asks you to | Today | Wanted late share |
|---|---|---|---|
| **Pressure** | weave, use cover | almost everything | 40-50% |
| **Hunter** | move, kite, sidestep a telegraph | Reaver, squads' Brutes, Lungers (rare) | 20-25% |
| **Zoner** | give up ground, pick your spot | Scorcher's embers, Mortar (half) | 10-15% |
| **Disruptor** | react *now*: break a grab, dodge a pull | none | 5-10% |
| **Anchor** | pick a target: kill it first | Medic, Warder, Nest | 5-10% |

Good-job rules (from the enemies that already work: Beam Sentry, Riot Shield, Spike Block, Reaver):
- **One clear telegraph, one clear answer.** A line, a cone or a ring you can see, and a move that beats it.
- **A window after.** It's open or tired after its move (the Reaver's +50%, the Spike Block's 0.8 s rest), so
  answering well is rewarded, not just survived.
- **A co-op angle where it fits.** Something a teammate can do for you (shoot the chain, peel the leech).

## 1. Wave recipe (no new enemies, no art: the cheapest big change)

- **Jobs, not a flat pool.** Each type gets a job tag (`Defs.cs`). `BuildWave` fills a late wave to the shares above
  instead of drawing evenly. Melee doesn't fall to 0% at wave 13: Hunters keep their 20-25% from the late melee
  (Lunger, Spike Block, Buzzsaw, Reaver), and their squads.
- **Headliners.** Each regular wave picks **2-3 featured types** (the newest unlocks are more likely) that make up most
  of its Pressure and Hunter share. The rest is a light mix. Wave 22 might be "Stitchers and Spike Blocks", with a few
  others. Each wave gets an identity, and the between-waves box can already show "next wave" (`WavePool` is used there).
- **Retire the basics.** A type's weight fades over the 10 waves after the next one unlocks: the Grunt is about 5% by
  wave 20 instead of 22%. The Firing line squad gives way to Hunting party, and so on.

## 2. Reworks: move existing enemies to other jobs (small art changes)

| Enemy | Now | Becomes | Job |
|---|---|---|---|
| **Charger** | dashes, then bursts into a ring | the dash only, and it's **stunned for 1 s** where it stops (+50% damage) | Hunter |
| **Phantom** | blinks next to you, bursts into a ring | blinks *behind* you, shows a 0.4 s slash arc, cuts (parry-able), then fades out | Hunter |
| **Mortar** | bombs burst into rings | bombs leave a **burning patch** for 4 s (no ring) | Zoner |
| **Scorcher** | ember trail **and** pairs of shots | the trail only, and a longer one: it fences you in | Zoner |
| **Splitter** | shoots, splits into Shards | no gun: walks at you, splits into Shards that rush | Hunter |

That moves about a quarter of today's late shooters to other jobs, without one new enemy.

## 3. New enemies that don't shoot (each needs a mockup)

Most of these reuse things the game already has (noted in brackets), so they cost less than they look.

| Enemy | From | Job | What it does | The answer |
|---|---|---|---|---|
| **Fuse Runner** | 15 | Hunter | Sprints at you; within 120 px it plants its feet, a ring shrinks for 0.8 s, then it blows up (35). The blast hurts **enemies too** | Step out, or lead it into its friends. Shot during the fuse, it blows up early where it stands |
| **Hookman** | 17 | Disruptor | Swings a chain, shows a line for 0.5 s, throws a hook that **yanks you 220 px** toward it and its squad (no damage) | Dodge or dash sideways, or perfect-parry it. A teammate's shot on the chain cuts it. 1 s open after a miss |
| **Sapper** | 18 | Zoner | Runs about planting mines that arm after 1 s (a blinking ring) and last 12 s; up to 4 each | Walk round, or **shoot a mine** to set it off, hurting enemies near it |
| **Burrower** | 20 | Hunter | Dives, and a dirt trail chases you (a bit slower than you walk); after 3 s it erupts under the trail's head (a 0.5 s ring) | Keep moving, or bait it then step away. It's stuck for 1 s after erupting [the Earthquake ability's Burrower look] |
| **Leech** | 23 | Disruptor | Leaps on a player (0.4 s crouch), latches: you're slowed 40% and drained 6/s until it's knocked off | A teammate shoots it off (it takes double damage while latched), or you dodge twice. Solo it lets go after 3 s [the Crawler's grab, `Zombies.cs`] |
| **Bulwark** | 25 | Hunter | A wide, slow wall-bodied brute that **shoves** players ahead of it (no damage) toward the rest of the wave. Front-armoured | Go round it. Its back takes double [the Riot Shield's front block] |
| **Jammer** | 27 | Anchor | Hangs back; a 200 px field round it where your **abilities don't recharge** and the dodge is off | Kill it first, or fight outside its field |
| **Pylons** | 30 | Zoner | Two posts walk in apart and plant; a crackling fence joins them and turns slowly (1 turn in 12 s), hurting what it crosses | Stay on the right side as it turns, or break one post [the Stitcher's thread] |

## Suggested order

1. **Wave recipe** (section 1) and the **Charger / Phantom / Mortar / Scorcher / Splitter** reworks (section 2).
   No new art beyond small looks, no protocol change for the recipe, testable with the bots and `wavemix`.
   The target is wave 22 at about 50% shooters instead of 91% (check with `wavemix` once it's built).
2. **Three new enemies** with mockups first: **Fuse Runner, Hookman, Sapper** (one Hunter, one Disruptor, one Zoner,
   all early enough to be seen in a normal run).
3. The rest (**Burrower, Leech, Bulwark, Jammer, Pylons**), then retune the fire rate and the budget
   (`LateGamePressure.md` A and B) with the new mix. With fewer shooters the screen may already be readable.

Each new enemy needs a Bestiary entry when the owner asks (the Bestiary and Armory pages aren't touched until then).
