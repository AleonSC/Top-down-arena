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
| Scattergun | Tank | 0.45 s | 7 | 520 x 0.5 s | - | 154 | A short-range spread of pellets. Up close every pellet hits much harder and shoves the enemy back. Point-blank hits load shells: Alt fires them all at once and the kick throws you back. |
| Flak Cannon | Tank | 0.8 s | 30 | 380 x 1.1 s | - | 36 | Shells fly to your cursor and burst in the air there, hurting enemies and destroying enemy bullets caught in the burst. Alt lays a proximity shell that bursts when an enemy steps on it. |
| Riot Repeater | Tank | 0.18 s | 11 | 600 x 0.8 s | - | 60 | Steady single shots. Every hit suppresses the enemy so it shoots slower, building up the more you hit it and fading when you stop. Alt lobs tear gas that suppresses and slows everything in it. |
| Grenade Launcher | Tank | 0.42 s | 34 | 900 x 0.42 s | - | 49 | A six-round drum of HE grenades lobbed over enemies to your cursor, bursting where they land. Alt swaps in the smoke drum: canisters that open into a cloud where regular enemies are slowed and can't attack, and bosses are slowed. |
| Repulsion Cannon | Healer | 0.7 s | 6 | - | 240 | 9 | Blasts enemies away in a cone for light damage. Enemies it hits shed tiny health orbs. Hits fill the Undertow: hold Alt to pull enemies in. |
| Revolver | Healer | 0.45 s | 24 | 700 x 1.1 s | - | 82 | Tap for aimed shots, or hold to fan the hammer and empty the cylinder in a quick spray. The last round in the cylinder is heavy. Alt: a pistol whip, a wide swing that destroys bullets and shoves enemies back. |
| Mender's Bow | Healer | 0.9 s | 0 | - | - | 40 | Hold to draw, release to fire. A full draw hits hard, pierces enemies and heals allies it passes. Let go right at full draw for a perfect shot. |
| Twin Pistols | Healer | 0.16 s | 10 | 780 x 0.8 s | - | 60 | A pistol in each hand, firing in turn. Hits knock tiny health orbs loose. Alt: Laser Sights, both guns fire together down two red beams that meet at your cursor, for harder rounds. |
| Twin Rifle | Dps | 0.13 s | 4 | 820 x 0.7 s | - | 90 | Two streams from wide-set barrels, angled to cross at your cursor. Hits where they cross are crossfire and do 1.5×. |
| Railgun | Dps | 0.7 s | 120 | 1600 x 0.7 s | - | 116 | Hold to charge, let go to fire a beam that ricochets off walls. Longer charges hit harder, wider and bounce more, and heat the gun. Alt dumps the heat as a cone of fire. |
| Burst SMG | Dps | 0.06 s | 5 | 760 x 0.8 s | - | 120 | Sprays very fast shots. Keep firing to spin it up: the spray tightens and damage climbs. It winds down fast when you stop. |
| Double Barrel | Dps | 0.32 s | 7 | 900 x 0.5 s | - | 82 | A side-by-side shotgun: 9 pellets a barrel, strongest up close, each pellet passing through 2 enemies. Two shells, then a break-open reload. Alt fires both barrels at once. |
| Twin Rifle Auto (variant) | Dps | 0.13 s | 4 | 820 x 0.5 s | - | 60 | Two rifles held out to your sides, each aiming and firing on its own at the nearest enemy on its side. You can't shoot. Alt locks both onto the enemy nearest your cursor until it dies: their streams meet on it for crossfire (1.5×). |
| Out of Ammo (variant) | Dps | 0.5 s | 12 | - | 80 | 52 | The rifles are out of rounds, so they swat. Each rifle knocks the nearest enemy bullet out of the air (one each, then a half-second recovery). Enemies close in front of you get a five-move chain: left bash, cross cut, right bash, reverse cut, then a rifle clap with a shockwave. Alt spins you for 1.2 s: it cuts everything round you and swats every bullet. |

