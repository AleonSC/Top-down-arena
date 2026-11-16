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
| Soldier | x1 | x1 | Guns and gear. Full health. |
| Elementalist | x0.85 | x1 | Spells: fire, frost, lightning and arcane magic. 85% health. |
| Summoner | x0.9 | x1 | Fights alongside minions that come with your weapon. 90% health. |
| Knight | x1.15 | x1 | Melee: swings cut small bullets out of the air. Armoured: 115% health. |
| Ninja | x0.85 | x1.12 | Thrown blades and quick melee; tricks and misdirection. Fast (+12% speed), 85% health. |
| Hemomancer | x1 | x1 | Blood spells that drain and heal. Abilities cost health; hits harder the lower your health. |

## Weapons

### Soldier

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Scattergun | Tank | 0.45 s | 7 | 520 x 0.5 s | - | 154 | A short-range spread of pellets. Up close every pellet hits much harder and shoves the enemy back. Point-blank hits load shells: Alt fires them all at once and the kick throws you back. |
| Flak Cannon | Tank | 0.8 s | 30 | 380 x 1.1 s | - | 36 | Shells fly to your cursor and burst in the air there, hurting enemies and destroying enemy bullets caught in the burst. Alt lays a proximity shell that bursts when an enemy steps on it. |
| Riot Repeater | Tank | 0.18 s | 11 | 600 x 0.8 s | - | 60 | Steady single shots. Every hit suppresses the enemy so it shoots slower, building up the more you hit it and fading when you stop. Alt lobs tear gas that suppresses and slows everything in it. |
| Repulsion Cannon | Healer | 0.7 s | 6 | - | 240 | 9 | Blasts enemies away in a cone for light damage. Enemies it hits shed tiny health orbs. Hits fill the Undertow: hold Alt to pull enemies in. |
| Revolver | Healer | 0.45 s | 24 | 700 x 1.1 s | - | 82 | Tap for aimed shots, or hold to fan the hammer and empty the cylinder in a quick spray. The last round in the cylinder is heavy. |
| Mender's Bow | Healer | 0.9 s | 0 | - | - | 40 | Hold to draw, release to fire. A full draw hits hard, pierces enemies and heals allies it passes. Let go right at full draw for a perfect shot. |
| Twin Rifle | Dps | 0.13 s | 4 | 820 x 0.7 s | - | 90 | Two streams from wide-set barrels, angled to cross at your cursor. Hits where they cross are crossfire and do 1.5×. |
| Railgun | Dps | 0.7 s | 45 | 1600 x 0.7 s | - | 101 | Slow, devastating shots that pierce through enemies. The path of each round stays charged for a moment and hurts anything in it. |
| Burst SMG | Dps | 0.06 s | 5 | 760 x 0.8 s | - | 120 | Sprays very fast shots. Keep firing to spin it up: the spray tightens and damage climbs. It winds down fast when you stop. |
| Double Barrel | Dps | 0.32 s | 7 | 900 x 0.5 s | - | 82 | A side-by-side shotgun: 9 pellets a barrel, strongest up close, each pellet passing through 2 enemies. Two shells, then a break-open reload. Alt fires both barrels at once. |
| Twin Rifle Auto (variant) | Dps | 0.13 s | 4 | 820 x 0.5 s | - | 60 | Two rifles held out to your sides, each aiming and firing on its own at the nearest enemy on its side. You can't shoot. Alt locks both onto the enemy nearest your cursor until it dies: their streams meet on it for crossfire (1.5×). |
| Out of Ammo (variant) | Dps | 0.5 s | 12 | - | 80 | 52 | The rifles are out of rounds, so they swat. Each rifle knocks the nearest enemy bullet out of the air (one each, then a half-second recovery). Enemies close in front of you get a five-move chain: left bash, cross cut, right bash, reverse cut, then a rifle clap with a shockwave. Alt spins you for 1.2 s: it cuts everything round you and swats every bullet. |

### Elementalist

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Flamethrower | Tank | 0.06 s | 3 | 480 x 0.4 s | - | 94 | A short-range stream of fire. Each flame hits the first enemy hard and carries on through the rest, setting them all burning. |
| Frost Lance | Tank | 0.4 s | 18 | - | 230 | 65 | A great icicle juts out from you, then splinters off its sides into enemies beside it. Both slow. |
| Storm Hammer | Tank | 0.7 s | 40 | 460 x 1.1 s | - | 60 | Throws a spinning hammer that smashes one enemy and electrifies it, so it arcs lightning to the enemies around it. |
| Radiant Orb | Healer | 0.2 s | 8 | - | 520 | 37 | Hold to cast an orb of light out to your cursor; it stays there while you hold. Drag it through enemies to burn them and over allies to heal them. |
| Chain Light | Healer | 0.5 s | 40 | - | - | 80 | A great cone of light in front of you. Its damage is shared between every enemy in it, so one alone takes it all. |
| Nimbus | Healer | 0.6 s | 16 | - | 380 | 26 | A small storm cloud floats over your cursor. Click to call lightning down beneath it. Rain falls under the cloud and heals every ally standing in it, with no clicking. |
| Fireball | Dps | 0.5 s | 32 | 430 x 1.4 s | - | 71 | A big, slow fireball that rolls on through every enemy in its path, hurting each and setting it burning. |
| Arc Lightning | Dps | 0.25 s | 40 | - | - | 116 | Hold to charge a strip of ground in front of you, longer the longer you hold. Let go: lightning shocks everything in it and stuns it. |
| Ice Shards | Dps | 0.3 s | 7 | 520 x 1 s | - | 70 | Each cast conjures shards of ice that orbit you. They launch at nearby enemies, homing, one after another. With the orbit full, a cast flies at your aim. |

### Summoner

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Golem Staff | Tank | 0.5 s | 40 | - | - | 79 | Hold to heave a boulder, let go to throw it in an arc: the longer you hold, the farther it flies. It crushes everything where it lands. A rune-carved stone Golem (a small Titan) rises beside you, slams enemies and blocks bullets. |
| Bone Guard | Tank | 0.3 s | 7 | 720 x 0.8 s | - | 37 | Bone darts that stick in enemies: enough in one cages it in place (not bosses). Skeleton guards circle you, block bullets, and shoot at caged enemies. |
| Bear Totem | Tank | 0.45 s | 11 | - | 85 | 99 | Two quick claw swipes in front of you that make enemies bleed. A Bear companion mauls nearby enemies. |
| Sprite Staff | Healer | 0.5 s | 7 | 540 x 1 s | - | 14 | A firefly that seeds the enemy it hits, then hops to nearby enemies that aren't seeded yet. Sprites fly to whoever is hurt and heal them, and every seeded enemy you kill frees another sprite for a while. |
| Treant Rod | Healer | 0.55 s | 15 | - | 250 | 34 | A thorny root whips out along your aim and snaps back, hitting everything on the line and healing allies it touches. Your Treant roots where it stands and blocks bullets; a root runs from you to it, healing allies who cross it and slowing enemies. |
| Spirit Chalk | Healer | 0.1 s | 26 | - | 450 | 26 | Hold and drag to draw a line of spirit ink. It burns and slows enemies touching it, heals allies standing on it and destroys enemy bullets that cross it. Draw a circle round enemies to trap them inside (not bosses). |
| Wolf Whistle | Dps | 0.3 s | 6.5 | 720 x 0.8 s | - | 150 | Darts mark the enemy they hit as prey, and your wolves hunt it. When every wolf bites it close together, they pull off a takedown that hurts and stuns. |
| Wisp Lantern | Dps | 0.25 s | 22 | - | - | 84 | Wisps orbit you, and they are your shots: each time you fire, one flies at the enemy nearest your cursor, strikes it and comes back. |
| Imp Caller | Dps | 0.35 s | 0 | - | - | 77 | Imps arrive over time and wait by you. Tap fire to send one charging at your cursor to explode; hold to merge them all into one big imp whose blast is bigger than theirs put together. |

