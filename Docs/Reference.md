# Reference tables

Generated from `Defs`, `Upgrades` and `Classes` by `Tools/SimTest/run.sh 1 1 refdocwrite`; don't edit by hand (the `refdoc` test fails when it is stale).
Numbers are before upgrades. Player-facing prose is in [README.md](../README.md).

## Roles

| Role | Health | Speed | Fire interval | Ability | Cooldown |
|---|---|---|---|---|---|
| Tank | 170 | 185 | 0.45 s | Barrier | 12 s |
| Healer | 140 | 215 | 0.25 s | Mend Pulse | 10 s |
| DPS | 110 | 240 | 0.1 s | Dash | 3.5 s |
| Wildcard | 125 | 225 | 0.1 s | Any DPS ability | 3.5 s |

## Classes

| Class | Health | Speed | About |
|---|---|---|---|
| Soldier | x1 | x1 | Guns and gear. Straightforward firearms with an Alt trick each (a point-blank shell, a proximity mine, a grenade volley) and field kit for abilities. Full health and normal speed: the steady pick. |
| Elementalist | x0.85 | x1 | Spells of fire, frost, lightning, water, wind, earth and light. Elements combine: soaked enemies conduct lightning, earth cracks enemies so they take more damage (and frozen ones shatter), light dazzles them so their shots go wide. Big area damage, at 85% health. |
| Summoner | x0.9 | x1 | Fights through minions. Your weapon brings its own creatures (wolves, wisps, imps, bees) that hunt, bite and soak hits, and they stay with you between waves. Point them at what matters. 90% health. |
| Knight | x1.15 | x1 | Melee. Holding fire swings instead of shooting, hitting every enemy in the arc and cutting small bullets out of the air in front of you (big orbs, mines and red aimed shots still get through). Armoured for the thick of it: 115% health. |
| Ninja | x0.85 | x1.12 | Thrown blades and quick melee with tricks and misdirection: stars that come back, stances, smoke and shadow clones. Fast (+12% move speed) but lightly protected (85% health): hit, slip away, hit again. |
| Hemomancer | x1 | x1 | Blood magic: health is your resource. Every 1st ability costs a share of your health (never below 20%: what you can't pay lengthens the cooldown), your kills drop health orbs only you can drink, and you hit harder the lower your health (up to +40%). Shorter cooldowns make up for it. Full health and normal speed: live on the edge. |
| Engineer | x1 | x0.95 | A builder who works in vectors: robots, walls, pylons and breaches drawn in cyan ink. Draftsman: +25% vectors. One robot weapon, ability and ultimate per role for now (the DPS also has the Bumble Remote). Full health, a little slower (95%). |
| Arcanist | x0.9 | x1 | A mage of telekinesis, runes and enchantments: shards and blades that fly at your will, enemies lifted and thrown, sigils on the floor. Kinetic Field: enemy shots slow down near you. One weapon, ability and ultimate per role for now. 90% health. |

## Weapons

### Soldier

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Scattergun | Tank | 0.45 s | 7 | 520 x 0.5 s | - | 154 | Primary Fire: A short-range shotgun spread, brutal up close.  Alternative Fire: Lob a shock grenade that stuns enemies where it lands. |
| Flak Cannon | Tank | 0.8 s | 30 | 380 x 1.1 s | - | 36 | Primary Fire: Shells that burst in the air at your cursor and destroy enemy bullets.  Alternative Fire: Lay a proximity shell that bursts when an enemy steps on it. |
| Riot Repeater | Tank | 0.18 s | 11 | 600 x 0.8 s | - | 60 | Primary Fire: Steady shots that suppress enemies so they shoot slower.  Alternative Fire: Lob tear gas that slows and suppresses. |
| Grenade Launcher | Tank | 0.42 s | 34 | 900 x 0.42 s | - | 49 | Primary Fire: Lob a drum of grenades that burst where they land.  Alternative Fire: Swap to smoke that slows enemies and stops them attacking. |
| Repulsion Cannon | Healer | 0.7 s | 6 | - | 240 | 9 | Primary Fire: Blasts enemies away in a cone and knocks health orbs loose.  Alternative Fire: Hold to pull enemies in. |
| Revolver | Healer | 0.5 s | 50 | 700 x 1.1 s | - | 100 | Primary Fire: Click to fire a hard, aimed shot, click again to pull the hammer back: it shoots as fast as you click.  Alternative Fire: Fan stance: click to Pistol Whip (stuns what it hits), click again to Fan the Hammer. Hold parry and click to fan any time; parry after a whip to whip again. |
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
| Static Shock | Healer | 0.1 s | 24 | - | 200 | 23 | Primary Fire: Hold for a short cone of static that wires enemies into one circuit and shocks them, harder the longer they stay. Teammates it touches get Overcharge: faster movement and shots for 10 s.  Alternative Fire: Arc Cannon: a giant ball of lightning rolls through everyone to your cursor and bursts into a stunning shock. |
| Fireball | Dps | 0.5 s | 32 | 430 x 1.4 s | - | 71 | Primary Fire: A big, slow fireball that rolls through enemies and sets them burning. |
| Arc Lightning | Dps | 0.25 s | 16 | - | - | 116 | Primary Fire: Hold to tag up to 10 enemies, let go to chain lightning through them and stun.  Alternative Fire: Three forked bolts at the enemies nearest your cursor. |
| Ice Shards | Dps | 0.3 s | 7 | 520 x 1 s | - | 70 | Primary Fire: Ice shards orbit you, then launch homing at nearby enemies. |
| Stonespear | Dps | 0.18 s | 24 | 900 x 0.6 s | - | 60 | Primary Fire: Slam the ground, then throw the earth spears that rise from it, one per click.  Alternative Fire: Hurl every spear at once. |
| Water Gun (variant) | Tank | 0.07 s | 5 | 640 x 0.55 s | - | 60 | Primary Fire: A long spray of water that splashes the enemy it hits and those near it: wet enemies are slowed. |
| Gale Gun (variant) | Tank | 0.06 s | 2.5 | 560 x 0.4 s | - | 82 | Primary Fire: A blast of cutting wind that passes through enemies: it slices them (they bleed) and slows them. |
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
| Hive Censer | Healer | 0.1 s | 18 | - | 420 | 83 | Primary Fire: Hold to pour insects from your cursor. They gather into swarms that wrap enemies (damage and slow) and shell allies in chitin, a barrier that wears off and is topped up near a swarm.  Alternative Fire: Hive Call: every swarm rushes to your cursor. |
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
| Arc Knuckles | Dps | 0.24 s | 22 | - | 80 | 88 | Primary Fire: Brawl stance: a Pull Punch opens a click wheel; click its sides to send punches and dashes that way, then pin, barrage and quake.  Alternative Fire: Swap to the Rocket stance: Rocket Dash, Flurry, Grab & Throw, Rocket Punch, one click each. |

### Ninja

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Kusarigama | Tank | 0.55 s | 26 | - | 280 | 37 | Primary Fire: Cut with the sickle, then throw the weight to drag an enemy to you.  Alternative Fire: Whirl the weight around you, pulling enemies in. |
| Bo Staff | Tank | 0.47 s | 16 | - | 105 | 48 | Primary Fire: A three-swing staff combo that cuts small bullets.  Alternative Fire: Shove a wall of staff ahead of you. |
| Fuma Shuriken | Tank | 0.7 s | 30 | - | - | 6 | Primary Fire: A giant star thrown in a loop that cuts everything both ways. |
| Nodachi | Tank | 0.3 s | 20 | - | 150 | 52 | Primary Fire: A tall katana with two five-step combo stances: wide sweeps, or fast jabs that block.  Alternative Fire: Switch stance. |
| Acupuncture Needles | Healer | 0.18 s | 10 | 900 x 0.8 s | - | 63 | Primary Fire: Fast needles that fly through enemies and stick in hurt teammates, healing over time. Five open the meridian for a burst of healing.  Alternative Fire: Bloodletting, three needles that cut and bleed enemies. Five in one enemy burst at the pressure point; a dying enemy's needles fly back to heal the most hurt teammate. |
| Spirit Fan | Healer | 0.4 s | 6 | 420 x 0.3 s | - | 75 | Primary Fire: A short gust that heals allies and pushes small bullets back. |
| Ofuda | Healer | 0.45 s | 45 | 380 x 1.1 s | - | 40 | Primary Fire: Healing Talisman: a paper charm that bends toward a hurt teammate at the end of its flight, heals them and bursts, healing teammates near, shoving enemies back and erasing small bullets.  Alternative Fire: Blast Ofuda: a charm that flies straight, sticks to the enemy it hits and blows up; one that misses lands and blows up a moment later. |
| Twin Tessen | Healer | 0.2 s | 10 | - | 105 | 54 | Primary Fire: A five-step dance of two war fans that heals the allies it brushes and ends in a petal wave.  Alternative Fire: Hold to pull the fans back, let go for a massive gust that throws enemies back and heals allies. |
| Shuriken | Dps | 0.35 s | 9 | 700 x 0.75 s | - | 140 | Primary Fire: Tight fans of stars that stick, then rip out and fly back.  Alternative Fire: Switch to a huge star thrown along a route you draw. |
| Katana | Dps | 0.3 s | 20 | - | 120 | 65 | Primary Fire: Two stance combos: dashing slices, or cuts that end in caltrops.  Alternative Fire: Switch stance. |
| Kunai | Dps | 0.7 s | 42 | 1400 x 0.5 s | - | 88 | Primary Fire: Slow, heavy throwing knives that leave a mark where they land.  Alternative Fire: Blink to the mark, and your next hit lands double. |
| Nunchaku | Dps | 0.3 s | 20 | - | 95 | 95 | Primary Fire: A four-step twin-nunchaku combo that ends in a stunning slam.  Alternative Fire: Guard with spinning discs that swat shots back. |

### Hemomancer

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Blood Lash | Tank | 0.32 s | 24 | - | 130 | 96 | Primary Fire: A four-step blood whip that chains enemies, then yanks them in.  Alternative Fire: Hold for Martyr's Leash. |
| Gore Mortar | Tank | 0.75 s | 34 | - | - | 133 | Primary Fire: Hold to charge, then lob a blood clot that leaves a slowing pool. |
| Leech Tendrils | Tank | 0.1 s | 80 | - | 160 | 31 | Primary Fire: Hold to grab and stun nearby enemies; they burst when the blood builds up. |
| Blood Blast | Tank | 0.42 s | 22 | - | 115 | 53 | Primary Fire: A six-step blood sword combo; every hit marks enemies, and their deaths heal you. |
| Transfusion Darts | Healer | 0.2 s | 7 | 850 x 0.85 s | - | 32 | Primary Fire: Darts that heal hurt allies for more than they cost you. |
| Sanguine Orb | Healer | 0.45 s | 16 | 420 x 1.4 s | - | 57 | Primary Fire: Homing orbs that make enemies bleed, or heal a hurt ally once charged. |
| Vein Tether | Healer | 0.08 s | 4 | - | 350 | 48 | Primary Fire: A draining beam that hurts enemies and heals you and the nearest ally. |
| Heart Flail | Healer | 0.45 s | 18 | - | 105 | 40 | Primary Fire: Click to swing a living heart that makes enemies bleed and sprays healing blood; hold to spin it, healing allies near you with every heartbeat. Hits quicken the beat: at 180 the next beat is a Cardiac Burst.  Alternative Fire: Open Heart: throw the heart to your cursor, where it roots and heals every ally near it through its veins. |
| Needle Swarm | Dps | 0.1 s | 4 | - | 260 | 140 | A swarm of blood needles circles you, blocking shots. Lost needles are siphoned from your health.  Primary Fire: Hold to grow a tendril to the enemy nearest your cursor; it wraps them and cuts until they die.  Alternative Fire: Throw a fan of needles from the swarm. |
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
| Bumble Remote | Dps | 0.24 s | 7 | - | - | 105 | You can't attack: a giant robot bee follows your cursor and fights for you. Enemies still go for you.  Primary Fire: Hold to double its stinger rate.  Alternative Fire: Swarm Burst: it dives to your cursor, blasts enemies away and releases a swarm of bees. |

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
| Barrier | Tank | 1st, choice 1 | 12 s | - | Bullets that hit the barrier are reflected back as your own. | A bubble that stops enemy bullets. |
| Roar | Tank | 1st, choice 2 | 14 s | - | Also knocks enemies within 250 px back and slows them for 2 s. | Every enemy targets you, and you take less damage. |
| Bulldoze | Tank | 1st, choice 3 | 9 s | - | Erases enemy bullets along the charge. | Charge forward, damaging and knocking back everything in your path. |
| Fortress | Tank | ultimate | 70 s | - | Lasts 14 s. | A dome only players and their bullets can cross. |
| Juggernaut Suit | Tank | ultimate | 75 s | - | 450 armour, and each shard does 18. | Heavy armour draws every enemy and throws flak back at shooters. |
| A Literal Tank | Tank | ultimate | 80 s | - | 550 armour, and the shells blast 30% wider. | Drive an actual tank for 15 s: heavy armour, big explosive shells, and it runs enemies over. |
| Armor Drop | Tank | ultimate | 80 s | - | The crate holds a 4th vest. | Call in a crate that hits enemies where it lands, then opens: 3 ballistic vests, 50 armor each. Armor never regenerates. |
| Deployable Cover | Tank | 1st, choice 5 | 12 s (+1 charges) | - | Covers take 600 damage, and each charge comes back 25% sooner. | Plant steel cover that stops enemy bullets while your team shoots through. |
| Shockwave | Tank | card | 14 s | - | The wave runs out 300 px. | A shockwave erases bullets and shoves enemies back. |
| Overdrive | Tank | card | 18 s | - | +1 extra projectile while it lasts. | Double your fire rate for a few seconds. |
| Blink | Tank | card | 7 s | - | Erases enemy bullets where you leave and where you land. | Teleport toward your cursor. |
| Starburst | Tank | card | 9 s | - | Fires 48 shots instead of 24. | Fire a ring of heavy shots. |
| Turret | Tank | card | 20 s | - | Deploys two turrets. | Deploy a turret that shoots for a while. |
| Stun Gun | Tank | card | 14 s | - | Nine shots instead of six. | Your next shots are stun bolts. |
| Royal Dive | Tank | card | 12 s | - | A wider honey pool (150 px) that lasts 6 s. | Leap and crash down at your cursor, leaving a honey pool. |
| Scythe Throw | Tank | card | 9 s | - | Catching it takes 60% off, and it cuts 50% harder. | Throw a giant scythe that cuts out and back; catch it to cool down faster. |
| Mend Pulse | Healer | 1st, choice 1 | 10 s | - | Also erases enemy bullets within its range. | Heal everyone nearby and revive downed allies. |
| Adrenaline Shot | Healer | 1st, choice 2 | 14 s | - | Also gives the nearest other ally within 250 px a half-length (4 s) dose. | Boost a player's speed and fire rate and cut their damage taken. |
| Deployable Medkit | Healer | 1st, choice 3 | 14 s | - | Bursts into 9 orbs worth 20 health each. | Throw a medkit that bursts into health orbs anyone can grab. |
| Lifeline | Healer | ultimate | 90 s | - | Everyone also gets 4 s of Overdrive. | Revive and fully heal the whole party, with brief invulnerability. |
| Field Hospital | Healer | ultimate | 150 s | - | 8 orbs, one back every 3 s. | Open a hospital cell behind a door: a shrine, healing orbs and a laser gate for a minute. |
| Supply Drop | Healer | ultimate | 110 s | - | The zone lasts 18 s and packs heal 40. | Call in a crate that hits enemies where it lands, then opens: faster cooldowns, shooting and movement inside, and health packs. |
| Decoy Dummy | Healer | 1st, choice 5 | 4 s (+2 charges) | - | Dummies have 50% more health, and the confetti blast reaches 130 px. | Throw an inflatable soldier that enemies shoot at instead of you. |
| Poison Dart | Dps | 1st, choice 1 | 8 s | - | Spreads to 5 enemies at a time, at full strength. | Poison the nearest enemy; the poison spreads when it dies. |
| Airstrike | Dps | 1st, choice 2 | 16 s | - | A second wall lands 160 px further out, half a second later. | Call a jet to carpet a line with bombs. |
| Claymore | Dps | 1st, choice 3 | 5 s (+1 charges) | - | Each claymore re-arms once after its first blast: it goes off twice. | Plant a mine at your cursor; the first enemy to step into the cone in front of it sets off a wide blast. |
| Artillery Assault | Dps | ultimate | 60 s | - | +4 extra projectiles instead of +2. | Fire three times as fast with extra projectiles. |
| Attack Helicopter | Dps | ultimate | 70 s | - | A second gun on the wing: two targets at once. | A gunship locks enemies one at a time and can't be hit. |
| Orbital Laser | Dps | ultimate | 90 s | - | The circle is 340 px wide instead of 260, and the beam ploughs 200 px on while it fires. | Call a laser down from orbit on your cursor: after a 1.9 s lock-on it kills every enemy in a huge circle (bosses lose 20%) and leaves the ground burning. |
| Rocket Drop | Dps | ultimate | 80 s | - | 13 s, and each blast throws 3 bomblets. | Call in a pod with a rocket launcher, infinite ammo, for 10 s. Each rocket's blast is huge and leaves the ground burning. |
| Deployable Turret | Dps | 1st, choice 5 | 45 s | - | Fires 14 s, rounds pierce 4, and the blast does double. | Climb into a heavy turret that spools up to a fast piercing fire. |

### Elementalist

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Aeroaegis | Tank | 1st, choice 1 | 11 s | - | Lasts 4.5 s and reflects bullets for double damage. | A wind vortex wraps a player: enemies can't enter and bullets reflect back. |
| Frost Nova | Tank | 1st, choice 2 | 12 s | - | Also erases enemy bullets around you. | Freeze every enemy near you and damage them. |
| Flame Wall | Tank | 1st, choice 3 | 14 s | - | The fire also slows enemies by 40%. | A wall of fire at your cursor that burns enemies and blocks bullets. |
| Glacial Tomb | Tank | ultimate | 75 s | - | Enemies stay frozen for 6 s and bosses take 500. | Erase every enemy bullet and freeze every enemy. |
| Lightning Rod | Tank | ultimate | 70 s | - | Lasts 8 s and each bolt arcs to 3 enemies. | You draw every enemy while taking much less damage, and lightning strikes whoever hits you. |
| Stone Colossus | Tank | ultimate | 80 s | - | Lasts 18 s and slams reach 25% wider. | Become a huge earth golem: stomp what you walk over and slam the ground with both fists. |
| Vortex | Tank | ultimate | 80 s | - | 300 px across instead of 240, and 55 damage a second. | A huge whirlwind round you drags enemies with it, shreds them and sweeps their shots away, then flings them out. |
| Flood | Tank | 1st, choice 5 | 16 s | - | Deluge: the flood is 30% wider and lasts 9 s. | Flood the ground around you: it throws enemies back, slows them and soaks them. |
| Healing Rain | Healer | 1st, choice 1 | 16 s | - | Lasts 10 s, and downed allies inside revive twice as fast. | A rain at your cursor that heals allies, and slows and soaks enemies. |
| Gust Gate | Healer | 1st, choice 2 | 14 s | - | The ring is 40% wider and pushes enemies twice as hard. | A wind ring that gives anyone passing through a speed boost. |
| Lumen Ward | Healer | 1st, choice 3 | 20 s | - | Also freezes enemies outside the light for as long as it lasts (not bosses). | A cone of light that heals allies and burns away enemy bullets. |
| Sanctum | Healer | ultimate | 80 s | - | The dome is half again as wide (300 px). | A dome of light that blocks bullets, keeps allies up and heals them. |
| Static Bubble | Healer | ultimate | 80 s | - | The burst reaches 240 px, and Static Rush lasts 8 s. | A bubble of static round you shoves enemies out, eats their shots and shields allies inside. What it touches feeds it until it bursts: Static Rush for everyone in the blast. |
| Water Ward | Healer | 1st, choice 5 | 3 s | - | Splashes and bursts reach 40% farther. | Water drops orbit you and splash enemies and eat shots. |
| Meteor | Dps | 1st, choice 1 | 10 s | - | Leaves burning ground for 3 s. | Call a meteor down at your cursor. |
| Blizzard | Dps | 1st, choice 2 | 14 s | - | Lasts 10 s. | A storm at your cursor that slows and damages enemies. |
| Tsunami | Dps | 1st, choice 3 | 16 s | - | Wet lasts 14 s, and the wave rolls back to you, wetting what it missed. | A wave that soaks enemies, so lightning arcs to every one. |
| Bullet Rain | Dps | ultimate | 70 s | - | Lasts 9 s. | A downpour of needles that hurts and soaks everything in a big square. |
| Living Bolt | Dps | ultimate | 80 s | - | Lasts 13 s and the trail stays 5 s. | Become lightning for 10 s: every click blinks you to the cursor, shocking all you pass and leaving a live trail. |
| Flash Fire | Dps | ultimate | 80 s | - | The fire burns 11 s. | A ring of fire rolls out around you and the ground burns for 8 s, hurting and slowing enemies; when it goes out they're left burning. |
| Gale Wall | Dps | 1st, choice 5 | 18 s | - | The wall is 40% wider and holds up to 60 charges. | A wind wall that traps enemy shots, then unleashes them as a blast. |

### Summoner

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Golem Wall | Tank | 1st, choice 1 | 13 s | - | Raises five golems. | Raise three golems that block every enemy bullet. |
| Earthquake | Tank | 1st, choice 2 | 10 s | - | Also freezes everything it hurts for 1 s (not bosses). | Slam the ground, then a Burrower tunnels through enemies. |
| Force Pact | Tank | 1st, choice 3 | 12 s | - | A second thrall: the pact keeps the last two types you chose. | Make a regular enemy fight for you. |
| Titan | Tank | ultimate | 80 s | - | The titan is 30% bigger and stays for 27 s. | Summon a huge stone titan that stomps enemies and blocks bullets. |
| Bone Phalanx | Tank | ultimate | 75 s | - | 10 skeletons with 180 health each. | Skeletons with tower shields march ahead, blocking shots and spearing enemies. |
| Iron Armadillo | Tank | ultimate | 75 s | - | Rolls 13 s and the uncurl slam hits for 130. | Curl into a giant armoured ball for 10 s: roll over enemies, bounce their shots back, slam down as you uncurl. |
| Tidal Tusker | Tank | ultimate | 75 s | - | Stays 16 s, and sprays two streams at once. | A spirit elephant rises beside you and follows you for 12 s. Enemies near it go for it instead of you, and its trunk hoses them with a steady stream that soaks and pushes them back. It ends with a splashing stomp. |
| Stampede | Tank | 1st, choice 5 | 14 s | - | 9 beasts, and the herd turns and charges back. | A line of spirit beasts tramples everything ahead. |
| Spirit Swarm | Healer | 1st, choice 1 | 14 s | - | Releases 8 sprites. | Release healing sprites. |
| Phoenix | Healer | 1st, choice 2 | 22 s | - | Revives at full health, and the phoenix keeps fighting. | A phoenix follows your cursor, shooting and healing, and revives anyone who goes down. |
| Soul Lantern | Healer | 1st, choice 3 | 18 s | - | Lasts 9 s, and the linked player can't go down. | A lantern links to an ally, healing them and burning nearby enemies. |
| World Tree | Healer | ultimate | 80 s | - | Lasts 16 s with 8 sprites. | Grow a tree that heals and slows around it and sends healing butterflies. |
| Honey Raid | Healer | ultimate | 80 s | - | The pool is 25% wider. | A giant bee flies over your cursor and dumps its honey: a big pool that slows and hurts enemies and heals allies. |
| Bat Swarm | Dps | 1st, choice 1 | 12 s | - | Releases 14 bats. | Release bats that hunt enemies. |
| Hydra | Dps | 1st, choice 2 | 18 s | - | A fourth head. | A three-headed hydra spits venom at enemies. |
| Alpha Call | Dps | 1st, choice 3 | 14 s | - | Also summons 3 wolves for 6 s. | Your minions bite faster, then lunge for triple damage. |
| Legion | Dps | ultimate | 70 s | - | Summons 7 wolves and 7 bats. | A blood moon: wolves and bats circle you, you run faster and your minions frenzy. |
| Rodent Minigun | Dps | ultimate | 70 s | - | Fires 2 s longer. | A portal opens in front of you and spins up into a minigun of mice, rats and hamsters, following your aim. |
| Mammoth Fall | Dps | ultimate | 75 s | - | Rampages 1.5 s longer. | Summon a woolly mammoth in the sky, aim it, and it slams down, stunning everything around, then rampages through enemies. |
| Land Shark | Dps | 1st, choice 5 | 14 s | - | A 30% wider puddle, and the bite hits for 160. | A puddle opens at your cursor and a shark leaps out, biting everything in it. |

### Knight

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Shield Wall | Tank | 1st, choice 1 | 12 s | - | Bullets that hit the shield are reflected back as your own. | Raise a wall of steel plates that blocks bullets at your front. |
| Leap Slam | Tank | 1st, choice 2 | 9 s | - | The landing also erases enemy bullets within 160 px. | Leap to your cursor untouchable, then slam and stun. |
| Cavalry Call | Tank | 1st, choice 3 | 18 s | - | Two more lancers ride outside the first pair, and the guard lasts 5 s. | Two lancers charge across the map, shoving and slowing enemies. |
| Last Stand | Tank | ultimate | 70 s | - | You take 5% of the damage instead of 10%. | Plant yourself behind a wall of steel plates that blocks every bullet in front. |
| Trojan Horse | Tank | ultimate | 75 s | - | 40% more health, and the arrows pierce one more enemy. | A wooden horse rolls out to the arena's edge. Players press Interact to climb in: safe inside, they shoot arrows out of it. When everyone's in, enemies go for the horse. |
| Drawbridge | Tank | ultimate | 75 s | - | The bridge stands for 8 s. | A drawbridge crushes the lane ahead, then blocks enemies and shots. |
| Siege Bell | Tank | 1st, choice 5 | 22 s | - | The stun lasts 3.5 s and the shockwave erases enemy bullets. | Ring a ghostly bell three times; the last toll's shockwave stuns enemies around it. |
| Consecrate | Healer | 1st, choice 1 | 14 s | - | Two more crosses fly out at 22 degrees either side (each target is hit once). | Throw a Holy Cross that heals and speeds allies and hurts enemies. |
| Guardian's Oath | Healer | 1st, choice 2 | 15 s | - | Charges up to 800 px. The ward is wider, follows you, and stands a downed ally back up at 25% health. | Haul an ally to safety and protect everyone in a ward of light. |
| Lay on Hands | Healer | 1st, choice 3 | 18 s | - | Also heals everyone else near them for half as much. | Heal the most hurt ally, or revive a downed one. |
| Hallowed Blade | Healer | ultimate | 80 s | - | Three swords fall: one at your cursor and one either side. | A giant holy sword lands, healing allies and burning enemies for a while. |
| Sanctum Glass | Healer | 1st, choice 5 | 16 s | - | The pane is 40% wider and its motes heal 50% more. | Raise a pane of stained glass ahead that turns enemy shots passing through into healing. |
| Bladed Dive | Dps | 1st, choice 1 | 10 s | - | Each kill heals you 5% of your health. | Rush forward untouchable with a spinning blade; a kill resets the cooldown. |
| The Gauntlet | Dps | 1st, choice 2 | 9 s | - | The Challenge lasts 8 s, and the charge's impact stuns for 1 s. | Challenge an enemy and charge it; kill it in time to reset the cooldown. |
| Caltrop Run | Dps | 1st, choice 3 | 12 s | - | Caltrops last 9 s and each catches 3 enemies. | Sprint and scatter caltrops behind you that cut, bleed and slow enemies. |
| Warhorn Volley | Dps | ultimate | 70 s | - | The volley is 40% wider and the fire burns for 8 s. | A volley of fire arrows sweeps the field, burning and pinning enemies. |
| Ballista | Dps | ultimate | 75 s | - | 10 bolts, and each one pins what it hits. | A giant ballista drops in front of you. Anyone can mount it (Interact) and fire 8 giant bolts that go through everything. |
| Cannon Call | Dps | 1st, choice 5 | 12 s | - | Two smaller balls land either side of the mark along your aim. | Flare a spot and a cannonball slams into it, blasting and stunning the pack; 3 kills give back half the cooldown. |

### Ninja

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Shadow Clone | Tank | 1st, choice 1 | 14 s | - | Every clone has double the health. | A wall of clones that every enemy attacks. |
| Smoke Bomb | Tank | 1st, choice 2 | 14 s | - | Enemies inside the smoke are also slowed by 40%. | A smoke cloud that slows enemy bullets and shields allies. |
| Ceremonial Armor | Tank | 1st, choice 3 | 2.5 s | - | A plate that breaks bursts for 30 damage to enemies within 60 px. | Armour plates circle you, blocking bullets and shoving enemies. |
| Thousand Clones | Tank | ultimate | 70 s | - | Twenty clones. | Twelve clones appear all over the map and draw every enemy. |
| Dragon's Dogma | Tank | ultimate | 70 s | - | Hunts for 7 s, and every enemy it devours heals you 3%. | A spirit dragon hunts across the arena, devouring enemies and their shots. |
| Spirit Avatar | Tank | ultimate | 70 s | - | Lasts 15 s and the cleave reaches 20% further. | A giant ink spirit wraps you: Fire paints a slow, wide Moon Cleave that throws enemies back. |
| Bamboo Grove | Tank | 1st, choice 5 | 14 s | - | The bamboo fence lasts 6 s. | Bamboo bursts from the ground around you, stunning enemies, then bleeding them. |
| Healing Dash | Healer | 1st, choice 1 | 15 s | - | The sprint lasts 4.5 s. | Sprint, leaving footprints that heal you and allies. |
| Substitution | Healer | 1st, choice 2 | 12 s | - | You and your ally are both invulnerable for 1 s after the swap. | Swap places with an ally and shield them. |
| Spirit Ward | Healer | 1st, choice 3 | 18 s | - | The ward follows you. | Plant a talisman that absorbs damage for allies, then heals them. |
| Blossom Storm | Healer | ultimate | 80 s | - | Lasts 8 s. | Petals heal the whole party and erase enemy bullets. |
| Astral Projection | Healer | ultimate | 70 s | - | Lasts 10 s and heals 40% more. | Leave your body safe and ignored for 8 s; your spirit rides a teammate, shoots astral bolts, and heals them and speeds them up. Right click: switch teammate. |
| Chakra Reserves | Healer | 1st, choice 5 | 28 s | - | A second thread reaches the next ally: half the heal and the full Surge. | Pour chakra into an ally: it heals them, speeds up their moving and attacks, and refreshes their cooldowns. |
| Shadow Step | Dps | 1st, choice 1 | 7 s | - | +1 charge. | Teleport behind an enemy and strike it hard. |
| Vanish | Dps | 1st, choice 2 | 16 s | - | Erases enemy bullets around you when you vanish. | Enemies lose track of you and your next hit deals double. |
| Blade Fan | Dps | 1st, choice 3 | 12 s | - | Throws twice as many kunai. | Spinning kunai cut enemies around you, then fly out in a ring. |
| Death Lotus | Dps | ultimate | 65 s | - | Strikes up to 14 enemies. | A flurry of cuts on up to 8 enemies near your cursor. |
| Forbidden Path | Dps | ultimate | 75 s | - | The beast lasts 11 s. | Become a shadow beast for 8 s: it locks onto the strongest enemy near and Fire tears through a five-part area combo. Drains your health. |
| Senbon Seekers | Dps | 1st, choice 5 | 6 s | - | Holds up to 12 needles. | Three poisoned needles circle you and fly at enemies that come close. Stacks to 9. |

### Hemomancer

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Blood Shell | Tank | 1st, choice 1 | 9 s | - | The burst also erases enemy bullets within 160 px. | Hold to wrap yourself and allies in a bubble that blocks bullets. |
| Blood Wall | Tank | 1st, choice 2 | 8 s | - | Enemies against the wall take double and are slowed 40%. | Toggle a wall of blood behind you that blocks bullets and hurts enemies. |
| Exsanguinate | Tank | 1st, choice 3 | 8 s | 10% of max health | Also slows the enemies it drains by 50% for 2 s. | Yank every nearby enemy in, stunned, and drain them. |
| Vein Tithe | Tank | ultimate | 75 s | - | Lasts 40 s and costs 25% less. | Bind every enemy on screen at the cost of your health. |
| Bloodbath | Tank | ultimate | 75 s | - | The pool spreads to 220 px and gives back 35% of the bleed. | Flood the floor with blood that slows and bleeds enemies and heals you. |
| Blood Golem | Tank | ultimate | 80 s | - | Lasts 13 s and each coat left bursts for 30. | Tear the blood off enemies and your own veins into coats, then fight as a golem made of them. |
| Blood Gift | Healer | 1st, choice 1 | 8 s | 25% of current health | Also heals everyone else within 200 px of them for half as much. | Pay health to heal the most hurt ally twice as much. |
| Blood Freeze | Healer | 1st, choice 2 | 14 s | 25% of max health | Frozen enemies take 25% more damage. | Freeze every enemy near you at a cost of health; enemies that die frozen heal you. |
| Martyr | Healer | 1st, choice 3 | 16 s | 40% of max health | You're invulnerable for 2 s after the sacrifice. | Pay health to revive a downed ally, or ward the most hurt one. |
| Sanguine Rain | Healer | ultimate | 80 s | - | Lasts 14 s and heals 4% a tick. | Rain healing circles that follow your allies. |
| Heartbeat | Healer | ultimate | 80 s | - | The waves hit 40% harder. | Your heart beats three times, each beat costing 8% of your health: a wave of blood bursts 320 px out of you, hitting for 45. Every enemy it kills drops a blood orb. |
| Arterial Spray | Dps | 1st, choice 1 | 8 s | - | The spray also washes away enemy bullets it touches. | Open a vein: a jet of blood hurts and bleeds everything in front of you. |
| Blood Debt | Dps | 1st, choice 2 | 16 s | - | Kills repay 50% more. | Borrow health to heal on hit, and pay it back with kills. |
| Vein Rush | Dps | 1st, choice 3 | 6 s | 10% of max health | The trail bursts after 1 s for 20 to everything on it. | Rush toward your cursor untouchable, bleeding enemies on the way. |
| Crimson Harvest | Dps | ultimate | 65 s | - | Hold 8 s, and 25% more blood. | Rip blood from every enemy into a ball, then throw it as a huge blast. |
| Blood Ravager | Dps | ultimate | 70 s | - | Lasts 12 s. | Eight blood legs burst out of you for 9 s: run fast and trample, and every click lashes a leg at an enemy nearby. |
| Blood Boil | Dps | ultimate | 70 s | - | Up to +90% damage at full boil. | Your health drains for 10 s; the lower it gets, the faster you move and fire and the harder you hit. Press again to end it. |
| Thorn Bloom | Dps | 1st, choice 5 | 8 s | 12% of max health | Tendrils jump 5 times. | Burst blood needles all round that chain between enemies; each needle that hits heals you. |

### Engineer

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Vector Wall | Tank | 1st, choice 1 | 10 s | - | Seven tiles, and it lasts 12 s. | Draw a wall that stops enemy shots. |
| Bastion | Tank | ultimate | 75 s | - | No gaps for the first 3 s, and it lasts 14 s. | Raise a ring of walls around you with gaps to shoot through. |
| Med Pylon | Healer | 1st, choice 1 | 6 s | - | Its field is 40% wider. | Plant a pylon whose field heals allies. |
| Safe Room | Healer | ultimate | 80 s | - | It lasts 18 s. | Open a wall into a room that heals and protects everyone inside. |
| Drag Pylon | Dps | 1st, choice 1 | 6 s | - | Its field is 40% wider. | Plant a pylon whose field slows enemies. |
| Breach | Dps | ultimate | 70 s | - | The blast is twice as wide. | Smash a wall to open a random room with loot or an ambush. |

### Arcanist

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Kinetic Grasp | Tank | 1st, choice 1 | 10 s | - | The throw hits 50% harder and stuns for twice as long. | Lift the biggest nearby enemy as a shield, then throw it. |
| Grand Sigil | Tank | ultimate | 75 s | - | It lasts 12 s. | A rune circle around you binds and slows enemies and freezes their bullets, then fires them back. |
| Arcane Tether | Healer | 1st, choice 1 | 12 s | - | It heals twice as much. | Pull the ally nearest your cursor to your side, safe and healed. |
| Sanctum Sigil | Healer | ultimate | 80 s | - | It lasts 12 s. | A healing circle follows you and its runes burst on enemies. |
| Telekinetic Crush | Dps | 1st, choice 1 | 9 s | - | It grabs 40% wider. | Lift enemies at your cursor, pull them together and slam them. |
| Runic Array | Dps | ultimate | 65 s | - | It lasts 9 s. | Four sigils behind you fire a spectral blade with every shot. |

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
| Reaper of Dawn | Hidden | 2600 | 95 | 34 | - | 250 | 100 | boss | The Twin Reapers' second half: fights alongside the Reaper of Dusk, then fuses with it into the Dual Reaper. |
| The Singularity | Boss | 3600 | 0 | 40 | - | 500 | 220 | boss | Boss (wave 20): corner gravity wells, pull, lasers and shrinking cages. |
| The Colossus | Boss | 6800 | 35 | 54 | - | 600 | 260 | boss | Boss (wave 25): huge stomp rings with one gap, and thrown rocks. |
| The Duelist | Boss | 6000 | 80 | 30 | - | 650 | 280 | boss | Boss (wave 30): teleports and leaves decoys that burst unless killed. |
| The Duelist | Hidden | 70 | 0 | 30 | - | 0 | 0 |  |  |
| The Lich | Boss | 5000 | 45 | 36 | - | 700 | 300 | boss | Boss (wave 35): immune while its phylactery stands; raises skeletons. |
| Phylactery | Hidden | 600 | 0 | 18 | - | 50 | 0 | heavy | The Lich's soul jar: the Lich can't be hurt while it stands, so break it first. |
| Storm Herald | Boss | 6000 | 55 | 40 | - | 800 | 320 | boss | Boss (wave 40), three phases: a gathering storm (strikes, lightning rods), a gale that pushes you and bends bullets, then a supercell that marks one player at a time. |
| The Devourer | Boss | 6500 | 40 | 50 | - | 900 | 340 | boss | Boss (wave 45), three phases: hungry for morsels (your shots heal it), then swallowing players, then a mouth that heals it from the front. |
| The Eclipse | Boss | 9000 | 30 | 52 | - | 2000 | 500 | boss | Final boss (wave 50), five phases and Totality: a sun and its moon. Hide in the moon's shadow from the flash, stay in the light, and kill it before the nova. |
| Gravity Well | Hidden | 260 | 160 | 15 | - | 40 | 15 |  | The Singularity's corner wells: they pull your shots into themselves. |
| Brute | Melee | 200 | 62 | 19 | 24 | 15 | 14 | melee | Slow, tough melee bruiser that hits hard (24) when it reaches you. |
| Lunger | Melee | 45 | 95 | 12 | 18 | 12 | 12 | melee | Circles in close, flashes a pink line, then leaps along it. |
| Hive Bomber | Hidden | 30 | 330 | 10 | - | 2 | 0 | melee | The Hive Queen's explosive drones: they fly straight at where you were and blow up. |
| Cage Well | Hidden | 40 | 0 | 11 | - | 5 | 2 | melee | The Singularity's small wells: they ring a player in a shrinking cage. |
| Rioter | Hidden | 40 | 110 | 12 | 15 | 3 | 7 | melee | A prisoner in an orange jumpsuit throwing cuffed punches. Some drag a ball and chain and run slower. |
| Hive Egg | Hidden | 60 | 0 | 15 | - | 4 | 3 | melee | The Hive Queen's eggs: break them before they hatch into drones. |
| Guard | Hidden | 90 | 125 | 13 | 14 | 6 | 15 | melee | A corrections officer in khaki with a peaked cap and a nightstick. Up close it swings forehand then backhand: step out of the amber arc. |
| The Dual Reaper | Hidden | 5200 | 150 | 58 | - | 500 | 200 | boss | The Twin Reapers fused into one: corner pillars, holes that can trap it, armored to a third of the damage. |
| Reaper Pillar | Hidden | 120 | 0 | 24 | - | 5 | 4 | melee | The Dual Reaper's corner pillars: each one killed leaves a hole that can trap it. |
| Singularity Shard | Hidden | 1000000 | 0 | 20 | - | 0 | 0 | melee | The Singularity's Split: shards that orbit and fire; damage to them goes to the core. |
| Ball Lightning | Hidden | 70 | 0 | 15 | 20 | 3 | 2 | melee | The Storm Herald's orbs: they drift at you on the wind, arc to each other and pop into 8 shots. |
| Morsel | Hidden | 60 | 0 | 14 | - | 0 | 0 |  | Food the Devourer spits out and crawls over to eat: shoot it or step on it first. |
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
| Small Well | Hidden | 30 | 0 | 11 | - | 1 | 3 | melee | Lobbed by a Graviton: for a few seconds it drags you toward it and bends your shots into it. |
| Implosion | Hidden | 1000000 | 0 | 10 | - | 0 | 0 | melee | What a Collapsar leaves when killed: it pulls players in, then bursts into a ring of shots. |
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
| Cell Door | Hidden | 90 | 0 | 16 | - | 4 | 5 | melee, heavy | A Turnkey's cell door, built into the arena's outer wall: its red beacon spins faster as a Rioter is due, then the gate slides open to let it out. It jams shut when its Turnkey dies. |
| Floor Mine | Hidden | 1 | 0 | 8 | - | 0 | 0 | melee, heavy | A Turnkey's mine: arms, then beeps and blows when you step close. Shoot it to blow it on enemies. |
| Shackle Trap | Hidden | 1 | 0 | 9 | - | 0 | 0 | melee, heavy | A Turnkey's shackle: arms, then snaps shut on the first player over it and holds them a moment. Shoot it to break it. |