### Elementalist

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Flamethrower | Tank | 0.06 s | 3 | 480 x 0.4 s | - | 94 | A short-range stream of fire. Each flame hits the first enemy hard and carries on through the rest, setting them all burning. |
| Frost Lance | Tank | 0.4 s | 18 | - | 178 | 65 | A five-step chain: Shiver Crescent, Backhand Rime, Frost Waltz, Crystal Fan, Avalanche Slam. Hits chill; 3 chill freezes; the slam shatters frozen enemies. Parry cancels a step. Alt: Absolute Zero. |
| Storm Hammer | Tank | 0.7 s | 40 | 460 x 1.1 s | - | 60 | Throws a spinning hammer that smashes one enemy and electrifies it, so it arcs lightning to the enemies around it. |
| Wind Clash | Tank | 0.34 s | 18 | - | 170 | 50 | Hold Fire for a combo of air waves that hurt and shove every enemy they pass: two crescents that cross, a gale lance straight out, then a ring that bursts round you. Alt: Tempest, a storm of wind round you for 3 s that hurts enemies in it and catches every enemy shot that comes in; you move 30% slower and can't attack while it blows. |
| Radiant Orb | Healer | 0.2 s | 8 | - | 520 | 37 | Hold to cast an orb of light out to your cursor; it stays there while you hold. Drag it through enemies to burn them and over allies to heal them. |
| Chain Light | Healer | 0.5 s | 40 | - | - | 80 | A great cone of light in front of you. Its damage is shared between every enemy in it, so one alone takes it all. |
| Nimbus | Healer | 0.6 s | 16 | - | 380 | 26 | A storm cloud floats over your cursor. Click to call lightning down beneath it. Rain falls under the cloud, healing every ally standing in it and soaking enemies (wet). Alt: Eye of the Storm, the cloud spins into a hurricane for 4 s that drags enemies round, tears up their shots and heals allies in its eye. |
| Fireball | Dps | 0.5 s | 32 | 430 x 1.4 s | - | 71 | A big, slow fireball that rolls on through every enemy in its path, hurting each and setting it burning. |
| Arc Lightning | Dps | 0.25 s | 16 | - | - | 116 | Hold to tag enemies in a wide cone in front of you, up to 10; they stay tagged if they walk out of it. Let go: lightning chains through every tagged enemy, harder the more you tagged, and stuns them. Alt fires three forked bolts at the enemies nearest your cursor. |
| Ice Shards | Dps | 0.3 s | 7 | 520 x 1 s | - | 70 | Each cast conjures shards of ice that orbit you. They launch at nearby enemies, homing, one after another. With the orbit full, a cast flies at your aim. |
| Stonespear | Dps | 0.18 s | 24 | 900 x 0.6 s | - | 60 | Slam the ground: it hurts everything round you and six earth spears tear out of it to hover at your sides. Each click throws one at your cursor, passing through an enemy. Alt hurls every spear left at the enemy nearest your cursor. Throw the last and the next click slams again. |
| Waterball (variant) | Dps | 0.5 s | 28 | 430 x 1.4 s | - | 56 | A big, slow ball of water that rolls on through every enemy in its path, hurting each and soaking it (wet for 4 s): wet enemies are slowed, and lightning arcs to them. |
| Earthball (variant) | Dps | 0.6 s | 48 | 360 x 1.4 s | - | 82 | A heavy ball of rock that stops on the first enemy it hits, stunning it (0.8 s; shorter on heavies and bosses), and breaks into rubble that hurts everything round it. |

### Summoner

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Golem Staff | Tank | 0.5 s | 40 | - | - | 79 | Hold to heave a boulder, let go to throw it in an arc: the longer you hold, the farther it flies. It crushes everything where it lands. A rune-carved stone Golem (a small Titan) rises beside you, slams enemies and blocks bullets. |
| Bone Guard | Tank | 0.3 s | 7 | 720 x 0.8 s | - | 37 | Bone darts that stick in enemies: enough in one cages it in place (not bosses), and darts into different enemies thread them together so a cage reels its neighbours in. Skeleton guards circle you, block bullets, and shoot at caged enemies. Alt: Ossuary Wall. |
| Bear Totem | Tank | 0.45 s | 11 | - | 85 | 99 | Two quick claw swipes in front of you that make enemies bleed. A Bear companion mauls nearby enemies. |
| Wyvern Staff | Tank | 0.1 s | 22 | - | - | 12 | A wind Wyvern hovers over you: its gale slows and shoves back the enemies under it, and slows their shots. Hold Fire and it flies out to breathe fire at your cursor for as long as the Cinder meter lasts; the gale refills it. Alt: Wyrm Slam, a dive that stuns and hurts. |
| Sprite Staff | Healer | 0.5 s | 7 | 540 x 1 s | - | 14 | A firefly that seeds the enemy it hits, then hops to nearby enemies that aren't seeded yet. Sprites fly to whoever is hurt and heal them, and every seeded enemy you kill frees another sprite for a while. |
| Treant Rod | Healer | 0.55 s | 15 | - | 250 | 34 | A thorny root whips out along your aim and snaps back, hitting everything on the line and healing allies it touches. Your Treant roots where it stands and blocks bullets; a root runs from you to it, healing allies who cross it and slowing enemies. |
| Spirit Chalk | Healer | 0.1 s | 26 | - | 450 | 26 | Hold and drag to draw a line of spirit ink. It burns and slows enemies touching it and heals allies standing on it. Draw a circle round enemies to trap them inside (not bosses). Alt: Ignite, sets your loops alight to burn what they hold. |
| Wolf Whistle | Dps | 0.3 s | 6.5 | 720 x 0.8 s | - | 150 | Darts mark prey for your wolves; every wolf biting it at once is a takedown. Kills grow the pack (up to 5); Alt merges it into an alpha. The pack takes 10% of the damage you take. |
| Wisp Lantern | Dps | 0.25 s | 22 | - | - | 84 | Wisps orbit you, and they are your shots: each time you fire, one flies at the enemy nearest your cursor, strikes it and comes back. |
| Imp Caller | Dps | 0.35 s | 0 | - | - | 77 | Imps arrive over time and wait by you. Tap fire to send one charging at your cursor to explode; hold to merge them all into one big imp whose blast is bigger than theirs put together. |
| Honeybee Staff | Dps | 0.4 s | 22 | - | - | 60 | Each click hatches a bee at your cursor: it buzzes a moment, then stings the nearest enemy once and dies. Three stings in a row on one enemy swarm it for more. Hold Alt to spray honey that slows; an enemy that dies in your honey bursts into two free bees. |