### Knight

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Warhammer | Tank | 0.75 s | 80 | - | 130 | 64 | Hold to charge, let go to slam the ground in front of you. The longer the charge, the harder it hits and the longer it stuns. |
| Lance | Tank | 0.24 s | 14 | - | 178 | 60 | A four-step chain with a lance and shield: two quick jabs, a wide sweep that bats enemies aside, then a Breaching Thrust that shoves everything in front far back and stuns it. Shoved enemies that hit another enemy or a wall take Impact damage. The weapon-action key hooks the enemy nearest your cursor, hauls it in and makes your next attack the Breaching Thrust. |
| Halberd | Tank | 0.6 s | 30 | - | 200 | 48 | Sweeps a long, thin blade across in front of you, hitting each enemy once per sweep. |
| Quarterstaff | Healer | 0.5 s | 18 | - | 120 | 36 | Left and right staff swipes that knock enemies back and bat enemy bullets back at them. Every enemy hit heals you and nearby allies and stores a Jab stack (up to 3); the weapon-action key spends one on a cone jab. |
| Lightbringer | Healer | 0.5 s | 17 | - | 80 | 58 | A three-step chain. Dawn Cut's crescent heals the allies it passes, Mercy Cut's wards them, and Noon Sunburst plants the sword in a ring that heals everyone near you. Hits fill a light gauge: full, the next Noon is Radiant. The weapon-action key spends the light on a Sunlance that heals the ally nearest your cursor. |
| Censer | Healer | 0.26 s | 13 | - | 85 | 35 | A censer on a chain, swung in a three-step rite: a sweep, a return sweep and a Benediction that goes all the way round. Every step heals allies near you and leaves incense that heals allies in it and makes enemy shots fired from inside it stray. Each enemy hit lights an Ember; the weapon-action key throws the censer to your cursor, spending them on a burst and a big healing cloud. |
| Twin Blades | Dps | 0.18 s | 16 | - | 90 | 98 | Two sabres in a five-step chain: rising cut, falling cut, double jab, pirouette, and a crossing shear that throws a crescent wave. Each step carries you forward. The weapon-action key hurls both sabres to cross at your cursor and fly back. |
| Greatsword | Dps | 0.7 s | 40 | - | 125 | 89 | A three-hit chain: two quick cuts, then a slow overhead rend. The edge of each sweep hangs in the air as a crescent of steel that stops enemy bullets. Hold the weapon-action key to guard: the arc stops bullets and builds Temper, and letting go fires a beam that hits harder for every point of Temper. |
| Rapier | Dps | 0.48 s | 18 | - | 130 | 85 | Precise thrusts at the nearest enemy in front, and they skewer enemy bullets along their line. The first thrust is slow, but every enemy you hit adds Tempo, and Tempo makes the thrusts quicker. Stop hitting and it holds a moment, then drains. The weapon-action key spends it on a Flourish: the blade whips back and forth, each swing throwing a crescent wave. |
| Arc Knuckles | Dps | 0.24 s | 22 | - | 80 | 88 | Coiled blue fists in a five-punch chain: jab, cross, a hook that sweeps a crowd, an uppercut that launches, and the Twin Hammer, a ground slam whose shockwave stuns. Raising the parry cancels a punch and starts the chain over; a perfect parry arms the gold Counter Hammer for your next press. The weapon-action key is a Rocket Dash; land it and press Fire as each move lands to chain a Flurry, a Grab & Throw and a Rocket Punch. |

### Ninja

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Kusarigama | Tank | 0.55 s | 26 | - | 280 | 37 | Cut with the sickle, then throw the weight to drag the first enemy in its lane to you. Hold the weapon-action key to whirl the weight around you, pulling everything near into the chain. |
| Bo Staff | Tank | 0.47 s | 16 | - | 105 | 48 | Click for a never-ending chain of three swings: a cone slash, a jab, then a full spin that starts it over. Every swing cuts small bullets. The weapon-action key shoves a wall of staff ahead of you and restarts the chain. |
| Fuma Shuriken | Tank | 0.7 s | 30 | - | - | 6 | A giant spinning star thrown in a wide loop out in front of you and back, cutting through everything both ways. |
| Medicine Darts | Healer | 0.18 s | 7 | 900 x 0.8 s | - | 50 | Fast needles that dose what they hit. Enemies get poisoned and hurt allies get healed, and doses stack. |
| Spirit Fan | Healer | 0.4 s | 6 | 420 x 0.3 s | - | 75 | A short gust that heals allies and pushes small enemy bullets back. Shots alternate between blowing enemies away and drawing them in. |
| Ofuda | Healer | 0.5 s | 25 | 360 x 1.6 s | - | 50 | Paper charms fly to your cursor and lie there as wards. An enemy that steps on one sets it off, hurting enemies around it and healing allies near it. |
| Shuriken | Dps | 0.35 s | 9 | 700 x 0.75 s | - | 140 | Tight fans of spinning stars that stick in what they hit. After a while each rips out for another hit and flies back to you, cutting through what it passes. Your weapon-action key switches to the Great Star stance, pulling stuck stars back: hold fire to draw a route, let go to throw one huge star along it and home again, cutting everything both ways. |
| Katana | Dps | 0.3 s | 20 | - | 120 | 65 | Two stance combos; your weapon-action key switches between them, and the one you leave keeps its place. Hayate dashes and slices and ends in a Momentum Burst that shoves enemies away. Senbon cuts, spins and slashes and ends in a storm of caltrops. The burst flings caltrops on the ground; caltrops thrown after a burst seek what it hit. |
| Kunai | Dps | 0.7 s | 42 | 1400 x 0.5 s | - | 88 | Slow, heavy throwing knives that hit harder against enemies not targeting you. Every throw leaves a mark where it lands: press your weapon-action key to blink there, and your next hit lands double. |

