# Top-Down Arena

A co-op top-down bullet hell for **1 to 3 players** over the network, made in **Unity 6**.

**Party size scaling:** solo, enemies have 70% health; with two players, 100%; with three, regular enemies
have 120% health and waves have 25% more of them, and bosses have 150% health.
Pick a role, then survive endless waves of enemies with a boss every 5th wave.

There are no art assets: everything is drawn in code, and the game sets itself up when you
press Play, so there are no scenes or prefabs to wire together.

New to the code? Read [`HANDOFF.md`](HANDOFF.md) for the architecture, tools, testing and audit notes.

## Opening the project

1. Install [Unity Hub](https://unity.com/download) and a **Unity 6** editor (6000.0 LTS).
   When installing, tick **Windows Build Support** so you can make a `.exe`.
2. In Unity Hub: **Add → Add project from disk** and pick this folder. If Hub says the exact
   editor version isn't installed, pick any Unity 6 version you have.
3. When it finishes importing, press **Play**. (The project creates `Assets/Scenes/Main.unity`
   for you the first time it opens.)
4. Commit the `.meta` files Unity generates on first import; Unity needs them to keep
   references stable.

## Roles and classes

In the lobby each player picks a **role** (Tank, Healer, DPS), a **class** (Soldier, Mage, Summoner, Knight, Ninja), then
one of that combination's three weapons and three Space abilities. Weapons and abilities are unique to
their role and class.

- **Soldier**: guns and gear, full health.
- **Mage**: spells (fire, frost, lightning, arcane), 85% health.
- **Summoner**: minions that come with your weapon (they stay between waves), 90% health.
- **Knight**: melee weapons that cut small bullets out of the air, armoured: 115% health (see below).
- **Ninja**: thrown blades and quick melee with tricks and misdirection; fast (+12% move speed), 85% health (see below).

| | Soldier | Mage | Summoner |
|---|---|---|---|
| **Tank** (240 HP, enemies prefer you) | Weapons: Scattergun, Flak Cannon, Riot Repeater<br>Abilities: Barrier, Roar, Bulldoze | Weapons: Flame Burst, Frost Lance (slows), Storm Hammer (chain lightning)<br>Abilities: **Arcane Aegis** (reflects bullets), **Frost Nova** (freezes nearby enemies), **Flame Wall** (burns enemies, blocks bullets) | Weapons: Golem Staff (+ bullet-blocking Golem), Bone Guard (+ 2 orbiting skeleton shields), Bear Totem (+ Bear)<br>Abilities: **Golem Wall** (3 stone walls), **Earthquake** (you and every minion slam), **Guardian Spirit** (eats nearby bullets for 8 s) |
| **Healer** (120 HP, healing aura) | Weapons: Light Bolt, Revolver, Mender's Bow<br>Abilities: Mend Pulse, Haven, Soul Link | Weapons: Radiant Orb (heals allies it passes), Chain Light, Prism Beam (grazing heals)<br>Abilities: **Time Rewind** (health back to 4 s ago, even undoing a downing), **Arcane Ward** (60 HP shields), **Sanctify** (erase bullets, heal) | Weapons: Sprite Staff (+ 2 healing sprites), Treant Rod (+ Treant guard, healing bolts), Totem Staff (plants healing totems)<br>Abilities: **Spirit Swarm** (5 healing sprites), **Phoenix** (fights, and instantly revives a downed ally), **Soul Lantern** (allies near it can't go down) |
| **DPS** (95 HP) | Weapons: Twin Rifle, Railgun, Burst SMG<br>Abilities: Dash, Cluster Grenade, Mark for Death | Weapons: Fireball (explodes), Arc Lightning (chains to 4), Arcane Missiles (curving)<br>Abilities: **Meteor** (warning circle, then 150 damage), **Blizzard** (slowing storm), **Arcane Surge** (+60% damage, +2 pierce) | Weapons: Wolf Whistle (+ 2 wolves), Wisp Lantern (+ 3 shooting wisps), Imp Caller (exploding imps every 3 s)<br>Abilities: **Bat Swarm** (8 bats), **Sacrifice** (all your minions explode, then return), **Alpha Call** (minions frenzy) |

Generic upgrades (damage, fire rate, cooldowns, Second Charge, legendaries) work for every class; minions
use your damage and attack-speed bonuses too.

### The Knight

Knights fight up close. Holding fire **swings** instead of shooting: each swing hits every enemy in its arc
(the Rapier only the nearest), and **cuts small bullets out of the air** in front of you. Big orbs and mines get
through (a mastered Whirlwind stops those too), and bosses' red aimed shots always do. Knights have 115%
health to make up for standing in the thick of it, and deal about a third more damage than the ranged
classes.

| | Weapons | Space abilities | Ultimate |
|---|---|---|---|
| **Tank** | **Warhammer** (slow 150° smash, knocks back), **Lance** (a heavy jab: a 180 × 88 px box straight ahead that slides out, shoves enemies 140 px back and stuns them for 1 s), **Halberd** (160 px thrust that hits everything on the blade) | **Shield Wall** (4 s: blocks every bullet coming at your front 120°, red aimed shots included, soaks 400 boss damage), **Leap Slam** (leap up to 320 px to your cursor, untouchable in the air; 50 damage and a 1 s stun where you land), **Unbreakable** (5 s: take 50% less damage, hits heal you 15% of what they deal) | **Last Stand** (70 s): 8 s where you can't drop below 1 HP, every enemy targets you, and you deal +50% damage |
| **Healer** | **Holy Mace** (each enemy hit heals you and allies near you for 2, up to 8 a swing), **Lightbringer** (sword cut plus a crescent of light that heals allies it passes), **Censer** (a flail whirled all the way round; each spin heals allies near you for 3) | **Consecrate** (6 s of blessed ground: allies heal 12 HP/s, enemies burn 20/s), **Guardian's Oath** (shield the ally nearest your cursor for 100, yourself for 50, for 6 s), **Lay on Hands** (heal the most hurt ally within 300 px for 70, or revive them at half health) | **Hallowed Blade** (80 s): a giant sword falls at your cursor for 200 damage around it, then blesses the ground for 8 s (allies heal 15 HP/s) |
| **DPS** | **Twin Blades** (very fast alternating slashes), **Greatsword** (huge 170° sweeps), **Rapier** (thrusts at the nearest enemy; each hit in a row on it deals 10% more, up to +60%) | **Whirlwind** (spin for 2 s hitting everything within 110 px every 0.2 s and slicing small bullets), **Lunge** (dash 260 px toward your cursor hitting everything you pass for 35; two charges), **Battle Fury** (6 s: swing 40% faster, +25% damage) | **Blade Tempest** (65 s): for 6 s every swing also hurls three piercing sword beams |