### Knight

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Warhammer | Tank | 0.75 s | 80 | - | 130 | 64 | Hold to charge, let go to slam the ground in front of you. The longer the charge, the harder it hits and the longer it stuns. |
| Lance | Tank | 0.24 s | 14 | - | 178 | 60 | A four-step chain with a lance and shield: two quick jabs, a wide sweep that bats enemies aside, then a Breaching Thrust that shoves everything in front far back and stuns it. Shoved enemies that hit another enemy or a wall take Impact damage. The weapon-action key hooks the enemy nearest your cursor, hauls it in and makes your next attack the Breaching Thrust. |
| Halberd | Tank | 0.6 s | 30 | - | 200 | 48 | Sweeps a long, thin blade across in front of you, hitting each enemy once per sweep. |
| Bulwark Barrier | Tank | 0.3 s | 12 | - | 80 | 45 | A kite shield that always guards your front half: every enemy shot that hits it stops, red aimed shots too. Fire is a three-hit chain: Rim Cut, Backhand, then a Boss Slam that shoves and stuns. Hold the weapon-action key to brace: you move slower, the guard widens and every shot it stops fills Ram. Fire while braced for a Shield Bash: a dash that hits, shoves and stuns harder with more Ram; at full Ram the soaked shots burst out as steel shards. |
| Quarterstaff | Healer | 0.5 s | 18 | - | 120 | 36 | Left and right staff swipes that knock enemies back and bat enemy bullets back at them. Every enemy hit heals you and nearby allies and stores a Jab stack (up to 3); the weapon-action key spends one on a cone jab. |
| Lightbringer | Healer | 0.5 s | 17 | - | 80 | 58 | A three-step chain. Dawn Cut's crescent heals the allies it passes, Mercy Cut's wards them, and Noon Sunburst plants the sword in a ring that heals everyone near you. Hits fill a light gauge: full, the next Noon is Radiant. The weapon-action key spends the light on a Sunlance that heals the ally nearest your cursor. |
| Censer | Healer | 0.26 s | 13 | - | 85 | 35 | A censer on a chain, swung in a three-step rite: a sweep, a return sweep and a Benediction that goes all the way round. Every step heals allies near you and leaves incense that heals allies in it and makes enemy shots fired from inside it stray. Each enemy hit lights an Ember; the weapon-action key throws the censer to your cursor, spending them on a burst and a big healing cloud. |
| Vowguard | Healer | 0.3 s | 16 | - | 115 | 74 | A sword and shield with two stances, each playing a chain of up to 10 steps you build in the lobby. Valor cuts, thrusts and bashes, and its hits light Faith. Sanctuary holds the shield up front: small shots there stop, light Faith, and its moves guard, heal and ward allies. Finishers spend Faith for more damage and healing. The weapon-action key switches stance. |
| Twin Blades | Dps | 0.18 s | 16 | - | 90 | 98 | Two sabres in a five-step chain: rising cut, falling cut, double jab, pirouette, and a crossing shear that throws a crescent wave. Each step carries you forward. The weapon-action key hurls both sabres to cross at your cursor and fly back. |
| Greatsword | Dps | 0.5 s | 40 | - | 125 | 87 | A ten-step chain: Undertow drags enemies in, Pommel Bash stuns, Cross Cut, Backstep Reap hops you back behind a crescent that stops bullets, Comet Leap jumps in and crashes down, then Full Moon spins and Eclipse brings down a golden lance of light. The weapon-action key throws the sword spinning to your cursor and back. |
| Rapier | Dps | 0.48 s | 18 | - | 130 | 85 | Precise thrusts at the nearest enemy in front, and they skewer enemy bullets along their line. The first thrust is slow, but every enemy you hit adds Tempo, and Tempo makes the thrusts quicker. Stop hitting and it holds a moment, then drains. The weapon-action key spends it on a Flourish: the blade whips back and forth, each swing throwing a crescent wave. |
| Arc Knuckles | Dps | 0.24 s | 22 | - | 80 | 88 | Coiled blue fists in a five-punch chain: jab, cross, a hook that sweeps a crowd, an uppercut that launches, and the Twin Hammer, a ground slam whose shockwave stuns. Raising the parry cancels a punch and starts the chain over; a perfect parry arms the gold Counter Hammer for your next press. The weapon-action key is a Rocket Dash; land it and press Fire as each move lands to chain a Flurry, a Grab & Throw and a Rocket Punch. |

