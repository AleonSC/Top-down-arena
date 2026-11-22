# Reference tables

Generated from `Defs`, `Upgrades` and `Classes` by `Tools/SimTest/run.sh 1 1 refdocwrite`; don't edit by hand (the `refdoc` test fails when it is stale).
Numbers are before upgrades. Player-facing prose is in [README.md](../README.md).

## Roles

| Role | Health | Speed | Fire interval | Ability | Cooldown |
|---|---|---|---|---|---|
| Tank | 200 | 185 | 0.45 s | Barrier | 12 s |
| Healer | 140 | 215 | 0.25 s | Mend Pulse | 10 s |
| DPS | 110 | 240 | 0.1 s | Dash | 3.5 s |
| Wildcard | 125 | 225 | 0.1 s | Any DPS ability | 3.5 s |

## Classes

| Class | Health | Speed | About |
|---|---|---|---|
| Soldier | x1 | x1 | Guns and gear. Straightforward firearms with an Alt trick each (a point-blank shell, a proximity mine, a grenade volley) and field kit for abilities. Full health and normal speed: the steady pick. |
| Elementalist | x0.85 | x1 | Spells of fire, frost, lightning, water, wind, earth and light. Elements combine: soaked enemies conduct lightning, burning ones keep burning, frozen ones slow down. Big area damage, at 85% health. |
| Summoner | x0.9 | x1 | Fights through minions. Your weapon brings its own creatures (wolves, wisps, imps, bees) that hunt, bite and soak hits, and they stay with you between waves. Point them at what matters. 90% health. |
| Knight | x1.15 | x1 | Melee. Holding fire swings instead of shooting, hitting every enemy in the arc and cutting small bullets out of the air in front of you (big orbs, mines and red aimed shots still get through). Armoured for the thick of it: 115% health. |
| Ninja | x0.85 | x1.12 | Thrown blades and quick melee with tricks and misdirection: stars that come back, stances, smoke and shadow clones. Fast (+12% move speed) but lightly protected (85% health): hit, slip away, hit again. |
| Hemomancer | x1 | x1 | Blood magic: health is your resource. Every 1st ability costs a share of your health (never below 1 HP), every hit heals you for 3% of its damage, and you hit harder the lower your health (up to +40%). Shorter cooldowns make up for it. Full health and normal speed: live on the edge. |
| Engineer | x1 | x0.95 | A builder who works in vectors: robots, walls, pylons and breaches drawn in cyan ink. Draftsman: +25% vectors. One robot weapon, ability and ultimate per role for now. Full health, a little slower (95%). |
| Arcanist | x0.9 | x1 | A mage of telekinesis, runes and enchantments: shards and blades that fly at your will, enemies lifted and thrown, sigils on the floor. Kinetic Field: enemy shots slow down near you. One weapon, ability and ultimate per role for now. 90% health. |

## Weapons

### Soldier

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Scattergun | Tank | 0.45 s | 7 | 520 x 0.5 s | - | 154 | Primary Fire: A short-range shotgun spread, brutal up close.  Alternative Fire: Fire all your loaded shells at once. |
| Flak Cannon | Tank | 0.8 s | 30 | 380 x 1.1 s | - | 36 | Primary Fire: Shells that burst in the air at your cursor and destroy enemy bullets.  Alternative Fire: Lay a proximity shell that bursts when an enemy steps on it. |
| Riot Repeater | Tank | 0.18 s | 11 | 600 x 0.8 s | - | 60 | Primary Fire: Steady shots that suppress enemies so they shoot slower.  Alternative Fire: Lob tear gas that slows and suppresses. |
| Grenade Launcher | Tank | 0.42 s | 34 | 900 x 0.42 s | - | 49 | Primary Fire: Lob a drum of grenades that burst where they land.  Alternative Fire: Swap to smoke that slows enemies and stops them attacking. |
| Repulsion Cannon | Healer | 0.7 s | 6 | - | 240 | 9 | Primary Fire: Blasts enemies away in a cone and knocks health orbs loose.  Alternative Fire: Hold to pull enemies in. |
| Revolver | Healer | 0.45 s | 24 | 700 x 1.1 s | - | 82 | Primary Fire: Tap for aimed shots, or hold to fan the hammer for a quick spray.  Alternative Fire: Pistol whip: a wide swing that destroys bullets and shoves enemies. |
| Mender's Bow | Healer | 0.9 s | 0 | - | - | 40 | Primary Fire: Hold to draw, release to fire a piercing arrow that heals allies it passes. |
| Twin Pistols | Healer | 0.16 s | 10 | 780 x 0.8 s | - | 60 | Primary Fire: A pistol in each hand, firing in turn; hits knock health orbs loose.  Alternative Fire: Laser Sights: both guns fire together for harder rounds. |
| Twin Rifle | Dps | 0.13 s | 4 | 820 x 0.7 s | - | 90 | Primary Fire: Two rifles whose streams cross at your cursor for crossfire damage. |
| Railgun | Dps | 0.7 s | 120 | 1600 x 0.7 s | - | 116 | Primary Fire: Hold to charge a beam that ricochets off walls.  Alternative Fire: Dump the gun's heat as a cone of fire. |
| Burst SMG | Dps | 0.06 s | 5 | 760 x 0.8 s | - | 120 | Primary Fire: A very fast spray that tightens and hits harder the longer you fire. |
| Double Barrel | Dps | 0.32 s | 7 | 900 x 0.5 s | - | 82 | Primary Fire: A two-shell shotgun, strongest up close, with pellets that pierce.  Alternative Fire: Fire both barrels at once. |
| Twin Rifle Auto (variant) | Dps | 0.13 s | 4 | 820 x 0.5 s | - | 60 | Primary Fire: Two rifles that aim and fire on their own at the nearest enemies.  Alternative Fire: Lock both onto the enemy nearest your cursor. |
| Out of Ammo (variant) | Dps | 0.5 s | 12 | - | 80 | 52 | Primary Fire: Empty rifles swat bullets away and bash enemies in a five-move chain.  Alternative Fire: Spin for a moment, cutting everything and swatting every bullet. |

### Elementalist

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Flamethrower | Tank | 0.06 s | 3 | 480 x 0.4 s | - | 94 | Primary Fire: A short stream of fire that burns everything it hits. |
| Frost Lance | Tank | 0.4 s | 18 | - | 178 | 65 | Primary Fire: A five-step lance combo that chills, freezes, then shatters frozen enemies.  Alternative Fire: Absolute Zero: a freezing burst around you. |
| Storm Hammer | Tank | 0.7 s | 40 | 460 x 1.1 s | - | 60 | Primary Fire: Throw a hammer that smashes one enemy and arcs lightning to its neighbours. |
| Wind Clash | Tank | 0.34 s | 18 | - | 170 | 50 | Primary Fire: Hold for a wind combo that hurts and shoves every enemy it passes.  Alternative Fire: A storm around you hurts enemies and catches their shots, but slows you. |
| Radiant Orb | Healer | 0.2 s | 8 | - | 520 | 37 | Primary Fire: Hold to guide an orb of light: it burns enemies and heals allies it passes over. |
| Chain Light | Healer | 0.5 s | 40 | - | - | 80 | Primary Fire: A cone of light whose damage is shared between every enemy in it. |
| Nimbus | Healer | 0.6 s | 16 | - | 380 | 26 | Primary Fire: A storm cloud over your cursor: click to call lightning, and its rain heals allies and soaks enemies.  Alternative Fire: The cloud becomes a hurricane that drags enemies in and heals allies. |
| Fireball | Dps | 0.5 s | 32 | 430 x 1.4 s | - | 71 | Primary Fire: A big, slow fireball that rolls through enemies and sets them burning. |
| Arc Lightning | Dps | 0.25 s | 16 | - | - | 116 | Primary Fire: Hold to tag up to 10 enemies, let go to chain lightning through them and stun.  Alternative Fire: Three forked bolts at the enemies nearest your cursor. |
| Ice Shards | Dps | 0.3 s | 7 | 520 x 1 s | - | 70 | Primary Fire: Ice shards orbit you, then launch homing at nearby enemies. |
| Stonespear | Dps | 0.18 s | 24 | 900 x 0.6 s | - | 60 | Primary Fire: Slam the ground, then throw the earth spears that rise from it, one per click.  Alternative Fire: Hurl every spear at once. |
| Waterball (variant) | Dps | 0.5 s | 28 | 430 x 1.4 s | - | 56 | Primary Fire: A big, slow water ball that soaks everything it rolls through: wet enemies are slowed. |
| Earthball (variant) | Dps | 0.6 s | 48 | 360 x 1.4 s | - | 82 | Primary Fire: A heavy rock ball that stuns the first enemy it hits, then shatters into rubble. |