How shot upgrades work on a melee weapon (the cards say so when you're a Knight):

- **Damage, fire rate (swing speed), crits, lifesteal, legendaries** work as usual.
- **Piercing Rounds**: +10% reach per stack (and sword waves pierce one more).
- **Barrage**: every swing also throws a **sword wave** per stack, fanned out to the sides.
- **Incendiary, Cryo, Shock and Explosive Rounds**, Swarm Rounds, Collapse Rounds and Graviton Rounds apply to
  every melee hit. **Homing, Ricochet and Velocity Rounds** only change your sword waves (Velocity's +15%
  damage counts for everything).

### The Ninja

Ninjas are quick (+12% move speed) and lightly protected (85% health). They mix thrown weapons with fast melee
(the Kusarigama, Bo Staff and Katana swing like a Knight's weapons; the Bo Staff and Katana cut small bullets).

| | Weapons | Space abilities | Ultimate |
|---|---|---|---|
| **Tank** | **Kusarigama** (a chain-sickle whipped in a 130 px, 120° arc that yanks enemies 40 px toward you), **Bo Staff** (spun all the way round you, 90 px, cutting small bullets), **Chain Shuriken** (a heavy star that flies out and comes back, cutting through everything both ways) | **Shadow Clone** (a decoy for 5 s that every enemy aims at; bursts for 60), **Smoke Bomb** (a 140 px cloud for 5 s: enemy bullets inside slow to half speed, allies inside take 30% less damage), **Iron Body** (2 s of 80% less damage, then a 50-damage shockwave) | **Thousand Clones** (70 s): five clones surround you for 8 s, drawing every enemy's fire, then burst |
| **Healer** | **Medicine Darts** (fast needles that heal allies they pass for 4), **Spirit Fan** (a short gust that heals allies in it and blows small enemy bullets back), **Ofuda** (slow homing paper charms that burst for 25 and heal allies near the burst for 8) | **Healing Mist** (a mist follows you for 6 s: allies inside heal 10 HP/s), **Substitution** (swap places with the ally nearest your cursor and shield them for 60; alone, slip toward your cursor and shield yourself), **Spirit Ward** (a talisman at your cursor for 8 s: allies near it regenerate and revive 3x faster) | **Blossom Storm** (80 s): for 5 s the whole party heals 20 HP/s and enemy bullets near every ally are erased |
| **DPS** | **Shuriken** (fans of 3 stars that ricochet once), **Katana** (fast 110° slashes; every 4th is a long lunging cut for double damage), **Kunai** (rapid knives, +50% damage to enemies that aren't targeting you) | **Shadow Step** (teleport behind the enemy nearest your cursor, up to 450 px away, and strike it for 80), **Vanish** (for 3 s enemies lose track of you; your next hit deals double), **Blade Fan** (12 kunai in a ring, 18 each) | **Death Lotus** (65 s): strike up to 8 enemies near your cursor in a flurry of cuts, 100 each |

Players can pick the same role if you want. Everyone also slowly regenerates health (0.5 HP/s).

Enemies sometimes drop **health orbs**: small green ones heal 15 HP (18% chance), and rare large ones
with a gold ring heal 60 HP (3% chance). Bosses always drop a large orb plus four small ones. Orbs drift
toward a hurt player who gets close, are left alone by players at full health (so your ally can grab
them), and blink before vanishing after 12 seconds.

### Weapons

Each role picks one of three starting weapons in the lobby. Damage, fire rate and Piercing upgrades work
with all of them.

**Extra projectiles** (Multishot, Iron Volley, and +2 during Bullet Storm) upgrade each weapon in its own
way instead of adding more bullets in a line. The Multishot and Iron Volley cards show what they'll do for
your weapon. Per extra projectile:

| Class | Weapon | Base | Each extra projectile |
|---|---|---|---|
| Tank · Soldier | **Scattergun** | Short-range spread of 5 pellets | +2 pellets and a 15% tighter cone |
| | **Flak Cannon** | Slow, heavy shells that explode on impact | Shells scatter 2 bomblets that pop where they land |
| | **Riot Repeater** | Steady single shots with a little spread | Fires in bursts: +1 round per burst, then a longer pause |
| Tank · Mage | **Flame Burst** | A short-range cone of fire | +2 flames and 25% more reach |
| | **Frost Lance** | Piercing ice spears that slow; the spearhead is wide (20 px), so it's easy to land | Spears shatter into 2 icicles sideways on every hit |
| | **Storm Hammer** | Heavy orb, lightning chains to 3 | Chains to 1 more enemy |
| Tank · Summoner | **Golem Staff** | Rocks, plus a bullet-blocking Golem | +1 Golem (up to 3) |
| | **Bone Guard** | Darts, plus 2 orbiting skeleton shields | +1 Skeleton Guard |
| | **Bear Totem** | Wide claw swipes, plus a Bear | +2 claws, wider swipes, and the Bear's bites hit enemies around its target |
| Healer · Soldier | **Light Bolt** | Accurate long-range bolts | +1 small homing bolt with every shot |
| | **Revolver** | Six hard-hitting rounds, the last one heavy (double damage). **Tap** for perfectly aimed shots (one every 0.45 s); **hold** to fan the hammer: after 0.2 s it empties the cylinder, a round every 0.07 s, each within 5° of your aim. Reloads in 1.2 s when empty, and slides rounds back in one at a time (every 0.25 s) once you stop shooting for 0.8 s | +2 rounds in the cylinder, loaded as heavy rounds |
| | **Mender's Bow** | **Hold to draw, release to fire** (you move at 85% while drawing). A tap looses a weak arrow (8 damage, heals 5); a full draw (0.9 s) hits for 40, flies at 950 px/s, pierces 2 and heals an ally it passes for 20. Let go within 0.25 s of full draw for a **perfect shot**: +25% (50 damage, heals 25). Holding longer keeps it at full; it never fires by itself | +1 arrow nocked: a full draw also looses an extra arrow (half damage) in a tight fan; taps stay single |
| Healer · Mage | **Radiant Orb** | Slow piercing orbs that heal allies | +1 Light Orb circling you that hurts enemies and heals allies it touches |
| | **Chain Light** | Bolts chain to 2 | Chains to 1 more enemy |
| | **Prism Beam** | Rapid short beam; grazing heals | +30% length, +25% damage, +50% healing |
| Healer · Summoner | **Sprite Staff** | Seeds, plus 2 healing sprites | +1 Sprite |
| | **Treant Rod** | Healing bolts, plus a Treant guard | The Treant grows 30%: blocks more, hits harder, reaches farther |
| | **Totem Staff** | Seeds; plants a healing totem every 10 s | Totems 60% more often |
| DPS · Soldier | **Twin Rifle** | Fast parallel twin stream | +1 barrel |
| | **Railgun** | Slow shots that pierce 3 | Throws 2 shrapnel shards sideways at every enemy it pierces |
| | **Burst SMG** | Very fast, inaccurate spray | +1 bullet per shot (half damage) and a wider spray |
| DPS · Mage | **Fireball** | Explodes on impact | Splits into 2 small fireballs on impact |
| | **Arc Lightning** | Bolts chain to 4 | Chains to 1 more enemy |
| | **Arcane Missiles** | 3 curving missiles | +1 missile, and they home in |
| DPS · Summoner | **Wolf Whistle** | Darts, plus 2 wolves | +1 Wolf |
| | **Wisp Lantern** | Weak bolts, plus 3 shooting wisps | +1 Wisp |
| | **Imp Caller** | Darts; exploding imps every 3 s | +1 Imp, arriving 50% faster |
| Tank · Knight | **Warhammer** | Slow overhead smash: 150° arc, 100 px reach, 50 damage, knocks back | +1 aftershock: a second smash 0.2 s later (60% damage, more reach) |
| | **Lance** | Heavy jab (36 damage) that hits a box straight ahead, 180 px long and 88 px wide, shoves enemies 140 px back (not bosses or heavies) and stuns them for 1 s (not bosses) | +30 px reach, 24 px wider and +60 px knockback |
| | **Halberd** | 160 px thrust (32) that hits everything along the blade | +30 px reach and a quick follow-up thrust (60% damage) |
| Healer · Knight | **Holy Mace** | 120° swings (22); each enemy hit heals you and allies within 220 px for 2 (up to 8 a swing) | +1 HP healed per enemy hit, and 60 px more healing reach |
| | **Lightbringer** | 100° sword cut (17) plus a crescent of light (12, pierces 2) that heals allies it passes for 6 | +1 crescent, fanned out |
| | **Censer** | Flail whirled all the way around you (14, 80 px); each spin heals allies within 140 px for 3 | +20 px reach and +1 HP healed per spin |
| DPS · Knight | **Twin Blades** | Very fast alternating 90° slashes (15) | +1 cut: each swing strikes once more (60% damage) |
| | **Greatsword** | Huge 170° sweeps (60) with 125 px reach | +1 sword beam: each sweep launches a piercing crescent (40% damage) |
| | **Rapier** | Narrow 130 px thrusts (18) at the nearest enemy in front; +10% per hit in a row on it (up to +60%) | Skewers 1 more enemy behind the first |
| Tank · Ninja | **Kusarigama** | 120° whips (26) with 130 px reach that yank enemies 40 px in | +20 px reach and the chain yanks 15 px harder |
| | **Bo Staff** | Spun all the way round you (16, 90 px), cutting small bullets | +20 px reach |
| | **Chain Shuriken** | A heavy star (22) that flies out 260 px and comes back, cutting through everything | +1 shuriken, fanned out |
| Healer · Ninja | **Medicine Darts** | Fast needles (7) that heal allies they pass for 4 | +1 dart, fanned out |
| | **Spirit Fan** | A short gust (6) that heals allies in it for 3 and blows small bullets back | +25% reach and +1 HP healed |
| | **Ofuda** | Slow homing charms that burst (25) and heal allies near the burst for 8 | +1 charm, fanned out |
| DPS · Ninja | **Shuriken** | Fans of 3 stars (9) that ricochet once | +1 star in the fan |
| | **Katana** | 110° slashes (20); every 4th is a 170 px lunging cut (40) | +1 cut: each slash strikes once more (60% damage) |
| | **Kunai** | Rapid knives (8), +50% on enemies that aren't targeting you | +1 knife, fanned out (half damage) |

With your own Revolver, its cylinder shows as pips under your character (gold = heavy rounds), and a ring
fills while it reloads.

Want a **wall of bullets** instead? The **Barrage** card (Rare, stacks to 3) makes every shot also fire a copy
fanned out to the side (8° steps, alternating sides). It stacks with the weapon upgrades above, so a Twin
Rifle with Multishot and Barrage fires several side-by-side barrels in a fan. A Revolver round fans out for a
single round of ammo.

### Parry

Hold **right click** to raise a small shield on the side facing your cursor. It blocks enemy bullets from that
side (including the red aimed shots that barriers can't stop) and melee enemies that run into it, shoving
them back. While it's up you **can't shoot** and you move at 70% speed. It soaks **150 damage**, then breaks and
can't be raised for 3 s; it refills when you lower it. Raise it just as a shot arrives for a **perfect parry**:
for the first instant after raising it, blocked bullets are sent back at enemies and cost nothing (it has to
have been down for half a second, so you can't spam it). A thin blue bar curving around the shield shows how much
it has left, shrinking toward your aim as it wears and blinking red when it's nearly gone; when it breaks it
cracks and shatters, and while it's lowered a faint bar shows it refilling. Against the Warden up close it holds for about 3 seconds of fire, enough to close the gap.

Parry upgrades show up like any other card:

| Upgrade | Rarity | Effect |
|---|---|---|
| **Reinforced Guard** | Common (x4) | +25% parry durability per stack |
| **Wide Guard** | Uncommon (x2) | The shield covers a 30% wider arc |
| **Riposte** | Rare (x2) | Perfect-parry window 0.12 s longer, and reflected shots deal double damage |
| **Counterstance** | Rare (x2) | A perfect parry gives +50% fire rate for 2.5 s |
| **Light Guard** | Rare | No slowdown while parrying |
| **Mirror Guard** | Weapon mod (x3) | Each blocked bullet has a 25% chance to fly back at enemies |

### Ultimates

**F** fires your **ultimate**. Each role and class combination has its own, fixed one: it
comes with your role and class and only changes if you pick a different one. Ultimates have long cooldowns
that upgrades don't shorten, and start each match half charged. The HUD shows the charge in the gold slot.

| | Soldier | Mage | Summoner | Knight |
|---|---|---|---|---|
| **Tank** | **Fortress** (70 s): for 8 s every enemy targets you and you take 75% less damage; erases nearby bullets and shoves enemies back | **Glacial Tomb** (75 s): erases every enemy bullet and freezes every enemy solid for 4 s; bosses take 250 damage instead | **Titan** (80 s): a stone titan smashes enemies for 15 s and blocks bullets (soaks 1000 boss damage) | **Last Stand** (70 s): for 8 s you can't drop below 1 HP, every enemy targets you, +50% damage |
| **Healer** | **Lifeline** (90 s): instantly revive and fully heal the whole party, plus 3 s of invulnerability | **Sanctum** (80 s): a 7 s holy dome; allies inside heal fast and can't go down, and bullets can't get in | **World Tree** (80 s): a tree for 10 s that heals allies, entangles nearby enemies and sends out 4 healing sprites | **Hallowed Blade** (80 s): a giant sword falls at your cursor (200 damage), then blesses the ground for 8 s |
| **DPS** | **Bullet Storm** (60 s): 6 s of triple fire rate and +2 extra projectiles | **Meteor Storm** (70 s): 12 meteors rain on enemies over 3 s, 150 damage each | **Legion** (70 s): 6 wolves and 6 bats for 10 s, and all your minions frenzy | **Blade Tempest** (65 s): for 6 s every swing also hurls three piercing sword beams |

### Ability mastery

Every ability (role abilities, the extra ones from upgrades, and ultimates) **levels up from 1 to 5 as you use
it** during a run. Each cast during a fight earns XP equal to the ability's cooldown in seconds, so fast and
slow abilities level at about the same pace. Instant abilities (Shockwave, Frost Nova, Mend Pulse, ...)
earn by what they did: half for a cast that hit nothing, up to 1.5x for one that hit, healed or erased a
lot. Casting between waves or with no enemies around earns nothing. Levels need 100 / 300 / 550 / 850 XP in
total: about level 3 by wave 8-10 and level 5 by wave 20-25 if you use it whenever it's ready. On the
ability bar, the thin green line on top of each ability's recharge bar is its progress to the next level (full
once mastered); the Tab menu shows the level.

- **Each level:** +8% to everything the ability does (damage, healing, shields, buff duration, and the zones,
  minions and shots it creates) and 4% off its cooldown. Level 5 is +32% and -16%.
- **Level 5 unlocks its capstone**, a change to how it works:

| Ability | Capstone | Ability | Capstone |
|---|---|---|---|
| **Barrier** | Bullets that hit the barrier are reflected back as your own. | **Mend Pulse** | Also erases enemy bullets within its range. |
| **Dash** | +1 charge. | **Shockwave** | Also knocks nearby enemies back 120 px. |
| **Overdrive** | +1 extra projectile while it lasts. | **Blink** | Erases enemy bullets where you leave and where you land. |
| **Nova** | Fires 48 shots instead of 24. | **Turret** | Deploys two turrets. |
| **Roar** | Also knocks enemies within 250 px back and slows them for 2 s. | **Bulldoze** | Erases enemy bullets along the charge. |
| **Haven** | Allies inside also take 30% less damage. | **Soul Link** | Lasts 9 s, and both of you are invulnerable for 1 s when it starts. |
| **Cluster Grenade** | Throws a second grenade 80 px further. | **Mark for Death** | Marks the three enemies nearest your cursor. |
| **Arcane Aegis** | Lasts 4.5 s and reflects bullets for double damage. | **Frost Nova** | Also erases enemy bullets around you. |
| **Flame Wall** | The fire also slows enemies by 40%. | **Time Rewind** | Also erases enemy bullets around every player. |
| **Arcane Ward** | The shield soaks 120 damage. | **Sanctify** | Also freezes enemies around you solid for 1.5 s (not bosses). |
| **Meteor** | Leaves burning ground for 3 s. | **Blizzard** | Lasts 10 s. |
| **Arcane Surge** | +1 extra projectile while it lasts. | **Golem Wall** | Raises five walls. |
| **Earthquake** | Also freezes the enemies it hits for 1 s (not bosses). | **Guardian Spirit** | The guardian also heals you 6 HP/s. |
| **Spirit Swarm** | Releases 8 sprites. | **Phoenix** | Revives at full health, and the phoenix keeps fighting. |
| **Soul Lantern** | The lantern follows you. | **Bat Swarm** | Releases 14 bats. |
| **Sacrifice** | Double explosion damage, and your companions return at once. | **Alpha Call** | Also summons 3 wolves for 6 s. |
| **Fortress** | Lasts 12 s. | **Glacial Tomb** | Enemies stay frozen for 6 s and bosses take 500. |
| **Titan** | The titan is bigger and stays for 25 s. | **Lifeline** | Everyone also gets 4 s of Overdrive. |
| **Sanctum** | The dome is half again as wide (300 px). | **World Tree** | Lasts 16 s with 8 sprites. |
| **Bullet Storm** | +4 extra projectiles instead of +2. | **Meteor Storm** | Twenty meteors. |
| **Legion** | Summons 10 wolves and 10 bats. | **Shield Wall** | Bullets that hit the shield are reflected back as your own. |
| **Leap Slam** | The landing also erases enemy bullets within 160 px. | **Unbreakable** | Lasts 8 s. |
| **Consecrate** | The blessed ground follows you. | **Guardian's Oath** | Everyone within 300 px gets the full shield. |
| **Lay on Hands** | Also heals everyone else near them for half as much. | **Whirlwind** | Lasts 3 s and cuts every bullet except red aimed shots. |
| **Lunge** | +1 charge (three in all). | **Battle Fury** | +1 extra projectile while it lasts. |
| **Last Stand** | Lasts 12 s. | **Hallowed Blade** | Three swords fall: one at your cursor and one either side. |
| **Blade Tempest** | Five sword beams a swing instead of three. |  |  |

A popup appears when an ability levels up. The **Tab** menu shows each ability's level as five pips (the next
one fills as it earns XP); hover it to read what level 5 unlocks.
Mastery resets each run.

## Levels and upgrades

Every enemy killed gives **both** players XP. Level up during a wave and you'll see **LEVEL UP!**; when
the wave is cleared, each player picks one of **3 upgrades per level gained**: click (or press 1/2/3) to
select a card, then **Confirm** (or Enter). A panel on the left shows your stats (damage, fire rate, speed,
cooldowns, damage taken, regen, projectiles, pierce, charges, bonus abilities, legendaries); with a card
selected, the stats it would change are highlighted with their new values. Don't like the cards? **Reroll** (or R) for a fresh set. You
start with 2 rerolls and everyone gets 2 more for each boss killed. The next wave waits until everyone
have chosen.

| Rarity | Color | What you get |
|---|---|---|
| **Common** | white | Small stat boosts: +8% damage, +8% fire rate, +6% speed, +15 HP, regen, -6% cooldowns |
| **Uncommon** | green | Bigger boosts (+20% damage, +40 HP, -15% cooldowns...) and damage reduction |
| **Rare** | blue | **Second Charge** for your Space ability, an upgrade for a specific ability you have, on any key (e.g. bigger Barrier, longer Roar, double-damage Bulldoze, bigger Haven, faster Soul Link, bigger grenades, harsher marks; for Knights: Tower Shield, Earthshaker, Iron Will, Sacred Ground, Bulwark, Miracle, Cyclone, Flurry, Bloodlust; for Ninjas: Mirror Image, Thick Smoke, Tempered Steel, Morning Dew, Body Double, Sacred Seal, Assassinate, Unseen Blade, Storm of Steel), wider aura; plus Multishot, Barrage, Piercing Rounds and Overcharge (second charge on Q/E/Shift) |
| **Very rare** | purple | A brand-new ability on **Q**, **E** or **Shift** (you choose the key; each one only once): a universal one (Shockwave, Overdrive, Blink, Nova or Turret), or one of your **class's two other Space abilities**, the ones you didn't pick in the lobby (these count double, so about 4 in 9 very rare cards are class abilities) |
| **Legendary** | orange | Unique weapon upgrades, **only from bosses**: after a boss dies, each player picks one of that boss's three (can't be rerolled) |
| **Weapon mod** | teal | Also **after every boss**, right after its legendary: choose any mod from the full list (each stacks up to 3 times) |

| Boss | Legendary weapon upgrades |
|---|---|
| The Warden | **Warden's Halo** (every 4th volley adds a ring of 8 shots) · **Iron Volley** (+1 projectile, +25% damage) · **Ringburst** (kills burst into 6 shots) |
| Hive Queen | **Swarm Rounds** (shots split into 3 on hit) · **Royal Jelly** (hits heal 1 HP, up to 10/s) · **Brood Mother** (20% of kills spawn a 4 s mini-turret) |
| Twin Reapers | **Executioner** (double damage below 35% HP) · **Twin Fang** (25% chance of double damage) · **Soul Harvest** (kills heal 5 HP) |
| The Singularity | **Collapse Rounds** (shots implode on impact) · **Graviton Rounds** (hits slow enemies 40%) · **Dark Matter** (+2 pierce, +20% damage) |

**Weapon mods** change how your shots behave, and you can see them on your bullets:

| Mod | Per stack |
|---|---|
| **Homing Rounds** | Shots bend toward an enemy within 260 px and about 45 degrees of their path, so near misses connect (they don't turn around to hunt). Costs 10% damage per stack; more stacks bend harder |
| **Ricochet** | After a hit, the shot bounces to another enemy within 300 px for 75% damage; +1 bounce per stack |
| **Incendiary Rounds** | Hits set enemies on fire for 3 s, burning 25% of the hit's damage per second |
| **Cryo Rounds** | Hits slow enemies 20% (up to 60%) with a 6% chance to freeze non-bosses for 1 s |
| **Shock Rounds** | 15% chance per hit for lightning to arc to 2 more enemies at 60% damage |
| **Explosive Rounds** | Shots explode on impact for 30% damage around them (bigger blast per stack) |
| **Velocity Rounds** | Shots fly 30% faster and farther, +15% damage |

Mods apply to your weapon's shots and your minions' shots, but not to grenades. On a Knight, the elemental mods
apply to every melee hit (see The Knight above).

Rarer cards show up a bit more often as you level. All the numbers live in
`Assets/Scripts/Core/Upgrades.cs` if you want to rebalance or add upgrades.

## Rules

- The arena is 1600x900 and **wraps around for players**: walk off any edge and you come in on the opposite side. Enemies and bullets don't wrap, so the edges make a good escape route.
- Your hitbox is the small white dot in the middle of your character, not the whole circle.
- **Status effects** show as a tint on the enemy plus an icon above it: **snowflake** = frozen (encased in ice, can't move or shoot), **blue double arrow** = slowed, **flame** = burning (flames rise off it), **red crosshair** = marked (takes extra damage).
- At 0 HP you're **downed**. Stand next to a downed ally for 3 seconds to revive them (or the healer can pulse them back).
- If everyone is downed, it's game over. Clearing a wave revives anyone downed and heals everyone a bit.
- New enemy types unlock as the waves go up (see below), with a boss every 5 waves.
- **Boss bodies:** touching a boss hurts once (25 + 0.6 per wave) and shoves you about 100 px back out; it can't hurt you again for 1 s.
- **Bullet budget:** enemy bullets on screen are capped at 50 + 4 per wave, up to 120 from wave 18 (30% more per extra player). When the screen is over the cap, regular enemies hold their fire and try again a moment later. Bosses always fire, and while a boss is up the other enemies only get 40% of the cap. Later waves get harder through tougher enemies, not more bullets.

## Enemies

The first waves are **melee rushers** (spiky, they never shoot, they hurt on contact). Shooters take over a
little more each wave: wave 1 is all melee, wave 7 about half, and from **wave 13** there are no melee left.

| Wave | Melee enemy | What it does |
|---|---|---|
| 1 | Swarmer | Small and fast, weaves toward you, comes in pairs |
| 3 | **Brute** | Slow and tough; hits hard (24) when it reaches you |
| 5 | **Lunger** | Circles in close, flashes a pink line, then leaps along it. Step aside |

| Wave | Shooter | What it does |
|---|---|---|
| 1 | Grunt | Aimed 3-shot spreads |
| 2 | Burster | Rings of bullets (one more per wave, up to 18) |
| 3 | Sniper | Fast, accurate shots from far away |
| 4 | Spinner | Wanders and sprays spirals in **bursts** (about 1.3 s on, 2 s off); it glows and spins up while bursting |
| 6 | **Charger** | Flashes a red warning line, then dashes at you and bursts into a ring. Step out of the line |
| 7 | **Splitter** | Splits into 3 fast Shards when killed |
| 8 | **Mortar** | Lobs bombs at where you're standing; they burst into a ring when they land. Keep moving |
| 9 | **Shielder** | A shield on its front blocks your shots. It turns slowly, so flank it |
| 11 | **Medic** | Heals nearby enemies every few seconds. Kill it first |
| 16 | **Phantom** | Flickers, blinks next to you and bursts into a ring |
| 21 | **Hexer** | Fires curving pincer shots that close in from both sides |
| 26 | **Juggernaut** | Armored and slow; can't be slowed or knocked back; fires rotating stars of big orbs |
| 31 | **Nest** | Stationary spawner that keeps hatching Shards until destroyed |
| 12+ | **Elites** | Any enemy can spawn with a gold ring: 2.5x health, bigger, shoots faster, worth 3x XP, always drops an orb. More common each wave |

## Bosses

**Mitigation, not immunity.** Barriers, Flame Walls, the Guardian Spirit, Golems, Treants, Stone Walls and
Arcane Aegis stop bullets *coming in*, so a tank can still soak fire for the team, but bullets fired from
inside them pass through: you can't muzzle a boss by standing on it. Against regular waves blockers hold
for their full duration; **only boss bullets wear them down**. Each soaks a limited amount of boss damage
(Barrier: 360, more with Bastion): it fades as it weakens, then breaks; companions are dazed for a few
seconds instead. Boss bullets are easy to spot: they have a **dark core**. Violet-spiked ones wear blockers
down; **red ones (aimed shots) can't be blocked** and must be dodged, and standing
inside a boss hurts more each wave.

Bosses give soft sound cues when they start an ability: a low swell when they switch attacks, a rising shimmer
before something telegraphed (lasers, bombers, lightning), a deep thump for slams and stomps, and a chime for
summons and phase changes.

The game has **50 waves** with a different boss every 5 waves. Bosses after wave 20 get extra health (+10% per wave on top of the normal scaling: x2 at wave 30, x4 at wave 50) to keep up with stacked upgrades. Their name, health bar and warnings show at
the top. Beat The Eclipse at wave 50 to win; the host can then keep going in **endless** mode (C), where the
bosses come back around, tougher each time.

| Wave | Boss | Mechanic |
|---|---|---|
| 5 | **The Warden** | Cycles through spirals, rings of big orbs, aimed fans, and **cross streams**: for 12 s it dashes to four different spots in turn, and at each one plants and fires straight streams up, down, left and right — a static "+" that cuts the arena into quadrants (faint guide lines show it while it is planted, not while it moves) |
| 10 | **Hive Queen** | Loops through three stages: **shooting drones** (tough Shards, 3x normal health, topped up to 8 each loop) — she's **immune while any are alive**, but they don't hold up the fight; then **explosive drones** that fly straight at the spot each player was standing on when they launched (a marker shows where) and blow up there, so keep moving; then her **bullet patterns** (aimed fans, then rings); then drones again |
| 15 | **Twin Reapers** | **Soul Bond**: whichever Reaper is more than 10% further down on health is **immune** until you even them out, so split your damage. They circle the arena fast, and a **tether** between them sweeps it and hurts on touch (dash through it). Kill one and the other **enrages**; finish it within **6 s** or the fallen twin revives at 60% |
| 20 | **The Singularity** | Sits in the middle and loops four attacks: **Pull** (4 s, drags everyone toward it under a three-armed spiral), **Collapse** (3 s of bullet rings), **Normal** (8 s: a purple spiral, then three red **lasers** from its centre to the edges that flicker as a warning and slowly sweep around; touching one hurts) and **Cage** (9 s: three small wells orbit every player in a ring that shrinks; touching the ring hurts, and shooting the three wells breaks it). Four big **Gravity Wells** sit in the corners all fight: they bend your shots hard into themselves and slowly drift you toward them, so kill them; they come back 15 s after the last one dies |
| 25 | **The Colossus** | Stomps out huge rings with **one gap** to slip through, and throws rocks |
| 30 | **The Duelist** | Teleports every few seconds and leaves **decoys** that look just like it; decoys burst into rings unless killed |
| 35 | **The Lich** | **Immune while its phylactery stands** somewhere in the arena; break it, then strike before a new one appears. Raises skeleton grunts |
| 40 | **Storm Herald** | Calls **lightning strikes** where you stand (warning circle first). Keep moving |
| 45 | **The Devourer** | Gets **hungry** for 3 s of every 10: your shots **heal** it, so stop shooting |
| 50 | **The Eclipse** | Final boss in 3 phases: spirals and rings → gravity wells and phantoms → lightning storms and a frantic spiral |

## Loadout preview

The hero card in the lobby shows your loadout in a small live arena: your character uses the chosen weapon
on three target dummies, so you can see how it attacks. Hover an ability button (or pick one) and it casts
that ability every few seconds. It's the real game running privately on your computer (nothing is sent to
the other players), so it always matches how weapons and abilities really behave.

## Lobby backgrounds

The lobby's **Background** card (bottom left) switches the animated background behind the menus: Spiral, Warp,
Aurora, Synthwave, Bokeh, Hex Pulse, Orbits, Digital Rain, Nebula or Tides. It shows a live thumbnail (click it
for the next one), arrows either side of the name and a dot per background (click one to jump to it). Untick
**Animated** for a still image. Each player's choice is saved on their own computer. Your pick also shows faintly behind the arena
during matches (dimmed so the fight stays easy to read).

The card's **-** button minimizes it to a slim tab that fades away after a few seconds; hover where it was to
bring the tab back, and click it to open the full card again (remembered on your computer).

## Paths

After each boss (and its rewards), the host picks one of **two paths** for the four regular waves before the next
boss; everyone sees the choice, and the next wave waits for it. Bosses are always the standard fight. A path changes
the enemy mix and the arena floor, and pays off by its danger (1-3 skulls):

| Danger | While on the path | Prize (before the boss) |
|---|---|---|
| 1 | no bonus | full heal and 2 rerolls |
| 2 | +25% XP and score | +1 upgrade pick |
| 3 | +50% XP and score | an extra legendary pick |

| Path | Danger | What changes |
|---|---|---|
| **Swarm Fields** | 2 | Mostly melee rushers (Swarmers, Brutes, Lungers), twice as many, at 60% health |
| **Sniper Ridge** | 2 | Mostly Snipers, Mortars and Grunts; hardly any melee |
| **Elite Gauntlet** | 3 | Half as many enemies, every one elite |

A chip under the wave plate shows the path and the waves left. Session recordings note each wave's path. More
paths (hazard floors, twists, arena rules) are planned in `Docs/Paths.md`.

## Run modifiers

The lobby's **Modifiers** tab lists switches that change the whole run, for everyone (Practice Range too). The
host turns them on and off; the others see them, and changing one takes back everyone's Ready. Active ones show
as chips under the wave plate during the match, and session recordings list them.

| Modifier | Effect | |
|---|---|---|
| Glass Cannon | Everyone deals 50% more damage, and takes 50% more | Risky |
| Tough Crowd | Enemies and bosses have 50% more health | Harder |
| Frenzy | Enemies and bosses shoot 30% faster (the bullet budget still applies) | Harder |
| Swift Foes | Enemies move 30% faster | Harder |
| Famine | Enemies never drop health orbs (bosses still do) | Harder |
| Lucky | Everyone starts with 3 extra upgrade rerolls | Easier |

## Practice Range

In the lobby the host picks **Run** (the normal 50 waves) or **Practice Range** at the top. Practice Range
plays waves you build yourself, with everyone in the lobby, and upgrade picks between waves as usual.

- **Practice waves** tab: add as many waves as you like (**+ Add**, **Copy**, **Remove**). Each has a **level**
  (1-50) that sets how strong its enemies are: their health and fire rate as on that wave of a normal run.
  Tick **Loop the waves** to go round again after the last one; otherwise the run ends with **PRACTICE COMPLETE**.
- **Enemies:** every one of the 17 regular enemies and 10 bosses is shown moving, as in a match, so you don't
  need to know their names. Click one to add it to the selected wave, right-click (or its corner **-**) to take
  one away; hovering shows what it does. Bosses spawn first and bring their usual adds; the Twin Reapers come
  as a pair.
- **Shared:** everyone in the lobby sees the waves change as they're edited, with the editor's name on the wave
  they're working on (players who can't edit follow along to it). The host can tick **Let others edit** so
  friends can change the waves too.
- Everyone keeps their own loadout (the **Your loadout** tab).
- **Save** writes the setup to a text file named after the name box, in the `practice` folder next to the
  sessions folder (**Open folder** shows it). **Load** lists saved setups. The files are easy to edit by hand:

  ```
  # Top-Down Arena practice setup: warden drills
  loop: no
  wave 5: Grunt x4, Sniper x2
  wave 10: The Warden x1, Medic x2
  ```

  One line per wave: `wave <level>:` then each enemy as `<name> x<count>` (the count defaults to 1). Names
  ignore case and "The". Unknown names are skipped and listed when you load.

## Controls

| | |
|---|---|
| Move | WASD / arrow keys |
| Aim | Mouse |
| Shoot | Hold left click |
| Role ability | Space |
| Parry | Hold right click |
| Ultimate | F |
| Bonus abilities | Q, E and Shift (once unlocked). When a card gives you one, choose its key on the card screen; move them between keys any time in the Tab menu (cooldowns move with them) |
| Pick an upgrade | Click a card (or 1 / 2 / 3), then Confirm (or Enter) |
| Reroll upgrades | Reroll button or R |
| Mute sound | M |
| Stats, upgrades and key layout (just for you) | Hold Tab (click a key on a bonus ability to move it). Works while choosing an upgrade too: the cards come back when you let go |
| Pause menu | Esc |
| Developer tools | F1 (see below) |
| Aim line on / off | V (the line runs from you to the crosshair, but never past your weapon's range: as far as your shots fly, or your swing reaches; a small cap marks the end when the crosshair is beyond it) |
| Aim debug overlay | F3 |
| After game over / victory (host) | R = play again, L = back to lobby, C = keep going (endless, after victory) |

## End of a run

When a run ends (game over or victory) a results screen shows the wave reached, time, score and total
kills, plus a column per player: level, damage dealt, kills (elite and boss), biggest hit, accuracy,
healing done, allies revived, damage taken, times downed, abilities used, and the upgrades picked by
rarity. The best number in each row is highlighted, with awards like **Most damage**, **Best healer** and
**Sharpshooter**. The host can play again, go back to the lobby, or keep going after a victory.

## Loadout screen (Tab)

Hold **Tab** during a match to see your stats, weapon, abilities (with charges, cooldowns and mastery level) and every
upgrade you've picked, rarest first. It only shows on your screen: the game **doesn't pause** and your
friend isn't affected, so you can check it mid-fight (you can still move and shoot while it's up).

## Pause menu and settings

Press **Esc** in a match to open the pause menu. Pausing **freezes the match for everyone** (your friend
sees who paused) and either player can resume. The menu has volume sliders for **Master**, **Music** (the menu theme), **Weapons** (your
and your friend's shots and spells), **Enemies** (enemy fire) and **Effects** (explosions, abilities, UI),
a mute toggle, and **Leave game**. Beside it, the **Field guide** opens the **Bestiary** (every enemy and boss)
and the **Armory** (every class, weapon, ability and upgrade) in your browser, with a live preview of each
(in the Unity editor you can also open the local copy from `Docs/`). The same audio options are under **Settings** on the main menu, and
they're saved between sessions.

**Music:** everything is generated in code, like the sound effects. The menus and lobby play a synthwave
theme (`Assets/Scripts/Game/MenuMusic.cs`); matches play a soft track during regular waves, and **every boss
has its own track** that crossfades in while it's on the field (`Assets/Scripts/Game/GameMusic.cs`, where each
track is a short recipe: tempo, key, chords and which instruments play). Tracks render in the background, and
the next boss's track is prepared ahead of time. The **Music** slider controls all of it.

## Playing with a friend

**You both need the same version of the game.** The version is shown at the bottom of the main menu and
in the lobby. If they don't match, the joining player is turned away with a message saying so. After
pulling an update, everyone should rebuild (or the host should send the new build).

One person **hosts**, the other **joins**. The host's game acts as the server, so you don't need anything else.

1. Both of you install [Tailscale](https://tailscale.com/download) (free) and join the same tailnet.
2. Host: open the game, click **Host game**. Click the **!** next to the Lobby title to see your port and addresses; give your friend the one starting with `100.`.
3. Friend: type that address into **Host address**, click **Join game**.
4. Both pick a loadout and click **Ready** (changing your loadout un-readies you). Once everyone is ready, the host clicks **Start game**.

Without Tailscale, the host can forward **TCP port 7777** on their router to their PC and give the friend
their public IP. On the same Wi-Fi, use the host's local `192.168.x.x` address. Windows Firewall may ask
the host to allow the game the first time they host; click **Allow**.

## Making a build to send to your friend

In Unity, use the menu **Arena → Build Windows Game**. It builds to `Build/TopDownArena/` and opens the folder.
Zip the whole `TopDownArena` folder (the `.exe` needs the files next to it) and send the zip.
On first launch Windows may say "Windows protected your PC"; click **More info → Run anyway** (the game isn't code-signed).

To test multiplayer on one computer, run the built `.exe` twice (or the `.exe` plus the editor): host in one, join `127.0.0.1` in the other.

## Testing tricks

The built game accepts command-line flags so you can skip the menus:

```
TopDownArena.exe --host --role=tank --autostart=2     # host and auto-start when a friend joins
TopDownArena.exe --join=127.0.0.1 --role=dps          # join a local host
```

Also: `--name=X`, `--port=N`, `--class=soldier|mage|summoner`, `--weapon=1|2|3`, `--ability=1|2|3`, `--start-wave=N` (jump ahead), `--bot` (your player moves and shoots by itself).

`Tools/SimTest` runs a host and a client against each other over localhost without Unity and checks that
they stay in sync (`Tools/SimTest/run.sh [start-wave] [seconds]`, needs Mono).
### Developer tools (F1)

Press **F1** anywhere for the developer menu (Esc, F1 or its X closes it). Tabs down the left, and a card at
the bottom of them shows what's on (wave, recording, bot, aim debug, players):

- **Match (host):** pick a wave with -10 / -5 / -1 / +1 / +5 / +10 and **Go to wave N**. It clears the arena, gets
  downed players back up and starts that wave after 2 s. The boss on that wave (or the next boss) is shown
  moving beside it. **Everyone** buttons: full heal, level up, max mastery and god mode for every player at once.
- **Players (host):** choose a player, then edit any of their stats by hand: type a number or use - / +.
  Type in **Filter** to narrow the list (e.g. "barrier"). Edited stats show in gold, are sent to every player's
  game, and stay on top of upgrades they pick later; **Reset stats** goes back to what their upgrades give.
  Also **God mode** (takes no damage), **Full heal**, **Level up** (an extra upgrade pick) and **Max mastery**
  (every ability they have to level 5). Joining players can see everyone's stats but not change them.
- **Tools:** record sessions (below) and open the sessions folder, the aim debug overlay (also F3), a bot that
  plays for you, open the practice folder, and the **Boss Lab**.

### Boss Lab (F1)

**F1 → Boss Lab** sets up a boss fight on the real game and lets you watch bots play it, to see how each
boss plays out and what to change.

- **Setup:** pick the boss (by wave), 1-3 players and each one's role, class, weapon and ability. Tick
  **I'll play this one** to take player 1 yourself. Choose the **bot skill** and the **upgrades**: by
  default each player gets the number of level-up picks a team typically has at that wave (random picks,
  measured from full runs), a legendary and a weapon mod for every boss already beaten, and ability mastery
  for the wave.
- **While it runs**, a panel on the right shows pause and 1x / 2x / 4x / 8x speed (**P** also pauses), fight
  time, boss health, the team's DPS on the boss, enemy bullets on screen against the bullet budget, each
  player's boss damage, damage taken, downs and ability casts, and what hit them most. **Restart** reruns the
  same setup; **Setup** changes it.
- When the boss dies, the party wipes, or 5 minutes pass, a result card shows the summary with **Run again**.
- Lab fights aren't saved as session recordings. Starting the lab leaves any match you're in.

**The bots** play like a person. They plan a short way ahead, moving in the 8 WASD directions to avoid where
bullets, enemy bodies, lightning strikes and the Reapers' tether will be. They keep a distance that suits
their weapon and role, lead their shots, and go for the adds that protect a boss first (phylacteries,
gravity wells, the Queen's shield drones). They revive downed allies, heal when someone is hurt, use
defensive abilities when things get tight, and use the rest on the boss.

| Skill | Reacts after | Plans ahead | Keeps track of | Changes direction |
|---|---|---|---|---|
| Casual | 0.35 s | 0.3 s | nearest 6 bullets | ~4 times a second |
| Good | 0.24 s | 0.4 s | nearest 12 bullets | ~7 times a second |
| Expert | 0.15 s | 0.5 s | nearest 25 bullets | ~12 times a second |

Bots don't parry, pick up health orbs, or reroll upgrades.

From the command line, `Tools/SimTest/run.sh 1 1 lab <wave> <skill 1-3> <runs> <seat> [seat] [seat]` runs
fights without drawing and prints the results. A seat is role/class/weapon, for example `T/S/0` (Tank
Soldier, first weapon), `H/M/1`, `D/U/2` (U = Summoner) or `T/K/0` (K = Knight).

### Session recording (for balancing)

**Record sessions** in the F1 menu is on by default (turn it off there; `--record` forces it on). A red
**● REC** in the bottom-right corner shows a match is being recorded. The Unity editor and a built game keep
separate settings, so check it in each. While you **host**, every match (solo or with friends) is saved to a JSON file in the sessions folder (the **Open sessions folder** button shows it; on Windows it's
`%USERPROFILE%\AppData\LocalLow\<company>\<product>\sessions`). The host decides every hit, so the file
covers all players. The end-of-run screen shows where it was saved. It's rewritten after every wave, so a crash
keeps everything up to the last cleared wave.

Each file records:

- the team (role, class, weapon, ability, ultimate), party size, start wave, and how it ended
- **per wave:** length, enemies spawned and killed by type, and per player: damage dealt (and to bosses),
  kills, damage taken **broken down by source** (e.g. "The Warden (aimed)", "Brute (contact)"), healing,
  health orbs, downs, revives, seconds spent under 25% health, health and level at the end, ability uses
  and parry blocks / perfect parries / breaks
- **per boss:** health, time to kill, each player's damage, damage players took and downs during the fight
- **every down** (who, when, what did it), **every upgrade pick** (the cards offered and the one taken) and rerolls
- **per enemy type:** spawned, killed, average lifetime and damage dealt to players
- a **sample every 5 s** of everyone's health, enemies and bullets on screen

Summarize one run or a whole folder of them (needs Python 3):

```
python Tools/SessionReport/report.py path/to/sessions
```

It prints boss kill rates and times, difficulty by wave (damage taken, downs, time at low health), what
downs players, where damage comes from, the most dangerous enemies per spawn, upgrade pick rates, ability
and parry use, and how each class and role performs.

`Tools/SimTest/run.sh 1 1 ttk [last-wave]` is a balance probe: it plays a whole run with invulnerable bots
that level up and pick upgrades at random, and prints how long each boss survived.

## Code layout

- `Assets/Scripts/Core/`: plain C# with no Unity dependency
  - `Defs.cs`: role, enemy and bullet stats; tweak numbers here to rebalance
  - `Upgrades.cs`: the upgrade cards, rarities and the extra abilities
  - `EnemyAI.cs`: waves, enemy behaviour and attack patterns, boss mechanics
  - `LateGame.cs`: waves 16-50 enemies and bosses, lightning strikes, victory and endless mode
  - `StatSheet.cs`: the stats shown beside the upgrade cards
  - `Kits.cs` / `Minions.cs`: minion and zone definitions; mage and summoner weapons, abilities and minion AI
  - `Ultimates.cs`: the nine ultimates
  - `Mastery.cs`: ability mastery levels, XP and capstone descriptions
  - `Knight.cs`: the Knight class: melee swings, bullet cutting, its abilities and ultimates
  - `Paths.cs` / `PathsUi.cs`: divergent paths (offer, choice, enemy mixes, bonus and prize; the cards, floor and chip)
  - `Modifiers.cs` / `ModifiersUi.cs`: run modifiers and the lobby's Modifiers tab
  - `Practice.cs`: the Practice Range's waves, their text format, and building a practice wave
  - `MenuBackgrounds.cs`: the 10 menu backgrounds (also dimmed behind the arena) and the lobby's picker
  - `LobbyUi.cs` / `PracticeUi.cs`: the lobby (mode switch, party, loadout with a live mini-arena preview) and the Practice Range editor
  - `Showcase.cs`: the loadout preview: an offline copy of the game with target dummies
  - `BotBrain.cs` / `BossLab.cs`: the bots that play like a person, and the Boss Lab's fight setup and scoring
  - `Game.cs`: the match: host simulation (enemies, bullets, waves, damage) and network sync
  - `Session.cs`: hosting, joining, lobby and role selection, message types
  - `Net.cs`: small TCP transport
- `Assets/Scripts/Game/ArenaApp.cs`: the Unity side: input, drawing (GL), menus and HUD (IMGUI)
- `Assets/Scripts/Game/SoundBank.cs`: sound effects, synthesized in code (no audio files). Each weapon has
  its own gunshot recipe (muzzle crack, body, thump, room tail, reverb) you can tweak
- `Assets/Editor/ArenaEditor.cs`: creates the scene and the **Arena** build menu