### Ninja

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Kusarigama | Tank | 0.55 s | 26 | - | 280 | 37 | Cut with the sickle, then throw the weight to drag the first enemy in its lane to you. Hold the weapon-action key to whirl the weight around you, pulling everything near into the chain. |
| Bo Staff | Tank | 0.47 s | 16 | - | 105 | 48 | Click for a never-ending chain of three swings: a cone slash, a jab, then a full spin that starts it over. Every swing cuts small bullets. The weapon-action key shoves a wall of staff ahead of you and restarts the chain. |
| Fuma Shuriken | Tank | 0.7 s | 30 | - | - | 6 | A giant spinning star thrown in a wide loop out in front of you and back, cutting through everything both ways. |
| Nodachi | Tank | 0.3 s | 20 | - | 150 | 52 | A katana as tall as you, with two stances on your weapon-action key, each with its own five-step combo that keeps its place. Arashi sweeps wide round you with armoured wind-ups and ends in Tenchi, a spin into a stunning slam. Mamori jabs fast and blocks shots in front of you while it does. Raising the parry starts both combos over. |
| Medicine Darts | Healer | 0.18 s | 7 | 900 x 0.8 s | - | 50 | Fast needles that dose what they hit. Enemies get poisoned and hurt allies get healed, and doses stack. |
| Spirit Fan | Healer | 0.4 s | 6 | 420 x 0.3 s | - | 75 | A short gust that heals allies and pushes small enemy bullets back. Shots alternate between blowing enemies away and drawing them in. |
| Ofuda | Healer | 0.5 s | 25 | 360 x 1.6 s | - | 50 | Paper charms fly to your cursor and lie there as wards. An enemy that steps on one sets it off, hurting enemies around it and healing allies near it. |
| Shuriken | Dps | 0.35 s | 9 | 700 x 0.75 s | - | 140 | Tight fans of spinning stars that stick in what they hit. After a while each rips out for another hit and flies back to you, cutting through what it passes. Your weapon-action key switches to the Great Star stance, pulling stuck stars back: hold fire to draw a route, let go to throw one huge star along it and home again, cutting everything both ways. |
| Katana | Dps | 0.3 s | 20 | - | 120 | 65 | Two stance combos; your weapon-action key switches between them, and the one you leave keeps its place. Hayate dashes and slices and ends in a Momentum Burst that shoves enemies away. Senbon cuts, spins and slashes and ends in a storm of caltrops. The burst flings caltrops on the ground; caltrops thrown after a burst seek what it hit. |
| Kunai | Dps | 0.7 s | 42 | 1400 x 0.5 s | - | 88 | Slow, heavy throwing knives that hit harder against enemies not targeting you. Every throw leaves a mark where it lands: press your weapon-action key to blink there, and your next hit lands double. |
| Nunchaku | Dps | 0.3 s | 20 | - | 95 | 95 | Twin Storm: a nunchaku in each hand. Fire is a four-step combo: Snap, Backhand, Double Figure-8, then the Twin Crush, an overhead slam that stuns. Your weapon-action key switches to the Twin Windmill guard: two spinning discs swat small shots back at enemies, and Fire lashes out with a Twin Counter that hits harder for every shot swatted. |

### Hemomancer

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Blood Lash | Tank | 0.32 s | 24 | - | 130 | 96 | A whip of blood, a four-step string: Crack, Backhand, Snap, Reap. Every lash that lands hooks a blood chain on the enemy (+1 link, +2 from the cracking tip or the Snap). Reap yanks every chained enemy in and turns the links into Clot (4% less damage taken each, up to 5). Alt: hold for Martyr's Leash. |
| Gore Mortar | Tank | 0.75 s | 34 | - | - | 133 | Hold to charge, let go to lob a massive blood clot over enemies; the longer you hold, the farther it flies, and it hits just as hard at any range. Each shot costs 6% of your max health. It bursts into a big pool of blood that slows enemies and slowly heals anyone standing in it, and after a moment the pool congeals and holds enemies still. |
| Leech Tendrils | Tank | 0.1 s | 80 | - | 160 | 31 | Hold to lash out blood tendrils that grasp nearby enemies and hold them stunned. The longer you hold, the more blood builds up in them, and after 5 seconds they burst. Let go early and they burst for the share built up so far. Holding costs 2% of your max health a second, and the tendrils need 1 second to recover after a burst. |
| Blood Blast | Tank | 0.42 s | 22 | - | 115 | 53 | Your blood shaped into a blade, a six-step chain that never times out: Crimson Slash, Backcut, Thorn Flurry, Vein Whirl, Sanguine Cleave, Crescent Flight. Every attack costs 1.3-3% of your max health and puts a blood stack on each enemy it hits (up to 10). When a stacked enemy dies, its blood flies back and heals you 2 a stack. |
| Transfusion Darts | Healer | 0.2 s | 7 | 850 x 0.85 s | - | 32 | Darts that transfuse your blood: one that reaches a hurt ally gives them more health than it costs you. Each enemy hit steals a sliver back. |
| Sanguine Orb | Healer | 0.45 s | 16 | 420 x 1.4 s | - | 57 | Orbs of blood that home in at the last moment, circle an enemy and cover it, making it bleed and charging your heal reserve. With 25 reserve, an orb that touches a hurt ally coats them in healing blood instead (30 health over 4 s). |
| Vein Tether | Healer | 0.08 s | 4 | - | 350 | 48 | A draining beam that hurts whatever you hold it on. Part of its damage heals, split between you and the nearest ally. |
| Blood Bolts | Dps | 0.13 s | 9 | 800 x 0.8 s | - | 121 | Fast bolts of blood that make enemies bleed. A bolt that fills an enemy's bleed bursts it, hurting it and the enemies around it. |
| Implosion | Dps | 0.45 s | 34 | - | 480 | 75 | Click an enemy: its blood collapses inward, hurting enemies pressed against it and pulling them in. |
| Blood Brawler | Dps | 0.26 s | 18 | - | 520 | 80 | Click an enemy and work its blood from range: Pulse, Reel, Heart Lift, then Marrow Burst bursts it into bone shards. Alt binds the enemies near it, so your moves land on them too. |
| Embolus Spears | Dps | 0.25 s | 30 | 1100 x 0 s | - | 63 | Arms out, a hardened blood spear in each hand. Fire throws the left, then the right, for 20% of your max health each: they pierce every enemy and stick in the wall. Fire again to recall both: they pierce again on the way back and give the health back. Alt bursts them where they are instead, for half the health back. |