### Summoner

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Golem Staff | Tank | 0.5 s | 40 | - | - | 79 | Primary Fire: Hold to heave a boulder that crushes where it lands; a stone Golem fights beside you. |
| Bone Guard | Tank | 0.3 s | 7 | 720 x 0.8 s | - | 37 | Primary Fire: Bone darts that cage enemies; skeleton guards block bullets and shoot caged enemies.  Alternative Fire: Raise an Ossuary Wall. |
| Bear Totem | Tank | 0.45 s | 11 | - | 85 | 99 | Primary Fire: Quick claw swipes that make enemies bleed; a Bear mauls nearby enemies. |
| Wyvern Staff | Tank | 0.1 s | 22 | - | - | 12 | Primary Fire: A Wyvern slows and shoves enemies under it; hold to make it breathe fire.  Alternative Fire: Wyrm Slam: a stunning dive. |
| Sprite Staff | Healer | 0.5 s | 7 | 540 x 1 s | - | 14 | Primary Fire: A firefly that seeds an enemy and hops on; sprites heal whoever is hurt. |
| Treant Rod | Healer | 0.55 s | 15 | - | 250 | 34 | Primary Fire: A thorny root whips out along your aim; your Treant blocks bullets and heals along its root. |
| Spirit Chalk | Healer | 0.1 s | 26 | - | 450 | 26 | Primary Fire: Hold and drag to draw ink that burns enemies and heals allies; circle enemies to trap them.  Alternative Fire: Ignite your loops to burn what they hold. |
| Wolf Whistle | Dps | 0.3 s | 6.5 | 720 x 0.8 s | - | 150 | Primary Fire: Darts mark prey for your wolves; kills grow the pack.  Alternative Fire: Merge the pack into an alpha. |
| Wisp Lantern | Dps | 0.25 s | 22 | - | - | 84 | Primary Fire: Wisps orbit you; each shot sends one to strike the nearest enemy and return. |
| Imp Caller | Dps | 0.35 s | 0 | - | - | 77 | Primary Fire: Imps wait by you: tap to send one to explode at your cursor.  Alternative Fire: Hold to merge them into one big imp. |
| Honeybee Staff | Dps | 0.4 s | 22 | - | - | 60 | Primary Fire: Click to hatch a bee that stings the nearest enemy; three stings in a row swarm it.  Alternative Fire: Spray slowing honey; enemies that die in it burst into bees. |

### Knight

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Warhammer | Tank | 0.75 s | 80 | - | 130 | 64 | Primary Fire: Hold to charge, let go to slam the ground: longer charges hit harder and stun longer. |
| Lance | Tank | 0.24 s | 14 | - | 178 | 60 | Primary Fire: A four-step lance combo that ends in a thrust that shoves and stuns.  Alternative Fire: Hook the nearest enemy and haul it in. |
| Halberd | Tank | 0.6 s | 30 | - | 200 | 48 | Primary Fire: A long, thin sweep that hits each enemy once. |
| Bulwark Barrier | Tank | 0.3 s | 12 | - | 80 | 45 | Primary Fire: A shield that stops every shot at your front, plus a three-hit combo that ends in a stun.  Alternative Fire: Hold to brace, then bash with a dash. |
| Quarterstaff | Healer | 0.5 s | 18 | - | 120 | 36 | Primary Fire: Staff swipes that knock enemies and bullets back, and heal you and allies.  Alternative Fire: Spend a Jab stack on a cone jab. |
| Lightbringer | Healer | 0.5 s | 17 | - | 80 | 58 | Primary Fire: A three-step sword combo that heals allies it passes and ends in a healing ring.  Alternative Fire: Fire a Sunlance that heals the ally nearest your cursor. |
| Censer | Healer | 0.26 s | 13 | - | 85 | 35 | Primary Fire: A three-step censer swing that heals allies and leaves incense that makes enemy shots stray.  Alternative Fire: Throw the censer to your cursor for a burst and a big healing cloud. |
| Vowguard | Healer | 0.3 s | 16 | - | 115 | 74 | Primary Fire: A sword and shield with two stances and chains you build in the lobby.  Alternative Fire: Switch stance. |
| Twin Blades | Dps | 0.18 s | 16 | - | 90 | 98 | Primary Fire: A five-step sabre combo that carries you forward and ends in a crescent wave.  Alternative Fire: Hurl both sabres to cross at your cursor and fly back. |
| Greatsword | Dps | 0.5 s | 40 | - | 125 | 87 | Primary Fire: A ten-step sword combo that drags in, stuns, leaps and ends in a golden lance.  Alternative Fire: Throw the sword spinning to your cursor and back. |
| Rapier | Dps | 0.48 s | 18 | - | 130 | 85 | Primary Fire: Fast thrusts at the nearest enemy that skewer bullets and build Tempo.  Alternative Fire: Spend Tempo on a Flourish: a whipping blade that throws crescent waves. |
| Arc Knuckles | Dps | 0.24 s | 22 | - | 80 | 88 | Primary Fire: A five-punch combo that ends in a stunning ground slam.  Alternative Fire: Rocket Dash, then chain extra moves. |

### Ninja

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Kusarigama | Tank | 0.55 s | 26 | - | 280 | 37 | Primary Fire: Cut with the sickle, then throw the weight to drag an enemy to you.  Alternative Fire: Whirl the weight around you, pulling enemies in. |
| Bo Staff | Tank | 0.47 s | 16 | - | 105 | 48 | Primary Fire: A three-swing staff combo that cuts small bullets.  Alternative Fire: Shove a wall of staff ahead of you. |
| Fuma Shuriken | Tank | 0.7 s | 30 | - | - | 6 | Primary Fire: A giant star thrown in a loop that cuts everything both ways. |
| Nodachi | Tank | 0.3 s | 20 | - | 150 | 52 | Primary Fire: A tall katana with two five-step combo stances: wide sweeps, or fast jabs that block.  Alternative Fire: Switch stance. |
| Medicine Darts | Healer | 0.18 s | 7 | 900 x 0.8 s | - | 50 | Primary Fire: Fast needles that poison enemies and heal allies. |
| Spirit Fan | Healer | 0.4 s | 6 | 420 x 0.3 s | - | 75 | Primary Fire: A short gust that heals allies and pushes small bullets back. |
| Ofuda | Healer | 0.5 s | 25 | 360 x 1.6 s | - | 50 | Primary Fire: Throw charms that sit as wards and burst when an enemy steps on them. |
| Shuriken | Dps | 0.35 s | 9 | 700 x 0.75 s | - | 140 | Primary Fire: Tight fans of stars that stick, then rip out and fly back.  Alternative Fire: Switch to a huge star thrown along a route you draw. |
| Katana | Dps | 0.3 s | 20 | - | 120 | 65 | Primary Fire: Two stance combos: dashing slices, or cuts that end in caltrops.  Alternative Fire: Switch stance. |
| Kunai | Dps | 0.7 s | 42 | 1400 x 0.5 s | - | 88 | Primary Fire: Slow, heavy throwing knives that leave a mark where they land.  Alternative Fire: Blink to the mark, and your next hit lands double. |
| Nunchaku | Dps | 0.3 s | 20 | - | 95 | 95 | Primary Fire: A four-step twin-nunchaku combo that ends in a stunning slam.  Alternative Fire: Guard with spinning discs that swat shots back. |

### Hemomancer

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Blood Lash | Tank | 0.32 s | 24 | - | 130 | 96 | Primary Fire: A four-step blood whip that chains enemies, then yanks them in.  Alternative Fire: Hold for Martyr's Leash. |
| Gore Mortar | Tank | 0.75 s | 34 | - | - | 133 | Primary Fire: Hold to charge, then lob a blood clot that leaves a slowing, healing pool. |
| Leech Tendrils | Tank | 0.1 s | 80 | - | 160 | 31 | Primary Fire: Hold to grab and stun nearby enemies; they burst when the blood builds up. |
| Blood Blast | Tank | 0.42 s | 22 | - | 115 | 53 | Primary Fire: A six-step blood sword combo; every hit marks enemies, and their deaths heal you. |
| Transfusion Darts | Healer | 0.2 s | 7 | 850 x 0.85 s | - | 32 | Primary Fire: Darts that heal hurt allies for more than they cost you. |
| Sanguine Orb | Healer | 0.45 s | 16 | 420 x 1.4 s | - | 57 | Primary Fire: Homing orbs that make enemies bleed, or heal a hurt ally once charged. |
| Vein Tether | Healer | 0.08 s | 4 | - | 350 | 48 | Primary Fire: A draining beam that hurts enemies and heals you and the nearest ally. |
| Blood Bolts | Dps | 0.13 s | 9 | 800 x 0.8 s | - | 121 | Primary Fire: Rapid bolts that make enemies bleed, then burst them when the bleed fills. |
| Implosion | Dps | 0.45 s | 34 | - | 480 | 75 | Primary Fire: Click an enemy to collapse its blood inward and pull nearby enemies in. |
| Blood Brawler | Dps | 0.26 s | 18 | - | 520 | 80 | Primary Fire: Click an enemy to work its blood from range, ending in a bone-shard burst.  Alternative Fire: Bind the enemies near it so your moves hit them too. |
| Embolus Spears | Dps | 0.25 s | 30 | 1100 x 0 s | - | 63 | Primary Fire: Throw a spear from each hand that pierces everything, then recall them to get health back.  Alternative Fire: Burst the spears where they are. |