### Hemomancer

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Blood Lash | Tank | 0.5 s | 24 | - | 130 | 96 | A whip of blood. Its tip cracks for double damage and taunts, and enemies hit closer in are flung out to the tip. Each hit clots your blood (+1 Clot, +2 from the tip; 4% less damage taken each, up to 5). At full Clot your next lash bursts all round you. |
| Gore Mortar | Tank | 0.75 s | 34 | - | - | 133 | Hold to charge, let go to lob a massive blood clot over enemies; the longer you hold, the farther it flies, and it hits just as hard at any range. Each shot costs 6% of your max health. It bursts into a big pool of blood that slows enemies and slowly heals anyone standing in it, and after a moment the pool congeals and holds enemies still. |
| Leech Tendrils | Tank | 0.1 s | 1.2 | - | 160 | 31 | Hold to lash out blood tendrils that grasp nearby enemies while you hold on. They are stunned and drained, and part of the damage heals you. |
| Transfusion Darts | Healer | 0.2 s | 7 | 850 x 0.85 s | - | 32 | Darts that transfuse your blood: one that reaches a hurt ally gives them more health than it costs you. Each enemy hit steals a sliver back. |
| Sanguine Orb | Healer | 0.45 s | 16 | 420 x 1.4 s | - | 57 | Orbs of blood that home in at the last moment, circle an enemy and cover it, making it bleed and charging your heal reserve. With 25 reserve, an orb that touches a hurt ally coats them in healing blood instead (30 health over 4 s). |
| Vein Tether | Healer | 0.08 s | 4 | - | 350 | 48 | A draining beam that hurts whatever you hold it on. Part of its damage heals, split between you and the nearest ally. |
| Blood Bolts | Dps | 0.13 s | 9 | 800 x 0.8 s | - | 121 | Fast bolts of blood that make enemies bleed. A bolt that fills an enemy's bleed bursts it, hurting it and the enemies around it. |
| Implosion | Dps | 0.45 s | 34 | - | 480 | 75 | Click an enemy: its blood collapses inward, hurting enemies pressed against it and pulling them in. |
| Bone Spear | Dps | 0.8 s | 60 | 1000 x 0.9 s | - | 79 | A heavy spear that pierces enemies. Each throw costs a little health, but the spear stays stuck where it stops, and walking over it gives the health back. |

## Abilities

### Soldier

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Barrier | Tank | Space 1 | 12 s | - | Bullets that hit the barrier are reflected back as your own. | A bubble that stops incoming enemy bullets. Bosses can break it (it soaks 360 of their damage). |
| Roar | Tank | Space 2 | 14 s | - | Also knocks enemies within 250 px back and slows them for 2 s. | Every enemy targets you for 5 s, and you take 40% less damage meanwhile. |
| Bulldoze | Tank | Space 3 | 9 s | - | Erases enemy bullets along the charge. | Charge forward, damaging and knocking back everything in your path. |
| Fortress | Tank | ultimate | 70 s | - | Lasts 14 s. | Raises a fixed, impenetrable dome here for 10 s. Only players and player bullets cross it: enemies and their shots can't get in or shoot through. |
| Juggernaut Suit | Tank | ultimate | 75 s | - | 450 armour, and each shard does 18. | Steel plates clamp on: for 10 s you carry 300 armour that takes damage before your health. You move 25% slower and every enemy, bosses too, comes for you. Each enemy bullet the armour takes throws a 12-damage flak shard straight back at whoever fired it. It ends early if the armour is gone or you go down. |
| Shockwave | Tank | card | 14 s | - | The wave runs out 300 px. | A shockwave runs out 230 px from you: it erases enemy bullets, hits enemies for 60 and shoves them back 120 px. |
| Overdrive | Tank | card | 18 s | - | +1 extra projectile while it lasts. | Double fire rate for 5 seconds. |
| Blink | Tank | card | 7 s | - | Erases enemy bullets where you leave and where you land. | Teleport toward your cursor. |
| Nova | Tank | card | 9 s | - | Fires 48 shots instead of 24. | Fire a ring of 24 heavy shots. |
| Turret | Tank | card | 20 s | - | Deploys two turrets. | Deploy a turret that shoots for 10 seconds. |
| Stun Gun | Tank | card | 14 s | - | Nine shots instead of six. | Draw a stun gun: your next 6 shots are stun bolts (15 damage) that stun enemies for 1.5 s (bosses are only slowed). The cooldown starts once all 6 are fired. |
| Royal Dive | Tank | card | 12 s | - | A wider honey pool (150 px) that lasts 6 s. | Leap (nothing can hit you) and crash down at your cursor, up to 380 px away: 120 damage to everything within 110 px, enemies knocked back and enemy bullets there wiped out. The landing leaves a honey pool for 4 s that slows enemies 40% and heals allies 6 a second. |
| Scythe Throw | Tank | card | 9 s | - | Catching it takes 60% off, and it cuts 50% harder. | Throw a giant scythe at your cursor (up to 380 px). It cuts everything it passes, 45 on the way out and 45 on the way back, and slices small enemy bullets. Catch it as it comes back to take 40% off the cooldown. |
| Mend Pulse | Healer | Space 1 | 10 s | - | Also erases enemy bullets within its range. | Heal everyone nearby and revive downed allies. |
| Adrenaline Shot | Healer | Space 2 | 14 s | - | Also gives the nearest other ally within 250 px a half-length (4 s) dose. | Inject the player closest to your cursor (you, if alone): 8 s of +30% move speed, +40% fire rate and 30% less damage taken. |
| Deployable Medkit | Healer | Space 3 | 14 s | - | Bursts into 9 orbs worth 20 health each. | Throw a medkit at your cursor: it bursts into 6 healing orbs (15 health each) that anyone can pick up for 6 s. |
| Lifeline | Healer | ultimate | 90 s | - | Everyone also gets 4 s of Overdrive. | Instantly revive and fully heal the whole party, with 3 s of invulnerability. |
| Poison Dart | Dps | Space 1 | 8 s | - | Spreads to 5 enemies at a time, at full strength. | Dart the enemy nearest your cursor: 20 damage, then poison for 18 a second over 5 s. If it dies poisoned, the poison spreads to the 3 nearest enemies (80% as strong), and on from them. |
| Airstrike | Dps | Space 2 | 16 s | - | A second wall lands 160 px further out, half a second later. | Throw a flare at your cursor: a jet carpets a line across the arena through it, across your aim (55 a bomb), and the line burns for 3 s. |
| Mark for Death | Dps | Space 3 | 12 s | - | Marks the three enemies nearest your cursor. | Mark the enemy nearest your cursor: it takes 50% more damage for 6 s. |
| Artillery Assault | Dps | ultimate | 60 s | - | +4 extra projectiles instead of +2. | For 6 s you fire three times as fast, with +2 extra projectiles. |