### Engineer

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Iron Fist | Tank | 0.7 s | 10 | - | 95 | 53 | Click to summon a Tank Bot at your cursor: a heavy robot Engineer (850 health) with a nail shotgun that draws enemies from 240 px. Click an enemy to send it there. With the robot out, a click is a robotic fist: a shove in an arc in front of you, 10 damage and a long push. A broken robot can be summoned again after 8 s. |
| Iron Fist | Tank | 0.7 s | 10 | - | 95 | 53 | Click to summon a Tank Bot at your cursor: a heavy robot Engineer (850 health) with a nail shotgun that draws enemies from 240 px. Click an enemy to send it there. With the robot out, a click is a robotic fist: a shove in an arc in front of you, 10 damage and a long push. A broken robot can be summoned again after 8 s. |
| Iron Fist | Tank | 0.7 s | 10 | - | 95 | 53 | Click to summon a Tank Bot at your cursor: a heavy robot Engineer (850 health) with a nail shotgun that draws enemies from 240 px. Click an enemy to send it there. With the robot out, a click is a robotic fist: a shove in an arc in front of you, 10 damage and a long push. A broken robot can be summoned again after 8 s. |
| Stasis Gun | Healer | 0.1 s | 14 | - | 300 | 37 | Click to summon a Medic Bot at your cursor: a robot Engineer (420 health) that shoots rivets and heals allies near it for 9 a second. Click an enemy to send it there. With the robot out, hold Fire for a stasis field at your cursor (60 px, up to 300 px away): enemies in it are held in place (bosses slowed) and take 14 damage a second. A broken robot can be summoned again after 8 s. |
| Stasis Gun | Healer | 0.1 s | 14 | - | 300 | 37 | Click to summon a Medic Bot at your cursor: a robot Engineer (420 health) that shoots rivets and heals allies near it for 9 a second. Click an enemy to send it there. With the robot out, hold Fire for a stasis field at your cursor (60 px, up to 300 px away): enemies in it are held in place (bosses slowed) and take 14 damage a second. A broken robot can be summoned again after 8 s. |
| Stasis Gun | Healer | 0.1 s | 14 | - | 300 | 37 | Click to summon a Medic Bot at your cursor: a robot Engineer (420 health) that shoots rivets and heals allies near it for 9 a second. Click an enemy to send it there. With the robot out, hold Fire for a stasis field at your cursor (60 px, up to 300 px away): enemies in it are held in place (bosses slowed) and take 14 damage a second. A broken robot can be summoned again after 8 s. |
| Vector Blade | Dps | 0.4 s | 24 | - | 88 | 64 | Click to summon an Assault Bot at your cursor: a fast robot Engineer (380 health) with a rapid rivet gun. Click an enemy to send it there. With the robot out, a click swipes a cyan blade in front of you for 24. A broken robot can be summoned again after 8 s. |
| Vector Blade | Dps | 0.4 s | 24 | - | 88 | 64 | Click to summon an Assault Bot at your cursor: a fast robot Engineer (380 health) with a rapid rivet gun. Click an enemy to send it there. With the robot out, a click swipes a cyan blade in front of you for 24. A broken robot can be summoned again after 8 s. |
| Vector Blade | Dps | 0.4 s | 24 | - | 88 | 64 | Click to summon an Assault Bot at your cursor: a fast robot Engineer (380 health) with a rapid rivet gun. Click an enemy to send it there. With the robot out, a click swipes a cyan blade in front of you for 24. A broken robot can be summoned again after 8 s. |

### Arcanist

| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |
|---|---|---|---|---|---|---|---|
| Orbit Shards | Tank | 0.3 s | 18 | 800 x 0.45 s | - | 122 | Four crystal shards circle you, cutting small enemy bullets out of the air. Fire flings one at your cursor (up to 360 px): it cuts everything it passes, 18 on the way out and 18 on the way back. Hold Alt for Shard Guard: they pull in tight and stop every bullet that reaches them, but you move at 55% speed and can't fling them. |
| Orbit Shards | Tank | 0.3 s | 18 | 800 x 0.45 s | - | 122 | Four crystal shards circle you, cutting small enemy bullets out of the air. Fire flings one at your cursor (up to 360 px): it cuts everything it passes, 18 on the way out and 18 on the way back. Hold Alt for Shard Guard: they pull in tight and stop every bullet that reaches them, but you move at 55% speed and can't fling them. |
| Orbit Shards | Tank | 0.3 s | 18 | 800 x 0.45 s | - | 122 | Four crystal shards circle you, cutting small enemy bullets out of the air. Fire flings one at your cursor (up to 360 px): it cuts everything it passes, 18 on the way out and 18 on the way back. Hold Alt for Shard Guard: they pull in tight and stop every bullet that reaches them, but you move at 55% speed and can't fling them. |
| Glyph Stylus | Healer | 0.26 s | 10 | 650 x 0.7 s | - | 38 | Writes rune bolts in the air: each goes through two enemies (10 each), and one that passes through an ally heals them for 7. |
| Glyph Stylus | Healer | 0.26 s | 10 | 650 x 0.7 s | - | 38 | Writes rune bolts in the air: each goes through two enemies (10 each), and one that passes through an ally heals them for 7. |
| Glyph Stylus | Healer | 0.26 s | 10 | 650 x 0.7 s | - | 38 | Writes rune bolts in the air: each goes through two enemies (10 each), and one that passes through an ally heals them for 7. |
| Dancing Blades | Dps | 0.3 s | 10 | 950 x 0.4 s | - | 117 | Three enchanted swords hover behind you. Fire launches one at your cursor (up to 380 px, onto the enemy nearest it): it cuts what it passes, then dances on the spot for 1 s, slashing everything within 42 px four times (10 each), and flies back. Alt calls them all home at once, cutting on the way. |
| Dancing Blades | Dps | 0.3 s | 10 | 950 x 0.4 s | - | 117 | Three enchanted swords hover behind you. Fire launches one at your cursor (up to 380 px, onto the enemy nearest it): it cuts what it passes, then dances on the spot for 1 s, slashing everything within 42 px four times (10 each), and flies back. Alt calls them all home at once, cutting on the way. |
| Dancing Blades | Dps | 0.3 s | 10 | 950 x 0.4 s | - | 117 | Three enchanted swords hover behind you. Fire launches one at your cursor (up to 380 px, onto the enemy nearest it): it cuts what it passes, then dances on the spot for 1 s, slashing everything within 42 px four times (10 each), and flies back. Alt calls them all home at once, cutting on the way. |

