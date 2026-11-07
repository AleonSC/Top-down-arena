# Top-Down Arena

A co-op top-down bullet hell for **1 to 3 players** over the network, made in **Unity 6**.

**Party size scaling:** solo, enemies have 85% health (Normal difficulty); with two players, 100%; with three, regular
enemies have 120% health and waves have 25% more of them, and bosses have 150% health. Bosses also have 1.2x to 5.5x
their base health, set per boss so each fight lasts about 60-120 s (`BossHpMult` in Balance.cs; the Warden and Hive
Queen 2.5x), and every boss has 10% more on top (`BossHpBonus`), and every enemy bullet hits 15% harder than its
listed damage. **Enemy damage grows 3% a wave** (Normal: 1.3x at wave 11, 2.5x at wave 50), on everything that hurts
you. The host picks a [difficulty](#difficulty) in the lobby: Easy, Normal, Hard, Nightmare or Custom. Pick a role,
then survive endless waves of enemies with a boss every 5th wave.

There are no art assets: everything is drawn in code, and the game sets itself up when you
press Play, so there are no scenes or prefabs to wire together.

New to the code? Read [`HANDOFF.md`](HANDOFF.md) for the architecture, tools, testing and audit notes.

## Opening the project

1. Install [Unity Hub](https://unity.com/download) and the **Unity 6.6** editor (6000.6.7f1; the exact version is pinned in `ProjectSettings/ProjectVersion.txt`).
   When installing, tick **Windows Build Support** so you can make a `.exe`.
2. In Unity Hub: **Add → Add project from disk** and pick this folder. If Hub says the exact
   editor version isn't installed, pick any Unity 6 version you have.
3. When it finishes importing, press **Play**. (The project creates `Assets/Scenes/Main.unity`
   for you the first time it opens.)
4. Commit the `.meta` files Unity generates on first import; Unity needs them to keep
   references stable.

## Roles and classes

The lobby is one calm blue CRT screen with a 36 px margin and soft, darker edges: the party column is on the left, the big loadout screen on the right washed with a soft gradient in your role's colour (it eases when you pick another role), and the tabs above it are small, faded caps with the open one a lit pill. Every option is a digital key (outlined, inverse video under the mouse or when picked; locked ones are dashed with a padlock), and the colour lives in the party cards, the live preview, the stat bars and the pixel icons. In it each player picks a **role** (Tank, Healer, DPS), a **class** (Soldier, Elementalist, Summoner, Knight,
Ninja, Hemomancer), then one of that combination's three weapons and three Space abilities. Weapons and abilities are
unique to their role and class.

- **Soldier**: guns and gear, full health.
- **Elementalist**: spells (fire, frost, lightning, arcane), 85% health.
- **Summoner**: minions that come with your weapon (they stay between waves), 90% health.
- **Knight**: melee weapons that cut small bullets out of the air, armoured: 115% health (see below).
- **Ninja**: thrown blades and quick melee with tricks and misdirection; fast (+12% move speed), 85% health (see
  below).
- **Hemomancer**: blood spells that drain and heal; every Space ability costs health, and it hits harder the lower its
  health; full health (see below).

| | Soldier | Elementalist | Summoner |
|---|---|---|---|
| **Tank** (240 HP, enemies prefer you) | Weapons: Scattergun (double damage and a shove point-blank), Flak Cannon (airbursts at your cursor, shredding enemy bullets), Riot Repeater (rounds suppress: enemies hit shoot slower)<br>Abilities: Barrier, Roar, Bulldoze | Weapons: Flamethrower (sets enemies burning), Frost Lance (an icicle out from you that splinters sideways, slowing), Storm Hammer (a spinning hammer that electrifies what it hits)<br>Abilities: **Aeroaegis** (a wind vortex round the player your cursor is nearest: enemies can't enter, bullets are reflected), **Frost Nova** (freezes nearby enemies), **Flame Wall** (burns enemies, blocks bullets) | Weapons: Golem Staff (hold to lob a crushing boulder, + bullet-blocking Golem), Bone Guard (darts that cage enemies, + 2 orbiting skeletons that shoot the caged), Bear Totem (melee claws that cause bleeding, + Bear)<br>Abilities: **Golem Wall** (3 basalt golems that block bullets until they are broken; recasting replaces them), **Earthquake** (you slam the ground, then a Burrower breaks out at your cursor and tunnels through the enemies, biting each one it passes under), **Force Pact** (12 s cooldown: takes the regular enemy nearest your cursor (not bosses or boss adds) out of the wave; after a second of violet chains it comes back as your **thrall**: an enemy of that type on your side that fights within 240 px of you with its own attack at 75% damage, with 2.5x that type's health + 15 per wave. Enemy shots and bodies hurt it, and a fallen thrall returns 15 s later as the same type (a ring of links round your feet fills while it's away). No enemy to take: it fizzles and the cooldown is refunded. Cast again on another enemy and the pact moves to that type; mastered, you keep two thralls: the last two types you chose) |
| **Healer** (140 HP, healing aura) | Weapons: Repulsion Cannon, Revolver, Mender's Bow<br>Abilities: Mend Pulse, **Adrenaline Shot** (instant, 14 s cooldown: injects the player closest to your cursor, you included, or just you when alone: 8 s of +30% move speed, +40% fire rate and 30% less damage taken; fire rate doesn't stack with Overdrive, the bigger wins; a heartbeat ring pulses round them while it lasts), Deployable Medkit (thrown at your cursor, 14 s cooldown: bursts into 6 healing orbs worth 15 health each that anyone can pick up for 6 s; a downed ally an orb reaches is picked up at 20% health) | Weapons: Radiant Orb (heals allies it passes), Chain Light (a cone of light, its damage shared between what's in it), Nimbus (a storm cloud follows your cursor, raining healing on allies under it; click calls lightning on the spot beneath it)<br>Abilities: **Healing Rain** (a rain at your cursor for 6 s: heals allies inside 8 HP a second, slows enemies 30%), **Energy Ward** (a bubble of energy around you and allies within 300 px that soaks 60 damage for 8 s; it shrinks and turns green as it wears, with a green core growing inside that is the heal charging up, and when it breaks or runs out half of what it soaked comes back as healing), **Lumen Ward** (20 s cooldown: a huge cone of light along your aim for 6 s; it moves with you, but keeps the direction it was cast in. Allies inside heal 9 HP/s and enemy bullets inside burn away; enemy bullets everywhere else on the map slow to a stop and fly again when it ends) | Weapons: Sprite Staff (a firefly that seeds up to three enemies, + 2 healing sprites; seeded kills free more), Treant Rod (a root lash that mends allies it touches; the Treant roots and a root to you heals allies and slows enemies crossing it), Spirit Chalk (hold and drag to draw a line of spirit ink that burns enemies, heals allies on it and destroys bullets; draw a loop round enemies to trap them)<br>Abilities: **Spirit Swarm** (5 healing sprites), **Phoenix** (fights, and instantly revives a downed ally), **Soul Lantern** (a lantern lands beside the ally nearest your cursor, or you when alone, and links to them for 6 s: it follows them, heals 8 health a second and flames the nearest enemy within 260 px for 7 every 0.6 s; 18 s cooldown) |
| **DPS** (95 HP) | Weapons: Twin Rifle (streams cross at your cursor for double), Railgun (leaves its path charged for a second), Burst SMG (spins up: tighter and harder the longer you fire)<br>Abilities: Dash, Cluster Grenade, Mark for Death | Weapons: Fireball (big, rolls through enemies, burning them), Arc Lightning (hold to charge a strip of lightning), Ice Shards (bank in orbit, launching at enemies that come near)<br>Abilities: **Meteor** (a fireball streaks in over the warning circle, then 150 damage), **Blizzard** (slowing storm), **Tsunami** (a wave that soaks enemies: slowed, and lightning arcs to every wet one) | Weapons: Wolf Whistle (darts mark the prey the 2 wolves hunt; every wolf biting at once is a takedown), Wisp Lantern (3 orbiting wisps you send out as your shots), Imp Caller (imps wait by you; tap to send them, hold to merge them into one big blast)<br>Abilities: **Bat Swarm** (8 bats circle you for a moment, then peel off and dive at enemies), **Hydra** (a three-headed hydra rises at your cursor for 8 s, each head spitting venom at a different enemy), **Alpha Call** (Blood Scent: your minions frenzy, building a stack with each bite, and lunge at 5) |

**Wildcard** (a fourth role, locked until you have won a run as a Tank, a Healer and a DPS; a co-op win counts): it
has no class. You pick any class's DPS weapon, any DPS Space ability for the usual Space key, and a second DPS ability
that sits on Q, E or Shift (your choice, changeable in the Tab menu like any bonus ability). Your ultimate is the DPS
ultimate of the first ability's class. 130 health, no role passive (no taunt, no aura). The price: 10% less damage
and 25% slower ability cooldowns (ultimate included). Class passives don't come with borrowed gear (a Hemomancer ability
costs its health but there is no Blood Price bonus); a weapon's own cards still appear, and borrowed abilities earn
mastery as usual. It can't be picked in the lobby or the Practice Range while locked. Bots keep their normal roles
unless you give one the Wildcard role by hand. Design notes: [`Docs/Wildcard.md`](Docs/Wildcard.md).

Generic upgrades (damage, fire rate, cooldowns, Second Charge, legendaries) work for every class; minions
use your damage and attack-speed bonuses too.

**Healers** (every class): the healing aura heals allies within 170 px for 9 HP/s and you for 1 HP/s; each
**Sanctuary** card adds another 9 HP/s for allies (and 40% reach) and +1 HP/s for you. A Healer's healing past
someone's full health becomes a shield (up to 20% of their max health, for 4 s; the aura's own ticks don't count).
Healer abilities work alone too: the **Adrenaline Shot** goes to you, **Spirit Ward** takes your own
damage, **Martyr** with no one down wards the most hurt ally (you, alone) for 12 s so going down puts them straight
back up at 50%, and a **Phoenix** brings anyone back, you included (the run doesn't end while one is out).

### The Knight

Knights fight up close. Holding fire **swings** instead of shooting: each swing hits every enemy in its arc
(the Rapier only the nearest), and **cuts small bullets out of the air** in front of you. Big orbs and mines get
through (a mastered Whirlwind stops those too), and bosses' red aimed shots always do. Knights have 115%
health to make up for standing in the thick of it, and deal about a third more damage than the ranged
classes.

| | Weapons | Space abilities | Ultimate |
|---|---|---|---|
| **Tank** | **Warhammer** (hold to charge, let go to slam a fixed circle ahead (160 px across, the same size however long you hold): up to 80 damage and a 0.9 s stun at full charge. A faint outline shows where it lands while you charge, and a disc grows inside it as the charge builds), **Lance** (a heavy jab: a 180 × 88 px box straight ahead that slides out, shoves enemies 140 px back and stuns them for 1 s), **Halberd** (a long, thin 18° blade swept across 80° in front of you, hitting each enemy once a sweep) | **Shield Wall** (seven steel plates across your front 120°, blocking every bullet, red aimed shots included; each shot wears down the plate it hits (400 damage shared between the plates), a broken plate leaves a gap, and the wall has no timer: it stays up until every plate is broken), **Leap Slam** (leap up to 320 px to your cursor, untouchable in the air; 50 damage and a 1 s stun where you land. With Visual effects on, the knight swells and rises with the shadow left on the floor, a hexagon closes in on the landing radius, and the landing sends out hex pressure rings, steel plates and sparks and leaves a hex rune; each stunned enemy sits in a hex cage whose ring drains over the stun), **Cavalry Call** (18 s cooldown: two lancers ride in at your sides and charge 1,100 px across the map along your aim; each enemy they meet takes 35, is shoved 150 px out of the lane and slowed 45% for 3 s, Heavy enemies take a 45 px shove and a 20% slow and bosses are only slowed; you take 20% less damage for 3 s. With Visual effects on, gold-and-ivory lancers drop in on shafts of light, with a lane band, bow wave and dust) | **Last Stand** (70 s): you plant yourself and brace nine steel plates in a crescent 120 px out (178° wide, facing your cursor; it follows your aim at 1.7 rad/s, so you commit to a side). It takes 0.5 s to plant, then it stops every enemy bullet from the front, red aimed shots included. Your teammates' shots pass out through it, and you take 10% of the damage of every shot it stops (shots from behind or the sides hit you in full). You can't move or use a dash, leap or lunge while planted. There's no timer: it stands until you go down or press the ability again, and the cooldown starts when it comes down. It also comes down between waves. With Visual effects on, three stakes drive in behind you, the plates slam down centre-first, each kicks and flashes where a shot lands, and they crack and rattle as your health runs down |
| **Healer** | **Quarterstaff** (wide sweeps that knock enemies back and bat enemy bullets back at them; each enemy hit heals you and allies near you for 2), **Lightbringer** (hits fill a light gauge; full, the next crescents heal three times as much; sword cut plus a crescent of light that heals allies it passes), **Censer** (leaves a healing incense trail that spoils enemy aim; a flail whirled all the way round; each spin heals allies near you for 3) | **Consecrate** (a Holy Cross flies out along your aim, growing as it goes: allies it crosses heal 35 and run 30% faster for 4 s; enemies take 25, are slowed 40% for 3 s and knocked back; hits each once), **Guardian's Oath** (charge to the player nearest your cursor, downed or not, and drag them clear; a ward of light then halves the damage everyone inside takes for 5 s and revives downed allies twice as fast), **Lay on Hands** (heal the most hurt ally within 300 px for 70, or revive them at half health) | **Hallowed Blade** (80 s): a giant sword falls at your cursor for 200 damage around it, then blesses the ground for 8 s (allies heal 15 HP/s) |
| **DPS** | **Twin Blades** (very fast alternating slashes; 10 hits in a row prime a dash-strike: press the weapon-action key to lunge through enemies for triple), **Greatsword** (each sweep leaves a bullet-stopping crescent for 0.5 s; huge 170° sweeps), **Rapier** (thrusts at the nearest enemy; each hit in a row on it deals 10% more, up to +60%; each thrust skewers up to 3 enemy bullets on its line, each adding to the streak) | **Whirlwind** (spin for 2 s hitting everything within 110 px every 0.2 s and slicing small bullets), **Lunge** (dash 260 px toward your cursor hitting everything you pass for 35; two charges), **Battle Fury** (6 s: swing 40% faster, +25% damage) | **Blade Tempest** (65 s): for 6 s every swing also hurls three piercing sword beams |

How shot upgrades work on a melee weapon (the cards say so when you're a Knight):

- **Damage, fire rate (swing speed), crits, lifesteal, legendaries** work as usual.
- **Piercing Rounds**: +10% reach per stack (and sword waves pierce one more).
- **Barrage**: every swing also throws a **sword wave** per stack, fanned out to the sides.
- **Incendiary, Cryo, Shock and Explosive Rounds**, Swarm Rounds, Collapse Rounds and Graviton Rounds apply to
  every melee hit. **Ricochet** flings bounce bolts off your hits (see the mods table). **Homing and Velocity
  Rounds** only change your sword waves (Velocity's +15% damage counts for everything).

### The Ninja

Ninjas are quick (+12% move speed) and lightly protected (85% health). They mix thrown weapons with fast melee
(the Kusarigama, Bo Staff and Katana swing like a Knight's weapons; the Bo Staff and Katana cut small bullets).

| | Weapons | Space abilities | Ultimate |
|---|---|---|---|
| **Tank** | **Kusarigama** (throw the chain-sickle 280 px to hook the first enemy it hits; press the weapon-action key to reel yourself in to it with a heavy, stunning slash), **Bo Staff** (a chain that never times out: a cone slash, a jab, then a full spin, cutting small bullets; press the weapon-action key to shove a bar of staff ahead of you, which knocks enemies back and starts the chain over), **Fuma Shuriken** (a giant star thrown in an oval loop 380 px out in front of you and back, cutting through everything both ways) | **Shadow Clone** (a wall of 9 afterimage clones for 7 s that every enemy aims at; each soaks 50 damage and never fights; walk into them to shove the wall; capstone: double health), **Smoke Bomb** (a 140 px cloud for 5 s: enemy bullets inside slow to half speed, allies inside take 30% less damage), **Iron Body** (2 s of 80% less damage, then a 50-damage shockwave) | **Thousand Clones** (70 s): five clones surround you for 8 s, drawing every enemy's fire, then burst |
| **Healer** | **Medicine Darts** (fast needles that dose what they hit: stacking poison on enemies, stacking healing over time on allies), **Spirit Fan** (alternates blowing enemies away and drawing them in; a short gust that heals allies in it and blows small enemy bullets back), **Ofuda** (charms lie at your cursor as wards that burst under enemies, healing allies near; slow homing paper charms that burst for 25 and heal allies near the burst for 8) | **Healing Dash** (sprint 70% faster for 3 s, leaving glowing footprints that last 6 s and heal 12 HP a second, you and your allies, while you stand near one; overlapping footprints don't stack; capstone 4.5 s sprint; Morning Dew makes them heal 50% more and 25% bigger), **Substitution** (swap places with the ally nearest your cursor and shield them for 60; alone, slip toward your cursor and shield yourself), **Spirit Ward** (a talisman at your cursor for 8 s that takes the damage allies near it would take, up to 150; when it breaks or ends, a quarter of what it took heals them) | **Blossom Storm** (80 s): for 5 s the whole party heals 20 HP/s and enemy bullets near every ally are erased |
| **DPS** | **Shuriken** (tight fans of 3 stars that stick in what they hit and come back after 5 s for another hit; press the weapon-action key to recall them sooner), **Katana** (swipe, swipe, lunge, always in that order: two 110° slashes then a 260 px lunge (you step 110 px forward with it) through everything in its line for 2.25×; tap ahead to queue the next step; every enemy a step hits builds a strike meter that drains when you stop hitting, and at full you hold the weapon-action key, aim at any enemy and let go to cut across the arena and land behind it for 120, 60 to everything on the way; a kill keeps the meter full and builds a chain that stops the meter draining, and five chains empower the next strike to 240 and 120), **Kunai** (every throw leaves a mark: press the weapon-action key to blink there for a double hit; slow, heavy knives, +50% damage to enemies that aren't targeting you) | **Shadow Step** (teleport behind the enemy nearest your cursor, up to 450 px away, and strike it for 80), **Vanish** (for 3 s enemies lose track of you and shoot at the smoke bank where you vanished; your next hit deals double), **Blade Fan** (12 kunai spin around you for 4 s, cutting enemies they touch for 18, at most once every 0.5 s each, and erasing enemy bullets; press it again, or wait, and they fly out in a ring, 18 each; 12 s cooldown) | **Death Lotus** (65 s): strike up to 8 enemies near your cursor in a flurry of cuts, 100 each (the arena darkens, each target takes three white-hot crimson cuts and leaves a crimson lotus stain, and the cuts show one after another over about a second) |

### The Hemomancer

Health is the Hemomancer's resource. It has full health and normal speed, and:

- **Every Space ability costs health**, a share of your max health shown on the card (Blood Gift: of your
  current health). A cost can't take you below 1 HP: at 1 HP the ability still goes off, but anything that
  scales with what you paid is worth nothing. Cooldowns are shorter than other classes' to make up for it.
- **Blood Price:** you deal more damage the more health you're missing, up to **+40%** at 10% health.
- **All your hits heal you for 3% of the damage they deal.**

| | Weapons | Space abilities (cost) | Ultimate |
|---|---|---|---|
| **Tank** | **Blood Lash** (a 130 px, 110° whip of blood, 24: its tip cracks for double and taunts, closer hits are flung out to the tip; hits build Clot, 4% less damage taken each, and at full Clot the next lash bursts all round you), **Clot Bolt** (a heavy bolt, 26, that leaves a pool of blood slowing enemies 40% for 3 s; after 1 s it congeals, sticking enemies in it for 1 s, and you take 15% less standing in your own pool), **Leech Tendrils** (hold fire: blood tendrils grasp up to 3 enemies in a 70° arc in front of you for as long as you hold on; they're stunned and drained slowly, and 25% of the drain heals you; spare tendrils pile onto what's held, so a lone boss takes all three) | **Blood Shell** (hold: a big bubble of blood, 96 px out, round you. It stops every enemy bullet, red aimed shots included, and enemies can't walk in; you and your allies can stand inside. Each bullet it stops costs you 7 health and heals every ally inside for 4. It stands until you let go or your health falls to a third, when it bursts and flings nearby enemies back; the 9 s cooldown starts then. Hardened Blood makes each hit cost 25% less and heal 50% more), **Crimson Pact** (10%: every enemy targets you for 4 s, then the damage you took bursts back out at 150% within 220 px), **Exsanguinate** (10%: pull enemies within 220 px in and drain 20 from each into you) | **Vein Tithe** (75 s, was Blood Rite): a blood artery grows from you to every enemy on screen and holds it for 30 s, so it can't move or shoot (enemies still spawning, and any that turn up later, aren't held). You pay 0.25% of your max health a second for each enemy held (a boss costs four times that) and deal 40% less damage; the drain gives no hit grace and can take you down. It ends at 30 s, or when you go down, and every artery lets go |
| **Healer** | **Transfusion Darts** (darts that give a hurt ally 20 health for 4 of yours; each enemy hit steals a sliver, 0.5), **Sanguine Orb** (orbs that home in at the last moment and circle an enemy to cover it in blood, 16 plus a bleed stack, charging a heal reserve; with 25 reserve, an orb that touches a hurt ally coats them in healing blood, 30 over 4 s), **Vein Tether** (a real beam: it hurts whatever is on your aim line, so keep it on your target; 40% of its damage heals, split between you and the nearest ally) | **Blood Gift** (25% of current health: the most hurt ally heals twice what you paid; alone, a shield for you), **Siphon Field** (10%: a 140 px field at your feet for 6 s, enemies inside lose 10 HP/s and allies heal 10 HP/s), **Martyr** (40%: revive the downed ally nearest your cursor, anywhere, at 50%; if no one is down, the most hurt ally heals twice what you paid) | **Sanguine Rain** (80 s): the Hemomancer pulls 12% of their health into a blood orb (1.8 s), then rain circles follow every ally for 10 s, healing each 3% of their max health every 0.5 s; it drains the caster 3% a second and ends when pressed again, when the time is up, or at 15% health|
| **DPS** | **Blood Bolts** (fast bolts, 9; the fifth in a row in one enemy makes it hemorrhage, bursting for 36 within 80 px), **Implosion** (click an enemy: its blood collapses inward, 34, hurting enemies against it for 15 and pulling them in; fires only with your cursor on an enemy, up to 480 px away), **Bone Spear** (stays stuck where it stops: walk over it for its 2 health back; a heavy spear, 60, that pierces 3; each throw costs 2 health) | **Blood Boil** (20%: enemies within 260 px burst after 1 s for three times what you paid), **Frenzy Pact** (5%: for 6 s fire 50% faster and heal 10% of the damage you deal, but lose 3 HP/s), **Hemo Nova** (10%: a ring of 16 blood bolts, 20 each, each that hits heals you 3) | **Crimson Harvest** (65 s): blood is torn out of every enemy in the arena (20 damage each, over about 1 s, and your hits heal you as usual) into a ball floating over you; each Grunt adds 1 blood, a Heavy 3, a boss 8, and the ball's area follows the blood. You move at 60% speed while it's up. Press the ability again to throw it at your cursor (up to 900 px; it throws itself after 5 s): it lands for 60 + 12 per blood at the core, falling to half at the edge of a 150 + 50·√blood px blast, flinging enemies (not bosses) and slowing them 35% for 4.5 s. The Blood Price cap is the normal +40% |

Capstones (level 5): Blood Shell also erases bullets within 160 px · Crimson Pact: 30% less damage while it
lasts · Exsanguinate also slows what it drains by 50% for 2 s · Blood Gift also heals others within 200 px of
them for half · Siphon Field follows you · Martyr: you're invulnerable for 2 s after · Blood Boil's bursts also
hit enemies within 80 px · Frenzy Pact: no health drain · Hemo Nova's bolts pierce 2 · Vein Tithe lasts 40 s and costs 25% less ·
Sanguine Rain lasts 14 s and heals 4% a tick · Crimson Harvest holds the ball 8 s and tears out 25% more blood.

Players can pick the same role if you want. Everyone also slowly regenerates health (0.25 HP/s on Normal; see
[Difficulty](#difficulty)).

Enemies sometimes drop **health orbs**: small green ones heal 15 HP (18% chance), and rare large ones
with a gold ring heal 60 HP (3% chance). Bosses always drop a large orb plus four small ones. Orbs drift
toward a hurt player who gets close, are left alone by players at full health (so your ally can grab
them), and blink before vanishing after 12 seconds.

### Weapons

Each role picks one of three starting weapons in the lobby. Damage, fire rate and Piercing upgrades work
with all of them.

**Extra projectiles** (Multishot, and +2 during Artillery Assault) upgrade each weapon in its own
way instead of adding more bullets in a line. The Multishot card shows what it'll do for
your weapon. Per extra projectile:

| Class | Weapon | Base | Each extra projectile |
|---|---|---|---|
| Tank · Soldier | **Scattergun** | Short-range spread of 5 pellets (7); within 120 px each does double and shoves the enemy back | +2 pellets and a 15% tighter cone |
| | **Flak Cannon** | Shells fly over enemies to your cursor and burst in the air (30 within 80 px), destroying enemy bullets in the burst | The burst scatters 2 bomblets that pop where they land |
| | **Riot Repeater** | Steady single shots (11) with a little spread; each round that hits suppresses the enemy: it shoots 8% slower, up to 50% (25% for bosses), fading 25% a second | Fires in bursts: +1 round per burst, then a longer pause |
| Tank · Elementalist | **Flamethrower** | A short stream of fire (about 190 px): 2 flames every 0.06 s that hit the first enemy for 3 and carry on through the rest for 1, setting them burning (5 a second for 3 s, renewed by every hit) | +1 flame a burst and 25% more reach |
| | **Frost Lance** | A 230 px icicle juts out from you (18), then splinters off its sides into enemies up to 60 px beside it (50%); both slow 30% | +30 px icicle and +25% damage, splinters fly 15 px further |
| | **Storm Hammer** | A spinning hammer (40) that electrifies the enemy it hits: it arcs to 3 enemies round it three times over a second (5 each) | +15% hammer damage and 1 more enemy arced to |
| Tank · Summoner | **Golem Staff** | Hold to charge, let go to lob a boulder over enemies: 140 px and 20 for a tap, 460 px and 40 at full; crushes within 75 px; plus a small Titan-like Golem (rises from rubble, wind-up slam, soaks about 400 bullet damage) | +1 Golem (up to 3) |
| | **Bone Guard** | Darts (7) that stick: the third in one enemy within 3 s cages it for 1.5 s (not bosses); 2 orbiting skeletons block bullets and shoot caged enemies | +1 Skeleton Guard |
| | **Bear Totem** | Melee: two quick claw swipes in front of you (85 px, 11 damage each), one claw on each hand (the left rakes across, then the right rakes back), that make enemies bleed (30% of the hit per second for 3 s, stacking up to five hits; each two hits leave another set of claw scars on the enemy), plus a Bear | +15 px reach, 25% more bleed, and the Bear's bites hit enemies around its target |
| Healer · Soldier | **Repulsion Cannon** | Blasts enemies away in a 70° cone (240 px, 6 damage, 90 px push; bosses and heavies don't budge); enemies it hits shed tiny health orbs (heal 6, at most every 1.5 s each) | +20° wider, +2 damage and +30 px of push |
| | **Revolver** | Six hard-hitting rounds, the last one heavy (double damage). **Tap** for perfectly aimed shots (one every 0.7 s, fast flying); **hold** to fan the hammer: after 0.2 s it empties the cylinder, a round every 0.07 s, each within 5° of your aim. Reloads in 1.2 s when empty, and slides rounds back in one at a time (every 0.25 s) once you stop shooting for 0.8 s | +2 rounds in the cylinder, loaded as heavy rounds |
| | **Mender's Bow** | **Hold to draw, release to fire** (you move at 85% while drawing). A tap looses a weak arrow (8 damage, heals 5); a full draw (0.9 s) hits for 40, flies at 950 px/s, pierces 2 and heals an ally it passes for 20. Let go within 0.25 s of full draw for a **perfect shot**: +25% (50 damage, heals 25). Holding longer keeps it at full; it never fires by itself | +1 arrow nocked: a full draw also looses an extra arrow (half damage) in a tight fan; taps stay single |
| Healer · Elementalist | **Radiant Orb** | Hold fire to cast an orb of light (20 px) out to your cursor, up to 520 px away, and keep it there while you hold: drag it through enemies (8 damage to each it touches, 5 times a second) and over allies (4 healing, 5 times a second); let go and it fades | +1 Light Orb (Barrage adds one too) that circles your orb while it's out (you otherwise), hurting enemies and healing allies it touches |
| | **Chain Light** | A 320 px cone of light (about 70° wide; Magnum Rounds widen it up to 100°, Homing Rounds narrow it 15% and strengthen it 10% a stack): 40 damage shared between every enemy in it (one alone takes it all), light arcing to each. Barrage adds cones edge to edge (60% damage each, like other weapons' Barrage copies), all within 200°: never a ring round you | +25% damage and a 15% longer cone |
| | **Nimbus** | A small storm cloud floats over your cursor, drifting after it at 600 px/s and staying within 380 px of you. Click to call lightning onto the spot beneath it (16 to each enemy in a 40 px blast, every 0.6 s). Rain falls in a 70 px circle under it and heals every ally standing in it, 8 a second | +1 lightning strike a click, landing in a ring round the cloud |
| Healer · Summoner | **Sprite Staff** | A firefly (7) that seeds the enemy it hits, then hops to the nearest enemy that isn't seeded yet (up to 2 hops within 300 px, so one shot can seed three), plus 2 healing sprites; killing a seeded enemy frees another sprite for 6 s (up to 4 more) | +1 Sprite |
| | **Treant Rod** | A thorny root whips out from you along your aim (250 px, 15 damage) and snaps back, hitting everything on the line and healing allies it touches (10); plus a Treant that roots where it stands and blocks bullets; a root from you to it heals allies crossing it (8/s) and slows and stings enemies crossing it (50%, 6/s); it walks after you past 360 px | The Treant grows 30%: blocks more, hits harder, reaches farther |
| | **Spirit Chalk** | Hold and drag to draw a line of spirit ink (the chalk follows your cursor at up to 700 px/s, within 450 px of you, and the line is drawn as one smooth curve for everyone, head and all; 600 px of line at a time: **draw more and the start of the line rubs out as you go**, so you never run dry; a dab also fades slowly over 10 s). It burns enemies touching it (26 a second) and slows them 30%, and heals allies standing on it (10 a second). Enemy bullets pass through plain ink; the rare card **Warding Ink** makes it destroy bullets that cross it, but each bullet wears the dot it hit down by 1.5 s. **Close a loop** (the line meets itself, round at least about 60 x 60 px) and every enemy inside is **trapped** for 3.5 s: it can't cross the ring, and the ring glows gold. Bosses and mini-bosses can't be trapped. A hole shot through the ring (or the ring fading) lets them go | +20% ink: a longer line before the start rubs out |
| DPS · Soldier | **Twin Rifle** | Twin streams (4 per barrel) from barrels sharing a 60 px width (extra barrels fill it in, they don't widen it; Barrage's copies fire from a narrow 20 px cluster), angled in to cross at your cursor's distance (120 px to full range, about 575 px); hits within 45 px of the crossing are crossfire, for double. Up close the streams are still apart, so they can pass either side of a small enemy: aim at the crossing. Your aim line marks it with an X | +1 barrel |
| | **Railgun** | Slow shots (45) that pierce 3; the path stays charged for 1 s behind the round, hurting anything in it (25 a second) | Throws 2 shrapnel shards sideways at every enemy it pierces |
| | **Burst SMG** | Very fast spray (5) that spins up as you keep firing: over 2 s the spray tightens from 9° to 2° either side and damage climbs to +60%; it winds down in 0.6 s when you stop | +1 bullet per shot (half damage) and a wider spray |
| DPS · Elementalist | **Fireball** | A big, slow fireball that rolls through every enemy in its path (22 each), setting them burning (8 a second for 3 s) | +1 fireball, fanned out |
| | **Arc Lightning** | Hold to charge a slight cone of lightning in front of you (36 px wide at your staff, 120 px at its far end), 160 px long for a tap out to half the arena at full; let go and everything in it is shocked (40, 0.4 s stun) | +25% damage |
| | **Ice Shards** | Each cast puts 3 shards of ice (7) into orbit round you (up to 6); they launch, homing, at enemies within 300 px, one every 0.08 s. With the orbit full, a cast flies at your aim | +1 shard a cast, +2 in orbit, and faster launches |
| DPS · Summoner | **Wolf Whistle** | Darts mark the enemy they hit as prey for 4 s and your 2 wolves hunt it (a fanned or piercing dart that clips an enemy farther from your cursor doesn't move the prey); every wolf biting it within a second is a takedown (20 per wolf, 0.6 s stun) | +1 Wolf |
| | **Wisp Lantern** | 3 wisps orbit you and are your shots: each shot sends one at the enemy nearest your cursor (22), and it comes back | +1 Wisp, and each shot sends one more |
| | **Imp Caller** | An imp every second (up to 5) waits by you; tap fire to send one charging at your cursor (45), or hold to merge them all into one big imp (blast 125% of theirs together, and wider) | +1 Imp, arriving 50% faster |
| Tank · Knight | **Warhammer** | Charge slam: hold to charge (0.9 s), let go to slam a fixed 160 px circle centred about 100 px ahead (it clears small bullets too); 20 damage and a 0.25 s stun for a tap, up to 80 damage and a 0.9 s stun at full charge | +1 aftershock: another slam 0.2 s later, 40 px further out (60% damage) |
| | **Lance** | Heavy jab (36 damage) that hits a box straight ahead, 180 px long and 88 px wide, shoves enemies 140 px back (not bosses or heavies) and stuns them for 1 s (not bosses) | +30 px reach, 24 px wider and +60 px knockback |
| | **Halberd** | Sweep: an 18° blade, 200 px long, swept across 80° in 0.18 s; 30 to each enemy it passes, once a sweep | +30 px reach and a return sweep (60% damage) |
| Healer · Knight | **Quarterstaff** | 150° sweeps (105 px, 18) that knock enemies back 70 px; each swing parries: enemy bullets in its arc fly back at enemies as your shots (12), big orbs too, but never red aimed shots; each enemy hit heals you and allies within 220 px for 2 (up to 8 a swing) | +1 follow-up strike with the other end (60% damage) |
| | **Lightbringer** | 100° sword cut (17) plus a crescent of light (12, pierces 2) that heals allies it passes for 6; every enemy hit fills a light gauge, and at 12 the next swing's crescents heal three times as much | +1 crescent, fanned out |
| | **Censer** | Flail whirled all the way around you (14, 80 px); each spin heals allies within 140 px for 3; moving leaves an incense trail (3 s) that heals allies in it (4/s per puff) and makes enemy shots fired inside it stray | +20 px reach and +1 HP healed per spin |
| DPS · Knight | **Twin Blades** | Very fast alternating 90° slashes (15); 10 hits in a row (each within 1.2 s) prime a dash-strike: press the weapon-action key to lunge 140 px, hitting everything you pass for triple | +1 cut: each swing strikes once more (60% damage) |
| | **Greatsword** | Huge 170° sweeps (60) with 125 px reach; each sweep's edge hangs for 0.5 s, stopping enemy bullets that cross it | +1 sword beam: each sweep launches a piercing crescent (40% damage) |
| | **Rapier** | Narrow 130 px thrusts (18) at the nearest enemy in front; +10% per hit in a row on it (up to +60%); each thrust skewers up to 3 enemy bullets along its line (not red aimed shots), each adding a stack | Skewers 1 more enemy behind the first |
| Tank · Ninja | **Kusarigama** | Throw the chain-sickle straight out (280 px) to hook the first enemy it hits (26); press the weapon-action key to reel yourself in to it and land a heavy slash (52, 0.5 s stun) | +40 px throw and +30% slash damage |
| | **Bo Staff** | A three-step chain with no timer, however long you wait: a 130° cone slash (16, 105 px, knocks back 14 px), a jab (22, 140 px, 28 px wide, 22 px knockback), then a full spin (30, 100 px, 20 px knockback) that starts it over; every swing cuts small bullets. Hold the button and it plays on by itself. Three pips over your head show where you are, and the third pulses when the next click is the spin. The weapon-action key **shoves**: the staff turns sideways and drives out as a bar 100 px wide and 78 px deep (6 damage, knocks enemies straight back 70 px, not bosses or heavies), once a second, and starts the chain over (so click, Alt, click is slash, shove, slash). The swings glow in the staff's sage green; the shove pulls back, drives out and sends pressure arcs off its front | +20 px reach and +15% damage on every swing and the shove, and +20 px on the shove's push |
| | **Fuma Shuriken** | A giant star (30) thrown in an oval loop 380 px out in front of you and back, cutting through everything both ways | +1 shuriken, looping the other way |
| Healer · Ninja | **Medicine Darts** | Fast needles (7) that dose what they hit: a stack of poison on enemies, a stack of healing on hurt allies (2 a second for 3 s each, up to 5 stacks) | +1 dart, fanned out |
| | **Spirit Fan** | A short gust (6) that heals allies in it for 3 and blows small bullets back; shots alternate blowing enemies in front 60 px away and drawing them 60 px in | +25% reach and +1 HP healed |
| | **Ofuda** | Charms fly to your cursor (up to 460 px) and lie as wards (up to 5, 10 s); an enemy stepping on one sets it off: 25 within 70 px and 12 healing to allies within 110 px | +1 charm, fanned out |
| DPS · Ninja | **Shuriken** | Tight fans of 3 stars (9, 0.08 rad apart: all three hit a boss out to about 650 px) that stick in what they hit. After 5 s each rips out on its own for another hit and flies back to you, cutting what it passes (50%); press the weapon-action key to recall them sooner | +1 star in the fan |
| | **Katana** | Swipe (20), swipe (20), lunge (45): 110° slashes, then a 260 px line through everything in it for 2.25×; the chain restarts if you wait 0.7 s. Each enemy hit builds the strike meter (+10 a swipe, +20 the lunge; drains at 25 a second after 1.2 s without a hit). Full meter: hold the weapon-action key (Left Alt) to aim (the arena dims, the enemy nearest your cursor is marked, up to 1.5 s), let go to cut across the arena and land 52 px behind it: 120 to it, 60 to everything on the line, untouchable throughout. If the strike kills its target the meter stays full and you gain a chain (kills along the line don't count); while you have a chain the meter doesn't drain, and a target that survives ends the streak. At 5 chains the next strike is empowered: 240 to the target, 120 to everything on the line, a wider gold-white cut (then the chain starts again). The chain shows as a small blade over your head with a notch lit per kill The meter shows as a rose arc round you; full, the blade is sheathed and glints | +1 cut: each slash strikes once more (60% damage) |
| | **Kunai** | Slow, heavy knives (30 damage, one every 0.7 s, fast flying), +50% on enemies that aren't targeting you; every throw is a shadow kunai that leaves a mark where it lands (3 s, the newest replaces the old): press the weapon-action key to blink there, and your next hit lands double | +1 knife, fanned out (half damage) |
| Tank · Hemomancer | **Blood Lash** | 110° whip (24) with 130 px reach: the tip cracks for double and taunts; hits build Clot (4% less damage taken each, up to 5); full Clot makes the next lash a burst all round you | +20 px reach and +1 max Clot |
| | **Clot Bolt** | A heavy bolt (26) that leaves a 70 px pool slowing enemies 40% for 3 s; after 1 s it congeals: enemies still in it are stuck for 1 s (not bosses); you take 15% less damage standing in your own pool | +1 bolt, fanned out |
| | **Leech Tendrils** | Hold fire: tendrils lash out across a 70° arc (160 px from your edge) and grasp the nearest 3 enemies for as long as you hold on (Barrage adds a tendril a stack; shields turn them away; a held enemy slips free past 130% of the reach). Held enemies can't move or shoot (bosses are held and drained but not stunned) and take 1.2 every 0.1 s per tendril on them (12/s each); spare tendrils pile onto the enemies already held (nearest first), so a lone enemy or boss takes all 3 (36/s). 25% of it heals you. Let go and they're free | +1 tendril and +20 px reach |
| Healer · Hemomancer | **Transfusion Darts** | Darts (7): one that reaches a hurt ally gives them 20 health for 4 of yours; each enemy hit steals 0.5 back | +1 dart, fanned out |
| | **Sanguine Orb** | Orbs that home in at the last moment and cover an enemy in blood (16 plus a bleed stack), charging a heal reserve; with 25 reserve, an orb touching a hurt ally coats them in healing blood (30 over 4 s) | +1 orb, fanned out |
| | **Vein Tether** | An instant beam, 350 px: 4 damage every 0.08 s to the first enemy on your aim line (Pierce passes through more, Barrage adds fanned beams; shields stop it). Only what you hold it on gets hurt. 40% of its damage heals you and the nearest ally | +25% beam length and +25% damage |
| DPS · Hemomancer | **Blood Bolts** | Fast bolts (9) that stick: the fifth within 2 s makes the enemy hemorrhage, bursting for 36 to it and every enemy within 80 px | +1 bolt, fanned out |
| | **Implosion** | No projectile: click an enemy (up to 480 px away) and its blood collapses inward (34), hurting enemies within 55 px of it (15) and pulling them 18 px in; ignores shields. Holding fire with nothing under the cursor waits | +1 implosion: another enemy near your target collapses too (60% damage; the target again if no one is near) |
| | **Bone Spear** | A heavy spear (60) that pierces 3; each throw costs 2 health, and the spear stays stuck where it stops for 8 s: walk over it for the 2 back | +1 spear, fanned out (costs no more health) |

With your own Revolver, its cylinder shows as pips under your character (gold = heavy rounds), and a ring
fills while it reloads. With **Visual effects** on, the gun kicks back and its cylinder clicks round on every shot, a flash
and drifting smoke leave the muzzle and a brass casing flips out (a heavy round is a bigger, orange flash). The Mender's
Bow's limbs bend as you draw, its arrowhead glints near full draw, a gold ring pops at full draw, and on release the
string snaps and a ring of light flies off (gold for a perfect shot); arrows fly with a longer streak. Drawing only.

Want a **wall of bullets** instead? The **Barrage** card (Rare, stacks to 3) makes every shot also fire a copy fanned
out to the side (8° steps, alternating sides), at 60% damage. It stacks with the weapon upgrades above, so a Twin
Rifle with Multishot and Barrage fires several side-by-side barrels in a fan. A Revolver round fans out for a single
round of ammo.

### Parry

Hold **right click** (or your Parry key) to raise a small shield on the side facing your cursor. It blocks enemy bullets from that side
(including the red aimed shots that barriers can't stop) and melee enemies that run into it, shoving them back. While
it's up you **can't shoot** and you move at 70% speed. It soaks **150 damage**, then breaks and can't be raised for 3
s; it refills when you lower it. Raise it just as a shot arrives for a **perfect parry**: for the first instant after
raising it, blocked bullets are sent back at enemies and cost nothing (it has to have been down for half a second, so
you can't spam it). A thin blue bar curving around the shield shows how much it has left, shrinking toward your aim as
it wears and blinking red when it's nearly gone; when it breaks it cracks and shatters, and while it's lowered a faint
bar shows it refilling. Against the Warden up close it holds for about 3 seconds of fire, enough to close the gap.

**Animation cancel:** raising the parry sends an attack chain back to its first step, clears the wait for the next step and cancels an attack that hasn't landed yet. Today that is the Katana's swipe, swipe, lunge (a lunge still pulling back never lands) and the Bo Staff's slash, jab, spin (a shove still drawing back never lands and costs no cooldown). Meters and streaks you have built (the Katana's strike meter and chain, Twin Blades' combo, the Rapier's streak) are kept.

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

| | Soldier | Elementalist | Summoner | Knight |
|---|---|---|---|---|
| **Tank** | **Fortress** (70 s): raises a fixed, impenetrable dome (240 px radius) where you stand for 10 s; only players and player bullets cross it, enemies (bosses too) and their shots can't get in or shoot through, and anything inside is pushed out | **Glacial Tomb** (75 s): erases every enemy bullet and freezes every enemy solid for 4 s; bosses take 250 damage instead. With Visual effects on a frost lattice spreads out from you, spires cage each frozen enemy and break when the freeze ends, and a ring of spears jabs at bosses | **Titan** (80 s): a huge stone titan (140 px across) rises from rubble over 1.4 s, then stomps after enemies for 17 s, raising its fists for 0.4 s before each slam (90 damage, 190 px, every 1.4 s, shoves enemies back; bosses and Heavy enemies aren't shoved). Its whole body blocks bullets (soaks 1000 boss damage). It crumbles when time is up | **Last Stand** (70 s): plant yourself behind a crescent of plates that stops every shot from the front while you take 10% of it (no timer; press again to cancel) |
| **Healer** | **Lifeline** (90 s): instantly revive and fully heal the whole party, plus 3 s of invulnerability. A heartbeat pulse rolls out from you and a halo and column of light drop over each player as it reaches them | **Sanctum** (80 s): a 7 s holy dome; bullets can't get in, allies inside can't go down, and a healing pulse rolls out from you every 0.9 s (7 pulses: 20 each, the last 30; each ally heals as the ring reaches them) | **World Tree** (80 s): a 100 px tree for 15 s that heals everyone under it (18 HP/s), slows and burns enemies, and sends a wave of healing butterflies, one to each ally (the most hurt first), every 1.9 s; each butterfly flutters to its ally and heals 14 on arrival | **Hallowed Blade** (80 s): a giant sword of light falls at your cursor (200 damage) and stays planted for 8 s; allies in its ring heal 15 HP/s and enemies burn 20 a second, and every 2 s a pulse crosses the whole arena (4 pulses, the last bigger) healing every ally 10 and hurting every enemy 13 as the ring reaches them |
| **DPS** | **Artillery Assault** (60 s): 6 s of triple fire rate and +2 extra projectiles, with a heat aura, speed streaks and a muzzle flash | **Bullet Rain** (70 s): a downpour at your cursor (330 px across) for 6 s: the whole arena darkens and rains, and inside the zone the drops harden into needles that do 45 damage a second to every enemy in it, bosses too, and leaves them **wet** (as Tsunami does: slowed, and any lightning anywhere arcs to them) for 3 s after they leave | **Legion** (70 s): a blood moon rises and tints the arena for 10 s; 6 wolves and 6 bats circle you for a second, then run 40% faster, and your other minions frenzy | **Blade Tempest** (65 s): for 6 s every swing also hurls three piercing sword beams |

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
| **Adrenaline Shot** | Also gives the nearest other ally within 250 px a half-length (4 s) dose. | **Deployable Medkit** | Bursts into 9 orbs worth 20 health each. |
| **Cluster Grenade** | Throws a second grenade 80 px further. | **Mark for Death** | Marks the three enemies nearest your cursor. |
| **Aeroaegis** | Lasts 4.5 s and reflects bullets for double damage. | **Frost Nova** | Also erases enemy bullets around you. |
| **Flame Wall** | The fire also slows enemies by 40%. | **Healing Rain** | Lasts 10 s, and downed allies inside revive twice as fast. |
| **Energy Ward** | The shield soaks 120 damage. | **Lumen Ward** | Also freezes enemies outside the light for as long as it lasts (not bosses). |
| **Meteor** | Leaves burning ground for 3 s. | **Blizzard** | Lasts 10 s. |
| **Tsunami** | Wet lasts 14 s, and the wave rolls back to you. | **Golem Wall** | Raises five golems. |
| **Earthquake** | The Burrower also freezes everything it bites for 1 s (not bosses). | **Force Pact** | A second thrall: the pact keeps the last two types you chose. |
| **Spirit Swarm** | Releases 8 sprites. | **Phoenix** | Revives at full health, and the phoenix keeps fighting. |
| **Soul Lantern** | Lasts 9 s, and the linked player can't go down. | **Bat Swarm** | Releases 14 bats. |
| **Hydra** | A fourth head. | **Alpha Call** | Also summons 3 wolves for 6 s. |
| **Fortress** | Lasts 14 s. | **Glacial Tomb** | Enemies stay frozen for 6 s and bosses take 500. |
| **Titan** | The titan is 30% bigger and stays for 27 s. | **Lifeline** | Everyone also gets 4 s of Overdrive. |
| **Sanctum** | The dome is half again as wide (300 px). | **World Tree** | Lasts 20 s, butterflies every 1.4 s and heal 18 each. |
| **Artillery Assault** | +4 extra projectiles instead of +2. | **Bullet Rain** | Lasts 9 s. |
| **Legion** | Summons 10 wolves and 10 bats. | **Shield Wall** | Bullets that hit the shield are reflected back as your own. |
| **Leap Slam** | The landing also erases enemy bullets within 160 px. | **Cavalry Call** | Two more lancers ride outside the first pair, and the guard lasts 5 s. |
| **Consecrate** | Two more crosses fly out 22 degrees either side (each target is hit once). | **Guardian's Oath** | Charges up to 800 px (not 600). The ward is wider, follows you, and stands a downed ally back up at 25% health. |
| **Lay on Hands** | Also heals everyone else near them for half as much. | **Whirlwind** | Lasts 3 s and cuts every bullet except red aimed shots. |
| **Lunge** | +1 charge (three in all). | **Battle Fury** | +1 extra projectile while it lasts. |
| **Last Stand** | You take 5% of the damage instead of 10%. | **Hallowed Blade** | Three swords fall: one at your cursor and one either side. |
| **Blade Tempest** | Five sword beams a swing instead of three. |  |  |

A popup appears when an ability levels up. The **Tab** menu shows each ability's level as five pips (the next one
fills as it earns XP). What level 5 unlocks is always written in gold under each ability's description: in the lobby
(your Space ability and your ultimate), on the Tab screen's ability tiles, on the cards that give an ability (in the
level-up picks and the Bazaar), and in the Field guide. Where space is tight the text gets smaller, then shortened
with "…"; hover a Tab tile to read it in full.

In the lobby, your **ultimate** sits at the bottom of the left column of the loadout screen, under the stat bars: a gold charge ring (half full, as every
ultimate starts a match half charged) with its key, its charge time, what it does and what mastering it adds. Click
it to watch it in the preview above; the ring fills while it plays.
Mastery resets each run.

## Levels and upgrades

Every enemy killed gives **both** players XP. Level up during a wave and you'll see **LEVEL UP!**; when the wave is
cleared, each player picks one of **3 upgrades per level gained**: click (or press 1/2/3) to select a card, then
**Choose Upgrade** (or Enter). The screen wears the lobby's blue CRT look: a glowing VT323 title, boxed cards with corner
brackets, and the lobby's digital keys for **Reroll** and **Choose Upgrade** (Choose is the dashed, locked key until you pick a
card, then it lights up with a green lamp). Each card has its rarity in a small key-like label on top, the icon (in its own
colours) in an outlined tile, and a soft glow (blue common, green uncommon, blue rare, purple very rare, orange legendary)
while it bobs slowly (rare and better ones also shine); the one you pick lifts, glows stronger and outlines in white-blue.
Upgrades with a stack cap show squares that fill with how many you hold (the next one is ringed on the picked card). What the pick changes ("Regeneration 0.25 → 3.25 HP/s", or a new
ability and its key) shows on the card itself. Hold **Tab** for all your stats. Boss rewards say **BOSS REWARD!** (legendaries)
or **WEAPON MOD** (a grid of every mod you can take). Don't like the cards? **Reroll** (or R) for a fresh set: the first click
(or press) arms it (the button turns orange and reads CLICK AGAIN TO REROLL for 3 seconds), the second rerolls. You
start with 2 rerolls and everyone gets 2 more for each boss killed. The next wave waits until everyone have chosen.

| Rarity | Color | What you get |
|---|---|---|
| **Common** | white | Small stat boosts: +10% damage, +10% fire rate, 15% bigger shots (Magnum Rounds), +6% speed, +15 HP, regen, -6% cooldowns |
| **Uncommon** | green | Bigger boosts (+20% damage, +40 HP, -15% cooldowns...) and damage reduction |
| **Rare** | blue | **Second Charge** for your Space ability, an upgrade for a specific ability you have, on any key (e.g. bigger Barrier, longer Roar, double-damage Bulldoze, longer Adrenaline Shot, faster Soul Lantern, bigger grenades, harsher marks; for Knights: Tower Shield, Earthshaker, Iron Will, Sacred Ground, Bulwark, Miracle, Cyclone, Flurry, Bloodlust; for Ninjas: Mirror Image, Thick Smoke, Tempered Steel, Morning Dew, Body Double, Sacred Seal, Assassinate, Unseen Blade, Storm of Steel; for Hemomancers: Hardened Blood, Blood Oath, Deep Veins, Lifeblood, Hungry Earth, Saint's Blood, Boiling Point, Bloodrush, Crimson Tide), wider aura; plus Multishot, Barrage, Piercing Rounds and Overcharge (second charge on Q/E/Shift) |
| **Very rare** | purple | A brand-new ability on **Q**, **E** or **Shift** (you choose the key; each one only once): a universal one (Shockwave, Overdrive, Blink, Nova or Turret), or one of your **class's two other Space abilities**, the ones you didn't pick in the lobby (these count triple, so about 6 in 11 very rare cards are class abilities) |
| **Legendary** | orange | Unique weapon upgrades, **only from bosses**: after a boss dies, each player picks one of that boss's three (can't be rerolled) |
| **Weapon mod** | teal | Also **after every boss**, right after its legendary: choose any mod from the full list (each stacks up to 3 times) |
| **Milestone** | gold | Their own pool, offered **every 10 levels** (levels 10, 20, 30...) as an **extra pick** on top of that level's normal one, before it. Three cards from: **Scholar** (+15% XP), **Second Helping** (+10% chance a level-up gives an extra pick), **Wide Choice** (+1 card in every level-up offer, max 2, so up to 5 cards), **Reroll Cache** (+3 rerolls now and +1 more after every boss), **Fortune** (rarer cards 8% more often), **Iron Constitution** (+25 max HP) and **Quickening** (-4% ability cooldowns). Each card stacks to a cap (5, Wide Choice 2, Reroll Cache 4), shown as circles on the card, and a maxed card leaves the pool. Milestone picks can't be rerolled. The very rare **Milestone Compass** (stacks to 3) is a normal card that gives every level-up a 5% chance per stack of also offering a milestone pick |

| Boss | Legendary weapon upgrades |
|---|---|
| The Warden | **Warden's Guard** (one of the Warden's guards walks at your side and fights enemies within 240 px of you with its baton, 14 damage every 0.7 s; it has 150 health + 15 per wave, enemy shots and bodies hurt it, and a fallen guard is replaced 15 s later; +1 guard per stack) · **Stun Gun** (a new Q / E / Shift ability, 14 s: draw a stun gun with 6 shots, 15 damage each, that stun enemies for 1.5 s (bosses are slowed 50% instead); your weapon is away until they're spent, and only then does the cooldown start; capstone: 9 shots) · **Searchlight** (a pool of light, 110 px, chases your cursor at 1100 px/s; enemies in it take 25% more damage from every player, and one held in it for 2 s is stunned for 1 s, bosses slowed 40% instead, then can't be locked again for 3 s; the 2nd stack widens it to 140 px, the 3rd makes it +40%) |
| Hive Queen | **Hive Drones** (two of her drones circle you and block bullets; after your kills one flies out to sting the nearest enemy 3 × 20 and comes home to heal you 4; every 3 s while you hit a boss; +1 drone a stack, and one more flies out each time) · **Royal Dive** (new ability, 12 s: her dive, untouchable, crashing at your cursor for 120 within 110 px with knockback, leaving a honey pool that slows enemies 40% and heals allies 6/s for 4 s) · **Bomber Escort** (every 5 s one of her bombers flies from you at the toughest enemy within 400 px and bursts for 55 within 70 px; +1 bomber a stack) |
| (no longer offered) | **Swarm Rounds**, **Royal Jelly** and **Brood Mother** (the Queen's old cards) and **Executioner**, **Twin Fang** and **Soul Harvest** (the Twin Reapers') are still in the game (old saves, dev tools) but no boss offers them |
| Twin Reapers | **Twin Scythes** (two of their scythes circle you in opposite directions, 60 and 100 px out, cutting what they pass for 16 and slicing small enemy bullets; every 1.6 s each reaps out at the nearest enemy within 240 px and flies back; 2nd stack every 1.1 s, 3rd a third scythe) · **Final Reap** (a hit that leaves a regular enemy below 25% reaps it and heals you 3; hits on a boss below 40% deal double; stacks: 30%, 35%; x2.5, x3) · **Scythe Throw** (new ability, 9 s: a giant scythe out to your cursor, up to 380 px, and back, cutting 45 each way and slicing small bullets; catch it to take 40% off the cooldown) |
| The Singularity | **Collapse Rounds** (shots implode on impact; the enemy hit takes half) · **Graviton Rounds** (hits slow enemies 40%; slowed enemies take 15% more damage from you) · **Dark Matter** (+1 pierce, +35% damage) |

**Weapon mods** change how your shots behave, and you can see them on your bullets:

| Mod | Per stack |
|---|---|
| **Homing Rounds** | Shots bend toward an enemy within 260 px and about 45 degrees of their path, so near misses connect (they don't turn around to hunt). Costs 5% damage per stack; more stacks bend harder. **Area weapons** (Implosion, Flak Cannon, Ofuda, Storm Hammer, Clot Bolt, Vein Tether, Frost Lance, Treant Rod, Nimbus, Spirit Chalk, Golem Staff, Arc Lightning) get a bigger area instead of bending: +25% per stack to the blast, burst, pool, collapse, beam width, lightning's reach, splinters, lash, lightning and rain, ink line, boulder or strip, for the same damage cost. **Cone weapons** (Chain Light, Leech Tendrils, Repulsion Cannon) condense instead: the cone, arc or blast is 15% narrower and hits 10% harder per stack (the Repulsion Cannon also pushes 10% harder), with no damage cost, so it's a choice between covering a crowd and focusing a target. **Wisp Lantern and Imp Caller:** the wisps and imps you send pick an enemy 50% farther from your cursor per stack, and one sent at empty ground (or whose target dies) turns onto an enemy within 260 px of it, for the same damage cost |
| **Ricochet** | After a hit, the shot bounces to another enemy within 300 px for 40% damage; +1 bounce per stack. Hits that can't bounce (melee, beams, cones, bursts, sent wisps and imps, and shots that pierce: Railgun, drawn arrows, Bone Spear, Fireball, flames, Fuma Shuriken) store their damage instead, and up to 4 times a second a **bounce bolt** carries 40% of it from the enemy hit to the nearest other one (+1 bounce per extra stack) |
| **Incendiary Rounds** | Hits set enemies on fire: each burns for another 40% of the hit's damage over 3 s (per stack), and burns from every hit add up, so fast and slow weapons gain about the same |
| **Cryo Rounds** | Hits slow enemies 20% (up to 60%) with a 6% chance to freeze non-bosses for 1 s |
| **Shock Rounds** | 25% chance per hit for lightning to arc to 2 more enemies at 60% damage |
| **Explosive Rounds** | Shots explode on impact for 30% damage around them (bigger blast per stack) |
| **Velocity Rounds** | Shots fly 30% faster and farther, +25% damage |

Mods apply to your weapon's shots and your minions' shots, but not to grenades. They also ride every hit of
weapons that don't fire bullets (melee swings, beams, cones, the Repulsion Cannon's blast, Ofuda ward bursts,
sent wisps and imps). On a Knight, the elemental mods apply to every melee hit (see The Knight above).

**Piercing Rounds** let shots pass through two more enemies per stack. Weapons without bullets that run out of pierce
(melee, the Flamethrower, Frost Lance, Fuma Shuriken, Repulsion Cannon, Radiant Orb, Chain Light, Spirit Fan, Ofuda,
Arc Lightning, Wisp Lantern, Imp Caller, and the Fireball, which already rolls through everything) reach 15% farther
per stack instead.

Rarer cards show up a bit more often as you level. All the numbers live in
`Assets/Scripts/Core/Upgrades.cs` if you want to rebalance or add upgrades.

## Rules

- The arena is 1600x900 and **wraps around for players**: walk off any edge and you come in on the opposite side.
  Enemies and bullets don't wrap, so the edges make a good escape route.
- Your hitbox is the small coloured dot ringed in white in the middle of your character, not the whole circle.
- **Status effects** show as a tint on the enemy plus an icon above it: **snowflake** = frozen (encased in ice, can't
  move or shoot), **blue double arrow** = slowed, **flame** = burning (flames rise off it), **blood drop** = bleeding
  (drops fall off it), **red crosshair** = marked (takes extra damage).
- At 0 HP you're **downed**. Stand next to a downed ally for 4 seconds (Normal difficulty) to revive them (or the
  healer can pulse them back).
- After a hit you can't be hit again for 0.25 s (Normal). Small health orbs drop from 12% of kills (Normal), large
  ones from 3%.
- If everyone is downed, it's game over. Clearing a wave revives anyone downed and heals everyone a bit.
- New enemy types unlock as the waves go up (see below), with a boss every 5 waves.
- **Knockback slides:** anything that shoves enemies (the Repulsion Cannon, Roar, Shockwave, the Scattergun up close,
  Knight swings, Bulldoze, Fortress, the Fuma Shuriken's fan) slides them the distance over 0.18 s instead of jumping
  them there, with a streak behind them, dust kicked up along the way and a puff where they were hit.
- **Boss bodies:** touching a boss hurts once (25, plus the difficulty's 3% a wave) and shoves you about 100 px back
  out; it can't hurt you again for 1 s.
- **Bullet budget:** enemy bullets on screen are capped at 40 + 3 per wave, up to 85 from wave 15 (20% more per extra
  player; times the difficulty's Bullet budget). When the screen is over the cap, regular enemies hold their fire and
  try again a moment later. Bosses always fire, and while a boss is up the other enemies get 60% of the cap (Normal).
  Later waves get harder through tougher enemies, not more bullets.
- **Taking turns:** only so many regular shooters fire at once: 4, one more every 6 waves up to 8 (wave 24), a quarter
  more for each extra player (times the difficulty's **Shooters at once**). Each gets a 5 s turn, then rests 1.5 s
  while whoever has waited longest takes over; the rest keep moving and hold fire. Bosses don't take turns.
- **Reading the shots:** a regular shooter glows (a white core, a ring closing in) for 0.3 s before each volley. Enemy
  bullets show what they'll do: a shot aimed at a player is a **dart** pointing where it flies, a curving shot is a
  **crescent**, and rings and sprays (6 or more shots at once) are **round**. Rings are sparse and slow enough to slip
  through: Burster up to 12 at 115 px/s, Charger 8, Phantom 8, Mortar bombs 7 (10 elite) at 125, Volatile 12.

## Enemies

The first waves are **melee rushers** (spiky, they never shoot, they hurt on contact). Shooters take over a
little more each wave: wave 1 is all melee, wave 7 about half, and from **wave 13** the early rushers are gone (melee
comes back in squads, and with the Reaver from wave 28).

With **Visual effects** on, regular enemies stand out from the floor: a soft dark shadow under each body and a faint glow in their own colour, much quieter than a player's halo. Bosses, spawning enemies and zombies don't get it.

With **Visual effects** on, anything that moves fast leaves an **air trail**: two thin streamlines off its shoulders that wobble and fade. It follows speed: none at or below 110 px/s (Grunts, Brutes, bosses), light for mid speeds (a Tank or Healer walking is about 10-25%), and full at 330 px/s and up (Hive Bombers, dashes, hard knockback). Players, enemies and minions all get it. Not sent over the network.

**CRT colour** (Settings > Visual effects, saved on this machine): tints the CRT screens (menus, lobby, profile, run summary) Blue (default), Green, Amber, Red, Violet or White. The role, class and rarity colours keep their own meaning and stay as they are.

With **Visual effects** on, the Ninja's **Shadow Step** is smoke: you burst into puffs that split into two halves, each following a pale eye round one side of the enemy, and meet again behind it to re-form as you strike (you aren't drawn for the first third of a second; the step and its damage are instant as before). Off, it's the plain ring and slash. The two ends of the step are sent over the network (`FxKind.ShadowStep`).

**Squads.** About 70% of a regular wave arrives as small teams that come in together from one edge and look after each
other; the rest are single enemies. Each squad unlocks at a wave, and none comes more than twice in one wave: Rush (1:
five Swarmers), Firing line (3: three Grunts, a Sniper behind), Brute pack (5: the Lunger waits for a Brute to reach
you), Splinters (7: two Splitters and a Grunt), Bombardment (8: a Charger and two Mortars), Shield wall (9: a Shielder
leading two Snipers that keep behind it), Field hospital (11: three Brutes and a Medic behind them), Hold the line
(14: a Pauser and two Grunts), Ambush (16: three Phantoms blink in around you), Beam battery (18: two Beam Sentries
guarded by a Shielder), Hunting party (19: two Leaders and a Sniper), Pincer (21: Hexers from opposite edges),
Seamstresses (22: two Stitchers side by side, and a Grunt), Scorched earth (24: two Scorchers and Spikers), Siege
(26: a Juggernaut and two Medics), Reaver pack (28: two Reavers and two Lungers), Hive (31: a Nest guarded by
Splitters), Bastion (36: a Warder, a Juggernaut and two Grunts). Early waves keep their share of rushers (a melee
squad as often as a melee enemy used to come). Paths with their own mix keep it.

| Wave | Melee enemy | What it does |
|---|---|---|
| 1 | **Swarmer** | The simplest enemy: a small white chomper with a wedge for a mouth and no eyes. Walks straight at you (115), 14 health, 6 per bite. Comes in packs of four, so cut them down together |
| 2 | **Spiker** | Small and fast, with a long thin spike on each face. Zig-zags toward you (swinging 40 to 65 degrees off line, flipping side every 0.2 to 0.45 s, straightening as it gets close), comes in pairs. (Called the Swarmer before the plain white one arrived) |
| 3 | **Brute** | Slow and tough; hits hard (24) when it reaches you |
| 5 | **Lunger** | Circles in close, flashes a pink line, then leaps along it. Step aside |
| 5 | **Buzzsaw** | A ring of spinning spikes. Fast (170) and it steers with momentum, so it swoops and overshoots. Each touch is only 4, but they land about eight times a second |
| 5 | **Spike Block** | A square with a big spike on each face. Rocks from side to side as it closes in; within about 140 px it stops, shakes and shows a red line (0.55 s, the line is locked), then lunges along it for 35. It rests 0.8 s afterward: shoot it then |

| Wave | Shooter | What it does |
|---|---|---|
| 1 | Grunt | Aimed 3-shot spreads |
| 2 | Burster | Rings of bullets (8, one more every 2 waves, up to 12), slow enough to slip through |
| 3 | Sniper | Fast, accurate shots from far away |
| 4 | Spinner | Wanders and sprays spirals in **bursts** (about 1.3 s on, 2 s off); it glows and spins up while bursting |
| 6 | **Charger** | Flashes a red warning line, then dashes at you and bursts into a ring. Step out of the line |
| 7 | **Splitter** | Splits into 3 fast Shards when killed |
| 8 | **Mortar** | Lobs bombs at where you're standing; they burst into a ring when they land. Keep moving |
| 9 | **Shielder** | A shield on its front blocks your shots. It turns slowly, so flank it. The shield (60% of its health) wears down as it blocks and crumbles on its own (about 33 s), cracking as it goes, then shatters |
| 11 | **Medic** | Heals nearby enemies every few seconds. Kill it first |
| 14 | **Pauser** | Fans 8 shots at you that slow to a stop about 100 px out, hang there glowing for 0.7 s (a ring closes in on each), then each rushes at the nearest player from where it hung. Keep moving while they hang, then sidestep the rush |
| 16 | **Phantom** | Flickers, blinks next to you and bursts into a ring |
| 18 | **Beam Sentry** | Walks to about 380 px from you and plants itself, aims a thin line at you for 1 s (turning slowly after you), then sweeps a 900 px beam a quarter turn across your side (16, plus 3% a wave on Normal). Step behind the sweep. At most 3 come in a wave |
| 19 | **Leader** | Aims where you'll be, not where you are: it works out how you're moving and leads its shots (up to 1.6 s ahead). A thin pink aim line shows where for 0.45 s, then a pair of shots. Change direction or stop when the line shows |
| 21 | **Hexer** | Fires curving pincer shots that close in from both sides |
| 22 | **Stitcher** | Fires slow orbs; each orb is tied to the nearest other orb within 280 px by a thread that hurts. Alone it fires two orbs that spread apart until the thread snaps; a pair stands side by side, 240 px apart, and fires together, so a fence sweeps at you. Step round an end. Blockers that eat the orbs (a barrier, flak) cut the thread |
| 24 | **Scorcher** | Circles you about 260 px out leaving a trail of embers (15/s, 3 s; they only burn players) and fires pairs of shots. Cut across its circle |
| 26 | **Juggernaut** | Armored and slow; can't be slowed or knocked back; fires rotating stars of big orbs |
| 28 | **Reaver** | Armoured melee (can't be slowed or knocked back): in reach it shows a red 120° cone for 0.4 s, then rakes it (26; a perfect parry blocks it). For 0.7 s after a swipe it's **open**: +50% damage |
| 31 | **Nest** | Stationary spawner that keeps hatching Shards until destroyed |
| 36 | **Warder** | Hangs back behind its allies; every 6 s up to three allies within 320 px get a bubble worth half their health, tethered to it. Kill it and every bubble pops |
| 12+ | **Elites** | Any enemy can spawn with a gold ring: 2.5x health, bigger, worth 3x XP, always drops an orb, and a **trait** shown as a marker over it (two traits from wave 35): **Hasted** (40% faster moving and firing), **Shielded** (a bubble worth 40% of its health that grows back 4 s after it was last hit), **Volatile** (bursts into a ring of 16 shots 0.6 s after it dies), **Splitting** (dies into two Shards, four from a Juggernaut, Nest or Brute), **Mirrored** (one in four of your shots that hit it bounce back at you), **Commander** (allies within 220 px fire 30% faster). More common each wave |

### Boss approaches

The four waves before each boss are its **approach**: part of each wave is the boss's own kind of enemy, so you meet
its world on the way to it (design: [`Docs/Approaches.md`](Docs/Approaches.md)). How much is a lobby setting
(Difficulty tab, below): **Blend** (the default) builds up over the four waves, 34%, 51%, 68% then 85% of each wave;
**Fully themed** makes all four waves the boss's own enemies; 0% turns it off. The rest of each wave is the usual mix,
paths still apply (an enemy-mix path builds from what's left; Elite Gauntlet makes the faction elite too), and the
boss fight itself is the same. A chip above the wave panel says "Approach · The Warden · 3/4", and the banner before
the first and last waves says you're on the road to the boss, then that it's near. So far the **Warden** has a faction
(waves 1-4), and the last wave before him always brings the **Riot Line**: four Riot Shields marching at you as a
wall, five Guards close behind it, who break out and rush you once the wall gets within about 190 px or half its
shields are down. The **Lich** has one too (waves 31-34): the same zombies as Zombie Infection, Shamblers first, then
Runners (in packs of three) and Spitters from wave 32, Crawlers from 33 and Menders from 34, with two squads (Graveyard
shift, Grave rush) and a last-wave **Dead march** (six Shamblers, two Menders, three Runners). They don't get back up
outside the modifier. His fight itself is unchanged. The other bosses' factions come next.

| From | The Warden's faction | What it does |
|---|---|---|
| 1 | **Rioter** | A prisoner on the loose: weaves at you, in pairs (contact 10). Squad: Breakout (six Rioters and a Guard) |
| 1 | **Guard** | A riot guard with a baton, straight at you (contact 14) |
| 2 | **Riot Shield** | A guard behind a tall shield that stops shots and swings from the front. Up close the shield flashes (0.4 s), then it lunges and **bashes** you: 10 damage and knocked **160 px** back (you can't be bashed again for 0.8 s). It holds the shield low for a second after, when front hits land. The shield holds 4x its health and cracks as it wears; broken, the guard staggers for a second, then flails and takes 50% more damage. Go round it or break it. Squad (wave 3): Cell block (two Riot Shields with Rioters pouring past) |
| 2 | **Gas Grenadier** | Keeps its distance and lobs a tear-gas canister near you every 3.6 s: a 70 px cloud that settles after 1 s and hurts (8 a second) for 4 s |

## Bosses

**Every boss sounds like itself.** Each mechanic has a sound for its warning and for its strike, from a palette of
synthesized boss sounds: a rising whine before lasers and aimed attacks and a buzzing beam when they fire, a crack and
deep thud for slams and landings, a rush for charges and dives, the Warden's prison siren, the Queen's swarm buzz
and honey splats, scythe swishes for the Reapers and the Duelist, an in-rush for the Singularity's pulls and the
Devourer's inhale, the Colossus's rumbles and breaking stone, cages slamming, the Devourer's jaws, the Herald's
thunder and a deep gong for rites, last stands and the Eclipse's totality. They follow the Enemies volume.
(`Tools/SoundCheck/render.sh` writes them to WAV files.)

**Bosses keep up with strong builds.** When a boss arrives, each player's upgrades are scored (damage x fire rate
x projectiles x crits, plus boss legendaries and companions) and compared with a typical build at that wave (one
picked sensibly from the same offers). A party far above typical meets a boss with more health: (how far above)
to the 0.6 power, up to x3. Bosses after wave 20 already get +10% health a wave for the same reason, so the bigger
of the two applies, not both. Typical and weaker builds meet the usual boss, and a strong build still kills faster.
Off in the Practice Range and the Boss Lab. (`run.sh 1 1 bosspower` prints the numbers.)

**Health at each stage change.** When a boss changes stage (every boss's stage shifts and last stand, and the
Twin Reapers fusing and splitting) it knocks a few **small health orbs** loose in a ring just outside it: two
plus one per player (15 health each).

**Mitigation, not immunity.** Barriers, Flame Walls, Force Pact thralls, Golems, Treants, Golem Wall golems and
Aeroaegis stop bullets *coming in*, so a tank can still soak fire for the team, but bullets fired from
inside them pass through: you can't muzzle a boss by standing on it. Against regular waves blockers hold
for their full duration; **only boss bullets wear them down**, except Golem Wall golems, which every shot wears (200 health each, and they stay until broken). Each soaks a limited amount of boss damage
(Barrier: 360, more with Bastion): with Visual effects on it's a ring of eight armour plates, one knocked off for every eighth it soaks, and they blow out when it breaks (off, it's a disc that fades as it weakens); companions are dazed for a few
seconds instead. Boss bullets are easy to spot: they have a **dark core**. Violet-spiked ones wear blockers
down; **red ones (aimed shots) can't be blocked** and must be dodged, and standing
inside a boss hurts more each wave.

Bosses give soft sound cues when they start an ability: a low swell when they switch attacks, a rising shimmer
before something telegraphed (lasers, bombers, lightning), a deep thump for slams and stomps, and a chime for
summons and phase changes.

The game has **50 waves** with a different boss every 5 waves. Bosses after wave 20 get extra health (+10% per wave on
top of the normal scaling: x2 at wave 30, x4 at wave 50) to keep up with stacked upgrades. Their name, health bar and
warnings show at the top. Beat The Eclipse at wave 50 to win; the host can then keep going in **endless** mode (C),
where the bosses come back around, tougher each time.

| Wave | Boss | Mechanic |
|---|---|---|
| 5 | **The Warden** | A prison riot in three stages, one mechanic at a time (2 s between stages, when the screen clears and it can't be hurt). **Count** (100–65%): sways through the middle — orb rings, a spiral (holding still), aimed fans (red lanes show each volley 0.35 s before it fires), then an 18 s **searchlight** from the corner tower that follows you at an outrunnable pace while it fires single **stun shots**; get stunned or linger in the light and a **shackle ring** of orbs closes in on you, its gap facing away from the Warden. Turn it round: **lead the light across the Warden** and it's blinded for 3 s (**exposed**: no firing, +50% damage), and a **perfect parry** sends a stun shot straight back to **stun it** for 1.5 s. Each searchlight turn also sends in **two Riot Shields** (if fewer are up) that work round to your side away from the light and bash you into it. **Lockdown** (65–30%): the 12 s **cross** (dash, plant, fire a static "+", four times) runs alongside three **gates** — walls of push bullets 2 rows deep, with two doors near the side walls; each slides a little way in and stops for a moment (its doors flash) so you can line up a door, then sweeps across, shoving you back the way you came (one hit a gate, pinned or not). A turn's gates all come from the same edge, and each has a **gold lock** in the middle: six hits break it (shots, blasts from grenades, boulders and flak, melee swings and beams all count), opening a third door there. With them, lobbed **tear gas** that never lands across that gate's doors. Then a **tear gas barrage**: two volleys of canisters in a pattern with clear lanes between, while it fires slow aimed bursts. **Riot** (30–0%): cell doors burst open and **rioters** pour out, nine a door (they attack whoever is nearest, the Warden too); any still standing after 10 s bring blue **guards** with batons, who beat them down and then come for you while it turns a riot shield toward you and fires a shotgun. Rioters that reach it take 0.6% of its health each, but only up to 5% a riot turn (after that it bats them away). Its **riot shield** soaks 7% of its max health from the front, cracking as it wears; **break it** and it's exposed for the rest of that riot turn, taking 50% more; then a telegraphed **baton charge**. Below 10% it **puts on armor** (a 2 s stage change: steel plates lock on, it can't be hurt; then a blue armor bar above its health, worth 47% of its max health, takes your hits first) and makes a **last stand** in the middle (about 30 s for a good duo): the lights go out (lit round you, the Warden and its searchlight, which hunts you again) under a steady red tint, with nothing flashing. It fires long **"+" bursts** of straight streams (2.2 s), turning 45° each time (+, ×, +...); in the 1 s pause between, red lanes show where the next goes and **one ring** of orbs goes out; every 3 s a **stun round** at you (perfectly parried, it stuns the Warden) |
| 10 | **Hive Queen** | A hive in three stages, one mechanic at a time (2 s between stages, when the screen clears and she can't be hurt). **Swarm** (100–65%): she sways along the top — **drones** (tough Shards; she's **shielded and waits** while they live, and any left after 10 s fly back into her and heal her 2% each), **bombers** that swoop out like wasps to a point beside you (left and right in turn), then lock onto your spot and dive in from the side (they don't fly the line between you and her, so shooting her won't catch them), aimed **fans**, then orb **rings**. **Honeycomb** (65–30%): she lays 4 **eggs** that each hatch 2 drones unless you break them in 6 s; the floor's hex **cells** glow and seal (18) in waves, always leaving a safe cell next to you; a sweeping **nectar spray** leaves honey puddles that slow you. **Frenzy** (30–0%): she drops into the arena — a **swarm cloud** of bees hunts you at an outrunnable 170 px/s, three **royal dives** land on your shadow (she can't be hurt in the air) with a ring of orbs, and a **bomber barrage** aims at where you are and where you're heading. Below 10% she makes a **last stand** in the middle |
| 15 | **Twin Reapers** | Two Reapers (2600 HP each) in three stages by their combined health, one mechanic at a time. **Twilight** (100–65%): a **tether sweep** that spins round a point following you, then **lines** and a **cross** fired holding still; **Soul Bond** (the more-hurt one is immune until you even them out). **The Dual Reaper** (65–30%): they spiral together and fuse (a power-up of marked blasts), armored to a third of the damage. It fires **twin rotating lasers** while 4 **pillars** stand in the corners — kill them, or it pulls you in and blows up — and each pillar leaves a **hole**. Then it **chases** you swinging both scythes; lure it over a hole and it falls in: break its **shield** and it's **open** for 4 s (half again the damage), then it leaps back and starts over. **Sundered** (30–0%): it splits back into Dusk and Dawn and the tether snaps — marked **reaps**, a **twin spiral**, then **crossfire**; kill one and the other **enrages**, and the fallen one revives at 60% after 6 s unless you finish the pair |
| 20 | **The Singularity** | A black hole in three stages, one mechanic at a time with breathers between (2 s between stages, when the screen clears and it can't be hurt). Four **Gravity Wells** spawn in the corners at the start of each stage: they bend your shots hard into themselves and drift you toward them; killed, they stay gone until the next stage. **Event Horizon** (100–65%): in the middle; **Pull** (4 s, drags everyone in under a three-armed spiral), **Collapse** (3 s of bullet rings), **Lasers** (8 s: a purple spiral, then three red lasers that flicker as a warning and sweep slowly round). **Accretion** (65–30%): it orbits the middle inside a **disk** of two rings of big orbs turning opposite ways, with gaps (touching one hurts); **Cage** (9 s: three small wells orbit every player in a shrinking ring; touching it hurts, shooting the wells breaks it), **Slingshot** (6 s: every 0.8 s three orbs leave the outer ring at you), **Twin lasers** (two, sweeping faster). **Spaghettify** (30–10%): back in the middle behind an **event horizon** that grows from 70 to 170 px (touching it hurts) and pulls all the time (60 px/s, slower than anyone walks); **Hawking** bursts (rings of 18 off the horizon), **White hole** (a 1 s white flash, then for 1.5 s it pushes and every enemy bullet on screen turns round), **Split** (three shards orbit and fire; the core can't be hurt, and damage to the shards goes to it). Below 10% its **last stand**: the horizon swells to 190 px, the pull doubles, a spiral and rings of big orbs |
| 25 | **The Colossus** | A stone giant in three stages that breaks down as it goes, one attack at a time with breathers between (2 s between stages, when the screen clears and it can't be hurt). Its **stomps** send out rings of **push blocks** (like the Warden's gates) with green **gaps**, each a short step (about 130 px) to one side of a player, never less than 100 px across: step into the gap and the ring passes you by. Caught, it passes over you once (20 times the difficulty's damage growth; you end up just behind it, never carried along), and walking into one from inside shoves you back. **Awakening** (100–65%): it walks at you, rock volleys between turns — Stomps, **Boulders** (thrown where you're heading, then a burst), **Fissures** (a crack races toward you, then erupts along its length). **Crumbling** (65–30%): its armour falls as **four plates of cover** that stop shots from both sides, and it walks faster — **Charge** (a red lane, then it runs; **a plate in the way breaks and dazes it: +50% damage**), **Rockslide** (rows of rocks from an edge with a moving gap), **Aftershocks** (rings whose gaps spiral). **Molten Core** (30–10%): everything leaves **lava** (20/s) — Lava stomps (two gaps), **Meteors** (land in orb rings and pools), **Cave-in** (pillars fall, stand as cover for 3 s, then shatter). **Last stand** below 10%: it kneels in the middle — two-gap stomps, volleys and meteors |
| 30 | **The Duelist** | A duel in three stages (2 s between, the screen clears and it can't be hurt). **En Garde** (100–65%): it strafes 280 px out — Volleys, a **Lunge** (warning line, a fast dash leaving blades that fire sideways), a **Guard** (your shots on its front bounce back at you; it turns slowly, so get behind it), and a Blink that leaves a decoy. **Masquerade** (65–30%): three identical decoys circle you with it, and only its shots are solid (theirs are hollow rings); a **Crossing** (an X of dashes leaving blades) and a lunge chain. **To the Death** (30–10%): a **dueling circle** 360 px round the middle you can't leave (leaning on it hurts 15); it **Salutes** before it lunges — a **perfect parry** as it arrives knocks it off balance for 2.5 s at +50% damage, a dodge gets you raked; a **Flurry** of slashes. Below 10% the circle closes to 280 and it spins and salutes |
| 35 | **The Lich** | **Three stages** among six graves, one mechanic at a time. **The Crypt** (100–65%): one phylactery guards it (break it for a 12 s window); soul fans, grave rings, the dead rising. **Soul Harvest** (65–30%): three phylacteries joined by soul beams that hurt to cross; soul sweep, bone prisons, wailing spirits. **Undeath** (30–10%): a spectre blinking from grave to grave; dead hands, a soul storm. **Final rite** below 10% |
| 40 | **Storm Herald** | A storm in three stages, one attack at a time with breathers between (2 s between stages, when the screen clears and it can't be hurt). **Gathering Storm** (100–65%): it drifts along the top, firing its 3-shot piercing volley in the breathers; **Strikes** (6 s: a 70 px strike on each player and two random spots every 1.8 s, 1.1 s warning, 25, +3% a wave), **Lightning rods** (7 s: four rods fall around you; every 1.4 s an arc flickers 0.6 s between a pair, then fires along the line, 18, +3% a wave), **Thunderclap** (5 s: rings of 36 with two gaps, each turned 25°). **Gale** (65–30%): a **wind** blows across the arena all stage, turning round with each attack; it pushes players 70 px/s and drifts every bullet 60 px/s, yours too. **Crosswind** (6 s: 5-shot piercing volleys aimed upwind so the wind carries them on), **Ball lightning** (7 s: twice, three 70 HP orbs drift at you on the wind; two within 200 px arc to each other, and each pops into 8 shots after 6 s), **Downburst** (6 s: rows of six 110 px strikes at your height, landing one after another in the wind's direction). **Supercell** (30–10%): the sky goes dark. The **Conductor** marks one player at a time (every 8 s, for 3 s, a 180 px ring): when it runs out the charge jumps to every ally in the ring, hurting both ends, and 10 shots burst out from them (alone, it only grounds into the shots). **Storm front** (7 s: a wall of strikes sweeps across with a two-strike gap that drifts), **Strike rain** (6 s: smaller, quicker strikes), **Rod cage** (7 s: twice, six rods ring you 240 px out and arcs join them, all but one open side). Below 10% its **last stand** in the middle: four lightning spokes turning 20°/s, a strike on everyone every 2 s, its volley |
| 45 | **The Devourer** | A hunger in three stages, one attack at a time with breathers between (2 s between stages, when the screen clears and it can't be hurt); it grows each stage (50 → 58 → 64 px). **Appetite** (100–65%): it keeps 260 px from you. **Spiral** (6 s: its double spiral, 2 shots every 0.08 s), **Snap** (5 s: three lunges at a player, a red line for 0.7 s, then a 1000 px/s dash and a bite in a 110 px half-circle, 13, +3% a wave), **Hungry** (5 s: its mouth opens and your shots **heal** it for half; rings of 20 big shots every 1.2 s; it spits 4 **morsels** (60 HP) and crawls over to eat them, each healing it 1%: shoot them or walk over them first). **Gorging** (65–30%): **Inhale** (5 s: twice, a 70° cone 600 px long pulls you 200 px/s towards its mouth, 80 at the far end; whoever reaches the mouth is **swallowed**: hidden inside for 3 s, 10 damage a second, while allies' hits on it do **double** and 300 of them spit you out early, with a ring of 12 shots), **Bile** (6 s: every 1.5 s a glob at each player and one at random, a 90 px pool of acid for 6 s, 12 a second), **Burrow** (7 s: twice, it dives (can't be hurt), a ripple chases a player for 1.9 s, stops, marks 100 px, then erupts with a bite and a ring of 16). **Ravenous** (30–10%): **the Maw**, a 90° cone in front of it: shots into it heal it for half, shots on its back do ×1.25; it turns to the nearest player at 60°/s (35°/s if you're alone). **Frenzy** (6 s: four lunges, 0.55 s warning, each ending in a ring of 12), **Jaws** (6 s: twice, two rows of 7 teeth 320 px either side of a player show for 0.6 s, then close at 140 px/s), **Vomit** (5 s: a spray of bile sweeping 70° either side of you and back). Below 10%, its **last meal** in the middle: it pulls everyone in at 55 px/s, ten teeth (two gaps) turn round it 170 px out, its spiral comes back at half the rate; being swallowed here does 20 a second |
| 50 | **The Eclipse** | The final boss: **five stages** and a last stand, one attack at a time with breathers between (2.5 s between stages, when the screen clears and it can't be hurt), about twice as long as any other boss. A sun and its moon line up as it goes, and each stage keeps something from the last. **Corona** (100–80%): it orbits the middle; its 4-arm **Spiral** and 24-shot **Corona rings**, and **Solar flares** (a line at each player and one more, 1 s warning, then a 40 px lane of fire, 25, +3% a wave); three sun shots in the breathers. **Penumbra** (80–60%): a **moon** orbits it 300 px out; it can't be hurt and **soaks your shots**. **Lunar wake** (the moon speeds up, trailing slow shots), **Eclipse flash** (the moon stops and the sun swells for 3 s, then everyone **outside the moon's shadow** takes 40, +3% a wave), **Gravity** (its old pull and 4-way shots); two Phantoms. **Umbra** (60–40%): the arena goes **dark**; you see 140 px round you and in three drifting **sunspots**, and outside them the dark drains 6/s. **Crescents** (blades that loop out through you and come back), **Closing umbra** (rings of shadow close in with two gaps), a **Dark spiral**; two Hexers. **Echoes** (40–20%): turns borrowed from earlier bosses: the **Colossus**'s stomp rings, the **Lich**'s twin soul beams, the **Storm Herald**'s strike rain and the **Singularity**'s pull and disk; the moon shoots back. **Last Light** (20–5%): the arena's **edges burn** and close in 160 px (you can't pass, and touching burns); a frantic 5-arm spiral, **Shadow nova** (two flashes, the moon closer and jumping round between them) and a **Solar storm** of flares and crescents. **Totality** below 5%: the moon is gone; it takes **+50% damage** while a **12 s nova timer** runs, and the nova hits everyone for 70% of their health and heals it 2% before starting again |

## Loadout preview

The lobby's loadout screen shows your loadout in a small live arena (top left): your character uses the chosen weapon
on three target dummies, so you can see how it attacks. A **zoom meter** along the top of the window (it fades in when the mouse is over the preview; the mouse wheel works too) zooms the view from 50% to 200%. Hover an ability button (or pick one) and it casts
that ability every few seconds. It's the real game running privately on your computer (nothing is sent to
the other players), so it always matches how weapons and abilities really behave.

## Menu backgrounds

**Settings > Visual effects** has a **Menu background** card that switches the animated background behind the menus and
lobby. There are 28:
Spiral, Warp, Aurora, Synthwave, Bokeh, Hex Pulse, Orbits, Digital Rain, Nebula, Tides, Ember Storm, Constellations,
Radar, Blueprint, Bullet Curtain, Frost Peaks, Circuit, Event Horizon, Firefly Wood, Plotter, **Dusk Grid** (a
rank-15 reward) and seven **reactive** backgrounds that answer your pointer and clicks (frozen with STILL), all rank rewards:
**Ripples** (25: rings spread from your clicks and a soft light follows the pointer), **Boot Log** (28: a green BIOS terminal
that logs faster as you move, lights the line under the pointer and logs each click with its position), **Vector Rocks** (39:
neon asteroids drift away from a little ship on your pointer; a click fires a ring of shots that splits them) and **Life Grid**
(43: Conway's Life in amber phosphor, seeded where the pointer goes; a click launches gliders), **Phosphor Pond** (47: a dot
grid that ripples where the pointer goes, a click makes a big ring), **Vector Tunnel** (49: neon gates rushing past, the
vanishing point leaning toward the pointer, a click is a speed burst) and **Signal Lost** (53: a phosphor test card that
tears at the pointer's height and rolls on a click). The reward ones are greyed and skipped until you have them. Each has its own
colour, which tints the card. The card shows a live thumbnail (click it for the next one), arrows either side of the
name and a strip of coloured swatches, one per background (hover one to see its name, click to jump to it). Click
**LIVE** to freeze the background into a still image (it then reads **STILL**). Click **BIGGER** to see the
background full screen with only a small bar over it (name, arrows or the left and right arrow keys, LIVE / STILL and
Back, or Esc). Each player's choice is saved on their
own computer. Your pick also shows faintly behind the arena during matches (dimmed so the fight stays easy to read).

## Arena props

Regular waves (not bosses, not the Practice Range) put things in the arena; boss fights only get health barrels.
What's left at the end of a wave stays for the next, and each wave tops it up; a boss wave clears it all away.

| Prop | What it does |
|---|---|
| **Crate** | (With Visual effects on, crates, pots, barrels and the chest draw as glowing holograms: glowing wireframes that drop out, flicker and turn pink as they wear; off keeps the wood and clay.) Cover. Enemy shots stop at it, red aimed shots included, and each one wears it down (8 hits + 1 per 3 waves); your shots fly over it. You and enemies can't walk through it. Two often stand side by side as a little wall. 2 crates at wave 1, one more about every 3 waves, up to 10 |
| **Iron-bound crate** | From wave 12, about a third of the crates: twice as tough |
| **Pot** | Shoot it or walk through it: it smashes into a health orb (35%) or a little XP for everyone. 1 at first, up to 5 |
| **Barrel** | Rare (from wave 3, a 25-45% chance of one a wave, sometimes two from wave 20). Any shot or blast sets it fizzing; 0.4 s later it blows up 120 px round: 40 + 10 per wave damage to enemies, 16 to players caught in it (scaled like enemy damage), smashing crates and pots and setting off other barrels |
| **Health barrel** | Boss fights only: a green barrel with a white cross drops in every 18-26 s (the first after 15 s; two at most), away from the boss. Shoot it or walk into it and it breaks into 2 health orbs |
| **Treasure chest** | Rare (8% of regular waves from wave 2; always on the first wave after a boss if a player has the Contraband perk). Touch it to open it: **each player gets their own item**, a card spinning in a beam of its rarity's colour that **only they can see** (uncommon to very rare from their own pool, or a 20% chance of a legendary from a boss met so far or the next one). Walk onto it to see its card, then **G** takes it or **X** throws it away. Items never despawn, and **the wave waits** until the chest is open and every item is dealt with (anyone down gets back up to go and collect) |

## Arena layouts

Each **boss cycle** (the regular waves between two bosses) gets one of six fixed floor plans, picked at random and never
the same one twice running. Boss waves, the Practice Range, challenge rooms and the loadout preview are the plain open
arena. A teal chip above the wave plate names the layout. Mockups: [`Docs/Mockups/arena-layouts.html`](Docs/Mockups/arena-layouts.html);
design notes: [`Docs/ArenaLayouts.md`](Docs/ArenaLayouts.md).

- **Walls and pillars** (glowing cyan outlines with diagonal hatching, the phosphor vector look of [`Docs/Mockups/arena-layouts-digital.html`](Docs/Mockups/arena-layouts-digital.html)) are solid: you and enemies can't walk through them, and they stop **every** shot, yours
  and theirs. Crates stay as they are: they wear away under enemy fire and your shots fly over them. Each layout
  starts the cycle with its own crates; random crates, pots and barrels still turn up, clear of the walls.
- **Edge gates.** Some layouts wall the edges of the arena. **Player gates** (teal chevrons) are the gaps you can wrap
  through, and each comes out of the matching gap on the opposite edge. **Enemy gates** (flickering red holograms with a dashed rim, part of the
  wall) are where enemies come in; squads form up in front of one. A layout with open edges lets you wrap anywhere
  and enemies come in along the edges it allows. Narrow Pass, Tiny Arena and Solid Walls close the arena, so the edge
  gates drop away (the inner walls stay).
- The host can turn layouts off for a run on the lobby's **Difficulty** tab (**Layouts off**, beside the boss approach picks).
- Enemies that walk steer round a wall that's in their way instead of pushing at it.
- With a layout up, regular waves have **30% more enemies** (waves 1-4 on Normal: 16, 17, 18 and 20 instead of 12, 13, 14
  and 15), because the walls and gates make the crowd easier to read. Boss waves are unchanged. The number is
  `LayoutEnemyMult` in `Balance.cs`.

| Layout | Edges | What it is |
|---|---|---|
| **Pillar Ring** | Centre gates | Eight pillars round an open middle (gaps about 135 px), crate pairs in the corners |
| **Twin Lanes** | Sides only | Two long walls with a 120 px door in each: a middle corridor and two outer strips. You can only wrap left and right |
| **Cross** | Twin gates | A plus-shaped wall: four rooms round a 160 px hub, a crate pair in each |
| **Islands** | Open | Crate clusters (a heavy 2 x 2 block in the middle) and two small pillars; the layout wears away as crates break |
| **Funnels** | Open | Two walls near the sides with two 120 px gates each; enemies only come in from the left and right |
| **Bunker** | Centre gates | A 320 x 240 fort with a 90 px door on each side and a hold point (ring) inside |

Edge sets: **Centre gates** (one player gate in the middle of each edge, two enemy gates beside it), **Twin gates** (two
player gates per edge, one enemy gate between them), **Sides only** (top and bottom walled, so you can only wrap left
and right; enemy gates on all four edges).

## Paths

After each boss (and its rewards), the host picks one of **two paths** for the four regular waves before the next
boss, or **no path** (the plain arena, no bonus or prize); everyone sees the choice, and the next wave waits for it.
Bosses are always the standard fight. A path changes the enemy mix and the arena floor, and pays off by its danger
(1-3 skulls):

| Danger | While on the path | Prize (before the boss) |
|---|---|---|
| 1 | no bonus | full heal and 2 rerolls |
| 2 | +25% XP and score | +1 upgrade pick |
| 3 | +50% XP and score | an extra legendary pick |

| Path | Danger | What changes |
|---|---|---|
| **Swarm Fields** | 2 | Mostly melee rushers (Swarmers, Spikers, Brutes, Lungers), twice as many, at 60% health |
| **Sniper Ridge** | 2 | Mostly Snipers, Mortars and Grunts; hardly any melee |
| **Elite Gauntlet** | 3 | Half as many enemies, every one elite |
| **Burning Grounds** | 3 | Fire patches flash a warning ring, then ignite and spread for a few seconds; they burn you (10 damage a second, plus 3% a wave on Normal) and the enemies |
| **Sanctuary** | 1 | Healing pools (10 HP a second) appear around the arena; 20% fewer enemies |
| **Frozen Lake** | 2 | You slide with momentum (speed builds up and carries after you let go); enemies are 10% slower |
| **Blood Moon** | 3 | No health orbs and no regeneration, but every kill heals its killer 2 HP (+0.1 per wave) |
| **Fogbound** | 2 | A mist hides everything more than about 350 px from every player (shared sight: stay close) |
| **Narrow Pass** | 2 | Glowing canyon walls close in to a 1120 x 720 pass: no wrapping round the edges, and bullets stop at the walls |
| **Bazaar** | 1 | The usual enemies, but every kill drops gold; its prize is a shop (below) instead of the heal and rerolls |

A chip above the wave panel shows the path and the waves left. Session recordings note each wave's path. The
mist rolls in and the walls close in during the countdown before a path's first wave, and lift before the boss.
Design notes and the decisions behind them are in `Docs/Paths.md`.

### The Bazaar's shop

On the Bazaar's waves every kill gives **every player the same gold** (0.3 per point of XP the kill is worth: about
325 each over waves 6-9, rising to about 1000 by wave 40); your gold shows above the wave panel. After its last
wave, everyone gets a shop before the boss:

- **Your shelf** (rolled for your class): four upgrades, one legendary and one weapon mod. Prices by rarity: common
  40, uncommon 60, rare 100, very rare 160, weapon mod 200, legendary 280. Click a card to select it (your stats
  preview it), click again to buy.
- **Services:** restock the shelf (25, then +25 each time), remove an upgrade (75; one stack, and abilities it gave
  go too), full heal (40), and a revive token (120; one at a time: when you go down you get back up 2 s later at
  half health, and it isn't game over while it's waiting).
- **Give** sends a teammate 10, 50, 100 or all of your gold. Giving to someone who's already done reopens their
  shop so they can choose what to do with it.
- **No timer:** the shop closes when everyone has clicked **Done shopping** (click again to keep shopping until the
  last player is done). After a minute the host can close it for everyone. When it closes, each player's leftover
  gold becomes their own XP (1 gold = 1 XP).

## Challenge rooms

Once in every five waves a **portal** swirls open during the break: after the 1st, 2nd or 3rd wave of each stretch
(in the first stretch after wave 2 or 3), so never right before a boss. A card over it names the challenge on the
other side. Everyone alive stands in it to go through: it fills faster the more of you are in it, but only goes
when you all are. The break's countdown waits while it's open; ignore it for 15 s (counting only while nobody's
choosing cards) and it closes, and the run carries on as usual.

Through the portal is a **challenge room**: the same arena with its own ground (a tint in the challenge's colour, a
rune circle, glyphs round the walls). A title card names the challenge and what it takes, and 3 s later it starts,
with an objective plate at the top of the screen:

| Challenge | Goal |
|---|---|
| **Defend the Crystal** | Keep the crystal in the middle alive for 40 s. Its enemies are mostly melee (Swarmers, Spikers, Lungers, Brutes; about 1 in 7 is a Grunt or Sniper), so it's about cutting off the bodies rushing it: two in three go for it (their hits and shots wear it down); its health shows as a ring round it. 600 health, +30% for each extra player. |
| **The Gatekeeper** | Beat a mini-boss with its own boss bar: a ring of big orbs with a gap, three aimed fans, and a charge along a red line that ends in a burst. Below half health it attacks a third faster and rings twice. It has half the Warden's health at your wave. |
| **Kill Rush** | Kill 40 enemies in 40 s (+14 for each extra player). Weak enemies (half health) pour in from every side. |
| **Hold the Zones** | Stand in each of three zones in turn until it fills (8 s; half as fast with an enemy in it, and it drains slowly if you leave) within 60 s. Half the enemies crowd the zone. |
| **Mirror Match** | A shadow of each player: their class and weapon in dark violet. Each hunts its own player, keeps the distance their weapon wants, sidesteps every few seconds and fires their way (a fan as wide as their extra shots, or a slash up close). Each has 30% of the Warden's health. |

**Win** and the room's enemies vanish and an **altar** rises in the middle: touch it (or wait 12 s) and everyone gets
a **Challenge prize** pick: three cards, each rare or very rare (no rerolls), and a 1 in 4 chance of a legendary
pick too. **Lose** (the crystal breaks, the clock runs out, or everyone goes down) and you're simply sent back:
it's safe to fail, the downed get back up at 30% health and nothing else is lost. Either way the break then starts
over. The room's two enemies are in the Field guide under **Challenge rooms**. Developer tools (F1, Match) can take
everyone straight into any of the five.

## Run modifiers

The lobby's **Modifiers** tab lists 18 switches (tiles in two columns, each with an icon, its name, what it does and an ON/OFF key like Ready's, grouped under **Easier**, **Risky** and **Harder**
headings, each tile marked with its tier's colour; click anywhere on a tile to switch it) that change the whole run, for everyone (Practice
Range too). The host turns them on and off; the others see them, and changing one takes back everyone's Ready. Active
ones show as chips above the wave panel during the match, and session recordings list them. Each tier has its own heading;
click its heading to fold it away (folded, it says how many are hidden and how many are on; remembered on your
computer). When the open tiers don't fit, the list scrolls with the mouse wheel.

| Modifier | Effect | |
|---|---|---|
| Glass Cannon | Everyone deals 50% more damage, and takes 50% more | Risky |
| Tough Crowd | Enemies and bosses have 50% more health | Harder |
| Frenzy | Enemies and bosses shoot 30% faster (the bullet budget still applies) | Harder |
| Swift Foes | Enemies move 30% faster | Harder |
| Famine | Enemies never drop health orbs (bosses still do) | Harder |
| Lucky | Everyone starts with 3 extra upgrade rerolls | Easier |
| Head Start | Everyone starts at level 3 (its two picks waiting) with a free legendary pick from the first boss's pool (not in the Practice Range) | Easier |
| Practiced | Abilities gain mastery twice as fast | Easier |
| Bloodlust | Every kill heals its killer (2 HP + 0.1 per wave, like Blood Moon), but enemies don't drop health orbs (bosses still do) and nobody regenerates (the Healer's aura stops healing themselves; it still heals allies) | Risky |
| Gold Rush | Every kill gives everyone gold (like the Bazaar), and a shop opens before each boss; gold you don't spend is kept for the next shop instead of turning into XP | Easier |
| Tiny Arena | After each boss, walls close in and take a quarter of the room (of its area), down to 40%. No wrapping round the edges while they're up, and bullets stop at them. They open for each boss's countdown and fight (not in the Practice Range) | Harder |
| Nightfall | Dark everywhere except 210 px round each player and a lantern beam (560 px, 48°) toward where each player aims; downed players lose their lantern | Harder |
| Hunted | As each wave starts, one player (at random, among those standing) is hunted: every enemy goes for them (a Roar still pulls enemies off), and they deal double damage. Red brackets turn round them, and the HUD chip says who. While they're down, enemies pick someone else | Risky |
| Death Throes | Every regular enemy (not bosses, nor things that give no XP) bursts into a ring of 6 slow shots 0.5 s after it dies, with a warning circle. Killing up close costs you | Harder |
| Solid Walls | No wrapping round the edges: the arena's sides are walls (drawn as a glowing border), and bullets stop at them | Harder |
| Cluttered | Every wave piles a lot more crates into the arena: 17 on wave 1, +3 a wave, up to 48 (a normal run has 2 to 10), heavy crates from wave 8. They stay until enemy fire breaks them, so the field turns into a maze you have to wind through. Your shots still fly over them; enemy shots stop at them. Boss fights clear the arena, as usual. Not in the Practice Range | Harder |
| Second Wind | Once per boss cycle (waves 1-5, 6-10, ...), each player who goes down gets back up by themselves 2 s later at half health; it's used before a revive token. The HUD chip shows whether yours is ready. While anyone holds one, everyone being down isn't game over | Easier |
| Zombie Infection | Every regular wave is a horde of green zombies, and the arena turns dark green. The count is the wave's usual x1.5 at wave 1, growing to x3 by wave 20, at most 60 alive at once (the rest come in as others fall), arriving faster so a bigger wave doesn't take longer; each zombie's XP is split so levels keep pace. **Seven kinds** (see the Field guide's Zombies group): **Shambler** (wide triangle, slow, hits hard; from wave 1), **Runner** (narrow triangle, faster than you, packs of three; 3), **Spitter** (pentagon; hangs back and lobs acid that burns 10/s and slows 30%; 6, at most 4 a wave), **Bloater** (swelling circle; bursts into a spore cloud that slows 40%, on you if it reaches you; 8), **Crawler** (trapezoid; grabs and roots you for 1.5 s, 6/s, until you shoot it for 25; 9, at most 3), **Shrieker** (diamond; screams every 6 s, frenzying the zombies within 320 px: 30% faster for 4 s; 12, at most 2), **Mender** (cross; heals the zombies within 220 px by 15% every 3 s; 15, at most 2). **The dead rise:** a killed zombie has a 25% chance to get back up 3 s later (a green marker where it lies) at half health, worth no XP, and it won't rise twice; near a Mender it's 50%, in half the time. One that was burning or cut down by a melee weapon stays down (a Bloater never rises), and the wave isn't over until they're up and beaten. **Sounds:** a groan now and then from the horde, a wet rasp as the dead get back up, a spit, a grab, the Shrieker's scream, a Bloater's burst and a Mender's chime (volume: Enemies). Bosses, their adds and challenge rooms are unchanged. Not in the Practice Range (the zombies are in its picker, though) | Harder |

## Difficulty

The lobby's **Difficulty** tab: the host picks one of five cards, and everyone sees it (changing it takes back
everyone's Ready). It applies to the whole run, Practice Range too, is kept in saved runs (a continued run keeps its
difficulty), shows as a chip above the wave panel during the match (not on Normal), and is written in session
recordings. **Custom** lets the host set every number below with a slider or by typing it in the box beside it
(percent numbers are typed as percent: `12` is 12%); changing any number turns the difficulty into Custom, starting
from the preset you had. Each slider has a tick where Normal is, and a value shows red when it's harder than Normal
and green when it's easier. Hover a number for what it does.

| Number | Easy | **Normal** | Hard | Nightmare | Custom range |
|---|---|---|---|---|---|
| Enemy health (enemies and bosses) | 0.75x | **1x** | 1.25x | 1.5x | 0.25-4x |
| Boss health (on top) | 0.8x | **1x** | 1.2x | 1.4x | 0.25-4x |
| Solo enemy health | 70% | **85%** | 100% | 115% | 40-150% |
| Enemy damage (bullets, touching, hazards) | 0.75x | **1x** | 1.25x | 1.5x | 0.25-4x |
| Damage growth per wave | +1.5% | **+3%** | +4% | +5% | 0-10% |
| Enemy count (regular waves) | 0.85x | **1x** | 1.15x | 1.3x | 0.5-2.5x |
| Enemy fire rate | 0.9x | **1x** | 1.1x | 1.2x | 0.5-2x |
| Enemy speed | 1x | **1x** | 1.1x | 1.2x | 0.5-2x |
| Elites (chance, from wave 12) | 0.5x | **1x** (up to 35%) | 1.5x | 2x | 0-3x (at most 90%) |
| Bullet budget | 0.85x | **1x** | 1.15x | 1.3x | 0.5-2x |
| Adds' fire in boss fights (share of the budget) | 40% | **60%** | 75% | 90% | 0-100% |
| Regeneration | 0.5 HP/s | **0.25 HP/s** | 0.15 HP/s | 0 | 0-3 HP/s |
| Health orb drops (small) | 18% | **12%** | 9% | 6% | 0-50% |
| Revive time | 3 s | **4 s** | 5 s | 6 s | 1-10 s |
| Hit grace (after a hit) | 0.35 s | **0.25 s** | 0.2 s | 0.15 s | 0.05-1 s |
| Boss approach (the themed share of the wave before a boss) | 85% | **85%** | 85% | 85% | 0-100% |
| Approach ramp (build up over the four waves) | On | **On** | On | On | On / Off |
| Arena layouts (a fixed floor plan each boss cycle) | On | **On** | On | On | On / Off |

The two **boss approach** numbers are flavour, not difficulty: they're the same on every preset, changing them doesn't
make the difficulty Custom, and picking a preset keeps them. **Blend** and **Fully themed** buttons under them set
both at once (60% with the ramp; 100% without).
The **Arena layouts** setting is flavour too: **Layouts on / Layouts off** buttons beside them. Off keeps the open arena all run
(and the usual enemy count); see Arena layouts above.

Easy is close to how the game played before difficulties (without the per-wave damage growth). Damage growth
multiplies with Enemy damage: Nightmare at wave 11 hits 1.5 × 1.5 = 2.25x as hard as a listed shot. Modifiers stack
on top (Tough Crowd, Frenzy, Swift Foes, Glass Cannon...).

## Boss Hunt

In the lobby the host picks **Run**, **Boss Hunt** or **Practice** at the top. A Boss Hunt is a short run that ends
with one boss: **ten waves**. Waves 1-9 bring only the boss's own enemies (its faction: for the Warden, Rioters and
Guards, then Riot Shields and Gas Grenadiers, its Breakout and Cell block squads, and its Riot line on waves 5 and
9); wave 10 is the boss, at wave-10 strength. Beat it and the hunt is won (**HUNT COMPLETE**); there's no endless, no
path offer, no challenge portal, and a hunt isn't saved to continue. Modifiers and the difficulty apply as in a run,
and so does the boss's health boost for strong builds. The HUD and the Tab screen count the hunt's waves (n / 10).

The **Boss hunt** tab is a dossier: a looping drum of the ten bosses (status tag: SOON, LOCKED, OPEN or WON xN; mouse wheel, PREV / NEXT or a click rolls one to the top, and the list never ends). The boss on top is the picked one: the host can scroll onto a locked or coming-soon boss to look at it, but START stays off until the top boss's hunt is open. For the top boss it shows a green-edged target scope with its live model, the wave ladder, your hunts won, best time and the hunt's rules. A boss's hunt **opens once you've beaten it in a
run** (the host's records decide, in co-op); the others say Locked or Coming soon, and the match won't start on a
locked one. **Only the Warden has a hunt so far**: a hunt's build-up waves need the boss's faction (only the
Warden has one yet), and later bosses will need the party handed a build that fits their strength. Records keep each hunt's plays, wins and best time per player
(not counted as runs). Winning a hunt unlocks that boss's perk (below). Plan: `Docs/BossHuntsAndPerks.md`.

## Perks

A perk is a rule you bring into a run, one per boss, unlocked by winning that boss's **Boss Hunt**. Pick them on the lobby's
**Perks** tab: your **slots** on the left (one to start, a second after you win a full run, a third after you win five different
hunts), and a card for each perk on the right. Click an unlocked card to equip it, click an equipped card (or the slot's
*remove*) to take it off, and hover a card for the rules. A second perk from the same boss takes the first one's slot. Everyone
brings their own perks in co-op, and each seat shows its perk icons on its party card. Perks are off in the Practice Range, the
Boss Lab and the lobby preview. A saved run keeps each seat's perks, and when you continue it the tab is read-only and shows
what the seat was saved with. Equipped perks are saved with your profile.

| Perk | Boss | Unlocked by | Effect |
|---|---|---|---|
| **Riot Gear** | The Warden | winning the Warden Hunt | A riot-shield plate blocks the next hit that would hurt you: no health lost, no damage counted, and half a second of invulnerability. Then the shield takes **30 s** to come back (the timer keeps running between waves and while you're downed). You start every run with it ready. It blocks one hit from an enemy: a shot, a swing, a blast, a boss slam. **Burning, gas, acid, lava and other damage-over-time ticks go through** and don't spend it. |
| **Contraband** | The Warden | winning the Warden Hunt on the Hard or Nightmare preset | **A treasure chest always turns up on the first wave after each boss** (waves 6, 11, 16...) when anyone in the party has it, on top of the random ones (not in Boss Hunts). **Your own item from any chest is rare or better** (never uncommon); teammates still get their own normal item from the same chest. See `Docs/Contraband.md`. |

The plate hovers at your side and swings toward the nearest incoming enemy shot (resting behind you otherwise). While the shield
recharges a ghost plate fills along its length, and it pops back when ready. A block flashes the plate white, throws sparks off
the impact and breaks the plate into three pieces that fly off (with visual effects reduced or off, only the flash). A small
**Riot Gear** chip above the ability bar's XP strip fills from the bottom and shows the seconds left, then READY. Teammates' plates
show too. A boss's perks share one slot, so equipping Contraband takes Riot Gear's place. Other bosses' perks come later: the tab shows them as locked placeholders. Runs don't yet note which perks were used (a
"no perks" record variant is planned). Plan and build notes: `Docs/RiotGear.md`, `Docs/BossHuntsAndPerks.md`.

## Practice Range

In the lobby the host picks **Run** (the normal 50 waves), **Boss Hunt** (above) or **Practice** (the Practice Range)
at the top. Practice Range plays waves you build yourself, with everyone in the lobby, and upgrade picks between waves
as usual.

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
- Everyone keeps their own loadout (the **Loadout** tab).
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
| Dodge | Ctrl, or double tap a movement key (changeable). A tiny 95 px dash with 0.22 s of invulnerability; 1.1 s cooldown. With no direction held it steps back from your aim |
| Shoot | Hold left click (changeable) |
| Role ability | Space |
| Parry | Hold right click (changeable) |
| Weapon secondary (Kunai blink, Twin Blades lunge, Shuriken recall, Bo Staff shove, Kusarigama reel) | Left Alt |
| Ultimate | F |
| Take / discard your chest item (standing on it) | G / X |
| Bonus abilities | Q, E and Shift (once unlocked). When a card gives you one, choose its key on the card screen; move them between keys any time in the Tab menu (cooldowns move with them) |
| Pick an upgrade | Click a card (or 1 / 2 / 3), then Confirm (or Enter) |
| Reroll upgrades | Reroll button or R (twice: the first arms it) |
| Ready for the next wave | Ready button or Y (rebindable), once your upgrade picks are done. Auto-ready switch beside it |
| Mute sound | M |
| Ping | Middle mouse (rebindable), in a match. **Tap** to mark your cursor with the ping you used last (Here to begin with). **Hold** for 0.2 s and a six-slice wheel opens at the cursor: move to a slice and let go to send it, let go in the middle to cancel. Everyone sees it in your colour (teal, amber, violet) as a ripple, a beam of light and a glyph ring with your name for a second. **Here** (5 s), **Focus** (6 s: snaps to the enemy nearest your cursor, rings it and follows it until it dies), **Danger** (4 s, pulses faster), **Help** (5 s: also rings you), **Loot** (5 s) and **Go** (5 s). Three live per player (a fourth replaces your oldest), 0.4 s between pings. A click doesn't fire while the wheel is open |
| Emotes | Hold T (rebindable): a see-through bar of every emote your rank has opens above the ability bar (in the Hangout, at the foot of the room). Scroll the wheel or click the arrows at its sides to move along it, click one in view or press its number (1-6, the slot it sits in: the numbers stay put, so what a key plays depends on how far along the bar you have scrolled) to play it. While it is open a click picks instead of firing. Let go and it closes |
| Stats, upgrades and key layout (just for you) | Hold Tab (click a key on a bonus ability to move it). Works while choosing an upgrade too: the cards come back when you let go |
| Pause menu | Esc |
| Change any of these keys | Settings → Keybinds (see below) |
| Developer tools | F1 (see below) |
| Kill every enemy on the field (dev, host) | X, once the F1 Match tab's kill-all key is on |
| Camera lean on / off | Settings → Gameplay (on by default). The view eases a few percent toward where you aim and move; the arena and wrapping are unchanged |
| Aim line on / off | V (the line runs from you to the crosshair, but never past your weapon's range: as far as your shots fly, or your swing reaches; a small cap marks the end when the crosshair is beyond it) |
| Hide / show the HUD | H, or Settings > Gameplay; the mode is remembered between launches (H cycles four modes: Full, Corner text (health, cooldown numbers and enemies left as plain text in the corners), On the player (a health arc, cooldown numbers under you, enemy count at the top), Hidden; it slides away and fades, "Press H to show the HUD" stays faintly at the bottom while it's hidden, and a slim health bar for each boss stays at the top in every mode but Full; also a Settings choice) |
| Aim debug overlay | F3 |
| DPS meter on / off (host); reset it | F2; Shift+F2 (see *DPS meter and target dummy*, below) |
| Freeze time with no menu (dev) | P, once the F1 Tools tab's freeze-time key is on; everything holds, attacks and effects mid-swing included; P again resumes. Pausing is shared, so it stops everyone |
| After game over / victory (host) | R = play again, L = back to lobby, C = keep going (endless, after victory) |

### Keybinds

**Settings → Keybinds** (main menu, lobby and pause menu) lists every control: the four move keys, the role
ability, the Ultimate, the three bonus ability keys, **Fire** and **Parry** (left and right click by default; each can
be any key or mouse button), the weapon secondary (Left Alt), take and discard a chest item, reroll, and the aim line,
HUD and mute toggles. For Fire and Parry, a "Left click / Right click" pair of buttons appears while you're picking
(the page can't take a click as a key press, since the click is how you chose the row). Left and right click can't go on
any other action, since they click everything else. Click a key, press the new one (a middle or side mouse button works too), and it
changes at once; **Esc** cancels. A key already used by another action swaps with it, so nothing ends up on two. The
arrow keys always move too. The ↺ beside a changed key puts it back; **Reset all** puts every key back. Saved on this computer. The menu keys
(Esc, Tab, 1-3, Enter, F1-F4) can't be changed, and menus are always clicked with the mouse. The HUD, the Tab screen and the
How to play page show your own keys.

## Launching a run

When a run starts it drops you in, all within the 3 s before wave 1 (on your own screen only): the lobby's two sides
slide off left and right; a ripple runs out from the middle of the arena, bending its grid; each player beams down as
a pillar of light in their colour, a quarter second apart, landing with a ring and their class icon flashing above
them; the HUD slides in; GET READY counts down; and a gold **WAVE 1** pops in as the fight starts. You can't move,
dash, shoot, parry or use abilities until then. **Play again** from the results screen does the same without the lobby
part.

## Ending a run

When the run ends, time slows almost to a stop on the killing blow and the camera eases in on it. On a game over the
colour drains out and a red vignette closes in; on a victory the screen flashes white, The Eclipse bursts into gold
rays and its shots turn into gold sparks. GAME OVER or VICTORY! slams down with a shake and a thud, then glides up as
the results come in: the stat tiles drop in one by one, the player cards rise and the buttons come up last. The slow
motion is the game's own clock, so everyone sees it; the rest is on your own screen.

## The HUD

Everything sits along the bottom, so the top of the arena stays clear: the **ability bar** (with your XP strip)
in the middle, the **wave panel** left of it (wave, enemies left, score, progress; the path and modifier chips
stack above it) and **your card** right of it (health, shield, level, XP). Teammates show as slim rows above
your card: name, class, level and health (or their revive progress when downed). Boss health bars go wide across
the top middle, and the "Hold TAB" hint is in the top-right corner.

**Between waves.** When the last enemy dies a **Wave n cleared** box drops in: a CRT screen like the lobby's, in your
CRT colour (Settings > Visual effects), its title gold after a boss. It shows the wave's **clear time** and **score**, a row per player with **kills, damage
dealt and damage taken** for that wave, and **awards**: **Untouched** (took no damage), **Reviver** (stood a teammate up),
and in a party **Most damage**, **Most kills** and **Most healing** (only for a single leader with something to show).
A **Squad** row adds them up. On the right: the enemy types the next wave draws from (new ones marked NEW; a boss wave
names its boss) and the **road to the next boss**. Along the bottom: the **Ready** key (Y), the **Auto-ready** switch, the
status and the countdown bar. The next wave only starts once **everyone is ready**, **3 seconds** after the last Ready, and there is no
timer while anyone is still waiting. **Auto-ready** is remembered between runs: you count as ready as soon as your upgrade
picks are done, and when everyone has it on the countdown is the usual 4 seconds, so solo play with it on moves at the
old pace (it is off by default). A small box beside each player shows who is ready (a tick, an A for auto-ready, dashed while they aren't).
The first countdown of a run, the Practice Range and the dev tools' wave jump don't use ready-up. Mockup:
`Docs/Mockups/wave-cleared-ready.html` (option 1).

**Chat.** In a match, press **Enter** to open a text line at the bottom right, above your card and the teammates'
rows. Type (up to 120 characters), **Enter** sends and **Esc** cancels. While you type, movement, abilities and
the pause key are ignored. Each message is a small pill with an edge in its player's colour; lines fade away one by
one after about 6 seconds, and the box is gone when it's quiet (a line from a teammate shows it again for a moment,
without taking your keyboard). The line also closes itself after 12 seconds with no typing. The host keeps it to
about one message a second per player. Mockup: `Docs/Mockups/chat-box.html`.

## End of a run

When a run ends a results screen in the level-up picker's style says **GAME OVER** (red, with the boss that
stopped you) or **VICTORY!** (gold), with the wave reached, time, score and total kills as tiles, then a card per
player: their class icon big on a backdrop in their colour, their role, level and awards (a trophy for each row
where they had the best number: **Most damage**, **Best healer**, **Sharpshooter**...), and beside it their
damage dealt, kills (elite and boss), biggest hit, accuracy, healing done, allies revived, damage taken, times
downed and abilities used, each with a bar against the party's best (gold where they lead), and the upgrades
picked by rarity. Under the cards a **rank strip** shows what the run earned towards your account rank (see **Rank**
below), and each card carries that player's rank badge. Click the strip, or the **Rank report** button next to Leave
(it pulses gold on a rank up), for the full **rank report**: your badge, name and XP bar with the breakdown ticking in
line by line (waves, bosses, revives, victory, then the modifier and difficulty multipliers and the total), the bar
filling and, on a rank up, RANK UP! with a flash; your party's ranks; and the reward you've just earned (with a preview
of the trail, frame, background or title) or the next one to go for and how much XP it needs. **Stats** goes back. **Leave** sits bottom-left; the host can play
again, go back to the lobby, or keep going after a victory.

## Lobby Hangout

The lobby's **Hangout** (a key on the Party line, which becomes **< Loadout** while you are in the room) is a room where everyone's character walks around. Move yours with **WASD** (or the arrow keys,
or your rebound movement keys); the others in the lobby move theirs, and everyone sees everyone. Each character is
your real class and weapon model, with the **trail** you have equipped behind it, your **rank badge** and a name plate
with your **frame's** colour (and a green dot once you've readied up). Works in every lobby mode. Design: [`Docs/Hangout.md`](Docs/Hangout.md).

The room is a big map (2400 x 1600) that you see through a window: the view follows you and stops at the glowing teal border. A **minimap** at the window's top left shows where everyone is. Switching to the Hangout from the Loadout tab
plays a 0.85 s grow: the loadout's preview window stretches into the room (it just snaps with reduced effects or from any other tab).

**PvP** is a checkbox under the minimap, **off** by default and yours alone; everyone sees each player's setting. With it **on** you face the mouse and **hold the left button** to fire your loadout weapon (its fire rate, shot speed and reach; one plain shot, none of the abilities, pellets, beams or swings), and other players with PvP on can shoot you. With it **off** you can neither shoot nor be shot, shots pass through you. Everyone has 100 health with a bar overhead; it comes back 3 s after the last hit. At 0 you are **knocked out** for 2.5 s (a ghost, can't move or shoot), then return at a spawn spot with full health. Turning PvP off heals you. The host decides every hit.

**Abilities** (PvP on only; **Space** or **right click**, one per role, like the very first build): a **DPS** has **Dash** (a quick 240 px lunge the way you walk, or the way you face when standing still; 2.5 s cooldown). A **Tank** has **Barrier** (a 360 degree bubble around you that deletes every other player's shots for 4 s; 10 s cooldown; your own shots pass out). A **Healer** has **Burst heal** (heals everyone within 230 px, you included, for 60 health and **revives anyone knocked out at once** with 60; 8 s cooldown). Your ability and its cooldown show under the PvP box. Turning PvP off drops a barrier.

## Rank

A **cosmetic account rank**. Every finished run (and Boss Hunt) earns XP; XP raises your **Rank**; ranks unlock
cosmetics only, never power. It is separate from the **Level** on the results cards, which is the in-run level.
Design: [`Docs/Rank.md`](Docs/Rank.md); mockups: `Docs/Mockups/rank-screens.html`.

**XP for a run:** 20 per wave cleared, 150 per boss defeated, 40 per ally revived and 500 for a win, times
(1 + each active modifier's bonus: **Harder +25%**, **Risky +15%**, **Easier +0%**, at most x3) and the difficulty's
step (Easy x0.75, Normal x1, Hard x1.25, Nightmare x1.5, Custom x1). A hunt counts its waves and the boss, with no
win bonus. The Practice Range and runs where dev tools (F1) or the bot were used earn nothing, like Records.
**Ranks:** rank *r* to *r+1* costs 200 x *r* XP (rank 8 needs 1,600). The tiers have titles: **Recruit** (1-4),
**Challenger** (5-9), **Veteran** (10-19), **Champion** (20-29), **Legend** (30+), shown by the badge's colour (steel,
blue, teal, violet, gold) and a chevron per step.

**Where it shows:** a **shield badge** with your rank on your portrait in the lobby (hover for the title and, for
yours, the XP), on the main menu's name chip (a generic player figure with your name, rank title, the badge and a thin
XP bar; click anywhere on it to open your profile) and on each results card;
the **rank strip** at the foot of the results (the badge, the bar, the total, how it was worked out, and RANK UP! with
what it unlocked); and **Profile > Rank** (your rank and XP, what is next, the reward ladder, the five tiers and what earns XP;
the last runs and their multipliers are in Records > Recent runs).

**Your look** is the profile's **Look** tab: click your name card on the main menu (see **Profile** below; a pencil key next to your name there renames you). Its keys are **Frame**, **Trail**, **Background**, **Card background**, **Name effect**, **Aim line** and **Cursor**; each opens a window to the right of the cabinet with **TRY IT ON** (a live preview of you: your trail, background, frame, name effect, aim line or cursor), **HOW OTHERS SEE YOU** (your lobby party card) and a scrolling list of every cosmetic of that kind with its picture, name, status (unlocked, default, or locked with the rank it needs) and an **EQUIP** key (the one in use says EQUIPPED; a locked one is a dashed padlock key with its rank). Hover a row to try it on in the preview and on the card; press EQUIP to keep it. The **Reward ladder** is in the Rank tab (a box with your next reward and the XP to go, beside a vertical ladder of rank shields that opens at your rank). One of
each kind at a time:

| Rank | Reward |
|---|---|
| 5 / 10 / 20 / 30 | Titles: Challenger, Veteran, Champion, Legend (automatic) |
| 8 | **Ember trail**: embers drift behind you as you move |
| 12 | **Steel frame**: a lit rim on your lobby party card and name chip |
| 14 | **Frost trail**: a pale chill line with ice crystals that stay where they formed, glint and melt |
| 16 | **Circuit frame**: a teal rim with bracketed corners and a pulse of light running round the edge |
| 15 | **Dusk Grid**: a 21st menu background (also pickable in Settings > Visual effects once unlocked) |
| 25 | **Ripples**: a reactive menu background (rings from your clicks, a light that follows the pointer; also pickable in Settings > Visual effects) |
| 28 | **Boot Log**: a reactive menu background (a BIOS terminal that logs your pointer and clicks) |
| 39 | **Vector Rocks**: a reactive menu background (an asteroid field with a ship on your pointer; click to fire) |
| 43 | **Life Grid**: a reactive menu background (Conway's Life in phosphor, seeded where you go; click for gliders) |
| 47 | **Phosphor Pond**: a reactive menu background (a dot grid that ripples where you go and click) |
| 49 | **Vector Tunnel**: a reactive menu background (neon gates rushing past, steered by the pointer) |
| 53 | **Signal Lost**: a reactive menu background (a test card that tears at the pointer and rolls on a click) |
| 18 | **Storm trail**: crackling static, a jagged yellow-white thread that re-rolls every few frames |
| 20 | **Ember Edge frame**: a flickering warm rim with embers lifting off the top edge |
| 22 | **Void trail**: a dark ribbon with a violet edge, motes pulled back toward you |
| 24 | **Void Pulse frame**: a violet rim with a breathing inner rim and two dark motes circling the card |
| 25 | **Gold stardust trail** |
| 30 | **Gold frame** |
| 40 | **Legend Foil frame**: gold with a bright band sweeping across the rim and twinkling corner stars |
| 35 | **Prism trail**: three ribbons cycling through the spectrum, with white glints |
| 45 | **Light Cycle trail**: a wall of light that follows where you've been but snaps to straight runs with right-angle corners; the floor tiles it crosses light up, and the wall breaks into squares that drift off as it ages (about 2.4 s). Cyan only. With Visual effects off it is one flat line |
| 50 | **Prism frame**: a rim cycling through the whole spectrum as it turns round the card |
| 6 | **Dusk Grid card**: a perspective grid sliding toward you under a violet horizon (the fill of your lobby party card, behind your name) |
| 10 | **Starfield card**: three layers of drifting stars and now and then a shooting star (the fill of your lobby party card, behind your name) |
| 13 | **Blueprint card**: cyan drafting paper with a grid, a dimension line and a circle that traces itself (the fill of your lobby party card, behind your name) |
| 19 | **Ember Glow card**: a warm glow along the bottom edge with embers lifting off it (the fill of your lobby party card, behind your name) |
| 23 | **Aurora card**: slow green and violet ribbons (the fill of your lobby party card, behind your name) |
| 27 | **Circuit Board card**: dark green traces with a pulse running along each (the fill of your lobby party card, behind your name) |
| 32 | **Rain on Glass card**: cool grey-blue with thin streaks and beads running down (the fill of your lobby party card, behind your name) |
| 37 | **Void Rift card**: a slow violet vortex behind your portrait (the fill of your lobby party card, behind your name) |
| 42 | **Gilded Foil card**: brushed gold with a sheen sweeping across (the fill of your lobby party card, behind your name) |
| 55 | **Prism Shards card**: spectrum shards turning slowly and overlapping (the fill of your lobby party card, behind your name) |
| 1 | **Wave emote**: a hand waves while you sway |
| 1 | **Thumbs up emote**: a thumb pops up, you bob |
| 3 | **GG emote** |
| 7 | **Cheer emote**: you hop, with a burst of confetti |
| 10 | **Heart emote**: small hearts floating up |
| 12 | **Laugh emote**: you shake with it |
| 15 | **Question emote** |
| 17 | **Sleep emote**: Zs drifting up while you sink |
| 21 | **Dance emote**: you sway to the beat, notes drifting off |
| 25 | **Spin emote**: two turns inside a ring of light |
| 31 | **Salute emote**: you snap upright |
| 41 | **Fireworks emote**: three bursts in turn |
| 52 | **Steam emote**: puffs of steam and a red bubble, you shake |
| 4 | **Steel Outline name**: a thin steel-blue line round white letters |
| 11 | **Frost name**: white fading to ice blue with a cold glow |
| 13 | **Ember name**: yellow fading to orange-red with sparks lifting off |
| 18 | **Neon name**: white letters with a cyan glow that breathes |
| 22 | **Wave name**: letters riding a gentle wave, cyan shading into violet |
| 27 | **Shimmer name**: silver with a white sheen gliding across |
| 33 | **Glitch name**: red and cyan fringes, and every couple of seconds a slice jumps sideways |
| 36 | **Glitchy name**: always glitching: letters jump, swap for symbols, the line splits and slides, the fringes pulse and it flickers |
| 38 | **Fire name**: yellow core to red tips, each letter flickering, flames licking up |
| 44 | **Gold Foil name**: rich gold with a sheen sweeping across |
| 48 | **Void name**: pale violet on a dark violet edge with a purple glow and circling motes |
| 56 | **Rainbow name**: every letter its own hue, turning round the spectrum |

**Aim lines** change the line from your weapon to the crosshair (the V key still turns it on and off). Classic is free; Dotted Tracer (rank 5), Chevron Flow (11), Ruler (14), Ember Wisp (17), Frost Shard (21), Storm Arc (26), Pixel Beam (29), Helix (34) and Prism (46) are rank rewards. Every look stops at your weapon's range and ends in a cap when the crosshair is farther; only you see your own line.

**Cursors** change your crosshair in a match. Classic is free; Fine Cross (rank 3), Corner Sight (4), Ember Ring (11), Frost Snowflake (15), Circuit Sight (17), Storm Arc (21), Void Eye (26), Gold Diamond (31), Prism Ring (38) and Light Cycle (46, it lays a corner-snapped wall of light behind the pointer) are rank rewards. Each swells for a quarter second when you fire. Your equipped cursor also replaces the system pointer in the main menu and lobby (the system pointer is hidden everywhere: each look draws its own Corner pointer, a small L-bracket in the look's colours with its own flourish (Ember sparks, Storm crackle, Circuit pulse, Gold twinkle, Prism spectrum, and so on). It collapses to a dot and the crosshair swells out as a match starts, and back again when a menu, the pause screen or the end screen opens; about 0.6 s, instant with reduced effects), and the Light Cycle cursor's wall fades out in under a second. Only you see your own cursor.

**Name effects** change how your name is drawn for everyone: on the lobby party card, the Hangout name plate and the tag over your head in a match (small names keep only colour and glow, so they stay readable; with Visual effects off each is still).

**Emotes** are a short bubble over your head for 2.4 s (a gesture, a heart, GG...) that everyone sees, in the Hangout and in a match; several also move your character (a hop, a sway, a shake, two spins), the model only, never your hitbox, and not with Visual effects off. Every one your rank has is on the emote bar (hold T); nothing to equip. After one there is a 2 s pause in a match (0.75 s in the Hangout).

Everyone in the match sees your trail, and everyone in the lobby sees your rank, frame and card background; the host clamps what
players report, and only accepts a frame or trail your rank has. XP, what's equipped and the last six runs are kept in
`profile.txt` (`xp`, `equip`, `rlog` lines).

## Saved runs and records

**Runs save themselves.** The host's game saves the run as each wave starts (once everyone has picked their
upgrades, the path is chosen and the shop is closed), and again if the match is left or the game closed between
waves. Leaving mid-wave keeps the save from that wave's start. A run that's won or lost loses its save. The
Practice Range, the lobby's loadout preview and Boss Lab fights are never saved.

**Play** on the main menu opens the saved-runs picker in the menu's box: a header screen (**YOUR RUNS**) with a **Back** key
beside it, and under it one scrolling screen with every save, newest first. The newest run is a tall row (wave, when,
score, time played, each seat's player and class, the path, difficulty and modifiers) with a big **Continue**; every
other run is a one-line row with its own **Continue**. Each row has a **DEL** key (click twice: it turns into
**REALLY?**). Scroll with the mouse wheel or drag the scroll bar; a "more" strip at the foot of the screen counts the
runs below. **New run** is under the screen. With no saves the screen says so and offers **New run**. **Back** or Esc closes it. Continuing hosts a
lobby for that run: it shows **Continuing a saved run** instead of the mode switch, and a **Seats** tab instead of
your loadout. Everyone plays the character they had: a friend who joins takes back the seat with their name (or
the first free seat, and moves aside if its owner turns up); **Take this seat** moves you to a free one. Classes,
loadouts and modifiers can't be changed. Seats nobody takes are left out. The match starts at the next wave, with
everyone's upgrades, ability keys, levels, XP, rerolls, mastery, gold, revive token, health and run stats back,
and the run's score, time, path and route. Saves are plain text in the game's data folder (`saves/*.run`),
with upgrades and abilities by name, so they keep working as the game changes.

**Profile** (the old Records and Your look pages, merged; opened by clicking your player card on the main menu, or from the pause and lobby menus' **Profile** row, which leaves out the Look tab): the same loadout box as the pause menu's (same opening, windows and Back key): a title, a profile card (your rank badge, name with a pencil key, title and rank, XP bar), four quiet tab keys (small faded letters, the open one a small lit pill) and the open tab's options as keys; each option opens in a CRT window to the right that slides out the first time and only retypes after that (its X key, Back and Esc close it first, then the profile). **Look** is the cosmetics above. **Rank** is Progress (rank, title, XP, the next unlocks), the Reward ladder, Tiers (the five titles with their rank ranges) and XP sources. **Records** is Overview, Bosses, Classes, Challenge rooms and **Recent runs** (below). **Unlocks** has a window per kind (Frame, Trail, Background, Card background, Name effect, Aim line, Cursor, Emotes) with a check or a padlock on each cosmetic, and the run-feat unlocks. The Records tab keeps this computer's player's runs started, won and lost, best wave, best endless wave,

**Records > Recent runs** (Docs/RecentRuns.md): a strip of your last 20 runs (newest first, four at a time with arrows; each card says the mode, class, wave and won, died or quit; a run you leave before it ends counts too, with no XP), six tiles for the open run (result, time, wave, damage, damage taken, XP) and two tabs. **Overview** has three graphs: **damage per second** over the run (your line in gold with a blue team line when you played in a party, alternating bands for the waves, a red tint on boss waves, a red dot where you went down), **health** over time for everyone in the party, and **accuracy per wave**. Hover a time graph for a readout of that moment. **Trends** shows the wave reached and the damage dealt run by run (green won, red died); click a run on it to open it. Each finished run keeps a sample of everyone's totals and health every 10 seconds, in a small file in your profile's `runs` folder (the newest 20 are kept); runs from before this, Boss Hunts, Challenge rooms and Practice have no graphs (Boss Hunts and rooms keep their own records). Co-op: every player's profile keeps its own copy with the whole party's lines. Runs with dev tools or the bot are not kept, like the rest of Records.
best score, time played, totals (kills, damage, healing, downs, revives), each boss beaten, and runs and wins per
class. The host tells every player's game when a boss falls and when a run ends, so in co-op everyone's records
grow. Practice Range matches, Boss Lab fights, and runs where dev tools (F1) or the bot were used don't count.
**Profiles:** the game asks **Who's playing?** at launch: click a profile, **+ New profile** to add one (up to 3: there are always four boxes, an unused slot says Empty, and New profile is grayed out when all three are used), **Manage profiles** (a keycap like the main menu's Play; each profile is a floppy disk held in a clamp cradle on a shelf, its name written in marker pen, and an unused slot or New profile is a dashed disk outline) to rename or delete (Delete asks twice). **Switch profile** above your name card on the main menu opens it again. Picking one plays a move into the menu (your disk lifts out of its clamps, flies up to the disk drive under the title, tips flat and slides into the slot; the drive reads it, then the menu draws in), and Switch profile plays it backwards (the disk pops back out), with sounds for each step; any key skips it. Each profile has its own name (used in the lobby), records, rank, cosmetics and saved runs; an older single profile is copied into one named after your old player name, and the old files are left where they were. Kept in `profile.txt` inside the profile's folder under `profiles/` in the game's data folder. **Profile > Rank** shows your account rank (see **Rank**). Each save keeps the one before as `profile.txt.bak`; a cut-off or damaged file is set aside (`profile.corrupt-*.txt`, `profile.damaged-*.txt`) and the backup is used. If the disk refuses a save, a red "Profile not saved" popup shows.

**Unlocks**: each is an entry in `Unlocks.All` (`Profile.cs`) with a condition on the records; it's earned the
moment the condition holds (an **Unlocked** toast in the match, listed in Profile > Unlocks). So far: **Warden Hunt** (beat the
Warden in a run; see Boss Hunt). Records also keep each Boss Hunt's plays, wins and best time.

## Run stats (Tab)

Hold **Tab** during a match for the **Run stats** screen, with three tabs (click to switch; it remembers the last
one):

- **Run so far:** the wave and where you are (path and wave of it, enemies left), time played, score, the next boss
  and the run's modifiers; your totals (damage, kills, healing, accuracy, biggest hit, level, times downed, allies
  revived); the **route** (each stretch of waves with its path, then the boss that ended it, down to the one coming
  next); your fight (damage taken, elite and boss kills, shots that hit, abilities used); and each player's share of
  the party's damage.
- **Your build:** your stats, weapon, abilities (charges, cooldowns, mastery, moving them between keys) and every
  upgrade you've picked, rarest first.
- **Team:** each teammate's build: a live model, class and level, health, weapon, abilities with mastery, and
  upgrades.

Switching tabs **zooms through**: the old page grows a little and fades while the new one rises in from just
behind (about a quarter of a second; teammates' live models appear once it settles). With **Reduced effects** it's a
plain crossfade, and with **Visual effects** off it switches instantly.

**Solo, holding Tab pauses the match** (letting go resumes it). **With friends it doesn't pause** (you can still move
and shoot while it's up); to look together, pause (**Esc**) and press **Run stats** in the pause menu. The host sends
everyone the run's numbers about once a second, and again the moment the game pauses.

## Pause menu and settings

Press **Esc** in a match to open the pause menu. Pausing **freezes the match for everyone** (your friend
sees who paused) and either player can resume. The pause menu, **Settings**, **Profile** and **How to play**
share one look, the **lobby loadout's box** (`Docs/Mockups/pause-settings-loadout-box.html`): a navy panel with a thin blue rim and bracket corners that slides out from the left (24 px in from the left, top and bottom) and stops 36 px short of the box beside it, the same gap the lobby's loadout box has to the screen's right edge; that is the standard placement for every drawer menu, and the window it opens takes the loadout box's place. The title ("PAUSED_", "SETTINGS_", "MENU_") and its sub-line sit straight on it over a thin rule, with the soft option keys below (a faint tint, brighter under the mouse, outlined when selected). The whole screen darkens softly toward its edges while one of these menus is up.
What an option opens appears as a second loadout box to the right (the pause menu's run summary sits there until you pick something): it slides out from behind the left box
the first time you pick something; picking another option while it is open only backspaces and retypes its text. Esc
(or the window's X key) slides it back in; Esc again goes back. Window controls are digital: ON/OFF keys, segment keys,
blocky bar sliders with - / + keys, and keybind keys that read "PRESS KEY_" while listening. Opening plays once: the
box slides out, the title types, its sub-line types a boot log (`> LINK OK > SAVE OK > WAVE n`)
and then the real line, a scan bar sweeps the keys and they type in one by one; then nothing
animates. With Reduced effects or Visual effects off there is no animation at all. Closing slides the cabinet and window
out (Back or Esc), and only then does the window really close.

- **Pause menu:** **Resume** (Esc), **Run stats** (the Tab screen, so a paused party can look at it together), **Field
  guide**, **How to play** (a window beside the list, its topics as keys along the top), **Settings** (the list switches to Audio, Visual
  effects, Gameplay, Keybinds and a Back key; each is a window), **Quit**, which asks first
  ("QUIT?", Keep playing picked to start with): **To lobby** (the host only: everyone beams out up a
  pillar of light, the screen goes dark and the lobby slides back in; the run stays saved to continue later), **To
  desktop** (closes the game), or **Keep playing** (Esc). The controls hint (Hold Tab, H) sits at the foot of the
  cabinet while paused. Choose with the mouse or ↑ ↓ and Enter. The frozen arena stays in view on the
  right with a **this run** screen (it fades while a window is out): the wave, time and score, chips for the path (and waves left), the run's modifiers
  and the difficulty, the next boss (moving), and this stretch's challenge room (won, lost, or when the next portal
  can open). The card runs down to near the bottom of the screen, with the players at its foot as compact cards
  (class, level, health; the Tab screen has the rest), stacked from the bottom up.
- **The lobby's menu** (Esc in the lobby): **Back to the lobby** (Esc again), **Settings** (the cabinet's list
  switches, like the pause menu's), the **FIELD GUIDE**, **HOW TO PLAY** (in the drawer too), **MAIN MENU** (the host leaving
  closes the lobby for everyone) and **Quit game**. The lobby has no Leave button or settings cog any more; its bottom
  bar is just **Ready** and **Start** (host), side by side in the middle, with who you're waiting on to their left.
- **The main menu**: the title over a small disk drive (it says DRIVE A: EMPTY on the picker and your name and READY once a profile is loaded) with a shot arcing over it on a tapered vector stroke. In a box in the middle of the screen: a big **Play** key over **Join game** and **Settings**; these are mechanical keycaps in a dark well, with a lamp in the corner that lights under the mouse, and they press down when clicked (so do **New run** and **Back** in the saved-runs picker). Under the box is
  **your player card** (your party card in small; click it to open your **Profile**, where the pencil next to your name renames you; it's remembered; hover it for **Switch
  profile**). Until you have opened your profile once (and for at most your first five launches on that profile), a small purple **YOUR PROFILE** tag beside the card bounces toward it; clicking the tag opens the profile too, and it holds still with Reduced effects; it fades out while the Join row is open. **Join game** opens a row
  under the box for the host's address and **Connect**. Hold the mouse over Play, Join game, Settings or any button in the saved-runs picker and the button turns into a worn VHS tape (blue cast, flicker, scanlines, a rolling tracking band, a tape counter); click it and it snaps into focus with a white flash and stays clean until you move off and back on (Reduced effects: tint and scanlines only, no snap). **Play** swaps the box's insides for the saved-runs picker (see
  Saved runs): **Continue** your latest run or start a **New run**, which grows the box into the lobby's loadout.
  **Quit** is a physical key with a lamp, beside a small "MAIN MENU_" CRT screen at the top of the box (like the saved-runs picker's Back). The
  first time it shows after the game starts it draws itself in: a pen writes the title and traces the arc, then the
  box opens and its buttons rise into it (2.75 s; any key or click skips it; with Reduced effects or Visual effects
  off, a short fade). Starting a run grows the box into the lobby's loadout while the rest of the menu fades out and
  the lobby fades in. (The Field guide and How to play are in the lobby's and the pause menu's drawer.)
- **Settings** (main menu, the lobby's menu, and the pause menu): three groups, **Audio** (level-meter volume sliders: 100 thin bars that turn amber then red near the top and ripple only while you drag, for
  **Master**, **Music** (all music; the **Music player** tab picks the track), **Weapons** (your and your friend's shots and spells), **Enemies** (enemy
  fire) and **Effects** (explosions, abilities, UI), and mute), **Visual effects** (with a live preview of what they
  do: shots and a hit, its number, an ability's name, a blast and its shake) and **Gameplay** (aim line, show the
  HUD, **camera lean**, and the **network port** hosting and joining use, 7777 unless changed; everyone in a game needs the same one).
- **Profile:** four tabs. **Look** (the cosmetics), **Rank** (Progress, Reward ladder, Tiers, XP sources), **Records** (Overview, Bosses with each one moving beside its count, Classes, **Recent runs**, **Challenge rooms** with each room played and
  won) and **Unlocks**. Mid-run and in the lobby it has no Look tab.
- **How to play** (in Settings: the drawer turns into its topics, and Back returns to the groups): Controls, The
  basics, Fighting, Between waves, Challenge rooms (with a little animated portal), Paths and modifiers, Playing
  together, and the **Field guide** under them.

Esc goes back (from the pause menu's Settings, to its list). The **Field guide** opens two in-game databases:

- **Bestiary:** every enemy, boss and boss add (44 entries), each with its live model, its stats, its health
  at your wave for your party (with the run's modifiers), what it does, when it appears, a staged boss's stages, and
  every attack (shot kind, count, speed, damage, and how often it fires now that enemies fire faster).
- **Armory:** tabs for **Classes** (each role × class: health, speed, its three weapons, Space abilities and
  ultimate), **Weapons** (live character, damage, attack rate, range as the aim line shows it, companions,
  what extra projectiles do), **Abilities** (cooldown, charges, health cost, mastery capstone, your mastery
  level, the cards that improve it) and **Upgrades** (every card by rarity: stacks, who it's offered to, which
  boss gives it, how many you have). The Armory reads the game's own definitions, so it's never out of date.

Both have a search box (it matches names and descriptions), ↑/↓ to browse, and names inside an entry link to their own
entries (a class's weapons, an upgrade's ability...). Whatever is yours this match is marked **YOU**. It reopens where
you left it, and **Esc** goes back. Its **Web page** button opens the published page of the book you're in (the
Armory's has live weapon demos; in the Unity editor it opens the local copy from `Docs/`). The same audio options are
under **Settings** on the main menu, and they're saved between sessions.

**Visual effects** (the right-hand column of **Settings**, saved between sessions; all drawn on your own screen only,
so they never change the fight or what your friend sees):

- **Visual effects** on/off. Off draws the arena exactly as before.
- **Screen shake:** Off, **Low** (the default) or Full. Getting hurt, a heavy enemy's death and above all a boss's
  death kick the camera; explosions only add a light rumble (several at once don't pile up).
- **Damage numbers:** damage to each enemy rises off it, merged every 0.15 s (so a fast weapon shows one total);
  big hits are larger and gold.
- **Reduced effects:** no screen shake, no hit-pause and no white flashes, for anyone they bother.

What it adds: the Tank Soldier's **Roar** (from [`Docs/Mockups/tank-abilities.html`](Docs/Mockups/tank-abilities.html)): a short pull-in, then three staggered rings with a filled blast and speed streaks, and each enemy flashes and pops a "!" as the wave reaches it; while the taunt runs, six armour plates turn round the soldier (flickering in its last second), a rim arc drains with the timer, dashed tethers run to the nearest ten enemies and a soft pulse goes out every second. **Bulldoze** shows its 60 px lane for half a second, then afterimages, speed streaks, dust off both heels, skid marks, a bow wave and a flicker for the invulnerable window; enemies in the lane flash and throw sparks (heavy ones more), and a double ring and a ring of dust mark where it ends. Both kick the camera (not in Reduced effects). Off, the old ring and arc draw as before. The Soldier DPS's abilities (from [`Docs/Mockups/soldier-dps-abilities.html`](Docs/Mockups/soldier-dps-abilities.html)): **Dash** leaves a tapered cyan comet streak (your own dash) that cools over 0.4 s, with sparks where you land; the **Cluster Grenade** is an olive grenade with a faint ring growing to its blast radius, then a white flash, an orange blast, eight streaking shards and smoke; **Mark for Death** puts a red reticle that snaps onto the enemy and circles it, a timer arc unwinding round it, and a small skull floating overhead, all blinking for the last second; **Chain Light** as a soft fan of light blooming out of your hand, with god-rays and a beam and halo on
each enemy it hits; **Arc Lightning** as a violet halo and sparks building on your staff with a dashed outline of the cone and tendrils of lightning creeping
along it, then a whole tree of lightning (a trunk with branches three deep, and a branch to each enemy) whipping down it,
cooling to violet and staying on the ground for a second, and a crackle on each enemy it stuns; **melee attacks** drawn as the weapon itself swinging through a glowing arc that throws sparks and leaves
embers (thrusts stab through a beam, the Lance slides out through a glowing box, and the Warhammer is raised over your
shoulder as it charges, then swung over into the floor with a glowing circle and a pillar of light); an **additive glow** on bullets, blasts, sparks, bosses and players; enemies **flash white and bulge**
when hit; every health bar gets a **damage ghost** (a pale chunk where health was lost lingers for 0.45 s, then drains
after it); **hit-pause** (the picture holds for a few frames on a heavy enemy's death or a huge hit, at most every 0.4
s, and longer for a boss); enemies **shatter** into shards of their own shape with a white flash, sparks and a
**scorch mark** that fades over 8 s; **sparks** where shots land and a flash at your muzzle; **trails** behind fast
shots; the **grid ripples** out from big blasts; a light tint for the path you're on or the boss you're
fighting; a faint **red tint in the corners** when you're
hurt or below 30% health; a gold **level-up** burst (bigger for a legendary); and a boss's **entrance card** (3.2 s,
with the HUD slid out of the way until it's done); the **ability bar's tiles react** (a tile pops with a white rim
when its cooldown finishes, bigger and gold for your ultimate; a returning second charge pops its pip; a used ability
presses its tile in; pressing one that's still recharging shakes it with a red rim and timer; Reduced effects keeps a
smaller pop and the red, without the shake or white flash); a health orb flying to you leaves a **trail** and a faint
link to you, and picking one up pulses green on you; a **puff of dust** at your feet when you set off, turn hard or
dash; and **weather on every path** (embers rising on Burning Grounds, snow on Frozen Lake, petals on Sanctuary, red
ash under the Blood Moon, mist motes in Fogbound, grit on Narrow Pass, pollen on Swarm Fields, wind-blown dust on
Sniper Ridge, gold motes on the Elite Gauntlet, lantern sparks at the Bazaar), fading in as the path starts.
**Weapons' shots have their own looks** (from [`Docs/Mockups/projectiles.html`](Docs/Mockups/projectiles.html)): the
Twin Rifle and Burst SMG fire tracers, the Railgun a heavy slug pushing rings of air (the Pistol a lighter one), the
Scattergun flechettes, Ice Shards
(from [`Docs/Mockups/ice-shards.html`](Docs/Mockups/ice-shards.html)) faceted ice kites with glints that shatter on a hit; lightning flickers and forks with a white-hot core; Shuriken spin with motion blur;
Fireballs shed embers and smoke, the Golem's boulder dust, Blood Bolts drips. The lobby's loadout preview shows the
new shot looks, trails, sparks, bullet endings and hit flashes too (not the glow). **The Soldier DPS's guns animate** (from [`Docs/Mockups/soldier-dps-gun-animations.html`](Docs/Mockups/soldier-dps-gun-animations.html)), keeping the glowing shots: the Twin Rifle's two guns angle in, kick, flick their charging handles, flash at each muzzle, drop brass out of the outer side and warm up the longer you hold fire; the Railgun's three coils light up one after another between shots with a ring closing on the muzzle when it is ready, and a shot is a white flash, arcs between the rails, a hard kick that shoves the rails apart and steam off the coils; the Burst SMG's three barrel tubes turn faster and heat to white as it spins up, its flash grows, it drops brass and smokes once hot, and a faint cone shows the real spray tightening. **Bullets end instead of
vanishing:** one that runs out of range shrinks and fades into a wisp of smoke; one that hits an enemy flattens into a
splash with sparks bouncing back; one that's blocked (your parry, a Barrier, a Shield Wall, a blocking minion, or a
Shielder's or Riot Shield's front) ricochets off, tumbling, with a bright spark; and one that hits a player bursts in
its own colour with a small ring (its name and the wave, the arena dimming for a moment). A boss's death bursts in
waves. Mocked up first in [`Docs/Mockups/visual-flair.html`](Docs/Mockups/visual-flair.html).

**Music:** everything is generated in code, like the sound effects. The menus and lobby play a synthwave
theme (`Assets/Scripts/Game/MenuMusic.cs`); matches play a soft track during regular waves, and **every boss
has its own track** that crossfades in while it's on the field (`Assets/Scripts/Game/GameMusic.cs`, where each
track is a short recipe: tempo, key, chords and which instruments play). Tracks render in the background, and
the next boss's track is prepared ahead of time. The **Music** slider controls all of it.

**Music player** (Settings > Audio > *Music player*): 22 tracks to pick from, which play everywhere (menus and matches)
instead of the automatic music until you pick **Automatic** again. Besides the menu theme, the wave battle track and the
ten boss themes there are eight extra moods (`Game/MusicLibrary.cs`): *Lo-fi Lounge*, *Crystal Caves* and *Deep Freeze*
(Chill); *Neon Highway*, *Sunrise Run*, *Arcade Heist* and *Dust Road* (Action); *Haunted Hollow* and *Iron Rain* (Dark);
*Last Stand* (Epic). Controls: Prev / Play-Pause / Next, Shuffle, Repeat (all, or this one), a progress bar, and
"Boss themes still play" (on by default: a boss fight swaps in its own theme, then your track returns). Wheel scrolls the
list. Your pick, shuffle and repeat are saved. Tracks render in the background the first time (a few seconds).

**Your own music in Unity:** drop audio files into `Assets/Resources/Music` and they appear in the music player as
*Custom* tracks, no setup (for long songs set the clip's Load Type to Streaming). For more control make a **Music Pack**
(Assets > Create > Arena > Music Pack, saved as `Assets/Resources/MusicPack`): assign a clip to replace the **menu**
theme, the **wave battle** track or any **boss's** theme (with a volume each), and list **Tracks** with your own title
and mood. Anything you don't fill in stays generated. Code: `Game/MusicPack.cs`, `LoadMusicPack` in `MusicPlayerUi.cs`.

**Your own music from files (no Unity needed):** in the Music player click **Open folder** (the game's `music` folder inside its save data folder; it is
made for you), drop in `.wav`, `.mp3` or `.ogg` files, and click **Rescan** (the tab also rescans when you open it). They
join the list as green *Custom* tracks, named after the file (`my_song.mp3` shows as "my song"), and work with everything
above: shuffle, repeat, play in menus and matches. Tracks are levelled to a similar loudness and loop. A file the game
can't read shows "Couldn't read this file" and is skipped. WAVs (8 / 16 / 24 / 32-bit or float) are decoded by the game;
mp3 and ogg by Unity (mp3 on Linux may not play).

## Playing with a friend

**You both need the same version of the game.** The version is shown at the bottom of the main menu and
in the lobby. If they don't match, the joining player is turned away with a message saying so. After
pulling an update, everyone should rebuild (or the host should send the new build).

One person **hosts**, the other **joins**. The host's game acts as the server, so you don't need anything else.

1. Both of you install [Tailscale](https://tailscale.com/download) (free) and join the same tailnet.
2. Host: open the game, click **Play**, then **New run** (or **Continue** a saved run). Click the **!** next to the Lobby title to see your port and addresses;
   give your friend the one starting with `100.`.
3. Friend: type that address into **Host address**, click **Join game**.
4. Both pick a loadout and click **Ready** (changing your loadout un-readies you). Once everyone is ready, the host
   clicks **Start**.

**Bots.** The host can fill the lobby's open slots with bots that play with them: click **+ Add a bot** on an OPEN SLOT (host only,
in the lobby, not while continuing a saved run). A bot takes the first role nobody has, a random class, weapon and Space ability,
and readies up by itself; it revives, heals, dodges and uses its abilities like the Boss Lab's bots (below), picks its own
upgrades and carries on between waves. On its card, the skill button cycles **Casual, Good, Expert**, **MIX** deals it a
new random loadout and **x** removes it. Click a bot's card (it gets a gold rim) to choose its role, class, weapon and
ability in the loadout panel, just like your own; click your own card to go back. With a bot's card chosen, type in the **Bot name** box above its preview to rename it. In the Hangout, bots stroll about the room and play emotes now and then (any emote, whatever their rank). Bots leave when you leave the lobby. A run with bots in the party isn't saved and isn't
counted in Records, XP or ranks, like Boss Lab fights and the F1 bot.

Without Tailscale, the host can forward **TCP port 7777** on their router to their PC and give the friend
their public IP. On the same Wi-Fi, use the host's local `192.168.x.x` address. Windows Firewall may ask
the host to allow the game the first time they host; click **Allow**.

## Making a build to send to your friend

In Unity, use the menu **Arena → Build Windows Game**. It builds to `Build/TopDownArena/` and opens the folder. Zip
the whole `TopDownArena` folder (the `.exe` needs the files next to it) and send the zip. On first launch Windows may
say "Windows protected your PC"; click **More info → Run anyway** (the game isn't code-signed).

To test multiplayer on one computer, run the built `.exe` twice (or the `.exe` plus the editor): host in one, join
`127.0.0.1` in the other.

## Testing tricks

The built game accepts command-line flags so you can skip the menus:

```
TopDownArena.exe --host --role=tank --autostart=2     # host and auto-start when a friend joins
TopDownArena.exe --join=127.0.0.1 --role=dps          # join a local host
```

Also: `--name=X`, `--port=N`, `--class=soldier|mage|summoner`, `--weapon=1|2|3`, `--ability=1|2|3`, `--start-wave=N`
(jump ahead), `--bot` (your player moves and shoots by itself).

`Tools/SimTest` runs a host and a client against each other over localhost without Unity and checks that
they stay in sync (`Tools/SimTest/run.sh [start-wave] [seconds] [suite]`, needs Mono). The default suite is
`core`; `features` runs every feature test, `all` runs both, and `run.sh list` shows every suite. Tests live in
`Tools/SimTest/Tests/`, one file per system, and their random rolls are seeded so a run is repeatable
(`SEED=random` for fresh rolls).
### Developer tools (F1)

Press **F1** anywhere for the developer menu (Esc, F1 or its X closes it). Tabs down the left, and a card at
the bottom of them shows what's on (wave, recording, bot, aim debug, players):

- **Match (host):** pick a wave with -10 / -5 / -1 / +1 / +5 / +10 and **Go to wave N**. It clears the arena, gets
  downed players back up and starts that wave after 2 s. The boss on that wave (or the next boss) is shown moving
  beside it. **Kill-all key (X)**: a switch (saved between sessions) that makes **X** kill every enemy on the field,
  bosses included, as your kills: XP, score, drops, gold and kill credit as usual, through shields and immunity. It
  doesn't end the wave: enemies still to come keep coming. X still throws away a chest item while you're standing on
  one. **Everyone** buttons: full heal, level up, max mastery and god mode for every player at once. **Challenge
  rooms** buttons (between waves): straight into Crystal, Gatekeeper, Kill Rush, Zones or Mirror.
- **Players (host):** choose a player, then edit any of their stats by hand: type a number or use - / +.
  Type in **Filter** to narrow the list (e.g. "barrier"). Edited stats show in gold, are sent to every player's
  game, and stay on top of upgrades they pick later; **Reset stats** goes back to what their upgrades give.
  Also **God mode** (takes no damage), **Full heal**, **Level up** (an extra upgrade pick) and **Max mastery**
  (every ability they have to level 5). Joining players can see everyone's stats but not change them.
- **Tools:** record sessions (below) and open the sessions folder, the aim debug overlay (also F3), the **freeze-time key (P)**
  (a saved switch: P freezes the whole picture, effects and animations included, without opening the pause menu; press it again to resume;
  hide the HUD with H), a bot that plays for you, **text editing** (also F4, below), open the practice folder, and the **Boss Lab**.
  **Account rank** sets this computer's rank without playing for it (works on the main menu too): **+1 rank**, **+10 ranks**,
  **Rank 30** (every reward), **Max rank** (999) and **Reset** (rank 1). Cosmetics the new rank hasn't reached come off.
- **DPS (host, in a match):** the DPS meter and target dummies, below.
- **Replays:** record the runs you host and watch them back, below.

### Replays (F1)

**F1 → Replays** records every run you host (switch **Record replays of runs I host** on first; it's saved between sessions, off by
default) and saves it as a `.replay` file in the `replays` folder when the run ends (or press **Stop and save**). The tab has
**Open folder**, **Refresh** and a list of your replays, newest first: name, date, mode, wave reached, length, size and who played. Each has **Open**,
**Rename** (type, then Enter or Save) and **Delete** (asks "Delete for good?" first). Runs under a second, the Practice Range's lobby preview and
Boss Lab fights aren't saved.

**Open** (from the main menu, not in a lobby) plays the run back in the game window as a spectator, with a **timeline** along the bottom:
a **wave lane** (a block per wave, red for a boss; click one to jump to where it starts), a **time bar** to click or drag (blue ticks are waves,
red a boss, amber a player going down), and buttons for **Pause / Play**, **-1 f / +1 f** (one frame), **|< Wave / Wave >|**, and speeds
**0.1x, 0.5x, 1x, 2x, 4x, 10x forward and 10x, 4x, 1x, 0.5x, 0.1x backward**. Keys: **Space** pause, **, .** step a frame, **[ ]** next slower / faster
speed (through backward too), **Left / Right** 5 s, **Home / End**, **V** (or the Following button) switch which player's screen you watch, **Esc** or
**Close replay** back to the menu. It plays to the end and holds there.
A replay is what a second player on the host's game would have seen, so it shows the match, not your inputs: you can watch, pause, rewind and scrub, but not
take over and play from a point. Seeking anywhere takes a few milliseconds; shots that were already in the air for more than 6 s before the spot you jumped to
aren't drawn. Nothing about a replay changes the network protocol.

### DPS meter and target dummy (F2)

For balancing. **F2** (or F1 → DPS) shows a **DPS meter** at the top left: for every player, the damage a second over
the last second (big number), the last 5 seconds, the average, the best second, the total, and how much of it was
burning, bleeding or poison. Every source counts (shots, minions, burning). The average only counts the time you
were hitting something, so walking about doesn't drag it down. Next to it is **sim**, the no-upgrades figure the
lobby shows for that weapon (`Defs.WeaponDps`), and how close you are to it. **Shift+F2** (or the **Reset** button)
starts the meter over. Only the host sees it; nothing is sent over the network.

**F1 → DPS → Spawn one in front of you** puts a **target dummy** down: a Juggernaut that never dies, never shoots,
never hurts to touch and can't be pushed, frozen or caged. It heals to full every tick and counts what it took (the
meter shows "took N"). It doesn't count as an enemy, so the wave ends with it standing. Up to four; **Patrol side to
side** makes them strafe to test moving targets, and **Remove all** clears them. Kill all (X) leaves them alone. Using
the dummy marks the run as a dev run, like god mode. In the Practice Range, a wave with no enemies and **Loop** off is
the quietest place to test.

### Editing text in the game (F4)

**F4** (or F1 → Tools → *Edit any text on screen*) turns on text editing: the text under the mouse is outlined in
gold; click it to change it in a box, then **Save**. The change shows straight away, everywhere that same text
appears, and is kept in `TextOverrides.txt` (at the top of the project when running in the Unity editor; in a
build, in the game's data folder). **Back to original** undoes an edit. F4, Esc or the banner's **Done** leaves the
mode (while it's on, clicking text edits it instead of pressing the button).

Numbers come from the game: an edit has to keep the same count of numbers, and they're filled in for you (so one
edit of "Pay 15% of your max health..." also covers 20%). To make edits permanent in the code, run
`python3 Tools/TextEdits/fold.py` (add `--dry-run` to see first): it rewrites every string literal that is exactly
an edited text and takes those edits out of `TextOverrides.txt`. Texts the code builds from pieces (like
"Wave " + number) stay in the file, still working, for changing by hand.

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
- **Bot team play** (the Boss Lab's bots, lobby bots and the F1 bot): a hurt bot walks to healing orbs and the team's
  healing zones (Haven, Healing Rain, Soul Lantern, World Tree, Consecrated Ground, and so on). Everyone focuses an
  enemy carrying a **Mark for Death**, and a Soldier DPS bot marks the boss (once, not again while it's marked). Healer
  bots cast a Medkit, Healing Rain, Soul Lantern, Holy Cross or Lumen Ward at the downed or most hurt ally, and aim a
  Mender's Bow arrow or Transfusion Dart through a hurt teammate within 420 px. A Roar tank roars when a teammate is
  hurt or down with enemies on top of them (or when it is under half health); a Knight tank plants **Last Stand** over
  a downed ally and keeps it up while they're being stood back up. Tests: `Tools/SimTest/run.sh 1 30 botteam`.
- Lab fights aren't saved as session recordings. Starting the lab leaves any match you're in.

**The bots** play like a person. They plan a short way ahead, moving in the 8 WASD directions to avoid where
bullets, enemy bodies, lightning strikes and the Reapers' tether will be. They keep a distance that suits
their weapon and role, close in until a target is within their weapon's reach before they pull the trigger (using anything already in reach on the way), lead their shots, and go for the adds that protect a boss first (phylacteries,
gravity wells, the Queen's shield drones). When a Tank's **Last Stand** is planted they stand in its lee, just behind the tank and away from where the plates face (unless someone needs reviving). They revive downed allies, heal when someone is hurt, use
defensive abilities when things get tight, and use the rest on the boss.

| Skill | Reacts after | Plans ahead | Keeps track of | Changes direction |
|---|---|---|---|---|
| Casual | 0.35 s | 0.3 s | nearest 6 bullets | ~4 times a second |
| Good | 0.24 s | 0.4 s | nearest 12 bullets | ~7 times a second |
| Expert | 0.15 s | 0.5 s | nearest 25 bullets | ~12 times a second |

Bots don't parry, pick up health orbs, or reroll upgrades. (The same bots can join your own lobby: see **Bots** under Playing with a friend.)

From the command line, `Tools/SimTest/run.sh 1 1 lab <wave> <skill 1-3> <runs> <seat> [seat] [seat]` runs
fights without drawing and prints the results. A seat is role/class/weapon, for example `T/S/0` (Tank
Soldier, first weapon), `H/M/1`, `D/U/2` (U = Summoner) or `T/K/0` (K = Knight).

### Wave editor (F1)

**F1 → Tools → Wave editor** designs waves on a timeline. A design is a set of waves; each wave has a **level** (how
strong its enemies are, as that wave of a run), **groups** and **props**.

- **Arena map** (top left): the five **spawn points A-E** (drag a letter to move it, for every wave) and the wave's
  props. **Props** mode: click to place the picked prop (crate, big crate, pot, barrel, chest), drag to move,
  right-click to remove. The map also shows the wave at the playhead: who has arrived and where they're heading.
- **Palette** (top middle): **Enemies** (the bosses' factions first, then everyone else), **Squads** (the regular
  squads and the bosses' own, like the Riot line) and **Bosses**. Click one to add a new group at the playhead;
  Shift-click an enemy to add one more to the selected group.
- **Selected group** (top right): its name, which spawn points it comes from (several take turns), when it starts,
  how it arrives (**one by one** every so many seconds, **together**, side by side in a **line**, or in a **ring**
  round a player) and how many of each enemy. A squad keeps its own members and formation.
- **Timeline** (bottom): one track per group. Drag a clip to change when it starts, drag its right edge to spread
  its enemies out; click or drag on the tracks to move the playhead; **Play** (or Space) runs the wave on the map.
  Delete removes the selected group. A wave ends once every group has arrived and everyone is down.
- **Waves:** `<` `>` step through them, `+` adds one, **Copy** duplicates it, `X` removes it; **Level** and **Loop**.
- **Save** writes `<name>.waves` to the `waves` folder next to the practice folder; **Load** opens one (or starts a
  new set). **Test** plays the waves in the Practice Range, hosted by you, starting on the wave you're editing (from
  the main menu, the lobby, or a match you host; it replaces that match). Esc closes the editor.

The `.waves` file is plain text, e.g.

```
point B 70,450
wave 6 level 6
  group Breakout rush | Rioter x6, Guard | from B | at 0 | every 0.4 | one by one
  group Riot line | squad Riot line | from A | at 11
  prop crate 610,330
```

**Building designed waves into the game.** Put a `.waves` file in the repo's `Data/Waves/` folder (in the Unity
editor, the Load panel's **Save to project** writes it there), then run

```bash
Tools/WaveGen/build.sh
```

It reads every file with the game's own parser and writes `Assets/Scripts/Core/DesignedWaves.Generated.cs`, code that
builds each set, so a final build carries them with no loose files (`DesignedWaves.Get("<name>")`). A file with a
problem stops it with the file, the line and what's wrong, and nothing is written. The Load panel lists the built-in
sets under **Built into the game** (open one to look at or change a copy). The `wavegen` sim test fails if a file
was added or changed without running the script. Next: hunts, approaches and paths using them (`Docs/AfterReset.md`).

### Session recording (for balancing)

**Record sessions** in the F1 menu is on by default (turn it off there; `--record` forces it on). A red **● REC** in
the top-right corner shows a match is being recorded. The Unity editor and a built game keep separate settings, so
check it in each. While you **host**, every match (solo or with friends) is saved to a JSON file in the sessions
folder (the **Open sessions folder** button shows it; on Windows it's
`%USERPROFILE%\AppData\LocalLow\<company>\<product>\sessions`). The host decides every hit, so the file covers all
players. The end-of-run screen shows where it was saved. It's rewritten after every wave and every 10 s, so a crash
keeps nearly everything. Leaving or quitting mid-run saves it too: the outcome reads "left" (Leave game, Back to
lobby), "quit" (the game was closed) or "restarted" (a new match started over it); a file whose outcome still reads
"in progress" is from a run that crashed.

Each file records:

- the team (role, class, weapon, ability, ultimate), party size, start wave, the mode (run or Practice Range),
  run modifiers, and how it ended (a run continued into endless mode records its real end, marked "(endless)")
- each player's final level and the abilities they ended with, at their mastery levels
- **per wave:** length, the path it was played on, enemies spawned and killed by type, damage the floor did to enemies
  (Burning Grounds' fire), and per player: damage dealt (and to bosses), kills, damage taken **broken down by source**
  (e.g. "The Warden (aimed)", "Brute (contact)"), healing done, healing received **broken down by source** (allies,
  own abilities, regeneration, lifesteal, heal on kill, orbs, Sanctuary pools, Blood Moon), health orbs, downs,
  revives, seconds spent under 25% health, health and level at the end, ability uses and parry blocks / perfect
  parries / breaks
- **per boss:** health, time to kill, each player's damage, damage players took and downs during the fight
- **every down** (who, when, what did it), **every upgrade pick** (the cards offered and the one taken) and rerolls
- **per enemy type:** spawned, killed, average lifetime and damage dealt to players
- **every path choice:** the two paths offered after each boss, the one taken (or the plain arena) and its prize
- **dev tool use** (god mode, stat edits, healing, level-ups, mastering, wave jumps, the bot playing for the host),
  so runs that weren't plain play can be told apart
- a **sample every 5 s** of everyone's health, enemies and bullets on screen

Summarize one run or a whole folder of them (needs Python 3):

```
python Tools/SessionReport/report.py path/to/sessions
```

It prints boss kill rates and times, difficulty by wave (damage taken, downs, time at low health), what
downs players, where damage and healing come from, how each path plays (damage taken, healing, downs, how
often it's offered and taken), the most dangerous enemies per spawn, upgrade pick rates, ability use and
mastery, parry use, and how each class and role performs. Runs that used dev tools are flagged, and Practice
Range sessions are left out of the tables unless you add `--practice`.

`Tools/SimTest/run.sh 1 1 ttk [last-wave]` is a balance probe: it plays a whole run with invulnerable bots
that level up and pick upgrades at random, and prints how long each boss survived.

## Code layout

See section 2 of [`HANDOFF.md`](HANDOFF.md) (one table row per source file).