### Elementalist

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Aeroaegis | Tank | Space 1 | 11 s | - | Lasts 4.5 s and reflects bullets for double damage. | For 3 s, a wind vortex wraps the player your cursor is nearest: enemies can't get in, and bullets that touch it are reflected back as your own. |
| Frost Nova | Tank | Space 2 | 12 s | - | Also erases enemy bullets around you. | Freeze every enemy near you for 3 s and deal 40 damage. |
| Flame Wall | Tank | Space 3 | 14 s | - | The fire also slows enemies by 40%. | A wall of fire at your cursor for 5 s: burns enemies and blocks bullets. |
| Glacial Tomb | Tank | ultimate | 75 s | - | Enemies stay frozen for 6 s and bosses take 500. | Erase every enemy bullet and freeze every enemy solid for 4 s (bosses take 250 damage instead). |
| Lightning Rod | Tank | ultimate | 70 s | - | Lasts 8 s and each bolt arcs to 3 enemies. | For 6 s you crackle with charge: you take 80% less damage and every enemy, bosses too, goes for you. Each enemy hit on you calls a bolt from the sky on whoever landed it (30), which arcs on to the 2 enemies nearest it (15 each). Wet enemies take extra arcs. |
| Healing Rain | Healer | Space 1 | 16 s | - | Lasts 10 s, and downed allies inside revive twice as fast. | A rain at your cursor for 6 s: heals allies inside 8 HP/s and slows enemies by 30%. |
| Energy Ward | Healer | Space 2 | 14 s | - | The shield soaks 120 damage. | Wrap you and allies within 300 px in a bubble of energy that soaks 60 damage for 8 s. It shrinks and turns green as it wears; when it breaks or runs out, half of what it soaked comes back as healing. |
| Lumen Ward | Healer | Space 3 | 20 s | - | Also freezes enemies outside the light for as long as it lasts (not bosses). | A huge cone of light along your aim for 6 s: heals everyone inside 9 HP/s and burns away enemy bullets in it. Enemy bullets everywhere else slow to a stop and fly again when it ends. |
| Sanctum | Healer | ultimate | 80 s | - | The dome is half again as wide (300 px). | A holy dome for 7 s: bullets can't get in, allies inside can't go down, and a healing pulse rolls out from you every 0.9 s (the last is bigger). |
| Meteor | Dps | Space 1 | 10 s | - | Leaves burning ground for 3 s. | Call a meteor at your cursor: after a short warning it hits for 150 damage. |
| Blizzard | Dps | Space 2 | 14 s | - | Lasts 10 s. | A storm at your cursor for 5 s that slows and damages enemies. |
| Tsunami | Dps | Space 3 | 16 s | - | Wet lasts 14 s, and the wave rolls back to you, wetting what it missed. | A wave rolls out where you aim: 12 damage, and it leaves enemies wet for 8 s, slowed 30%. Lightning (Arc Lightning, Storm Hammer, Chain Light, Nimbus) arcs to every wet enemy on the map. |
| Bullet Rain | Dps | ultimate | 70 s | - | Lasts 9 s. | Call a downpour at your cursor for 6 s and harden the drops into needles: 45 damage a second to every enemy in it, and leaves them wet. |

### Summoner

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Golem Wall | Tank | Space 1 | 13 s | - | Raises five golems. | Raise three basalt golems in front of you. They block every enemy bullet and stay until they are broken (200 health each); casting again replaces them. |
| Earthquake | Tank | Space 2 | 10 s | - | Also freezes everything it hurts for 1 s (not bosses). | You slam the ground (25 damage, slows), then a Burrower breaks out at your cursor and tunnels through the enemies for 3 s, hurting each one it passes under (30 damage). |
| Force Pact | Tank | Space 3 | 12 s | - | A second thrall: the pact keeps the last two types you chose. | Take the regular enemy nearest your cursor (not bosses) and make it fight for you. It stays near you with its own attack and comes back 15 s after it falls. Cast again on another enemy to switch the pact to that type. |
| Titan | Tank | ultimate | 80 s | - | The titan is 30% bigger and stays for 27 s. | Summon a huge stone titan for 17 s. It rises from rubble, stomps after enemies, slams them after a short wind-up and blocks bullets with its whole body (it soaks 1000 boss damage). |
| Bone Phalanx | Tank | ultimate | 75 s | - | 10 skeletons with 180 health each. | A bone gate opens ahead of you and 8 skeletons with tower shields march a line 260 px along your aim over 6 s. Their shields stop every enemy shot from the front (red aimed shots too) while your team shoots through; enemies are shoved ahead and speared for 15 every 0.5 s. Each skeleton has 120 health; a broken one leaves a gap. Bosses and walls stop the line. |
| Spirit Swarm | Healer | Space 1 | 14 s | - | Releases 8 sprites. | Release 5 healing sprites for 6 s. |
| Phoenix | Healer | Space 2 | 22 s | - | Revives at full health, and the phoenix keeps fighting. | A phoenix fights for 10 s. If anyone goes down (you too), it revives them instantly. |
| Soul Lantern | Healer | Space 3 | 18 s | - | Lasts 9 s, and the linked player can't go down. | A lantern appears beside the ally nearest your cursor (you, alone) and links to them for 6 s: it follows, heals 8 health a second and flames nearby enemies. |
| World Tree | Healer | ultimate | 80 s | - | Lasts 16 s with 8 sprites. | Grow a great tree for 15 s that heals and slows around it and sends a wave of healing butterflies to every ally every 1.9 s. |
| Bat Swarm | Dps | Space 1 | 12 s | - | Releases 14 bats. | Release 8 bats that hunt enemies for 6 s. |
| Hydra | Dps | Space 2 | 18 s | - | A fourth head. | A three-headed hydra rises at your cursor for 8 s. Each head picks its own enemy and spits venom at it (10 damage, every 0.6 s per head). |
| Alpha Call | Dps | Space 3 | 14 s | - | Also summons 3 wolves for 6 s. | Blood Scent for 6 s: your minions start at 2 stacks, and each bite adds one (up to 5): faster, and quicker bites. At 5 stacks the next bite lunges for triple damage and drops back to 2. |
| Legion | Dps | ultimate | 70 s | - | Summons 7 wolves and 7 bats. | A blood moon rises. Summon 4 wolves and 4 bats that circle you, then run 40% faster for 10 s; your other minions frenzy. |