## Abilities

### Soldier

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Barrier | Tank | 1st, choice 1 | 12 s | - | Bullets that hit the barrier are reflected back as your own. | A bubble that stops enemy bullets. |
| Roar | Tank | 1st, choice 2 | 14 s | - | Also knocks enemies within 250 px back and slows them for 2 s. | Every enemy targets you, and you take less damage. |
| Bulldoze | Tank | 1st, choice 3 | 9 s | - | Erases enemy bullets along the charge. | Charge forward, damaging and knocking back everything in your path. |
| Fortress | Tank | ultimate | 70 s | - | Lasts 14 s. | A dome only players and their bullets can cross. |
| Juggernaut Suit | Tank | ultimate | 75 s | - | 450 armour, and each shard does 18. | Heavy armour draws every enemy and throws flak back at shooters. |
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
| Decoy Dummy | Healer | 1st, choice 5 | 4 s (+2 charges) | - | Dummies have 50% more health, and the confetti blast reaches 130 px. | Throw an inflatable soldier that enemies shoot at instead of you. |
| Poison Dart | Dps | 1st, choice 1 | 8 s | - | Spreads to 5 enemies at a time, at full strength. | Poison the nearest enemy; the poison spreads when it dies. |
| Airstrike | Dps | 1st, choice 2 | 16 s | - | A second wall lands 160 px further out, half a second later. | Call a jet to carpet a line with bombs. |
| Mark for Death | Dps | 1st, choice 3 | 12 s | - | Marks the three enemies nearest your cursor. | Mark an enemy so it takes more damage. |
| Artillery Assault | Dps | ultimate | 60 s | - | +4 extra projectiles instead of +2. | Fire three times as fast with extra projectiles. |
| Attack Helicopter | Dps | ultimate | 70 s | - | A second gun on the wing: two targets at once. | A gunship locks enemies one at a time and can't be hit. |
| Deployable Turret | Dps | 1st, choice 5 | 45 s | - | Fires 14 s, rounds pierce 4, and the blast does double. | Climb into a heavy turret that spools up to a fast piercing fire. |

### Elementalist

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Aeroaegis | Tank | 1st, choice 1 | 11 s | - | Lasts 4.5 s and reflects bullets for double damage. | A wind vortex wraps a player: enemies can't enter and bullets reflect back. |
| Frost Nova | Tank | 1st, choice 2 | 12 s | - | Also erases enemy bullets around you. | Freeze every enemy near you and damage them. |
| Flame Wall | Tank | 1st, choice 3 | 14 s | - | The fire also slows enemies by 40%. | A wall of fire at your cursor that burns enemies and blocks bullets. |
| Glacial Tomb | Tank | ultimate | 75 s | - | Enemies stay frozen for 6 s and bosses take 500. | Erase every enemy bullet and freeze every enemy. |
| Lightning Rod | Tank | ultimate | 70 s | - | Lasts 8 s and each bolt arcs to 3 enemies. | You draw every enemy while taking much less damage, and lightning strikes whoever hits you. |
| Flood | Tank | 1st, choice 5 | 16 s | - | Deluge: the flood is 30% wider and lasts 9 s. | Flood the ground around you: it throws enemies back, slows them and soaks them. |
| Healing Rain | Healer | 1st, choice 1 | 16 s | - | Lasts 10 s, and downed allies inside revive twice as fast. | A rain at your cursor that heals allies and slows enemies. |
| Gust Gate | Healer | 1st, choice 2 | 14 s | - | The ring is 40% wider and pushes enemies twice as hard. | A wind ring that gives anyone passing through a speed boost. |
| Lumen Ward | Healer | 1st, choice 3 | 20 s | - | Also freezes enemies outside the light for as long as it lasts (not bosses). | A cone of light that heals allies and burns away enemy bullets. |
| Sanctum | Healer | ultimate | 80 s | - | The dome is half again as wide (300 px). | A dome of light that blocks bullets, keeps allies up and heals them. |
| Water Ward | Healer | 1st, choice 5 | 3 s | - | Splashes and bursts reach 40% farther. | Water drops orbit you and splash enemies and eat shots. |
| Meteor | Dps | 1st, choice 1 | 10 s | - | Leaves burning ground for 3 s. | Call a meteor down at your cursor. |
| Blizzard | Dps | 1st, choice 2 | 14 s | - | Lasts 10 s. | A storm at your cursor that slows and damages enemies. |
| Tsunami | Dps | 1st, choice 3 | 16 s | - | Wet lasts 14 s, and the wave rolls back to you, wetting what it missed. | A wave that soaks enemies, so lightning arcs to every one. |
| Bullet Rain | Dps | ultimate | 70 s | - | Lasts 9 s. | A downpour of needles that hurts and soaks everything in a big square. |
| Gale Wall | Dps | 1st, choice 5 | 18 s | - | The wall is 40% wider and holds up to 60 charges. | A wind wall that traps enemy shots, then unleashes them as a blast. |