### Engineer

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Iron Fist | Tank | 0.7 s | 10 | - | 95 | 53 | Primary Fire: Summon a heavy Tank Bot that draws enemies and shoots nails.  Alternative Fire: With the bot out, shove enemies away with a robotic fist. |
| Iron Fist | Tank | 0.7 s | 10 | - | 95 | 53 | Primary Fire: Summon a heavy Tank Bot that draws enemies and shoots nails.  Alternative Fire: With the bot out, shove enemies away with a robotic fist. |
| Iron Fist | Tank | 0.7 s | 10 | - | 95 | 53 | Primary Fire: Summon a heavy Tank Bot that draws enemies and shoots nails.  Alternative Fire: With the bot out, shove enemies away with a robotic fist. |
| Stasis Gun | Healer | 0.1 s | 14 | - | 300 | 37 | Primary Fire: Summon a Medic Bot that shoots rivets and heals nearby allies.  Alternative Fire: With the bot out, hold to freeze enemies in place at your cursor. |
| Stasis Gun | Healer | 0.1 s | 14 | - | 300 | 37 | Primary Fire: Summon a Medic Bot that shoots rivets and heals nearby allies.  Alternative Fire: With the bot out, hold to freeze enemies in place at your cursor. |
| Stasis Gun | Healer | 0.1 s | 14 | - | 300 | 37 | Primary Fire: Summon a Medic Bot that shoots rivets and heals nearby allies.  Alternative Fire: With the bot out, hold to freeze enemies in place at your cursor. |
| Vector Blade | Dps | 0.4 s | 24 | - | 88 | 64 | Primary Fire: Summon a fast Assault Bot with a rapid rivet gun.  Alternative Fire: With the bot out, swipe a cyan blade in front of you. |
| Vector Blade | Dps | 0.4 s | 24 | - | 88 | 64 | Primary Fire: Summon a fast Assault Bot with a rapid rivet gun.  Alternative Fire: With the bot out, swipe a cyan blade in front of you. |
| Vector Blade | Dps | 0.4 s | 24 | - | 88 | 64 | Primary Fire: Summon a fast Assault Bot with a rapid rivet gun.  Alternative Fire: With the bot out, swipe a cyan blade in front of you. |

### Arcanist

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Orbit Shards | Tank | 0.3 s | 18 | 800 x 0.45 s | - | 122 | Primary Fire: Fling one of four orbiting shards that cuts everything it passes, out and back.  Alternative Fire: Pull the shards in tight to stop every bullet, but you move slower. |
| Orbit Shards | Tank | 0.3 s | 18 | 800 x 0.45 s | - | 122 | Primary Fire: Fling one of four orbiting shards that cuts everything it passes, out and back.  Alternative Fire: Pull the shards in tight to stop every bullet, but you move slower. |
| Orbit Shards | Tank | 0.3 s | 18 | 800 x 0.45 s | - | 122 | Primary Fire: Fling one of four orbiting shards that cuts everything it passes, out and back.  Alternative Fire: Pull the shards in tight to stop every bullet, but you move slower. |
| Glyph Stylus | Healer | 0.26 s | 10 | 650 x 0.7 s | - | 38 | Primary Fire: Writes rune bolts that pierce two enemies and heal an ally they pass through. |
| Glyph Stylus | Healer | 0.26 s | 10 | 650 x 0.7 s | - | 38 | Primary Fire: Writes rune bolts that pierce two enemies and heal an ally they pass through. |
| Glyph Stylus | Healer | 0.26 s | 10 | 650 x 0.7 s | - | 38 | Primary Fire: Writes rune bolts that pierce two enemies and heal an ally they pass through. |
| Dancing Blades | Dps | 0.3 s | 10 | 950 x 0.4 s | - | 117 | Primary Fire: Launch a floating sword that cuts, dances on the enemy, then returns.  Alternative Fire: Call every sword home at once, cutting on the way. |
| Dancing Blades | Dps | 0.3 s | 10 | 950 x 0.4 s | - | 117 | Primary Fire: Launch a floating sword that cuts, dances on the enemy, then returns.  Alternative Fire: Call every sword home at once, cutting on the way. |
| Dancing Blades | Dps | 0.3 s | 10 | 950 x 0.4 s | - | 117 | Primary Fire: Launch a floating sword that cuts, dances on the enemy, then returns.  Alternative Fire: Call every sword home at once, cutting on the way. |

## Abilities

### Soldier

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Barrier | Tank | 1st, choice 1 | 12 s | - | Bullets that hit the barrier are reflected back as your own. | A bubble that stops incoming enemy bullets. Bosses can break it (it soaks 360 of their damage). |
| Roar | Tank | 1st, choice 2 | 14 s | - | Also knocks enemies within 250 px back and slows them for 2 s. | Every enemy targets you for 5 s, and you take 40% less damage meanwhile. |
| Bulldoze | Tank | 1st, choice 3 | 9 s | - | Erases enemy bullets along the charge. | Charge forward, damaging and knocking back everything in your path. |
| Fortress | Tank | ultimate | 70 s | - | Lasts 14 s. | Raises a fixed, impenetrable dome here for 10 s. Only players and player bullets cross it: enemies and their shots can't get in or shoot through. |
| Juggernaut Suit | Tank | ultimate | 75 s | - | 450 armour, and each shard does 18. | Steel plates clamp on: for 10 s you carry 300 armour that takes damage before your health. You move 25% slower and every enemy, bosses too, comes for you. Each enemy bullet the armour takes throws a 12-damage flak shard straight back at whoever fired it. It ends early if the armour is gone or you go down. |
| Deployable Cover | Tank | 1st, choice 5 | 12 s (+1 charges) | - | Covers take 600 damage, and each charge comes back 25% sooner. | Two charges. Plant a bracket of steel plate in front of you that stops enemy bullets and enemies until it has taken 400 damage; your team's shots pass through. Up to 3 stand at once (another folds away the oldest), and they fold away when the wave is cleared. |
| Shockwave | Tank | card | 14 s | - | The wave runs out 300 px. | A shockwave runs out 230 px from you: it erases enemy bullets, hits enemies for 60 and shoves them back 120 px. |
| Overdrive | Tank | card | 18 s | - | +1 extra projectile while it lasts. | Double fire rate for 5 seconds. |
| Blink | Tank | card | 7 s | - | Erases enemy bullets where you leave and where you land. | Teleport toward your cursor. |
| Starburst | Tank | card | 9 s | - | Fires 48 shots instead of 24. | Fire a ring of 24 heavy shots. |
| Turret | Tank | card | 20 s | - | Deploys two turrets. | Deploy a turret that shoots for 10 seconds. |
| Stun Gun | Tank | card | 14 s | - | Nine shots instead of six. | Draw a stun gun: your next 6 shots are stun bolts (15 damage) that stun enemies for 1.5 s (bosses are only slowed). The cooldown starts once all 6 are fired. |
| Royal Dive | Tank | card | 12 s | - | A wider honey pool (150 px) that lasts 6 s. | Leap (nothing can hit you) and crash down at your cursor, up to 380 px away: 120 damage to everything within 110 px, enemies knocked back and enemy bullets there wiped out. The landing leaves a honey pool for 4 s that slows enemies 40% and heals allies 6 a second. |
| Scythe Throw | Tank | card | 9 s | - | Catching it takes 60% off, and it cuts 50% harder. | Throw a giant scythe at your cursor (up to 380 px). It cuts everything it passes, 45 on the way out and 45 on the way back, and slices small enemy bullets. Catch it as it comes back to take 40% off the cooldown. |
| Mend Pulse | Healer | 1st, choice 1 | 10 s | - | Also erases enemy bullets within its range. | Heal everyone nearby and revive downed allies. |
| Adrenaline Shot | Healer | 1st, choice 2 | 14 s | - | Also gives the nearest other ally within 250 px a half-length (4 s) dose. | Inject the player closest to your cursor (you, if alone): 8 s of +30% move speed, +40% fire rate and 30% less damage taken. |
| Deployable Medkit | Healer | 1st, choice 3 | 14 s | - | Bursts into 9 orbs worth 20 health each. | Throw a medkit at your cursor: it bursts into 6 healing orbs (15 health each) that anyone can pick up for 6 s. |
| Lifeline | Healer | ultimate | 90 s | - | Everyone also gets 4 s of Overdrive. | Instantly revive and fully heal the whole party, with 3 s of invulnerability. |
| Decoy Dummy | Healer | 1st, choice 5 | 4 s (+2 charges) | - | Dummies have 50% more health, and the confetti blast reaches 130 px. | Three charges. Throw an inflatable soldier at your cursor: regular enemies within 240 px go for it and shoot at it instead of you (bosses only glance at it for a second). It has 40 health (+4 a wave) and lasts 12 s; up to 3 stand at once. When it's popped, a confetti blast shoves enemies within 90 px back and stuns them for 0.4 s. |
| Poison Dart | Dps | 1st, choice 1 | 8 s | - | Spreads to 5 enemies at a time, at full strength. | Dart the enemy nearest your cursor: 20 damage, then poison for 18 a second over 5 s. If it dies poisoned, the poison spreads to the 3 nearest enemies (80% as strong), and on from them. |
| Airstrike | Dps | 1st, choice 2 | 16 s | - | A second wall lands 160 px further out, half a second later. | Throw a flare at your cursor: a jet carpets a line across the arena through it, across your aim (55 a bomb), and the line burns for 3 s. |
| Mark for Death | Dps | 1st, choice 3 | 12 s | - | Marks the three enemies nearest your cursor. | Mark the enemy nearest your cursor: it takes 50% more damage for 6 s. |
| Artillery Assault | Dps | ultimate | 60 s | - | +4 extra projectiles instead of +2. | For 6 s you fire three times as fast, with +2 extra projectiles. |
| Attack Helicopter | Dps | ultimate | 70 s | - | A second gun on the wing: two targets at once. | Call in a gunship for 10 s. It circles you and locks one enemy at a time, the one nearest you, hosing it with a chaingun (20 rounds a second, 8 damage each) until it dies, then the next. It can't be hit. |
| Deployable Turret | Dps | 1st, choice 5 | 45 s | - | Fires 14 s, rounds pierce 4, and the blast does double. | Drop a heavy twin-barrel turret on your spot and climb in for 10 s: you can't move, and it fires instead of your weapon. Hold Fire and it turns onto your cursor, spooling up from 7 to 30 rounds a second of 22 damage that pierce 2 enemies. The turret takes the damage meant for you (350 health); if it is wrecked you are thrown clear and it blows early. It overheats and blows after, for 80 damage within 160 px. |