### Knight

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Shield Wall | Tank | Space 1 | 12 s | - | Bullets that hit the shield are reflected back as your own. | Raise a wall of 7 steel plates: it stops every enemy bullet coming at your front (120°), even red aimed shots. Every shot wears down the plate it hits (400 damage in all, shared between the plates) and a broken plate leaves a gap. The wall stays up until every plate is gone. |
| Leap Slam | Tank | Space 2 | 9 s | - | The landing also erases enemy bullets within 160 px. | Leap to your cursor (up to 320 px), untouchable in the air. Landing deals 50 damage around you and stuns enemies for 1 s. |
| Cavalry Call | Tank | Space 3 | 18 s | - | Two more lancers ride outside the first pair, and the guard lasts 5 s. | Two lancers ride in at your sides and charge across the map along your aim. Each enemy they meet takes 35, is shoved out of the lane and slowed 45% for 3 s (Heavy enemies and bosses barely move and are slowed less). You take 20% less damage for 3 s. |
| Last Stand | Tank | ultimate | 70 s | - | You take 5% of the damage instead of 10%. | You plant yourself and brace a wall of steel plates 120 px out, 178° wide, facing your cursor (it turns slowly). It stops every enemy bullet from the front; your teammates' shots pass out through it. You take 10% of the damage it stops. You can't move while it stands, and it has no timer: it stays until you go down or press the ability again, and the cooldown starts when it comes down. |
| Consecrate | Healer | Space 1 | 14 s | - | Two more crosses fly out at 22 degrees either side (each target is hit once). | Throw a Holy Cross along your aim. It grows as it flies: allies it crosses heal 35 and run 30% faster for 4 s; enemies take 25, are slowed 40% for 3 s and are knocked back. |
| Guardian's Oath | Healer | Space 2 | 15 s | - | Charges up to 800 px. The ward is wider, follows you, and stands a downed ally back up at 25% health. | Charge to the player nearest your cursor (up to 600 px), downed or not, and drag them clear. A Ward of Light then cuts damage by half for everyone inside for 5 s, and downed allies in it revive twice as fast. |
| Lay on Hands | Healer | Space 3 | 18 s | - | Also heals everyone else near them for half as much. | Heal the most hurt ally within 300 px (or yourself) for 70, or instantly revive them at half health if they're down. |
| Hallowed Blade | Healer | ultimate | 80 s | - | Three swords fall: one at your cursor and one either side. | A giant holy sword falls at your cursor: 200 damage around it, then it stays planted for 8 s (allies in the ring heal 15 HP/s, enemies burn), and every 2 s a pulse crosses the whole arena healing allies and hurting enemies for a third as much. |
| Bladed Dive | Dps | Space 1 | 10 s | - | Each kill heals you 5% of your health. | Rush toward your cursor (up to 240 px), untouchable, your blade spinning round you: four cuts land along the way, each hitting every enemy within 64 px of you for 18. Kill anything with it and the cooldown resets. |
| The Gauntlet | Dps | Space 2 | 9 s | - | The Challenge lasts 8 s, and the charge's impact stuns for 1 s. | Throw down the gauntlet at the enemy nearest your cursor along your aim (up to 380 px; it flies over anything in between): it takes 12 and is Challenged for 5 s. You shoulder-charge to it, untouchable, bowling everything in the way aside (18 and a shove), and hit it for 30. Kill the Challenged enemy before the Challenge runs out and the gauntlet flies back: the cooldown resets. |
| Caught Steel | Dps | Space 3 | 16 s | - | Holds up to 18 shards. | For 6 s, enemy bullets that skim past you without touching you are caught and hang round you as steel (up to 12). Press again (or let the time run out) and a sweeping cut hurls them out along your aim: each shard cuts the bullets in its way and hits the first enemy for 22. A hit knocks a third of them loose. The cooldown starts when you throw them. |
| Drawbridge | Dps | ultimate | 75 s | - | The bridge stands for 8 s. | A gatehouse rises behind you and its drawbridge falls along your aim: 500 px long and 100 wide, it crushes everything in the lane for 260 and throws enemies at its edges aside (45). Then it lies there for 5 s as a walkway: enemies can't step onto it and the rails grind any pressed against them (80 a second), and their shots stop at its rails, red aimed shots too. |