### Summoner

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Golem Wall | Tank | 1st, choice 1 | 13 s | - | Raises five golems. | Raise three golems that block every enemy bullet. |
| Earthquake | Tank | 1st, choice 2 | 10 s | - | Also freezes everything it hurts for 1 s (not bosses). | Slam the ground, then a Burrower tunnels through enemies. |
| Force Pact | Tank | 1st, choice 3 | 12 s | - | A second thrall: the pact keeps the last two types you chose. | Make a regular enemy fight for you. |
| Titan | Tank | ultimate | 80 s | - | The titan is 30% bigger and stays for 27 s. | Summon a huge stone titan that stomps enemies and blocks bullets. |
| Bone Phalanx | Tank | ultimate | 75 s | - | 10 skeletons with 180 health each. | Skeletons with tower shields march ahead, blocking shots and spearing enemies. |
| Stampede | Tank | 1st, choice 5 | 14 s | - | 9 beasts, and the herd turns and charges back. | A line of spirit beasts tramples everything ahead. |
| Spirit Swarm | Healer | 1st, choice 1 | 14 s | - | Releases 8 sprites. | Release healing sprites. |
| Phoenix | Healer | 1st, choice 2 | 22 s | - | Revives at full health, and the phoenix keeps fighting. | A phoenix follows your cursor, shooting and healing, and revives anyone who goes down. |
| Soul Lantern | Healer | 1st, choice 3 | 18 s | - | Lasts 9 s, and the linked player can't go down. | A lantern links to an ally, healing them and burning nearby enemies. |
| World Tree | Healer | ultimate | 80 s | - | Lasts 16 s with 8 sprites. | Grow a tree that heals and slows around it and sends healing butterflies. |
| Bat Swarm | Dps | 1st, choice 1 | 12 s | - | Releases 14 bats. | Release bats that hunt enemies. |
| Hydra | Dps | 1st, choice 2 | 18 s | - | A fourth head. | A three-headed hydra spits venom at enemies. |
| Alpha Call | Dps | 1st, choice 3 | 14 s | - | Also summons 3 wolves for 6 s. | Your minions bite faster, then lunge for triple damage. |
| Legion | Dps | ultimate | 70 s | - | Summons 7 wolves and 7 bats. | A blood moon: wolves and bats circle you, you run faster and your minions frenzy. |

### Knight

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Shield Wall | Tank | 1st, choice 1 | 12 s | - | Bullets that hit the shield are reflected back as your own. | Raise a wall of steel plates that blocks bullets at your front. |
| Leap Slam | Tank | 1st, choice 2 | 9 s | - | The landing also erases enemy bullets within 160 px. | Leap to your cursor untouchable, then slam and stun. |
| Cavalry Call | Tank | 1st, choice 3 | 18 s | - | Two more lancers ride outside the first pair, and the guard lasts 5 s. | Two lancers charge across the map, shoving and slowing enemies. |
| Last Stand | Tank | ultimate | 70 s | - | You take 5% of the damage instead of 10%. | Plant yourself behind a wall of steel plates that blocks every bullet in front. |
| Consecrate | Healer | 1st, choice 1 | 14 s | - | Two more crosses fly out at 22 degrees either side (each target is hit once). | Throw a Holy Cross that heals and speeds allies and hurts enemies. |
| Guardian's Oath | Healer | 1st, choice 2 | 15 s | - | Charges up to 800 px. The ward is wider, follows you, and stands a downed ally back up at 25% health. | Haul an ally to safety and protect everyone in a ward of light. |
| Lay on Hands | Healer | 1st, choice 3 | 18 s | - | Also heals everyone else near them for half as much. | Heal the most hurt ally, or revive a downed one. |
| Hallowed Blade | Healer | ultimate | 80 s | - | Three swords fall: one at your cursor and one either side. | A giant holy sword lands, healing allies and burning enemies for a while. |
| Bladed Dive | Dps | 1st, choice 1 | 10 s | - | Each kill heals you 5% of your health. | Rush forward untouchable with a spinning blade; a kill resets the cooldown. |
| The Gauntlet | Dps | 1st, choice 2 | 9 s | - | The Challenge lasts 8 s, and the charge's impact stuns for 1 s. | Challenge an enemy and charge it; kill it in time to reset the cooldown. |
| Caught Steel | Dps | 1st, choice 3 | 16 s | - | Holds up to 18 shards. | Catch passing bullets as steel, then hurl them back. |
| Drawbridge | Dps | ultimate | 75 s | - | The bridge stands for 8 s. | A drawbridge crushes the lane ahead, then blocks enemies and shots. |
| Warhorn Volley | Dps | ultimate | 70 s | - | The volley is 40% wider and the fire burns for 8 s. | A volley of fire arrows sweeps the field, burning and pinning enemies. |