### Elementalist

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Aeroaegis | Tank | 1st, choice 1 | 11 s | - | Lasts 4.5 s and reflects bullets for double damage. | For 3 s, a wind vortex wraps the player your cursor is nearest: enemies can't get in, and bullets that touch it are reflected back as your own. |
| Frost Nova | Tank | 1st, choice 2 | 12 s | - | Also erases enemy bullets around you. | Freeze every enemy near you for 3 s and deal 40 damage. |
| Flame Wall | Tank | 1st, choice 3 | 14 s | - | The fire also slows enemies by 40%. | A wall of fire at your cursor for 5 s: burns enemies and blocks bullets. |
| Glacial Tomb | Tank | ultimate | 75 s | - | Enemies stay frozen for 6 s and bosses take 500. | Erase every enemy bullet and freeze every enemy solid for 4 s (bosses take 250 damage instead). |
| Lightning Rod | Tank | ultimate | 70 s | - | Lasts 8 s and each bolt arcs to 3 enemies. | For 6 s you crackle with charge: you take 80% less damage and every enemy, bosses too, goes for you. Each enemy hit on you calls a bolt from the sky on whoever landed it (30), which arcs on to the 2 enemies nearest it (15 each). Wet enemies take extra arcs. |
| Flood | Tank | 1st, choice 5 | 16 s | - | Deluge: the flood is 30% wider and lasts 9 s. | Stamp and flood the ground 190 px round you for 6 s: enemies touching you are thrown back 60 px (10 damage), and every enemy wading in the water is slowed 40% (heavies and bosses 20%) and soaked wet, so lightning arcs to it, while it's in and 3 s after. |
| Healing Rain | Healer | 1st, choice 1 | 16 s | - | Lasts 10 s, and downed allies inside revive twice as fast. | A rain at your cursor for 6 s: heals allies inside 8 HP/s and slows enemies by 30%. |
| Gust Gate | Healer | 1st, choice 2 | 14 s | - | The ring is 40% wider and pushes enemies twice as hard. | A ring of wind at your cursor for 8 s. Anyone who walks through it, you included, gets +80% move speed for 3 s. Enemies inside are pushed gently out. |
| Lumen Ward | Healer | 1st, choice 3 | 20 s | - | Also freezes enemies outside the light for as long as it lasts (not bosses). | A huge cone of light toward your cursor for 6 s that turns as you aim: heals everyone inside 9 HP/s and burns away enemy bullets in it or touching it. |
| Sanctum | Healer | ultimate | 80 s | - | The dome is half again as wide (300 px). | A dome of light for 7 s: bullets can't get in, allies inside can't go down, and a healing pulse rolls out from you every 0.9 s (the last is bigger). |
| Water Ward | Healer | 1st, choice 5 | 3 s | - | Splashes and bursts reach 40% farther. | Call 2 drops of water down round you; they circle you, up to 10. A drop that touches an enemy splashes: 12 damage to every enemy within 52 px, and they're soaked wet (slowed 30%, lightning arcs to them) for 4 s. A drop that meets an enemy shot eats it and bursts into a mist that soaks everything within 72 px. |
| Meteor | Dps | 1st, choice 1 | 10 s | - | Leaves burning ground for 3 s. | Call a meteor at your cursor: after a short warning it hits for 150 damage. |
| Blizzard | Dps | 1st, choice 2 | 14 s | - | Lasts 10 s. | A storm at your cursor for 5 s that slows and damages enemies. |
| Tsunami | Dps | 1st, choice 3 | 16 s | - | Wet lasts 14 s, and the wave rolls back to you, wetting what it missed. | A wave rolls out where you aim: 12 damage, and it leaves enemies wet for 8 s, slowed 30%. Lightning (Arc Lightning, Storm Hammer, Chain Light, Nimbus) arcs to every wet enemy on the map. |
| Bullet Rain | Dps | ultimate | 70 s | - | Lasts 9 s. | Call a downpour over a big square (520 px) at your cursor for 6 s and harden the drops into needles: 45 damage a second to every enemy in it, and leaves them wet. |
| Gale Wall | Dps | 1st, choice 5 | 18 s | - | The wall is 40% wider and holds up to 60 charges. | A curved wall of wind in front of you for 5 s: enemy shots that reach it stop and hang in it, each one a charge (up to 40). Press again (or when it ends) to unleash a big cone toward your cursor: 40 damage + 6 per charge, and enemies are shoved back. |

### Summoner

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Golem Wall | Tank | 1st, choice 1 | 13 s | - | Raises five golems. | Raise three basalt golems in front of you. They block every enemy bullet and stay until they are broken (200 health each); casting again replaces them. |
| Earthquake | Tank | 1st, choice 2 | 10 s | - | Also freezes everything it hurts for 1 s (not bosses). | You slam the ground (25 damage, slows), then a Burrower breaks out at your cursor and tunnels through the enemies for 3 s, hurting each one it passes under (30 damage). |
| Force Pact | Tank | 1st, choice 3 | 12 s | - | A second thrall: the pact keeps the last two types you chose. | Take the regular enemy nearest your cursor (not bosses) and make it fight for you. It stays near you with its own attack and comes back 15 s after it falls. Cast again on another enemy to switch the pact to that type. |
| Titan | Tank | ultimate | 80 s | - | The titan is 30% bigger and stays for 27 s. | Summon a huge stone titan for 17 s. It rises from rubble, stomps after enemies, slams them after a short wind-up and blocks bullets with its whole body (it soaks 1000 boss damage). |
| Bone Phalanx | Tank | ultimate | 75 s | - | 10 skeletons with 180 health each. | A bone gate opens ahead of you and 8 skeletons with tower shields march a line 260 px along your aim over 6 s. Their shields stop every enemy shot from the front (red aimed shots too) while your team shoots through; enemies are shoved ahead and speared for 15 every 0.5 s. Each skeleton has 120 health; a broken one leaves a gap. Bosses and walls stop the line. |
| Stampede | Tank | 1st, choice 5 | 14 s | - | 9 beasts, and the herd turns and charges back. | Call a line of 7 spirit beasts across your aim. They charge 320 px, trampling every enemy for 20 damage and shoving them along ahead of the herd. An enemy driven into a wall takes 15 more and is stunned for 0.5 s. Bosses and heavy enemies are trampled but not shoved. |
| Spirit Swarm | Healer | 1st, choice 1 | 14 s | - | Releases 8 sprites. | Release 5 healing sprites for 6 s. |
| Phoenix | Healer | 1st, choice 2 | 22 s | - | Revives at full health, and the phoenix keeps fighting. | A phoenix follows your cursor for 10 s, shooting and pulsing 8% max health to everyone near it every 2 s. If anyone goes down (you too), it revives them instantly. |
| Soul Lantern | Healer | 1st, choice 3 | 18 s | - | Lasts 9 s, and the linked player can't go down. | A lantern appears beside the ally nearest your cursor (you, alone) and links to them for 6 s: it follows, heals 8 health a second and flames nearby enemies. |
| World Tree | Healer | ultimate | 80 s | - | Lasts 16 s with 8 sprites. | Grow a great tree for 15 s that heals and slows around it and sends a wave of healing butterflies to every ally every 1.9 s. |
| Bat Swarm | Dps | 1st, choice 1 | 12 s | - | Releases 14 bats. | Release 8 bats that hunt enemies for 6 s. |
| Hydra | Dps | 1st, choice 2 | 18 s | - | A fourth head. | A three-headed hydra rises at your cursor for 8 s. Each head picks its own enemy and spits venom at it (10 damage, every 0.6 s per head). |
| Alpha Call | Dps | 1st, choice 3 | 14 s | - | Also summons 3 wolves for 6 s. | Blood Scent for 6 s: your minions start at 2 stacks, and each bite adds one (up to 5): faster, and quicker bites. At 5 stacks the next bite lunges for triple damage and drops back to 2. |
| Legion | Dps | ultimate | 70 s | - | Summons 7 wolves and 7 bats. | A blood moon rises. Summon 4 wolves and 4 bats that circle you, then run 40% faster for 10 s; your other minions frenzy. |