### Ninja

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Shadow Clone | Tank | Space 1 | 14 s | - | Every clone has double the health. | Summon a wall of 9 clones for 7 s. Every enemy targets them and each soaks 50 damage. They never fight: walk into them to shove the wall. |
| Smoke Bomb | Tank | Space 2 | 14 s | - | Enemies inside the smoke are also slowed by 40%. | A 140 px cloud at your feet for 5 s: enemy bullets inside slow to half speed, and allies inside take 30% less damage. |
| Iron Body | Tank | Space 3 | 12 s | - | The shockwave also erases enemy bullets within 160 px. | For 2 s you take 80% less damage, then a shockwave hits everything within 140 px for 50. |
| Thousand Clones | Tank | ultimate | 70 s | - | Twenty clones. | Twelve clones appear all over the map. Every enemy goes for the nearest one at once. They soak 100 damage each and last until they die or the round ends; each bursts for 60 when killed. |
| Healing Dash | Healer | Space 1 | 15 s | - | The sprint lasts 4.5 s. | Sprint 70% faster for 3 s, leaving glowing footprints that last 6 s. You and your allies heal 12 HP a second while standing near one (overlapping footprints don't stack). |
| Substitution | Healer | Space 2 | 12 s | - | You and your ally are both invulnerable for 1 s after the swap. | Swap places with the ally nearest your cursor and shield them for 60 for 6 s. Alone: slip to your cursor and shield yourself. |
| Spirit Ward | Healer | Space 3 | 18 s | - | The ward follows you. | Plant a talisman at your cursor for 8 s: it takes the damage allies near it would take (up to 150). When it breaks or ends, a quarter of what it took heals allies near it. |
| Blossom Storm | Healer | ultimate | 80 s | - | Lasts 8 s. | For 5 s petals heal the whole party 20 HP/s and erase enemy bullets near every ally. |
| Shadow Step | Dps | Space 1 | 7 s | - | +1 charge. | Teleport behind the enemy nearest your cursor (up to 450 px away) and strike it for 80. |
| Vanish | Dps | Space 2 | 16 s | - | Erases enemy bullets around you when you vanish. | For 3 s enemies lose track of you. Your next hit (within 4 s) deals double damage. |
| Blade Fan | Dps | Space 3 | 12 s | - | Throws twice as many kunai. | 12 kunai spin around you for 4 s, cutting enemies they touch for 18 (once every 0.5 s each) and erasing enemy bullets. Press again, or wait, and they fly out in a ring, 18 each. |
| Death Lotus | Dps | ultimate | 65 s | - | Strikes up to 14 enemies. | Strike up to 8 enemies near your cursor in a flurry of cuts, 100 damage each. |

### Hemomancer

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Blood Shell | Tank | Space 1 | 9 s | - | The burst also erases enemy bullets within 160 px. | Hold to wrap yourself in a big bubble of blood. It blocks bullets and enemies; allies can stand inside. Each bullet it stops costs you 7 health and heals every ally inside for 4. It bursts at a third of your health, or when you let go. |
| Blood Wall | Tank | Space 2 | 8 s | - | Enemies against the wall take double and are slowed 40%. | Toggle: a wall of blood is laid behind you as you walk. It stops enemy bullets and enemies can't cross it (they bleed 12 a second against it); allies walk through. It stands 6 s. It drains 1% of your max health a second, doubling every 4 s, until you press again or go down. |
| Exsanguinate | Tank | Space 3 | 8 s | 10% of max health | Also slows the enemies it drains by 50% for 2 s. | Pay 10% of your max health: barbed blood whips hook every enemy within 220 px and yank them in to you, stunned on the way. Each drains 20 health into you as it lands. Heavy enemies and bosses stay put but are still drained. |
| Vein Tithe | Tank | ultimate | 75 s | - | Lasts 40 s and costs 25% less. | Blood tendrils bind every enemy on screen for 30 s: they can't move or shoot. You pay 0.25% of your max health a second for each one held (a boss costs four times that), and deal 40% less damage. It ends when the time runs out or you go down. |
| Bloodbath | Tank | ultimate | 75 s | - | The pool spreads to 220 px and gives back 35% of the bleed. | Channel: blood floods the floor 170 px round you (it stays where you cast it) for up to 10 s, draining 4% of your max health a second. Enemies wading in it are slowed 50% (Heavy enemies 35%, bosses 20%) and bleed 15 a second, and every drop heals you for 25% of it, wherever you are. Allies standing in it, you included, take 20% less damage. Press again to stop; it also stops at 15% health or if you go down. |
| Blood Gift | Healer | Space 1 | 8 s | 25% of current health | Also heals everyone else within 200 px of them for half as much. | Pay 25% of your current health: the most hurt ally heals twice as much. Alone, it becomes a shield for you. |
| Blood Freeze | Healer | Space 2 | 14 s | 25% of max health | Frozen enemies take 25% more damage. | Pay 25% of your max health: a shockwave bursts out 260 px around you and freezes every enemy it reaches for 5 s (bosses for 3 s). Frozen enemies can't move or shoot. |
| Martyr | Healer | Space 3 | 16 s | 40% of max health | You're invulnerable for 2 s after the sacrifice. | Pay 40% of your max health to revive the downed ally nearest your cursor, anywhere, at 50% health. If no one is down, the most hurt ally (you, if you're alone) is warded for 12 s: if they go down, they get straight back up at 50%. |
| Sanguine Rain | Healer | ultimate | 80 s | - | Lasts 14 s and heals 4% a tick. | Draw 12% of your health into a blood orb (1.8 s), then rain circles follow your allies for 10 s, trickling 3% of their health every 0.5 s. It drains you 3% a second and ends when you press it again or fall to 15%. |
| Arterial Spray | Dps | Space 1 | 8 s | - | The spray also washes away enemy bullets it touches. | Press to open a vein: a 60° jet of blood, 200 px long, follows your aim, 30 damage a second to everything in it (more the lower your health), slowing them 20% and making them bleed. It drains 4% of your max health a second. Press again to stop; it sputters out at 15% health. The cooldown starts when it stops. |
| Blood Debt | Dps | Space 2 | 16 s | - | Kills repay 50% more. | Borrow against your blood: your health drops to 25% (or 5% of your max, if you're already that low) and the rest becomes a debt. For 8 s, kills pay it back (a Grunt 4% of your max health, a Heavy or an elite 10%, hurting a boss 10% of the damage) and your hits heal 15% instead of 3%. Pay it all and you keep the 15% for 2 s more; what's unpaid after 8 s is lost. |
| Vein Rush | Dps | Space 3 | 6 s | 10% of max health | The trail bursts after 1 s for 20 to everything on it. | Pay 10% of your max health and rush 280 px toward your cursor as a streak of blood, untouchable. Every enemy you pass through takes 25 and bleeds, and gives back 2% of your max health. You leave a trail of blood that slows enemies 30% for 2 s. |
| Crimson Harvest | Dps | ultimate | 65 s | - | Hold 8 s, and 25% more blood. | Rip blood from every enemy in the arena (20 damage each) into a floating ball, bigger the more enemies fed it. Press again to throw it at your cursor for a huge blast; it throws itself after 5 s. |

## Enemies

| Enemy | Group | Health | Speed | Radius | Contact | Score | XP | Notes | What it does |
|---|---|---|---|---|---|---|---|---|---|
| Grunt | Ranged | 60 | 85 | 14 | - | 10 | 10 |  | Fires aimed 3-shot spreads. |
| Burster | Ranged | 110 | 55 | 17 | - | 20 | 18 |  | Fires rings of bullets; more in each ring on later waves. |
| Sniper | Ranged | 50 | 95 | 12 | - | 15 | 14 |  | Fast, accurate shots from far away. |
| Spinner | Ranged | 150 | 35 | 18 | - | 25 | 22 |  | Wanders and sprays spirals in bursts; it spins up while bursting. |
| The Warden | Boss | 2400 | 45 | 46 | - | 300 | 150 | boss | Boss (wave 5): a prison riot in three stages — searchlight, gates and gas, then rioters. |
| Charger | Ranged | 90 | 70 | 15 | - | 20 | 18 |  | Flashes a red line, then dashes along it and bursts into a ring. |
| Splitter | Ranged | 120 | 50 | 19 | - | 20 | 16 |  | Splits into 3 fast Shards when killed. |
| Shard | Ranged | 25 | 130 | 8 | - | 5 | 4 |  | Tiny and fast. Comes from Splitters, Nests and the Hive Queen. |
| Mortar | Ranged | 80 | 40 | 16 | - | 20 | 18 |  | Lobs bombs at where you stand; they burst into a ring on landing. |
| Shielder | Ranged | 140 | 60 | 17 | - | 25 | 22 |  | A front shield blocks shots and swings. It turns slowly: flank it. |
| Medic | Ranged | 70 | 75 | 13 | - | 25 | 20 |  | Heals nearby enemies every few seconds. Kill it first. |
| Hive Queen | Boss | 3000 | 40 | 44 | - | 400 | 180 | boss | Boss (wave 10): a hive in three stages — drones and bombers, sealing cells and honey, then a frenzied swarm. |
| Reaper of Dusk | Boss | 2600 | 95 | 34 | - | 250 | 100 | boss | Boss pair (wave 15): a tether sweep, then they fuse into the Dual Reaper (pillars, holes), then split for a finale. |
| Reaper of Dawn | Hidden | 2600 | 95 | 34 | - | 250 | 100 | boss |  |
| The Singularity | Boss | 3600 | 0 | 40 | - | 500 | 220 | boss | Boss (wave 20): corner gravity wells, pull, lasers and shrinking cages. |
| Phantom | Late | 90 | 90 | 13 | - | 25 | 22 |  | Flickers, blinks next to you and bursts into a ring. |
| Hexer | Late | 100 | 60 | 14 | - | 25 | 22 |  | Fires curving pincer shots that close in from both sides. |
| Juggernaut | Late | 420 | 32 | 26 | - | 60 | 50 | heavy | Armored and slow, can't be slowed or knocked back. Rotating stars of orbs. |
| Nest | Late | 260 | 0 | 22 | - | 40 | 35 | heavy | Stationary spawner that keeps hatching Shards until destroyed. |
| The Colossus | Boss | 6800 | 35 | 54 | - | 600 | 260 | boss | Boss (wave 25): huge stomp rings with one gap, and thrown rocks. |
| The Duelist | Boss | 6000 | 80 | 30 | - | 650 | 280 | boss | Boss (wave 30): teleports and leaves decoys that burst unless killed. |
| The Duelist | Hidden | 70 | 0 | 30 | - | 0 | 0 |  |  |
| The Lich | Boss | 5000 | 45 | 36 | - | 700 | 300 | boss | Boss (wave 35): immune while its phylactery stands; raises skeletons. |
| Phylactery | Hidden | 600 | 0 | 18 | - | 50 | 0 | heavy |  |
| Storm Herald | Boss | 6000 | 55 | 40 | - | 800 | 320 | boss | Boss (wave 40), three stages: a gathering storm (strikes, lightning rods), a gale that pushes you and bends bullets, then a supercell that marks one player at a time. |
| The Devourer | Boss | 6500 | 40 | 50 | - | 900 | 340 | boss | Boss (wave 45), three stages: hungry for morsels (your shots heal it), then swallowing players, then a mouth that heals it from the front. |
| The Eclipse | Boss | 9000 | 30 | 52 | - | 2000 | 500 | boss | Final boss (wave 50), five stages and Totality: a sun and its moon. Hide in the moon's shadow from the flash, stay in the light, and kill it before the nova. |
| Gravity Well | Hidden | 260 | 160 | 15 | - | 40 | 15 |  |  |
| Spiker | Melee | 22 | 135 | 9 | 10 | 5 | 5 | melee | Small and fast melee rusher with long thin spikes. It zig-zags toward you in pairs. |
| Brute | Melee | 200 | 62 | 19 | 24 | 15 | 14 | melee | Slow, tough melee bruiser that hits hard (24) when it reaches you. |
| Lunger | Melee | 45 | 95 | 12 | 18 | 12 | 12 | melee | Circles in close, flashes a pink line, then leaps along it. |
| Hive Bomber | Hidden | 30 | 330 | 10 | - | 2 | 0 | melee |  |
| Cage Well | Hidden | 40 | 0 | 11 | - | 5 | 2 | melee |  |
| Rioter | Hidden | 40 | 110 | 12 | 10 | 3 | 7 | melee | A prisoner in an orange jumpsuit throwing cuffed punches. Some drag a ball and chain and run slower. |
| Hive Egg | Hidden | 60 | 0 | 15 | - | 4 | 3 | melee |  |
| Guard | Hidden | 90 | 125 | 13 | 14 | 6 | 15 | melee | A corrections officer in khaki with a peaked cap and a nightstick. Up close it swings forehand then backhand: step out of the amber arc. |
| The Dual Reaper | Hidden | 5200 | 150 | 58 | - | 500 | 200 | boss |  |
| Reaper Pillar | Hidden | 120 | 0 | 24 | - | 5 | 4 | melee |  |
| Singularity Shard | Hidden | 1000000 | 0 | 20 | - | 0 | 0 | melee |  |
| Ball Lightning | Hidden | 70 | 0 | 15 | 20 | 3 | 2 | melee |  |
| Morsel | Hidden | 60 | 0 | 14 | - | 0 | 0 |  |  |
| Beam Sentry | Late | 160 | 70 | 17 | - | 30 | 12 |  | Plants itself, aims a line at you, then sweeps a beam across your side. |
| Scorcher | Late | 130 | 95 | 15 | - | 30 | 12 |  | Circles you leaving a trail of embers. Cut across before it closes. |
| Reaver | Late | 240 | 150 | 19 | 20 | 40 | 16 | melee, heavy | Armoured melee: a red cone, then a rake. Open for a moment after: +50% damage. |
| Warder | Late | 180 | 65 | 16 | - | 40 | 16 |  | Puts bubbles on up to three allies. Kill it and they all pop. |
| Pauser | Late | 100 | 60 | 15 | - | 25 | 12 |  | Fans out shots that stop dead, glow, then all rush at where you are. Keep moving. |
| Leader | Late | 90 | 80 | 13 | - | 25 | 12 |  | Aims where you're going, not where you are. Change direction when it fires. |
| Stitcher | Late | 120 | 65 | 15 | - | 30 | 14 |  | Fires slow orbs; nearby orbs are tied by a harmful thread. Slip through the gaps. |
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
| Buzzsaw | Melee | 30 | 170 | 12 | 4 | 8 | 8 | melee | A fast ring of spinning spikes that swoops round on curves. Each touch is small, but they come about eight times a second. |
| Spike Block | Melee | 60 | 120 | 14 | 12 | 14 | 14 | melee | Rocks from side to side as it closes in, then stops, flashes a red line and lunges for a big hit (35). Slow to recover. |
| Swarmer | Melee | 14 | 115 | 9 | 6 | 3 | 3 | melee | The simplest melee rusher: a white chomper with no eyes that walks straight at you. Weak alone, and it comes in packs. |
| Fuse Runner | Late | 50 | 200 | 12 | 8 | 12 | 10 | melee | A powder keg on the run: it plants its feet near you and blows up after a 0.8 s fuse, hurting enemies too. Step out, or shoot it early. |
| Hookman | Late | 150 | 70 | 15 | - | 25 | 14 | melee | Throws a harpoon along a blinking line and yanks you to its squad. Sidestep or parry; a miss leaves it open (+50%). |
| Sapper | Late | 110 | 85 | 14 | - | 25 | 14 | melee | Plants tripwire pods; armed pods are joined by lasers. Cross a laser or step on a pod and it blows. Shoot a pod to use it on enemies. |
| Tripwire Pod | Hidden | 1 | 0 | 7 | - | 0 | 0 | melee, heavy | A Sapper's pod: arms in 1 s, then a laser joins it to armed pods near it. Shoot it to blow it up. |
| Swordsman | Melee | 85 | 95 | 15 | 8 | 22 | 14 | melee | Walks up, raises a long sword and cuts the cone drawn on the floor in front of it. Step out of the cone, then hit it while it recovers. |
| Shield Swordsman | Melee | 85 | 75 | 15 | 8 | 28 | 16 | melee | A Swordsman behind a shield that stops every shot from the front. The shield drops when it cuts and stays down while it recovers: shoot it then, or from the side. |
| Taser Guard | Ranged | 70 | 80 | 13 | - | 9 | 11 |  | An officer in slate with a hi-vis band. Keeps its distance, charges its taser and fires a white Fork Bolt that stuns you. Dodge the bolt, or parry it. |
| Security Guard | Ranged | 45 | 70 | 15 | - | 9 | 9 |  | An officer in navy with a service pistol. Strafes, locks on and fires one tracer round at a time: seven shots, then a long reload. Sidestep the lane, or rush it while it reloads. |
| Thug | Melee | 140 | 62 | 19 | 20 | 10 | 10 | melee | A hunched prisoner brute with giant taped fists. Slow and tough, hits hard when it reaches you. |
| Shiv | Melee | 45 | 95 | 12 | 18 | 9 | 9 | melee | A prisoner with a taped shank: hooded, or shirtless with one in each hand. Circles in close, flashes a line, then lunges along it. |
| Spawn Portal | Hidden | 200 | 0 | 30 | - | 30 | 20 | melee, heavy | A burst of the wave comes through it. Shoot it closed before it empties to cancel the rest; it drops coins for each one it held. |
| Worker | Melee | 40 | 110 | 12 | 10 | 3 | 7 | melee | A small, quick worker bee that weaves at you, in loose pairs. |
| Soldier | Melee | 90 | 125 | 13 | 14 | 6 | 15 | melee | An armoured bee that rushes straight at you, jaws snapping. |
| Comb Shield | Melee | 60 | 120 | 16 | 8 | 10 | 10 | melee | A bee behind a plate of wax cells that stops shots and bashes you back. Break the cells, or go round it. |
| Honey Lobber | Ranged | 50 | 75 | 14 | - | 8 | 9 |  | Lobs honey at you. A ring marks the spot; the puddle slows whoever stands in it. |
| Stinger | Ranged | 105 | 80 | 13 | - | 9 | 11 |  | Keeps its distance, charges a venom lance and fires one barbed dart that stuns you. Strafe across the line, or parry it. |
| Needler | Ranged | 70 | 70 | 15 | - | 9 | 9 |  | Hovers and strafes, locks on, then fires one white needle at a time: seven shots, then a long reload. |
| Bumble | Melee | 140 | 62 | 19 | 20 | 10 | 10 | melee | A huge, furry bumblebee. Slow and tough, hits hard when it reaches you. |
| Hornet | Melee | 45 | 95 | 12 | 18 | 9 | 9 | melee | Circles in close, flashes a red lane, then darts along it. |
| Wisp | Melee | 14 | 120 | 9 | 6 | 3 | 3 | melee | A soul flame that drifts straight at you in a loose pack and burns on touch. Thin them before they reach you. |
| Acolyte | Melee | 90 | 120 | 13 | 8 | 14 | 12 | melee | A hooded chaser. Stops just short, raises its sickle over a pale cone, then one wide swing. Step back out of the cone. |
| Bound Wraith | Melee | 60 | 95 | 12 | 8 | 10 | 9 | melee | Comes as a pair circling near you, joined by a tether that hurts. Kill one and the other brings it back in 3 s, unless you kill both. |
| Scythe Wraith | Melee | 75 | 85 | 14 | 18 | 12 | 11 | melee | Marks a long line through you, then dashes along it and reaps both sides of its path. Leave the line sideways. |
| Lamplighter | Ranged | 55 | 70 | 13 | - | 10 | 10 |  | Plants its feet, its lantern flares and gold lines show the volley: three lines at you, then a four-way cross. |
| Mourner | Ranged | 80 | 60 | 15 | - | 12 | 12 |  | Stands still and weeps a twin spiral, one arm each way, then rests. Push in while it rests. |
| Gravedigger | Melee | 140 | 62 | 19 | 20 | 10 | 10 | melee | A cultist brute with a shovel. Slow and tough, hits hard when it reaches you. |
| Shade | Late | 90 | 90 | 13 | - | 25 | 22 |  | A shrouded Phantom: fades to smoke, shows a ring where it will land, appears and fires. |
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
| Rubble | Melee | 40 | 110 | 12 | 10 | 3 | 7 | melee | A fist-sized chunk of the Colossus that rolls at you, weaving, in pairs. |
| Quarryman | Melee | 90 | 125 | 13 | 14 | 6 | 15 | melee | A quarry worker with a hard hat, a headlamp and a pickaxe. Walks straight at you. |
| Stomper | Melee | 180 | 58 | 21 | 20 | 12 | 20 | melee | A slab-shouldered stone brute. Every few seconds it lifts a foot and stomps out a small ring of push blocks with one green gap: step into the gap. |
| Slab Bearer | Melee | 60 | 120 | 16 | 8 | 10 | 10 | melee | Carries a quarried slab that stops shots and bashes you back. Break the slab and it drops it as cover for a few seconds; then the bearer falls fast. |
| Rock Hurler | Ranged | 50 | 75 | 14 | - | 8 | 9 |  | Lobs a boulder where you're heading. Walk out of the ring: it lands hard, bursts into shards and leaves a rubble pile that stops shots. |
| Driller | Melee | 45 | 95 | 12 | 18 | 9 | 9 | melee | Circles in close with a jackhammer, shows a lane, then drills along it. The crack it leaves erupts a moment later: get off the line. |
| Geomancer | Ranged | 70 | 80 | 13 | - | 9 | 11 |  | Keeps its distance, plants its crystal staff and sends a crack racing at you. It glows, then erupts: step off the line. |
| Cinder Priest | Ranged | 70 | 80 | 13 | - | 9 | 11 |  | Charges a molten core, then fires one magma bolt straight at you. Wherever it stops, it leaves a pool of lava. Strafe across the line, or parry it. |
| Riot Captain | Hidden | 3000 | 70 | 30 | 20 | 150 | 80 | mini-boss, heavy | The Warden's mini-boss: a tower shield blocks the front. Bait its shield charge into a wall to daze it; it also fires buckshot, lobs tear gas and whistles for Taser Guards. |
| Fencer | Melee | 32 | 115 | 11 | 10 | 4 | 7 | melee | A masked foil-fencer that runs in crowds. Close up it shows a short red line, then hops down it: step off the line. |
| Blade Dancer | Melee | 70 | 105 | 13 | 14 | 8 | 14 | melee | Shows a long warning line, then lunges down it and leaves blades hanging on the path. Each fires one shot out of each side: never stand beside the line. |
| Parrier | Melee | 60 | 110 | 15 | 8 | 10 | 10 | melee | On guard behind a main-gauche: shots into its front are parried straight back at you, and it bashes you away. It turns slowly: get behind it. |
| Second | Ranged | 45 | 70 | 13 | - | 9 | 9 |  | Hangs back, glints gold, then throws an aimed fan of five daggers. Slip between them. |
| Marshal | Ranged | 55 | 75 | 14 | - | 8 | 9 |  | The duel's referee: raises its baton and draws a gold circle round you. For 4 s you can't leave it, and leaning on the edge hurts: fight in the middle. |
| Gallant | Ranged | 80 | 90 | 14 | 20 | 10 | 12 |  | Salutes with its sword raised, then lunges at you. Parry it perfectly as it arrives and it reels, open to 50% more damage; dodge it and it rakes the spot. |
| Harlequin | Melee | 240 | 60 | 20 | 20 | 14 | 22 | melee | A heavy two-sworded jester. It marks a line through you and dashes it, vaults to the other arm and dashes across: an X of blades that fire out sideways. Stand in a gap of the X. |
| Mummer | Melee | 40 | 150 | 11 | 14 | 9 | 10 | melee | A masked player that blinks and becomes three. They circle you and all fire, but only the real one's shots are solid; the copies' are hollow rings. Find the real one. |
| Mummer | Hidden | 1 | 0 | 11 | - | 0 | 0 | melee | One of a Mummer's two copies: its shots are hollow rings that pass through you. It vanishes when its time is up. |
| Grave Caller | Ranged | 60 | 60 | 14 | - | 12 | 16 |  | Zombie: hangs back and cracks three graves open round you; a Shambler claws out of each. Step out of the ring, then kill it: its risen crumble. |
| Gnawling | Melee | 40 | 110 | 11 | 10 | 3 | 7 | melee | A mouth on legs, the Devourer in miniature. Weaves at you in pairs. |
| Butcher | Melee | 90 | 125 | 13 | 14 | 6 | 15 | melee | A hooded butcher with a bone mask. Close in, it raises its cleaver and chops a half-circle in front: step out of it. |
| Gulper | Melee | 180 | 58 | 22 | 20 | 12 | 25 | melee | A swollen gut. Its cheeks swell, then it breathes in and pulls you toward its mouth: step out of the cone sideways. |
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