### Ninja

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Shadow Clone | Tank | 1st, choice 1 | 14 s | - | Every clone has double the health. | A wall of clones that every enemy attacks. |
| Smoke Bomb | Tank | 1st, choice 2 | 14 s | - | Enemies inside the smoke are also slowed by 40%. | A smoke cloud that slows enemy bullets and shields allies. |
| Ceremonial Armor | Tank | 1st, choice 3 | 2.5 s | - | A plate that breaks bursts for 30 damage to enemies within 60 px. | Armour plates circle you, blocking bullets and shoving enemies. |
| Thousand Clones | Tank | ultimate | 70 s | - | Twenty clones. | Twelve clones appear all over the map and draw every enemy. |
| Dragon's Dogma | Tank | ultimate | 70 s | - | Hunts for 7 s, and every enemy it devours heals you 3%. | A spirit dragon hunts across the arena, devouring enemies and their shots. |
| Healing Dash | Healer | 1st, choice 1 | 15 s | - | The sprint lasts 4.5 s. | Sprint, leaving footprints that heal you and allies. |
| Substitution | Healer | 1st, choice 2 | 12 s | - | You and your ally are both invulnerable for 1 s after the swap. | Swap places with an ally and shield them. |
| Spirit Ward | Healer | 1st, choice 3 | 18 s | - | The ward follows you. | Plant a talisman that absorbs damage for allies, then heals them. |
| Blossom Storm | Healer | ultimate | 80 s | - | Lasts 8 s. | Petals heal the whole party and erase enemy bullets. |
| Shadow Step | Dps | 1st, choice 1 | 7 s | - | +1 charge. | Teleport behind an enemy and strike it hard. |
| Vanish | Dps | 1st, choice 2 | 16 s | - | Erases enemy bullets around you when you vanish. | Enemies lose track of you and your next hit deals double. |
| Blade Fan | Dps | 1st, choice 3 | 12 s | - | Throws twice as many kunai. | Spinning kunai cut enemies around you, then fly out in a ring. |
| Death Lotus | Dps | ultimate | 65 s | - | Strikes up to 14 enemies. | A flurry of cuts on up to 8 enemies near your cursor. |

### Hemomancer

| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |
|---|---|---|---|---|---|---|
| Blood Shell | Tank | 1st, choice 1 | 9 s | - | The burst also erases enemy bullets within 160 px. | Hold to wrap yourself and allies in a bubble that blocks bullets. |
| Blood Wall | Tank | 1st, choice 2 | 8 s | - | Enemies against the wall take double and are slowed 40%. | Toggle a wall of blood behind you that blocks bullets and hurts enemies. |
| Exsanguinate | Tank | 1st, choice 3 | 8 s | 10% of max health | Also slows the enemies it drains by 50% for 2 s. | Yank every nearby enemy in, stunned, and drain them. |
| Vein Tithe | Tank | ultimate | 75 s | - | Lasts 40 s and costs 25% less. | Bind every enemy on screen at the cost of your health. |
| Bloodbath | Tank | ultimate | 75 s | - | The pool spreads to 220 px and gives back 35% of the bleed. | Flood the floor with blood that slows and bleeds enemies and heals you. |
| Blood Gift | Healer | 1st, choice 1 | 8 s | 25% of current health | Also heals everyone else within 200 px of them for half as much. | Pay health to heal the most hurt ally twice as much. |
| Blood Freeze | Healer | 1st, choice 2 | 14 s | 25% of max health | Frozen enemies take 25% more damage. | Freeze every enemy near you at a cost of health. |
| Martyr | Healer | 1st, choice 3 | 16 s | 40% of max health | You're invulnerable for 2 s after the sacrifice. | Pay health to revive a downed ally, or ward the most hurt one. |
| Sanguine Rain | Healer | ultimate | 80 s | - | Lasts 14 s and heals 4% a tick. | Rain healing circles that follow your allies. |
| Arterial Spray | Dps | 1st, choice 1 | 8 s | - | The spray also washes away enemy bullets it touches. | Open a vein: a jet of blood hurts and bleeds everything in front of you. |
| Blood Debt | Dps | 1st, choice 2 | 16 s | - | Kills repay 50% more. | Borrow health to heal on hit, and pay it back with kills. |
| Vein Rush | Dps | 1st, choice 3 | 6 s | 10% of max health | The trail bursts after 1 s for 20 to everything on it. | Rush toward your cursor untouchable, bleeding enemies on the way. |
| Crimson Harvest | Dps | ultimate | 65 s | - | Hold 8 s, and 25% more blood. | Rip blood from every enemy into a ball, then throw it as a huge blast. |
| Thorn Bloom | Dps | 1st, choice 5 | 8 s | 12% of max health | Tendrils jump 5 times. | Burst blood needles all round that chain between enemies. |

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