### Knight

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Shield Wall | Tank | 1st, choice 1 | 12 s | - | Bullets that hit the shield are reflected back as your own. | Raise a wall of 7 steel plates: it stops every enemy bullet coming at your front (120°), even red aimed shots. Every shot wears down the plate it hits (400 damage in all, shared between the plates) and a broken plate leaves a gap. The wall stays up until every plate is gone. |
| Leap Slam | Tank | 1st, choice 2 | 9 s | - | The landing also erases enemy bullets within 160 px. | Leap to your cursor (up to 320 px), untouchable in the air. Landing deals 50 damage around you and stuns enemies for 1 s. |
| Cavalry Call | Tank | 1st, choice 3 | 18 s | - | Two more lancers ride outside the first pair, and the guard lasts 5 s. | Two lancers ride in at your sides and charge across the map along your aim. Each enemy they meet takes 35, is shoved out of the lane and slowed 45% for 3 s (Heavy enemies and bosses barely move and are slowed less). You take 20% less damage for 3 s. |
| Last Stand | Tank | ultimate | 70 s | - | You take 5% of the damage instead of 10%. | You plant yourself and brace a wall of steel plates 120 px out, 178° wide, facing your cursor (it turns slowly). It stops every enemy bullet from the front; your teammates' shots pass out through it. You take 10% of the damage it stops. You can't move while it stands, and it has no timer: it stays until you go down or press the ability again, and the cooldown starts when it comes down. |
| Consecrate | Healer | 1st, choice 1 | 14 s | - | Two more crosses fly out at 22 degrees either side (each target is hit once). | Throw a Holy Cross along your aim. It grows as it flies: allies it crosses heal 35 and run 30% faster for 4 s; enemies take 25, are slowed 40% for 3 s and are knocked back. |
| Guardian's Oath | Healer | 1st, choice 2 | 15 s | - | Charges up to 800 px. The ward is wider, follows you, and stands a downed ally back up at 25% health. | Press, then click a player (up to 600 px), downed or not: charge to them, grab them and haul them back to where you stood. A Ward of Light then cuts damage by half for everyone inside for 5 s, and downed allies in it revive twice as fast. |
| Lay on Hands | Healer | 1st, choice 3 | 18 s | - | Also heals everyone else near them for half as much. | Heal the most hurt ally within 300 px (or yourself) for 70, or instantly revive them at half health if they're down. |
| Hallowed Blade | Healer | ultimate | 80 s | - | Three swords fall: one at your cursor and one either side. | A giant holy sword falls at your cursor: 200 damage around it, then it stays planted for 8 s (allies in the ring heal 15 HP/s, enemies burn), and every 2 s a pulse crosses the whole arena healing allies and hurting enemies for a third as much. |
| Bladed Dive | Dps | 1st, choice 1 | 10 s | - | Each kill heals you 5% of your health. | Rush toward your cursor (up to 240 px), untouchable, your blade spinning round you: four cuts land along the way, each hitting every enemy within 64 px of you for 18. Kill anything with it and the cooldown resets. |
| The Gauntlet | Dps | 1st, choice 2 | 9 s | - | The Challenge lasts 8 s, and the charge's impact stuns for 1 s. | Throw down the gauntlet at the enemy nearest your cursor along your aim (up to 380 px; it flies over anything in between): it takes 12 and is Challenged for 5 s. You shoulder-charge to it, untouchable, bowling everything in the way aside (18 and a shove), and hit it for 30. Kill the Challenged enemy before the Challenge runs out and the gauntlet flies back: the cooldown resets. |
| Caught Steel | Dps | 1st, choice 3 | 16 s | - | Holds up to 18 shards. | For 6 s, enemy bullets that skim past you without touching you are caught and hang round you as steel (up to 12). Press again (or let the time run out) and a sweeping cut hurls them out along your aim: each shard cuts the bullets in its way and hits the first enemy for 22. A hit knocks a third of them loose. The cooldown starts when you throw them. |
| Drawbridge | Dps | ultimate | 75 s | - | The bridge stands for 8 s. | A gatehouse rises behind you and its drawbridge falls along your aim: 500 px long and 100 wide, it crushes everything in the lane for 260 and throws enemies at its edges aside (45). Then it lies there for 5 s as a walkway: enemies can't step onto it and the rails grind any pressed against them (80 a second), and their shots stop at its rails, red aimed shots too. |
| Warhorn Volley | Dps | ultimate | 70 s | - | The volley is 40% wider and the fire burns for 8 s. | Blow a warhorn and a volley of fire arrows falls behind you and sweeps across the field along your aim (1,400 px ahead, 700 wide). Every enemy it reaches takes 120, burns for 20 a second for 5 s and is pinned (40% slower for 3 s). Enemy shots in its path are knocked out of the air (red aimed shots fly on). |

### Ninja

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Shadow Clone | Tank | 1st, choice 1 | 14 s | - | Every clone has double the health. | Summon a wall of 9 clones for 7 s. Every enemy targets them and each soaks 50 damage. They never fight: walk into them to shove the wall. |
| Smoke Bomb | Tank | 1st, choice 2 | 14 s | - | Enemies inside the smoke are also slowed by 40%. | A 140 px cloud at your feet for 5 s: enemy bullets inside slow to half speed, and allies inside take 30% less damage. |
| Ceremonial Armor | Tank | 1st, choice 3 | 2.5 s | - | A plate that breaks bursts for 30 damage to enemies within 60 px. | Summon a lacquered armour plate that circles you until it breaks: up to 10, 4 on an inner ring and 6 further out. Each has 45 health, stops the enemy bullets it touches and pushes enemies away. With all 10 up you take 20% less damage. |
| Thousand Clones | Tank | ultimate | 70 s | - | Twenty clones. | Twelve clones appear all over the map. Every enemy goes for the nearest one at once. They soak 100 damage each and last until they die or the round ends; each bursts for 60 when killed. |
| Dragon's Dogma | Tank | ultimate | 70 s | - | Hunts for 7 s, and every enemy it devours heals you 3%. | A great spirit dragon bursts out of you and hunts across the arena for 5 s, devouring every enemy its jaws reach and swallowing their shots. Bosses and heavy enemies take a 150 bite instead. Then it coils back into you. |
| Healing Dash | Healer | 1st, choice 1 | 15 s | - | The sprint lasts 4.5 s. | Sprint 70% faster for 3 s, leaving glowing footprints that last 6 s. You and your allies heal 12 HP a second while standing near one (overlapping footprints don't stack). |
| Substitution | Healer | 1st, choice 2 | 12 s | - | You and your ally are both invulnerable for 1 s after the swap. | Swap places with the ally nearest your cursor and shield them for 60 for 6 s. Alone: slip to your cursor and shield yourself. |
| Spirit Ward | Healer | 1st, choice 3 | 18 s | - | The ward follows you. | Plant a talisman at your cursor for 8 s: it takes the damage allies near it would take (up to 150). When it breaks or ends, a quarter of what it took heals allies near it. |
| Blossom Storm | Healer | ultimate | 80 s | - | Lasts 8 s. | For 5 s petals heal the whole party 20 HP/s and erase enemy bullets near every ally. |
| Shadow Step | Dps | 1st, choice 1 | 7 s | - | +1 charge. | Teleport behind the enemy nearest your cursor (up to 450 px away) and strike it for 80. |
| Vanish | Dps | 1st, choice 2 | 16 s | - | Erases enemy bullets around you when you vanish. | For 3 s enemies lose track of you. Your next hit (within 4 s) deals double damage. |
| Blade Fan | Dps | 1st, choice 3 | 12 s | - | Throws twice as many kunai. | 12 kunai spin around you for 4 s, cutting enemies they touch for 18 (once every 0.5 s each) and erasing enemy bullets. Press again, or wait, and they fly out in a ring, 18 each. |
| Death Lotus | Dps | ultimate | 65 s | - | Strikes up to 14 enemies. | Strike up to 8 enemies near your cursor in a flurry of cuts, 100 damage each. |

### Hemomancer

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Blood Shell | Tank | 1st, choice 1 | 9 s | - | The burst also erases enemy bullets within 160 px. | Hold to wrap yourself in a big bubble of blood. It blocks bullets and enemies; allies can stand inside. Each bullet it stops costs you 7 health and heals every ally inside for 4. It bursts at a third of your health, or when you let go. |
| Blood Wall | Tank | 1st, choice 2 | 8 s | - | Enemies against the wall take double and are slowed 40%. | Toggle: a wall of blood is laid behind you as you walk. It stops enemy bullets and enemies can't cross it (they bleed 12 a second against it); allies walk through. It stands 6 s. It drains 1% of your max health a second, doubling every 4 s, until you press again or go down. |
| Exsanguinate | Tank | 1st, choice 3 | 8 s | 10% of max health | Also slows the enemies it drains by 50% for 2 s. | Pay 10% of your max health: barbed blood whips hook every enemy within 220 px and yank them in to you, stunned on the way. Each drains 20 health into you as it lands. Heavy enemies and bosses stay put but are still drained. |
| Vein Tithe | Tank | ultimate | 75 s | - | Lasts 40 s and costs 25% less. | Blood tendrils bind every enemy on screen for 30 s: they can't move or shoot. You pay 0.25% of your max health a second for each one held (a boss costs four times that), and deal 40% less damage. It ends when the time runs out or you go down. |
| Bloodbath | Tank | ultimate | 75 s | - | The pool spreads to 220 px and gives back 35% of the bleed. | Channel: blood floods the floor 170 px round you (it stays where you cast it) for up to 10 s, draining 4% of your max health a second. Enemies wading in it are slowed 50% (Heavy enemies 35%, bosses 20%) and bleed 15 a second, and every drop heals you for 25% of it, wherever you are. Allies standing in it, you included, take 20% less damage. Press again to stop; it also stops at 15% health or if you go down. |
| Blood Gift | Healer | 1st, choice 1 | 8 s | 25% of current health | Also heals everyone else within 200 px of them for half as much. | Pay 25% of your current health: the most hurt ally heals twice as much. Alone, it becomes a shield for you. |
| Blood Freeze | Healer | 1st, choice 2 | 14 s | 25% of max health | Frozen enemies take 25% more damage. | Pay 25% of your max health: a shockwave bursts out 260 px around you and freezes every enemy it reaches for 5 s (bosses for 3 s). Frozen enemies can't move or shoot. |
| Martyr | Healer | 1st, choice 3 | 16 s | 40% of max health | You're invulnerable for 2 s after the sacrifice. | Pay 40% of your max health to revive the downed ally nearest your cursor, anywhere, at 50% health. If no one is down, the most hurt ally (you, if you're alone) is warded for 12 s: if they go down, they get straight back up at 50%. |
| Sanguine Rain | Healer | ultimate | 80 s | - | Lasts 14 s and heals 4% a tick. | Draw 12% of your health into a blood orb (1.8 s), then rain circles follow your allies for 10 s, trickling 3% of their health every 0.5 s. It drains you 3% a second and ends when you press it again or fall to 15%. |
| Arterial Spray | Dps | 1st, choice 1 | 8 s | - | The spray also washes away enemy bullets it touches. | Press to open a vein: a 60° jet of blood, 200 px long, follows your aim, 30 damage a second to everything in it (more the lower your health), slowing them 20% and making them bleed. It drains 4% of your max health a second. Press again to stop; it sputters out at 15% health. The cooldown starts when it stops. |
| Blood Debt | Dps | 1st, choice 2 | 16 s | - | Kills repay 50% more. | Borrow against your blood: your health drops to 25% (or 5% of your max, if you're already that low) and the rest becomes a debt. For 8 s, kills pay it back (a Grunt 4% of your max health, a Heavy or an elite 10%, hurting a boss 10% of the damage) and your hits heal 15% instead of 3%. Pay it all and you keep the 15% for 2 s more; what's unpaid after 8 s is lost. |
| Vein Rush | Dps | 1st, choice 3 | 6 s | 10% of max health | The trail bursts after 1 s for 20 to everything on it. | Pay 10% of your max health and rush 280 px toward your cursor as a streak of blood, untouchable. Every enemy you pass through takes 25 and bleeds, and gives back 2% of your max health. You leave a trail of blood that slows enemies 30% for 2 s. |
| Crimson Harvest | Dps | ultimate | 65 s | - | Hold 8 s, and 25% more blood. | Rip blood from every enemy in the arena (20 damage each) into a floating ball, bigger the more enemies fed it. Press again to throw it at your cursor for a huge blast; it throws itself after 5 s. |
| Thorn Bloom | Dps | 1st, choice 5 | 8 s | 12% of max health | Tendrils jump 5 times. | Pay 12% of your max health: twelve blood needles burst out of you all round, 240 px. Each stops on the first enemy it meets for 45 and a bleed stack, and whips a tendril on to the nearest enemy no tendril has reached yet (180 px) for 30, which chains on: 3 jumps a needle, 20% less each jump. |

### Engineer

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Vector Wall | Tank | 1st, choice 1 | 10 s | - | Seven tiles, and it lasts 12 s. | Draw a 5-tile wall across your aim at the cursor (up to 220 px away). It stops enemy shots for 8 s, or until it has soaked its fill. |
| Bastion | Tank | ultimate | 75 s | - | No gaps for the first 3 s, and it lasts 14 s. | Raise a ring of vector walls round you (130 px) for 10 s, with four gaps to shoot out of. The walls stop enemy shots. |
| Med Pylon | Healer | 1st, choice 1 | 6 s | - | Its field is 40% wider. | Plant a pylon at your cursor (up to 320 px away). Its field heals allies inside for 10 a second. It stays until you plant the next one. |
| Safe Room | Healer | ultimate | 80 s | - | It lasts 18 s. | The Breach hammer opens the wall you aim at: in the Expanding arena a room behind it heals everyone inside for 20 a second and cuts the damage they take by 30% for 12 s. No room: a healing dome (160 px) at your cursor. |
| Drag Pylon | Dps | 1st, choice 1 | 6 s | - | Its field is 40% wider. | Plant a pylon at your cursor (up to 320 px away). Its field slows enemies inside by 45% (bosses 20%). It stays until you plant the next one. |
| Breach | Dps | ultimate | 70 s | - | The blast is twice as wide. | Smash the wall you aim at (up to 220 px): 80 damage and a shove by the wall. In the Expanding arena a free random room opens behind it with one thing inside: vectors, gold or an ambush. No wall in reach: the blast and the loot land at your cursor. |

### Arcanist

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Kinetic Grasp | Tank | 1st, choice 1 | 10 s | - | The throw hits 50% harder and stuns for twice as long. | Lift the biggest enemy near your cursor (not a boss) and hold it in front of you as a shield: it soaks the bullets that hit it. Press again, or wait 4 s, to throw it along your aim: 50 damage to it and to the enemy it hits, which is knocked back and stunned. Nothing in reach: most of the cooldown comes back. |
| Grand Sigil | Tank | ultimate | 75 s | - | It lasts 12 s. | Draw a rune circle round you (200 px) for 8 s. Enemies inside are bound for 1 s, then slowed by half and drawn to you. Enemy bullets freeze where they meet its rim; when it ends, every frozen bullet flies back out as a spectral blade. |
| Arcane Tether | Healer | 1st, choice 1 | 12 s | - | It heals twice as much. | Pull the ally nearest your cursor (up to 480 px away, downed or not) to your side. They can't be hurt on the way and land healed for 30. Nobody in reach: most of the cooldown comes back. |
| Sanctum Sigil | Healer | ultimate | 80 s | - | It lasts 12 s. | A healing circle (170 px) follows you for 8 s: allies inside heal 25 a second. Six runes turn on its rim; each bursts on the first enemy that touches it (50 damage round it and a shove). |
| Telekinetic Crush | Dps | 1st, choice 1 | 9 s | - | It grabs 40% wider. | Lift the enemies at your cursor (120 px, up to 400 px away) and pull them together for half a second, then slam them: 40 damage, +12 for each other enemy caught (up to +60), and a short stun. Bosses aren't lifted. |
| Runic Array | Dps | ultimate | 65 s | - | It lasts 9 s. | Four sigils open behind you for 6 s: every Fire also sends a spectral blade from each of them at your cursor (16 each, through one enemy), even while your blades are out. |

## Enemies

| Enemy | Group | Health | Speed | Radius | Contact | Score | XP | Notes | What it does |
|---|---|---|---|---|---|---|---|---|---|
| Grunt | Ranged | 60 | 85 | 14 | - | 10 | 10 |  | Fires aimed 3-shot spreads. |
| Sniper | Ranged | 50 | 95 | 12 | - | 15 | 14 |  | Fast, accurate shots from far away. |
| Spinner | Ranged | 150 | 35 | 18 | - | 25 | 22 |  | Wanders and sprays spirals in bursts; it spins up while bursting. |
| The Warden | Boss | 2400 | 45 | 46 | - | 300 | 150 | boss | Boss (wave 5): a prison riot in three phases — searchlight, gates and gas, then rioters. |
| Shard | Ranged | 25 | 130 | 8 | - | 5 | 4 |  | Tiny and fast. Comes from the Hive Queen and Splitting elites. |
| Mortar | Ranged | 80 | 40 | 16 | - | 20 | 18 |  | Lobs bombs at where you stand; they burst into a ring on landing. |
| Shielder | Ranged | 140 | 60 | 17 | - | 25 | 22 |  | A front shield blocks shots and swings. It turns slowly: flank it. |
| Hive Queen | Boss | 3000 | 40 | 44 | - | 400 | 180 | boss | Boss (wave 10): a hive in three phases — drones and bombers, sealing cells and honey, then a frenzied swarm. |
| Reaper of Dusk | Boss | 2600 | 95 | 34 | - | 250 | 100 | boss | Boss pair (wave 15): a tether sweep, then they fuse into the Dual Reaper (pillars, holes), then split for a finale. |
| Reaper of Dawn | Hidden | 2600 | 95 | 34 | - | 250 | 100 | boss |  |
| The Singularity | Boss | 3600 | 0 | 40 | - | 500 | 220 | boss | Boss (wave 20): corner gravity wells, pull, lasers and shrinking cages. |
| The Colossus | Boss | 6800 | 35 | 54 | - | 600 | 260 | boss | Boss (wave 25): huge stomp rings with one gap, and thrown rocks. |
| The Duelist | Boss | 6000 | 80 | 30 | - | 650 | 280 | boss | Boss (wave 30): teleports and leaves decoys that burst unless killed. |
| The Duelist | Hidden | 70 | 0 | 30 | - | 0 | 0 |  |  |
| The Lich | Boss | 5000 | 45 | 36 | - | 700 | 300 | boss | Boss (wave 35): immune while its phylactery stands; raises skeletons. |
| Phylactery | Hidden | 600 | 0 | 18 | - | 50 | 0 | heavy |  |
| Storm Herald | Boss | 6000 | 55 | 40 | - | 800 | 320 | boss | Boss (wave 40), three phases: a gathering storm (strikes, lightning rods), a gale that pushes you and bends bullets, then a supercell that marks one player at a time. |
| The Devourer | Boss | 6500 | 40 | 50 | - | 900 | 340 | boss | Boss (wave 45), three phases: hungry for morsels (your shots heal it), then swallowing players, then a mouth that heals it from the front. |
| The Eclipse | Boss | 9000 | 30 | 52 | - | 2000 | 500 | boss | Final boss (wave 50), five phases and Totality: a sun and its moon. Hide in the moon's shadow from the flash, stay in the light, and kill it before the nova. |
| Gravity Well | Hidden | 260 | 160 | 15 | - | 40 | 15 |  |  |
| Brute | Melee | 200 | 62 | 19 | 24 | 15 | 14 | melee | Slow, tough melee bruiser that hits hard (24) when it reaches you. |
| Lunger | Melee | 45 | 95 | 12 | 18 | 12 | 12 | melee | Circles in close, flashes a pink line, then leaps along it. |
| Hive Bomber | Hidden | 30 | 330 | 10 | - | 2 | 0 | melee |  |
| Cage Well | Hidden | 40 | 0 | 11 | - | 5 | 2 | melee |  |
| Rioter | Hidden | 40 | 110 | 12 | 15 | 3 | 7 | melee | A prisoner in an orange jumpsuit throwing cuffed punches. Some drag a ball and chain and run slower. |
| Hive Egg | Hidden | 60 | 0 | 15 | - | 4 | 3 | melee |  |
| Guard | Hidden | 90 | 125 | 13 | 14 | 6 | 15 | melee | A corrections officer in khaki with a peaked cap and a nightstick. Up close it swings forehand then backhand: step out of the amber arc. |
| The Dual Reaper | Hidden | 5200 | 150 | 58 | - | 500 | 200 | boss |  |
| Reaper Pillar | Hidden | 120 | 0 | 24 | - | 5 | 4 | melee |  |
| Singularity Shard | Hidden | 1000000 | 0 | 20 | - | 0 | 0 | melee |  |
| Ball Lightning | Hidden | 70 | 0 | 15 | 20 | 3 | 2 | melee |  |
| Morsel | Hidden | 60 | 0 | 14 | - | 0 | 0 |  |  |
| Riot Shield | Melee | 60 | 120 | 16 | 8 | 10 | 10 | melee | An officer in steel-blue riot gear behind a tall shield that bashes you back. Go round it, or break the shield: then it falls fast. |
| Gas Grenadier | Ranged | 50 | 75 | 14 | - | 8 | 9 |  | An officer in olive drab and a gas mask, lobbing tear-gas canisters at you. Step out of the cloud. |
| The Gatekeeper | Hidden | 3000 | 55 | 34 | 25 | 150 | 80 | mini-boss, heavy | Challenge room mini-boss: a ring of shots with a gap, three aimed fans, and a charge along a shown line that ends in a burst. |
| Shadow | Hidden | 1800 | 190 | 16 | - | 60 | 40 |  | Mirror Match: a dark copy of a player, with their class and their way of fighting. It sidesteps every few seconds. |
| Shambler | Melee | 70 | 70 | 14 | 18 | 8 | 10 | melee | Zombie: a wide, slow lurcher that hits hard. Don't get cornered. |
| Runner | Melee | 28 | 175 | 10 | 10 | 5 | 5 | melee | Zombie: frail but faster than you walk, in packs of three. Turn and shoot. |
| Spitter | Ranged | 45 | 60 | 12 | - | 8 | 9 |  | Zombie: hangs back and lobs globs that leave burning, slowing acid. Keep moving. |
| Crawler | Melee | 55 | 95 | 11 | 6 | 8 | 8 | melee | Zombie: scuttles low and grabs you, rooting you in place. Shoot it to break free. |
| Bloater | Melee | 90 | 50 | 18 | 20 | 10 | 14 | melee | Zombie: swollen and slow; it bursts into a slowing spore cloud. Pop it at range. |
| Shrieker | Ranged | 40 | 80 | 11 | - | 10 | 12 |  | Zombie: hangs back and screams, sending the horde round it into a frenzy. Silence it. |
| Mender | Ranged | 50 | 65 | 12 | - | 10 | 14 |  | Zombie: heals the horde round it and makes the dead rise faster. Kill it first. |
| Swarmer | Melee | 14 | 115 | 9 | 6 | 3 | 3 | melee | The simplest melee rusher: a white chomper with no eyes that walks straight at you. Weak alone, and it comes in packs. |
| Taser Guard | Ranged | 70 | 80 | 13 | - | 9 | 11 |  | An officer in slate with a hi-vis band. Keeps its distance, charges its taser and fires a white Fork Bolt that stuns you. Dodge the bolt, or parry it. |
| Security Guard | Ranged | 45 | 70 | 15 | - | 9 | 9 |  | An officer in navy with a service pistol. Strafes, locks on and fires one tracer round at a time: seven shots, then a long reload. Sidestep the lane, or rush it while it reloads. |
| Thug | Melee | 175 | 62 | 19 | 25 | 10 | 10 | melee | A hunched prisoner brute with giant taped fists. Slow and tough, hits hard when it reaches you. |
| Shiv | Melee | 45 | 115 | 12 | 18 | 9 | 9 | melee | A prisoner with a taped shank: hooded, or shirtless with one in each hand. Circles in close, flashes a line, then lunges along it. |
| Spawn Portal | Hidden | 200 | 0 | 30 | - | 30 | 20 | melee, heavy | A burst of the wave comes through it. Shoot it closed before it empties to cancel the rest; it drops coins for each one it held. |
| Worker | Melee | 40 | 110 | 12 | 15 | 3 | 7 | melee | A small, quick worker bee that weaves at you, in loose pairs. |
| Soldier | Melee | 90 | 125 | 13 | 14 | 6 | 15 | melee | An armoured bee that rushes straight at you, jaws snapping. |
| Comb Shield | Melee | 60 | 120 | 16 | 8 | 10 | 10 | melee | A bee behind a plate of wax cells that stops shots and bashes you back. Break the cells, or go round it. |
| Honey Lobber | Ranged | 50 | 75 | 14 | - | 8 | 9 |  | Lobs honey at you. A ring marks the spot; the puddle slows whoever stands in it. |
| Stinger | Ranged | 105 | 80 | 13 | - | 9 | 11 |  | Keeps its distance, charges a venom lance and fires one barbed dart that stuns you. Strafe across the line, or parry it. |
| Needler | Ranged | 70 | 70 | 15 | - | 9 | 9 |  | Hovers and strafes, locks on, then fires one white needle at a time: seven shots, then a long reload. |
| Bumble | Melee | 175 | 62 | 19 | 25 | 10 | 10 | melee | A huge, furry bumblebee. Slow and tough, hits hard when it reaches you. |
| Hornet | Melee | 45 | 115 | 12 | 18 | 9 | 9 | melee | Circles in close, flashes a red lane, then darts along it. |
| Wisp | Melee | 14 | 120 | 9 | 6 | 3 | 3 | melee | A soul flame that drifts straight at you in a loose pack and burns on touch. Thin them before they reach you. |
| Acolyte | Melee | 90 | 120 | 13 | 8 | 14 | 12 | melee | A hooded chaser. Stops just short, raises its sickle over a pale cone, then one wide swing. Step back out of the cone. |
| Bound Wraith | Melee | 60 | 95 | 12 | 8 | 10 | 9 | melee | Comes as a pair circling near you, joined by a tether that hurts. Kill one and the other brings it back in 3 s, unless you kill both. |
| Scythe Wraith | Melee | 75 | 85 | 14 | 18 | 12 | 11 | melee | Marks a long line through you, then dashes along it and reaps both sides of its path. Leave the line sideways. |
| Lamplighter | Ranged | 55 | 70 | 13 | - | 10 | 10 |  | Plants its feet, its lantern flares and gold lines show the volley: three lines at you, then a four-way cross. |
| Mourner | Ranged | 80 | 60 | 15 | - | 12 | 12 |  | Stands still and weeps a twin spiral, one arm each way, then rests. Push in while it rests. |
| Gravedigger | Melee | 140 | 62 | 19 | 20 | 10 | 10 | melee | A cultist brute with a shovel. Slow and tough, hits hard when it reaches you. |
| Shade | Late | 90 | 90 | 13 | - | 25 | 22 |  | A shrouded wraith: fades to smoke, shows a ring where it will land, appears and fires. |
| Debris | Melee | 22 | 125 | 10 | 9 | 3 | 6 | melee | Tumbling rubble that falls at you on a curving inward spiral, so it's hard to line up. |
| Void Acolyte | Ranged | 45 | 85 | 13 | - | 7 | 10 |  | A void cultist. Fires three slow orbs: the outer two bend in and meet where you stand. Step forward or back, not sideways. |
| Pulsar | Ranged | 70 | 75 | 13 | - | 9 | 12 |  | A spinning neutron star. Its two beams flicker for a second, then switch on and sweep a quarter turn. Walk with the sweep. |
| Graviton | Ranged | 50 | 80 | 14 | - | 8 | 10 |  | Lobs a small gravity well by you every 6 s. For 4 s it drags you in and swallows shots near it. Get clear, or shoot the well. |
| Orbiter | Ranged | 60 | 95 | 12 | - | 8 | 11 |  | Circles you with three orbs turning round it. One flares, then it slings that orb straight at you, fast. |
| Collapsar | Melee | 220 | 58 | 22 | 20 | 12 | 16 | melee | A slow, dense star that shrinks as it's shot. Killed, it collapses: a short pull, then a ring of 12 shots. Kill it at range. |
| Small Well | Hidden | 30 | 0 | 11 | - | 1 | 3 | melee |  |
| Implosion | Hidden | 1000000 | 0 | 10 | - | 0 | 0 | melee |  |
| Lens | Melee | 90 | 70 | 14 | 12 | 9 | 11 | melee | Carries a gravity lens in front of it. Shots into its face curve round it and away; it turns slowly, so hit it from the side. |
| White Dwarf | Melee | 40 | 230 | 9 | 10 | 8 | 10 | melee | A small hot star that darts in, flashes white, then shoves you back and turns your shots near it round at you. Stop shooting when it flashes. |
| Rubble | Melee | 40 | 110 | 12 | 15 | 3 | 7 | melee | A fist-sized chunk of the Colossus that rolls at you, weaving, in pairs. |
| Quarryman | Melee | 90 | 125 | 13 | 14 | 6 | 15 | melee | A quarry worker with a hard hat, a headlamp and a pickaxe. Walks straight at you. |
| Stomper | Melee | 180 | 58 | 21 | 25 | 12 | 20 | melee | A slab-shouldered stone brute. Every few seconds it lifts a foot and stomps out a small ring of push blocks with one green gap: step into the gap. |
| Slab Bearer | Melee | 60 | 120 | 16 | 8 | 10 | 10 | melee | Carries a quarried slab that stops shots and bashes you back. Break the slab and it drops it as cover for a few seconds; then the bearer falls fast. |
| Rock Hurler | Ranged | 50 | 75 | 14 | - | 8 | 9 |  | Lobs a boulder where you're heading. Walk out of the ring: it lands hard, bursts into shards and leaves a rubble pile that stops shots. |
| Driller | Melee | 45 | 115 | 12 | 18 | 9 | 9 | melee | Circles in close with a jackhammer, shows a lane, then drills along it. The crack it leaves erupts a moment later: get off the line. |
| Geomancer | Ranged | 70 | 80 | 13 | - | 9 | 11 |  | Keeps its distance, plants its crystal staff and sends a crack racing at you. It glows, then erupts: step off the line. |
| Cinder Priest | Ranged | 70 | 80 | 13 | - | 9 | 11 |  | Charges a molten core, then fires one magma bolt straight at you. Wherever it stops, it leaves a pool of lava. Strafe across the line, or parry it. |
| Riot Captain | Hidden | 3000 | 70 | 30 | 20 | 150 | 80 | mini-boss, heavy | The Warden's mini-boss: a tower shield blocks the front. Bait its shield charge into a wall to daze it; it also fires buckshot, lobs tear gas and whistles for Taser Guards. |
| Fencer | Melee | 32 | 115 | 11 | 10 | 4 | 7 | melee | A masked foil-fencer that runs in crowds. Close up it shows a short red line, then hops down it: step off the line. |
| Blade Dancer | Melee | 70 | 105 | 13 | 14 | 8 | 14 | melee | Shows a long warning line, then lunges down it and leaves blades hanging on the path. Each fires one shot out of each side: never stand beside the line. |
| Parrier | Melee | 60 | 110 | 15 | 8 | 10 | 10 | melee | On guard behind a main-gauche: shots into its front are parried straight back at you, and it bashes you away. It turns slowly: get behind it. |
| Second | Ranged | 45 | 70 | 13 | - | 9 | 9 |  | Hangs back, glints gold, then throws an aimed fan of five daggers. Slip between them. |
| Marshal | Ranged | 55 | 75 | 14 | - | 8 | 9 |  | The duel's referee: raises its baton and draws a gold circle round you. For 4 s you can't leave it, and leaning on the edge hurts: fight in the middle. |
| Gallant | Ranged | 80 | 90 | 14 | 20 | 10 | 12 |  | Salutes with its sword raised, then lunges at you. Parry it perfectly as it arrives and it reels, open to 50% more damage; dodge it and it rakes the spot. |
| Harlequin | Melee | 240 | 60 | 20 | 25 | 14 | 22 | melee | A heavy two-sworded jester. It marks a line through you and dashes it, vaults to the other arm and dashes across: an X of blades that fire out sideways. Stand in a gap of the X. |
| Mummer | Melee | 40 | 150 | 11 | 14 | 9 | 10 | melee | A masked player that blinks and becomes three. They circle you and all fire, but only the real one's shots are solid; the copies' are hollow rings. Find the real one. |
| Mummer | Hidden | 1 | 0 | 11 | - | 0 | 0 | melee | One of a Mummer's two copies: its shots are hollow rings that pass through you. It vanishes when its time is up. |
| Grave Caller | Ranged | 60 | 60 | 14 | - | 12 | 16 |  | Zombie: hangs back and cracks three graves open round you; a Shambler claws out of each. Step out of the ring, then kill it: its risen crumble. |
| Gnawling | Melee | 40 | 110 | 11 | 15 | 3 | 7 | melee | A mouth on legs, the Devourer in miniature. Weaves at you in pairs. |
| Butcher | Melee | 90 | 125 | 13 | 14 | 6 | 15 | melee | A hooded butcher with a bone mask. Close in, it raises its cleaver and chops a half-circle in front: step out of it. |
| Gulper | Melee | 180 | 58 | 22 | 25 | 12 | 25 | melee | A swollen gut. Its cheeks swell, then it breathes in and pulls you toward its mouth: step out of the cone sideways. |
| Carcass Bearer | Melee | 60 | 120 | 16 | 8 | 10 | 10 | melee | Carries a side of meat that stops shots and bashes you back. Break it and it drops as a morsel: shoot it or walk over it before a Gnawling eats it. |
| Bile Spitter | Ranged | 50 | 75 | 14 | - | 8 | 9 |  | Lobs a glob of bile where you're heading. Walk out of the ring: it leaves a pool of acid for a few seconds. |
| Ember | Melee | 16 | 135 | 9 | 8 | 3 | 6 | melee | A scrap of solar flare that weaves in at you, dropping sparks that hang in the air behind it. |
| Corona Priest | Ranged | 45 | 85 | 13 | - | 7 | 10 |  | A sun-cultist. Its halo flares, then bursts into a ring of slow shots: one at you, and a gap beside it. |
| Heliograph | Ranged | 70 | 75 | 13 | - | 9 | 12 |  | Tilts a sun mirror at you: a line flickers for a second, then becomes a lane of fire. Step off the line. |
| Moonward | Melee | 100 | 70 | 14 | 12 | 9 | 11 | melee | A pale knight with a small moon circling it. The moon swallows any shot it touches: fire when it swings round the back. |
| Gnomon | Melee | 240 | 55 | 22 | 20 | 14 | 20 | melee, heavy | A walking sundial with a sun lantern at its back. The lantern swells, then flashes: the only safe place is the slab's shadow, just to the side of you. |
| Dusk Lobber | Ranged | 50 | 80 | 13 | - | 8 | 9 |  | Lobs a ball of night that bursts into a pool of the Umbra's dark: you can barely see inside, and it drains you. Stay in the light. |
| Sickle | Ranged | 60 | 90 | 13 | - | 9 | 11 |  | Throws a crescent blade that loops out through you and comes back round to it. Dodge it twice. |
| Nova | Melee | 40 | 230 | 9 | 10 | 8 | 10 | melee | A small unstable star. Close in it lights a 3 s fuse, then goes nova. While lit it takes 50% more damage: burst it or back off. |
| Gutworm | Melee | 70 | 110 | 13 | 12 | 9 | 12 | melee | Dives, and a ripple chases you underground. It stops, marks the ground, then erupts with a bite and a ring of shots: get out of the mark. |
| Hook Thrower | Melee | 55 | 80 | 14 | - | 9 | 10 | melee | Shows a line, then throws a meat hook down it. A hit hurts and reels you in: sidestep the line, or shoot the chain. |
| Maw Priest | Ranged | 90 | 80 | 14 | - | 10 | 12 |  | Wears the Devourer's jaw: shots into its maw heal it, shots in its back do more. It turns slowly: circle it. |
| Dust Devil | Melee | 22 | 135 | 10 | 8 | 3 | 6 | melee | A small whirlwind that weaves in at you. Its touch shoves you hard, into whatever is behind you. |
| Squall Gunner | Ranged | 45 | 85 | 13 | - | 7 | 10 |  | Carries its own gust: it aims three shots upwind of you, and they curve back onto you. Dodge against the curve. |
| Stormcaller | Ranged | 50 | 80 | 13 | - | 8 | 11 |  | Raises its staff and calls lightning on you: a ring fills under you, then the strike. Step out of the ring. |
| Grounder | Ranged | 90 | 70 | 15 | - | 9 | 12 |  | A copper shield grounds your shots. Twelve charge it, and it claps a ring back with two gaps. Flank it. |
| Pylon | Ranged | 55 | 110 | 12 | - | 5 | 7 |  | Comes in pairs that plant either side of you. The arc between them flickers, then fires. Step off the line, or kill one. |
| Lineman | Ranged | 60 | 85 | 13 | - | 8 | 11 |  | Hooks a player with a charge. When it runs out it jumps to every ally nearby. The marked player walks away from the team. |
| Thunderhead | Melee | 220 | 55 | 24 | 20 | 12 | 20 | melee, heavy | A slow, heavy storm cloud. As it rolls at you it drops a row of strikes ahead of it. Sidestep the row. |
| Turnkey | Ranged | 70 | 160 | 13 | 6 | 12 | 16 | melee | A prison janitor with a ring of keys. Runs from you, stops to plant mines, shackle traps and wet floors, and builds Cell Doors that let Rioters out. Corner it while it works. |
| Cell Door | Hidden | 90 | 0 | 16 | - | 4 | 5 | melee, heavy | A Turnkey's cell door: lets a Rioter out every few seconds. It jams shut when its Turnkey dies. |
| Floor Mine | Hidden | 1 | 0 | 8 | - | 0 | 0 | melee, heavy | A Turnkey's mine: arms, then beeps and blows when you step close. Shoot it to blow it on enemies. |
| Shackle Trap | Hidden | 1 | 0 | 9 | - | 0 | 0 | melee, heavy | A Turnkey's shackle: arms, then snaps shut on the first player over it and holds them a moment. Shoot it to break it. |
