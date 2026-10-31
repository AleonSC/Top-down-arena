# Top-Down Arena

A co-op top-down bullet hell for **1 to 3 players** over the network, made in **Unity 6**.

**Party size scaling:** solo, enemies have 70% health; with two players, 100%; with three, regular enemies
have 120% health and waves have 25% more of them, and bosses have 150% health.
Bosses also have 1.2x to 4x their base health, set per boss so each fight lasts about 60-120 s (`BossHpMult` in Balance.cs), and every enemy bullet hits 15% harder than its listed damage.
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

In the lobby each player picks a **role** (Tank, Healer, DPS), a **class** (Soldier, Elementalist, Summoner, Knight, Ninja, Hemomancer), then
one of that combination's three weapons and three Space abilities. Weapons and abilities are unique to
their role and class.

- **Soldier**: guns and gear, full health.
- **Elementalist**: spells (fire, frost, lightning, arcane), 85% health.
- **Summoner**: minions that come with your weapon (they stay between waves), 90% health.
- **Knight**: melee weapons that cut small bullets out of the air, armoured: 115% health (see below).
- **Ninja**: thrown blades and quick melee with tricks and misdirection; fast (+12% move speed), 85% health (see below).
- **Hemomancer**: blood spells that drain and heal; every Space ability costs health, and it hits harder the lower its health; full health (see below).

| | Soldier | Elementalist | Summoner |
|---|---|---|---|
| **Tank** (240 HP, enemies prefer you) | Weapons: Scattergun (double damage and a shove point-blank), Flak Cannon (airbursts at your cursor, shredding enemy bullets), Riot Repeater (rounds suppress: enemies hit shoot slower)<br>Abilities: Barrier, Roar, Bulldoze | Weapons: Flamethrower (sets enemies burning), Frost Lance (an icicle out from you that splinters sideways, slowing), Storm Hammer (a spinning hammer that electrifies what it hits)<br>Abilities: **Arcane Aegis** (reflects bullets), **Frost Nova** (freezes nearby enemies), **Flame Wall** (burns enemies, blocks bullets) | Weapons: Golem Staff (hold to lob a crushing boulder, + bullet-blocking Golem), Bone Guard (darts that cage enemies, + 2 orbiting skeletons that shoot the caged), Bear Totem (melee claws that cause bleeding, + Bear)<br>Abilities: **Golem Wall** (3 stone walls), **Earthquake** (you and every minion slam), **Guardian Spirit** (eats nearby bullets for 8 s) |
| **Healer** (120 HP, healing aura) | Weapons: Repulsion Cannon, Revolver, Mender's Bow<br>Abilities: Mend Pulse, Haven, Soul Link (alone: links to a Soul Ghost of you that fights beside you, firing light bolts, 11 every 0.3 s; it has half your health, takes half damage and regenerates while linked, and fades when the link ends) | Weapons: Radiant Orb (heals allies it passes), Chain Light (a cone of light, its damage shared between what's in it), Prism Beam (shoot through an ally: it heals them and splits in three)<br>Abilities: **Time Rewind** (health back to 4 s ago, even undoing a downing), **Arcane Ward** (60 HP shields; when one breaks or runs out, half of what it soaked comes back as healing), **Sanctify** (erase bullets, heal) | Weapons: Sprite Staff (+ 2 healing sprites; seeded kills free more), Treant Rod (healing bolts; the Treant roots and a vine to you heals allies and slows enemies crossing it), Totem Staff (throws healing totems that seed hits charge into heal pulses)<br>Abilities: **Spirit Swarm** (5 healing sprites), **Phoenix** (fights, and instantly revives a downed ally), **Soul Lantern** (allies near it can't go down) |
| **DPS** (95 HP) | Weapons: Twin Rifle (streams cross at your cursor for double), Railgun (leaves its path charged for a second), Burst SMG (spins up: tighter and harder the longer you fire)<br>Abilities: Dash, Cluster Grenade, Mark for Death | Weapons: Fireball (big, rolls through enemies, burning them), Arc Lightning (hold to charge a strip of lightning), Arcane Missiles (bank in orbit, launching at enemies that come near)<br>Abilities: **Meteor** (warning circle, then 150 damage), **Blizzard** (slowing storm), **Arcane Surge** (+60% damage, +2 pierce) | Weapons: Wolf Whistle (darts mark the prey the 2 wolves hunt; every wolf biting at once is a takedown), Wisp Lantern (3 orbiting wisps you send out as your shots), Imp Caller (imps wait by you; tap to send them, hold to merge them into one big blast)<br>Abilities: **Bat Swarm** (8 bats), **Sacrifice** (all your minions explode, then return), **Alpha Call** (minions frenzy) |

Generic upgrades (damage, fire rate, cooldowns, Second Charge, legendaries) work for every class; minions
use your damage and attack-speed bonuses too.

**Healers** (every class): the healing aura heals allies within 170 px for 9 HP/s and you for 1 HP/s; each
**Sanctuary** card adds another 9 HP/s for allies (and 40% reach) and +1 HP/s for you. A Healer's healing past someone's full
health becomes a shield (up to 20% of their max health, for 4 s; the aura's own ticks don't count). Healer
abilities work alone too: **Haven** heals 50% faster with nobody to revive, **Spirit Ward** takes your own damage, **Martyr** with no
one down wards the most hurt ally (you, alone) for 12 s so going down puts them straight back up at 50%, and a
**Phoenix** brings anyone back, you included (the run doesn't end while one is out).

### The Knight

Knights fight up close. Holding fire **swings** instead of shooting: each swing hits every enemy in its arc
(the Rapier only the nearest), and **cuts small bullets out of the air** in front of you. Big orbs and mines get
through (a mastered Whirlwind stops those too), and bosses' red aimed shots always do. Knights have 115%
health to make up for standing in the thick of it, and deal about a third more damage than the ranged
classes.

| | Weapons | Space abilities | Ultimate |
|---|---|---|---|
| **Tank** | **Warhammer** (hold to charge, let go to slam a 100 px wide strip ahead: up to 80 damage and a 0.9 s stun at full charge), **Lance** (a heavy jab: a 180 × 88 px box straight ahead that slides out, shoves enemies 140 px back and stuns them for 1 s), **Halberd** (a long, thin 18° blade swept across 80° in front of you, hitting each enemy once a sweep) | **Shield Wall** (4 s: blocks every bullet coming at your front 120°, red aimed shots included, soaks 400 boss damage), **Leap Slam** (leap up to 320 px to your cursor, untouchable in the air; 50 damage and a 1 s stun where you land), **Unbreakable** (5 s: take 50% less damage, hits heal you 15% of what they deal) | **Last Stand** (70 s): 8 s where you can't drop below 1 HP, every enemy targets you, and you deal +50% damage |
| **Healer** | **Quarterstaff** (wide sweeps that knock enemies back and bat enemy bullets back at them; each enemy hit heals you and allies near you for 2), **Lightbringer** (hits fill a light gauge; full, the next crescents heal three times as much; sword cut plus a crescent of light that heals allies it passes), **Censer** (leaves a healing incense trail that spoils enemy aim; a flail whirled all the way round; each spin heals allies near you for 3) | **Consecrate** (6 s of blessed ground: allies heal 12 HP/s, enemies burn 20/s), **Guardian's Oath** (shield the ally nearest your cursor for 100, yourself for 50, for 6 s), **Lay on Hands** (heal the most hurt ally within 300 px for 70, or revive them at half health) | **Hallowed Blade** (80 s): a giant sword falls at your cursor for 200 damage around it, then blesses the ground for 8 s (allies heal 15 HP/s) |
| **DPS** | **Twin Blades** (very fast alternating slashes; 10 hits in a row prime a dash-strike that lunges you through enemies for triple), **Greatsword** (each sweep leaves a bullet-stopping crescent for 0.5 s; huge 170° sweeps), **Rapier** (thrusts at the nearest enemy; each hit in a row on it deals 10% more, up to +60%; each thrust skewers up to 3 enemy bullets on its line, each adding to the streak) | **Whirlwind** (spin for 2 s hitting everything within 110 px every 0.2 s and slicing small bullets), **Lunge** (dash 260 px toward your cursor hitting everything you pass for 35; two charges), **Battle Fury** (6 s: swing 40% faster, +25% damage) | **Blade Tempest** (65 s): for 6 s every swing also hurls three piercing sword beams |

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
| **Tank** | **Kusarigama** (throw the chain-sickle 280 px to hook the first enemy it hits; fire again to reel yourself in to it with a heavy, stunning slash), **Bo Staff** (spun all the way round you, 90 px, cutting small bullets; click while moving to vault 150 px, striking everything you pass), **Fuma Shuriken** (a giant star thrown in an oval loop 380 px out in front of you and back, cutting through everything both ways) | **Shadow Clone** (a decoy for 5 s that every enemy aims at; bursts for 60), **Smoke Bomb** (a 140 px cloud for 5 s: enemy bullets inside slow to half speed, allies inside take 30% less damage), **Iron Body** (2 s of 80% less damage, then a 50-damage shockwave) | **Thousand Clones** (70 s): five clones surround you for 8 s, drawing every enemy's fire, then burst |
| **Healer** | **Medicine Darts** (fast needles that dose what they hit: stacking poison on enemies, stacking healing over time on allies), **Spirit Fan** (alternates blowing enemies away and drawing them in; a short gust that heals allies in it and blows small enemy bullets back), **Ofuda** (charms lie at your cursor as wards that burst under enemies, healing allies near; slow homing paper charms that burst for 25 and heal allies near the burst for 8) | **Healing Mist** (a mist follows you for 6 s, shedding dew around you, two drops a second that heal 10 each, for you and your allies to pick up), **Substitution** (swap places with the ally nearest your cursor and shield them for 60; alone, slip toward your cursor and shield yourself), **Spirit Ward** (a talisman at your cursor for 8 s that takes the damage allies near it would take, up to 150; when it breaks or ends, a quarter of what it took heals them) | **Blossom Storm** (80 s): for 5 s the whole party heals 20 HP/s and enemy bullets near every ally are erased |
| **DPS** | **Shuriken** (fans of 3 stars that stick in what they hit; double-tap fire to recall them for another hit), **Katana** (fast 110° slashes; hold off 0.6 s to sheathe it, and the next cut is a 220 px quick-draw through everything in its line for 2.5×), **Kunai** (every 4th leaves a mark: double-tap fire to blink there for a double hit; rapid knives, +50% damage to enemies that aren't targeting you) | **Shadow Step** (teleport behind the enemy nearest your cursor, up to 450 px away, and strike it for 80), **Vanish** (for 3 s enemies lose track of you; your next hit deals double), **Blade Fan** (12 kunai in a ring, 18 each) | **Death Lotus** (65 s): strike up to 8 enemies near your cursor in a flurry of cuts, 100 each |

### The Hemomancer

Health is the Hemomancer's resource. It has full health and normal speed, and:

- **Every Space ability costs health**, a share of your max health shown on the card (Blood Gift: of your
  current health). A cost can't take you below 1 HP: at 1 HP the ability still goes off, but anything that
  scales with what you paid is worth nothing. Cooldowns are shorter than other classes' to make up for it.
- **Blood Price:** you deal more damage the more health you're missing, up to **+40%** at 10% health.
- **All your hits heal you for 3% of the damage they deal.**

| | Weapons | Space abilities (cost) | Ultimate |
|---|---|---|---|
| **Tank** | **Blood Lash** (a 130 px, 110° whip of blood, 24: its tip cracks for double and taunts, closer hits are flung out to the tip; hits build Clot, 4% less damage taken each, and at full Clot the next lash bursts all round you), **Clot Bolt** (a heavy bolt, 26, that leaves a pool of blood slowing enemies 40% for 3 s; after 1 s it congeals, sticking enemies in it for 1 s, and you take 15% less standing in your own pool), **Leech Tendrils** (hold fire: blood tendrils grasp up to 3 enemies in a 70° arc in front of you for as long as you hold on; they're stunned and drained slowly, and 25% of the drain heals you; spare tendrils pile onto what's held, so a lone boss takes all three) | **Blood Shell** (15%: a shield worth three times what you paid, 5 s), **Crimson Pact** (10%: every enemy targets you for 4 s, then the damage you took bursts back out at 150% within 220 px), **Exsanguinate** (10%: pull enemies within 220 px in and drain 20 from each into you) | **Blood Rite** (75 s): for 8 s every enemy targets you and you take 60% less damage; the damage you take heals your allies |
| **Healer** | **Transfusion Darts** (darts that give a hurt ally 20 health for 4 of yours; each enemy hit steals a sliver, 0.5), **Sanguine Orb** (orbs that home in at the last moment and circle an enemy to cover it in blood, 16 plus a bleed stack, charging a heal reserve; with 25 reserve, an orb that touches a hurt ally coats them in healing blood, 30 over 4 s), **Vein Tether** (a real beam: it hurts whatever is on your aim line, so keep it on your target; 40% of its damage heals, split between you and the nearest ally) | **Blood Gift** (25% of current health: the most hurt ally heals twice what you paid; alone, a shield for you), **Siphon Field** (10%: a 140 px field at your feet for 6 s, enemies inside lose 10 HP/s and allies heal 10 HP/s), **Martyr** (40%: revive the downed ally nearest your cursor, anywhere, at 50%; if no one is down, the most hurt ally heals twice what you paid) | **Sanguine Rain** (80 s): for 6 s every hit any ally lands heals them for 20% of its damage |
| **DPS** | **Blood Bolts** (fast bolts, 9; the fifth in a row in one enemy makes it hemorrhage, bursting for 36 within 80 px), **Implosion** (click an enemy: its blood collapses inward, 34, hurting enemies against it for 15 and pulling them in; fires only with your cursor on an enemy, up to 480 px away), **Bone Spear** (stays stuck where it stops: walk over it for its 2 health back; a heavy spear, 60, that pierces 3; each throw costs 2 health) | **Blood Boil** (20%: enemies within 260 px burst after 1 s for three times what you paid), **Frenzy Pact** (5%: for 6 s fire 50% faster and heal 10% of the damage you deal, but lose 3 HP/s), **Hemo Nova** (10%: a ring of 16 blood bolts, 20 each, each that hits heals you 3) | **Crimson Apotheosis** (65 s): for 6 s you can't drop below 1 HP and Blood Price goes up to +100%; when it ends you heal for half the damage you dealt |

Capstones (level 5): Blood Shell also erases bullets within 160 px · Crimson Pact: 30% less damage while it
lasts · Exsanguinate also slows what it drains by 50% for 2 s · Blood Gift also heals others within 200 px of
them for half · Siphon Field follows you · Martyr: you're invulnerable for 2 s after · Blood Boil's bursts also
hit enemies within 80 px · Frenzy Pact: no health drain · Hemo Nova's bolts pierce 2 · Blood Rite lasts 12 s ·
Sanguine Rain lasts 9 s and heals 30% · Crimson Apotheosis lasts 9 s.

Players can pick the same role if you want. Everyone also slowly regenerates health (0.5 HP/s).

Enemies sometimes drop **health orbs**: small green ones heal 15 HP (18% chance), and rare large ones
with a gold ring heal 60 HP (3% chance). Bosses always drop a large orb plus four small ones. Orbs drift
toward a hurt player who gets close, are left alone by players at full health (so your ally can grab
them), and blink before vanishing after 12 seconds.

### Weapons

Each role picks one of three starting weapons in the lobby. Damage, fire rate and Piercing upgrades work
with all of them.

**Extra projectiles** (Multishot, and +2 during Bullet Storm) upgrade each weapon in its own
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
| Tank · Summoner | **Golem Staff** | Hold to charge, let go to lob a boulder over enemies: 140 px and 20 for a tap, 460 px and 40 at full; crushes within 75 px; plus a bullet-blocking Golem | +1 Golem (up to 3) |
| | **Bone Guard** | Darts (7) that stick: the third in one enemy within 3 s cages it for 1.5 s (not bosses); 2 orbiting skeletons block bullets and shoot caged enemies | +1 Skeleton Guard |
| | **Bear Totem** | Melee: two quick claw swipes in front of you (85 px, 11 damage each) that make enemies bleed (30% of the hit per second for 3 s, stacking up to five hits), plus a Bear | +15 px reach, 25% more bleed, and the Bear's bites hit enemies around its target |
| Healer · Soldier | **Repulsion Cannon** | Blasts enemies away in a 70° cone (240 px, 6 damage, 90 px push; bosses and heavies don't budge); enemies it hits shed tiny health orbs (heal 6, at most every 1.5 s each) | +20° wider, +2 damage and +30 px of push |
| | **Revolver** | Six hard-hitting rounds, the last one heavy (double damage). **Tap** for perfectly aimed shots (one every 0.45 s); **hold** to fan the hammer: after 0.2 s it empties the cylinder, a round every 0.07 s, each within 5° of your aim. Reloads in 1.2 s when empty, and slides rounds back in one at a time (every 0.25 s) once you stop shooting for 0.8 s | +2 rounds in the cylinder, loaded as heavy rounds |
| | **Mender's Bow** | **Hold to draw, release to fire** (you move at 85% while drawing). A tap looses a weak arrow (8 damage, heals 5); a full draw (0.9 s) hits for 40, flies at 950 px/s, pierces 2 and heals an ally it passes for 20. Let go within 0.25 s of full draw for a **perfect shot**: +25% (50 damage, heals 25). Holding longer keeps it at full; it never fires by itself | +1 arrow nocked: a full draw also looses an extra arrow (half damage) in a tight fan; taps stay single |
| Healer · Elementalist | **Radiant Orb** | Hold fire to cast an orb of light (20 px) out to your cursor, up to 520 px away, and keep it there while you hold: drag it through enemies (8 damage to each it touches, 5 times a second) and over allies (4 healing, 5 times a second); let go and it fades | +1 Light Orb (Barrage adds one too) that circles your orb while it's out (you otherwise), hurting enemies and healing allies it touches |
| | **Chain Light** | A 320 px cone of light (about 70° wide): 40 damage shared between every enemy in it (one alone takes it all), light arcing to each | +25% damage and a 15% longer cone |
| | **Prism Beam** | Rapid short beam (4); through an ally it heals them and splits into three beams fanning out beyond them | +30% length, +25% damage, +50% healing |
| Healer · Summoner | **Sprite Staff** | Seeds (9) that stick in enemies, plus 2 healing sprites; killing a seeded enemy frees another sprite for 6 s (up to 4 more) | +1 Sprite |
| | **Treant Rod** | Healing bolts, plus a Treant that roots where it stands and blocks bullets; a vine from you to it heals allies crossing it (8/s) and slows and stings enemies crossing it (50%, 6/s); it walks after you past 360 px | The Treant grows 30%: blocks more, hits harder, reaches farther |
| | **Totem Staff** | Seeds (9); every 10 s your next shot throws a healing totem to your cursor (up to 400 px); 6 seed hits on enemies near a totem make it pulse 25 healing to allies near it | Totems 60% more often |
| DPS · Soldier | **Twin Rifle** | Twin streams (5 per barrel) angled in to cross at your cursor's distance (120 px to full range); hits within 45 px of the crossing are crossfire, for double. Your aim line marks the crossing with an X | +1 barrel |
| | **Railgun** | Slow shots (45) that pierce 3; the path stays charged for 1 s behind the round, hurting anything in it (25 a second) | Throws 2 shrapnel shards sideways at every enemy it pierces |
| | **Burst SMG** | Very fast spray (5) that spins up as you keep firing: over 2 s the spray tightens from 9° to 2° either side and damage climbs to +60%; it winds down in 0.6 s when you stop | +1 bullet per shot (half damage) and a wider spray |
| DPS · Elementalist | **Fireball** | A big, slow fireball that rolls through every enemy in its path (22 each), setting them burning (8 a second for 3 s) | +1 fireball, fanned out |
| | **Arc Lightning** | Hold to charge a 90 px wide strip in front of you, 160 px for a tap out to half the arena at full; let go and everything in it is shocked (40, 0.4 s stun) | +25% damage |
| | **Arcane Missiles** | Each cast puts 3 missiles (7) into orbit round you (up to 6); they launch, homing, at enemies within 300 px, one every 0.08 s. With the orbit full, a cast flies at your aim | +1 missile a cast, +2 in orbit, and faster launches |
| DPS · Summoner | **Wolf Whistle** | Darts mark the enemy they hit as prey for 4 s and your 2 wolves hunt it (a fanned or piercing dart that clips an enemy farther from your cursor doesn't move the prey); every wolf biting it within a second is a takedown (20 per wolf, 0.6 s stun) | +1 Wolf |
| | **Wisp Lantern** | 3 wisps orbit you and are your shots: each shot sends one at the enemy nearest your cursor (22), and it comes back | +1 Wisp, and each shot sends one more |
| | **Imp Caller** | An imp every 3 s (up to 3) waits by you; tap fire to send them charging at your cursor (45 each), or hold to merge them into one big imp (blast 125% of theirs together, and wider) | +1 Imp, arriving 50% faster |
| Tank · Knight | **Warhammer** | Charge slam: hold to charge (0.9 s), let go to slam a 130 × 100 px strip ahead; 20 damage and a 0.25 s stun for a tap, up to 80 damage and a 0.9 s stun at full charge | +1 aftershock: another slam 0.2 s later, 40 px further out (60% damage) |
| | **Lance** | Heavy jab (36 damage) that hits a box straight ahead, 180 px long and 88 px wide, shoves enemies 140 px back (not bosses or heavies) and stuns them for 1 s (not bosses) | +30 px reach, 24 px wider and +60 px knockback |
| | **Halberd** | Sweep: an 18° blade, 200 px long, swept across 80° in 0.18 s; 30 to each enemy it passes, once a sweep | +30 px reach and a return sweep (60% damage) |
| Healer · Knight | **Quarterstaff** | 150° sweeps (105 px, 18) that knock enemies back 70 px; each swing parries: enemy bullets in its arc fly back at enemies as your shots (12), big orbs too, but never red aimed shots; each enemy hit heals you and allies within 220 px for 2 (up to 8 a swing) | +1 follow-up strike with the other end (60% damage) |
| | **Lightbringer** | 100° sword cut (17) plus a crescent of light (12, pierces 2) that heals allies it passes for 6; every enemy hit fills a light gauge, and at 12 the next swing's crescents heal three times as much | +1 crescent, fanned out |
| | **Censer** | Flail whirled all the way around you (14, 80 px); each spin heals allies within 140 px for 3; moving leaves an incense trail (3 s) that heals allies in it (4/s per puff) and makes enemy shots fired inside it stray | +20 px reach and +1 HP healed per spin |
| DPS · Knight | **Twin Blades** | Very fast alternating 90° slashes (15); 10 hits in a row (each within 1.2 s) prime a dash-strike: your next slash lunges you 140 px, hitting everything you pass for triple | +1 cut: each swing strikes once more (60% damage) |
| | **Greatsword** | Huge 170° sweeps (60) with 125 px reach; each sweep's edge hangs for 0.5 s, stopping enemy bullets that cross it | +1 sword beam: each sweep launches a piercing crescent (40% damage) |
| | **Rapier** | Narrow 130 px thrusts (18) at the nearest enemy in front; +10% per hit in a row on it (up to +60%); each thrust skewers up to 3 enemy bullets along its line (not red aimed shots), each adding a stack | Skewers 1 more enemy behind the first |
| Tank · Ninja | **Kusarigama** | Throw the chain-sickle straight out (280 px) to hook the first enemy it hits (26); fire again to reel yourself in to it and land a heavy slash (52, 0.5 s stun) | +40 px throw and +30% slash damage |
| | **Bo Staff** | Spun all the way round you (16, 90 px), cutting small bullets; click while moving to vault 150 px, striking everything you pass (24), once every 1.2 s | +20 px reach and a 30 px longer vault |
| | **Fuma Shuriken** | A giant star (30) thrown in an oval loop 380 px out in front of you and back, cutting through everything both ways | +1 shuriken, looping the other way |
| Healer · Ninja | **Medicine Darts** | Fast needles (7) that dose what they hit: a stack of poison on enemies, a stack of healing on hurt allies (2 a second for 3 s each, up to 5 stacks) | +1 dart, fanned out |
| | **Spirit Fan** | A short gust (6) that heals allies in it for 3 and blows small bullets back; shots alternate blowing enemies in front 60 px away and drawing them 60 px in | +25% reach and +1 HP healed |
| | **Ofuda** | Charms fly to your cursor (up to 460 px) and lie as wards (up to 5, 10 s); an enemy stepping on one sets it off: 25 within 70 px and 12 healing to allies within 110 px | +1 charm, fanned out |
| DPS · Ninja | **Shuriken** | Fans of 3 stars (9) that stick in what they hit for 5 s; double-tap fire to recall them: each rips out for another hit and flies back to you, cutting what it passes (50%) | +1 star in the fan |
| | **Katana** | 110° slashes (20); stop firing for 0.6 s and it's sheathed (a glint runs along it): the next cut is a quick-draw, 220 px through everything in its line, for 2.5× (50) | +1 cut: each slash strikes once more (60% damage) |
| | **Kunai** | Rapid knives (8), +50% on enemies that aren't targeting you; every 4th is a shadow kunai that leaves a mark where it lands (3 s): double-tap fire to blink there, and your next hit lands double | +1 knife, fanned out (half damage) |
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

| | Soldier | Elementalist | Summoner | Knight |
|---|---|---|---|---|
| **Tank** | **Fortress** (70 s): for 8 s every enemy targets you and you take 75% less damage; erases nearby bullets and shoves enemies back | **Glacial Tomb** (75 s): erases every enemy bullet and freezes every enemy solid for 4 s; bosses take 250 damage instead | **Titan** (80 s): a stone titan smashes enemies for 15 s and blocks bullets (soaks 1000 boss damage) | **Last Stand** (70 s): for 8 s you can't drop below 1 HP, every enemy targets you, +50% damage |
| **Healer** | **Lifeline** (90 s): instantly revive and fully heal the whole party, plus 3 s of invulnerability | **Sanctum** (80 s): a 7 s holy dome; allies inside heal fast and can't go down, and bullets can't get in | **World Tree** (80 s): a tree for 10 s that heals allies, entangles nearby enemies and sends out 4 healing sprites | **Hallowed Blade** (80 s): a giant sword falls at your cursor (200 damage), then blesses the ground for 8 s |
| **DPS** | **Bullet Storm** (60 s): 6 s of triple fire rate and +2 extra projectiles | **Meteor Storm** (70 s): 12 meteors rain on enemies over 3 s, 150 damage each | **Legion** (70 s): 6 wolves and 6 bats for 10 s, and your other minions frenzy | **Blade Tempest** (65 s): for 6 s every swing also hurls three piercing sword beams |

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
| **Common** | white | Small stat boosts: +10% damage, +10% fire rate, +6% speed, +15 HP, regen, -6% cooldowns |
| **Uncommon** | green | Bigger boosts (+20% damage, +40 HP, -15% cooldowns...) and damage reduction |
| **Rare** | blue | **Second Charge** for your Space ability, an upgrade for a specific ability you have, on any key (e.g. bigger Barrier, longer Roar, double-damage Bulldoze, bigger Haven, faster Soul Link, bigger grenades, harsher marks; for Knights: Tower Shield, Earthshaker, Iron Will, Sacred Ground, Bulwark, Miracle, Cyclone, Flurry, Bloodlust; for Ninjas: Mirror Image, Thick Smoke, Tempered Steel, Morning Dew, Body Double, Sacred Seal, Assassinate, Unseen Blade, Storm of Steel; for Hemomancers: Hardened Blood, Blood Oath, Deep Veins, Lifeblood, Hungry Earth, Saint's Blood, Boiling Point, Bloodrush, Crimson Tide), wider aura; plus Multishot, Barrage, Piercing Rounds and Overcharge (second charge on Q/E/Shift) |
| **Very rare** | purple | A brand-new ability on **Q**, **E** or **Shift** (you choose the key; each one only once): a universal one (Shockwave, Overdrive, Blink, Nova or Turret), or one of your **class's two other Space abilities**, the ones you didn't pick in the lobby (these count triple, so about 6 in 11 very rare cards are class abilities) |
| **Legendary** | orange | Unique weapon upgrades, **only from bosses**: after a boss dies, each player picks one of that boss's three (can't be rerolled) |
| **Weapon mod** | teal | Also **after every boss**, right after its legendary: choose any mod from the full list (each stacks up to 3 times) |

| Boss | Legendary weapon upgrades |
|---|---|
| The Warden | **Warden's Guard** (one of the Warden's guards walks at your side and fights enemies within 240 px of you with its baton, 14 damage every 0.7 s; it has 150 health + 15 per wave, enemy shots and bodies hurt it, and a fallen guard is replaced 15 s later; +1 guard per stack) · **Stun Gun** (a new Q / E / Shift ability, 14 s: draw a stun gun with 6 shots, 15 damage each, that stun enemies for 1.5 s (bosses are slowed 50% instead); your weapon is away until they're spent, and only then does the cooldown start; capstone: 9 shots) · **Searchlight** (a pool of light, 110 px, chases your cursor at 1100 px/s; enemies in it take 25% more damage from every player, and one held in it for 2 s is stunned for 1 s, bosses slowed 40% instead, then can't be locked again for 3 s; the 2nd stack widens it to 140 px, the 3rd makes it +40%) |
| Hive Queen | **Swarm Rounds** (shots split into 3 on hit) · **Royal Jelly** (hits heal 1 HP, up to 10/s) · **Brood Mother** (40% of kills spawn a 6 s mini-turret; hitting a boss brings one every 8 s) |
| Twin Reapers | **Executioner** (double damage below 50% HP) · **Twin Fang** (25% chance of double damage) · **Soul Harvest** (kills heal 5 HP) |
| The Singularity | **Collapse Rounds** (shots implode on impact; the enemy hit takes half) · **Graviton Rounds** (hits slow enemies 40%; slowed enemies take 15% more damage from you) · **Dark Matter** (+1 pierce, +20% damage) |

**Weapon mods** change how your shots behave, and you can see them on your bullets:

| Mod | Per stack |
|---|---|
| **Homing Rounds** | Shots bend toward an enemy within 260 px and about 45 degrees of their path, so near misses connect (they don't turn around to hunt). Costs 5% damage per stack; more stacks bend harder. **Area weapons** (Implosion, Flak Cannon, Ofuda, Storm Hammer, Clot Bolt, Vein Tether, Leech Tendrils, Frost Lance, Golem Staff, Chain Light, Arc Lightning) get a bigger area instead of bending: +25% per stack to the blast, burst, pool, collapse, beam width, tendrils' arc, lightning's reach, splinters, boulder, cone or strip, for the same damage cost. **Wisp Lantern and Imp Caller:** the wisps and imps you send pick an enemy 50% farther from your cursor per stack, and one sent at empty ground (or whose target dies) turns onto an enemy within 260 px of it, for the same damage cost |
| **Ricochet** | After a hit, the shot bounces to another enemy within 300 px for 40% damage; +1 bounce per stack. Hits that can't bounce (melee, beams, cones, bursts, sent wisps and imps, and shots that pierce: Railgun, drawn arrows, Bone Spear, Fireball, flames, Fuma Shuriken) store their damage instead, and up to 4 times a second a **bounce bolt** carries 40% of it from the enemy hit to the nearest other one (+1 bounce per extra stack) |
| **Incendiary Rounds** | Hits set enemies on fire: each burns for another 25% of the hit's damage over 3 s (per stack), and burns from every hit add up, so fast and slow weapons gain about the same |
| **Cryo Rounds** | Hits slow enemies 20% (up to 60%) with a 6% chance to freeze non-bosses for 1 s |
| **Shock Rounds** | 15% chance per hit for lightning to arc to 2 more enemies at 60% damage |
| **Explosive Rounds** | Shots explode on impact for 30% damage around them (bigger blast per stack) |
| **Velocity Rounds** | Shots fly 30% faster and farther, +15% damage |

Mods apply to your weapon's shots and your minions' shots, but not to grenades. They also ride every hit of
weapons that don't fire bullets (melee swings, beams, cones, the Repulsion Cannon's blast, Ofuda ward bursts,
sent wisps and imps). On a Knight, the elemental mods apply to every melee hit (see The Knight above).

**Piercing Rounds** let shots pass through one more enemy per stack. Weapons without bullets that run out of
pierce (melee, the Flamethrower, Frost Lance, Fuma Shuriken, Repulsion Cannon, Radiant Orb, Chain Light, Spirit
Fan, Ofuda, Arc Lightning, Wisp Lantern, Imp Caller) reach 10% farther per stack instead.

Rarer cards show up a bit more often as you level. All the numbers live in
`Assets/Scripts/Core/Upgrades.cs` if you want to rebalance or add upgrades.

## Rules

- The arena is 1600x900 and **wraps around for players**: walk off any edge and you come in on the opposite side. Enemies and bullets don't wrap, so the edges make a good escape route.
- Your hitbox is the small white dot in the middle of your character, not the whole circle.
- **Status effects** show as a tint on the enemy plus an icon above it: **snowflake** = frozen (encased in ice, can't move or shoot), **blue double arrow** = slowed, **flame** = burning (flames rise off it), **blood drop** = bleeding (drops fall off it), **red crosshair** = marked (takes extra damage).
- At 0 HP you're **downed**. Stand next to a downed ally for 3 seconds to revive them (or the healer can pulse them back).
- If everyone is downed, it's game over. Clearing a wave revives anyone downed and heals everyone a bit.
- New enemy types unlock as the waves go up (see below), with a boss every 5 waves.
- **Boss bodies:** touching a boss hurts once (25 + 0.6 per wave) and shoves you about 100 px back out; it can't hurt you again for 1 s.
- **Bullet budget:** enemy bullets on screen are capped at 50 + 4 per wave, up to 120 from wave 18 (30% more per extra player). When the screen is over the cap, regular enemies hold their fire and try again a moment later. Bosses always fire, and while a boss is up the other enemies only get 40% of the cap. Later waves get harder through tougher enemies, not more bullets.

## Enemies

The first waves are **melee rushers** (spiky, they never shoot, they hurt on contact). Shooters take over a
little more each wave: wave 1 is all melee, wave 7 about half, and from **wave 13** the early rushers are gone (melee
comes back in squads, and with the Reaver from wave 28).

**Squads.** About 70% of a regular wave arrives as small teams that come in together from one edge and look after each
other; the rest are single enemies. Each squad unlocks at a wave, and none comes more than twice in one wave:
Rush (1: four Swarmers), Firing line (3: three Grunts, a Sniper behind), Brute pack (5: the Lunger waits for a Brute to
reach you), Splinters (7: two Splitters and a Grunt), Bombardment (8: a Charger and two Mortars), Shield wall (9: a Shielder leading two Snipers that keep behind
it), Field hospital (11: three Brutes and a Medic behind them), Ambush (16: three Phantoms blink in around you),
Beam battery (18: two Beam Sentries guarded by a Shielder), Pincer (21: Hexers from opposite edges), Scorched earth
(24: two Scorchers and Swarmers), Siege (26: a Juggernaut and two Medics), Reaver pack (28: two Reavers and two
Lungers), Hive (31: a Nest guarded by Splitters), Bastion (36: a Warder, a Juggernaut and two Grunts). Early waves keep
their share of rushers (a melee squad as often as a melee enemy used to come). Paths with their own mix keep it.

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
| 9 | **Shielder** | A shield on its front blocks your shots. It turns slowly, so flank it. The shield (60% of its health) wears down as it blocks and crumbles on its own (about 33 s), cracking as it goes, then shatters |
| 11 | **Medic** | Heals nearby enemies every few seconds. Kill it first |
| 16 | **Phantom** | Flickers, blinks next to you and bursts into a ring |
| 18 | **Beam Sentry** | Walks to about 380 px from you and plants itself, aims a thin line at you for 1 s (turning slowly after you), then sweeps a 900 px beam a quarter turn across your side (20 + 0.3 × wave). Step behind the sweep |
| 21 | **Hexer** | Fires curving pincer shots that close in from both sides |
| 24 | **Scorcher** | Circles you about 260 px out leaving a trail of embers (15/s, 3 s; they only burn players) and fires pairs of shots. Cut across its circle |
| 26 | **Juggernaut** | Armored and slow; can't be slowed or knocked back; fires rotating stars of big orbs |
| 28 | **Reaver** | Armoured melee (can't be slowed or knocked back): in reach it shows a red 120° cone for 0.4 s, then rakes it (26; a perfect parry blocks it). For 0.7 s after a swipe it's **open**: +50% damage |
| 31 | **Nest** | Stationary spawner that keeps hatching Shards until destroyed |
| 36 | **Warder** | Hangs back behind its allies; every 6 s up to three allies within 320 px get a bubble worth half their health, tethered to it. Kill it and every bubble pops |
| 12+ | **Elites** | Any enemy can spawn with a gold ring: 2.5x health, bigger, worth 3x XP, always drops an orb, and a **trait** shown as a marker over it (two traits from wave 35): **Hasted** (40% faster moving and firing), **Shielded** (a bubble worth 40% of its health that grows back 4 s after it was last hit), **Volatile** (bursts into a ring of 16 shots 0.6 s after it dies), **Splitting** (dies into two Shards, four from a Juggernaut, Nest or Brute), **Mirrored** (one in four of your shots that hit it bounce back at you), **Commander** (allies within 220 px fire 30% faster). More common each wave |

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
| 5 | **The Warden** | A prison riot in three stages, one mechanic at a time (2 s between stages, when the screen clears and it can't be hurt). **Count** (100–65%): sways through the middle — orb rings, a spiral (holding still), aimed fans, then an 18 s **searchlight** from the corner tower that follows you at an outrunnable pace while it fires single **stun shots**; get stunned or linger in the light and a **shackle ring** of orbs closes in on you, its gap facing away from the Warden. **Lockdown** (65–30%): the 12 s **cross** (dash, plant, fire a static "+", four times) runs alongside three **gates** — walls of push bullets 2 rows deep, with two doors near the side walls, that shove you back the way you came — and lobbed **tear gas**. **Riot** (30–0%): cell doors burst open and **rioters** pour out, nine a door (they attack whoever is nearest, the Warden too); any still standing after 10 s bring blue **guards** with batons, who beat them down and then come for you while it turns a riot shield toward you and fires a shotgun; then a telegraphed **baton charge**. Below 10% it makes a **last stand** in the middle |
| 10 | **Hive Queen** | A hive in three stages, one mechanic at a time (2 s between stages, when the screen clears and she can't be hurt). **Swarm** (100–65%): she sways along the top — **drones** (tough Shards; she's **shielded and waits** while they live, and any left after 10 s fly back into her and heal her 2% each), **bombers** at the spot you're standing on, aimed **fans**, then orb **rings**. **Honeycomb** (65–30%): she lays 4 **eggs** that each hatch 2 drones unless you break them in 6 s; the floor's hex **cells** glow and seal (18) in waves, always leaving a safe cell next to you; a sweeping **nectar spray** leaves honey puddles that slow you. **Frenzy** (30–0%): she drops into the arena — a **swarm cloud** of bees hunts you at an outrunnable 170 px/s, three **royal dives** land on your shadow (she can't be hurt in the air) with a ring of orbs, and a **bomber barrage** aims at where you are and where you're heading. Below 10% she makes a **last stand** in the middle |
| 15 | **Twin Reapers** | Two Reapers (2600 HP each) in three stages by their combined health, one mechanic at a time. **Twilight** (100–65%): a **tether sweep** that spins round a point following you, then **lines** and a **cross** fired holding still; **Soul Bond** (the more-hurt one is immune until you even them out). **The Dual Reaper** (65–30%): they spiral together and fuse (a power-up of marked blasts), armored to a third of the damage. It fires **twin rotating lasers** while 4 **pillars** stand in the corners — kill them, or it pulls you in and blows up — and each pillar leaves a **hole**. Then it **chases** you swinging both scythes; lure it over a hole and it falls in: break its **shield** and it's **open** for 4 s (half again the damage), then it leaps back and starts over. **Sundered** (30–0%): it splits back into Dusk and Dawn and the tether snaps — marked **reaps**, a **twin spiral**, then **crossfire**; kill one and the other **enrages**, and the fallen one revives at 60% after 6 s unless you finish the pair |
| 20 | **The Singularity** | A black hole in three stages, one mechanic at a time with breathers between (2 s between stages, when the screen clears and it can't be hurt). Four **Gravity Wells** spawn in the corners at the start of each stage: they bend your shots hard into themselves and drift you toward them; killed, they stay gone until the next stage. **Event Horizon** (100–65%): in the middle; **Pull** (4 s, drags everyone in under a three-armed spiral), **Collapse** (3 s of bullet rings), **Lasers** (8 s: a purple spiral, then three red lasers that flicker as a warning and sweep slowly round). **Accretion** (65–30%): it orbits the middle inside a **disk** of two rings of big orbs turning opposite ways, with gaps (touching one hurts); **Cage** (9 s: three small wells orbit every player in a shrinking ring; touching it hurts, shooting the wells breaks it), **Slingshot** (6 s: every 0.8 s three orbs leave the outer ring at you), **Twin lasers** (two, sweeping faster). **Spaghettify** (30–10%): back in the middle behind an **event horizon** that grows from 70 to 170 px (touching it hurts) and pulls all the time (60 px/s, slower than anyone walks); **Hawking** bursts (rings of 18 off the horizon), **White hole** (a 1 s white flash, then for 1.5 s it pushes and every enemy bullet on screen turns round), **Split** (three shards orbit and fire; the core can't be hurt, and damage to the shards goes to it). Below 10% its **last stand**: the horizon swells to 190 px, the pull doubles, a spiral and rings of big orbs |
| 25 | **The Colossus** | A stone giant in three stages that breaks down as it goes, one attack at a time with breathers between (2 s between stages, when the screen clears and it can't be hurt). Its **stomps** send out rings of **push blocks** (like the Warden's gates) with green **gaps**: you can't cross a ring, it shoves you back, carries you out if it catches you and rolls over you against the edge (hurting again), so slip through a gap. **Awakening** (100–65%): it walks at you, rock volleys between turns — Stomps, **Boulders** (thrown where you're heading, then a burst), **Fissures** (a crack races toward you, then erupts along its length). **Crumbling** (65–30%): its armour falls as **four plates of cover** that stop shots from both sides, and it walks faster — **Charge** (a red lane, then it runs; **a plate in the way breaks and dazes it: +50% damage**), **Rockslide** (rows of rocks from an edge with a moving gap), **Aftershocks** (rings whose gaps spiral). **Molten Core** (30–10%): everything leaves **lava** (20/s) — Lava stomps (two gaps), **Meteors** (land in orb rings and pools), **Cave-in** (pillars fall, stand as cover for 3 s, then shatter). **Last stand** below 10%: it kneels in the middle — two-gap stomps, volleys and meteors |
| 30 | **The Duelist** | A duel in three stages (2 s between, the screen clears and it can't be hurt). **En Garde** (100–65%): it strafes 280 px out — Volleys, a **Lunge** (warning line, a fast dash leaving blades that fire sideways), a **Guard** (your shots on its front bounce back at you; it turns slowly, so get behind it), and a Blink that leaves a decoy. **Masquerade** (65–30%): three identical decoys circle you with it, and only its shots are solid (theirs are hollow rings); a **Crossing** (an X of dashes leaving blades) and a lunge chain. **To the Death** (30–10%): a **dueling circle** 360 px round the middle you can't leave (leaning on it hurts 15); it **Salutes** before it lunges — a **perfect parry** as it arrives knocks it off balance for 2.5 s at +50% damage, a dodge gets you raked; a **Flurry** of slashes. Below 10% the circle closes to 280 and it spins and salutes |
| 35 | **The Lich** | **Three stages** among six graves, one mechanic at a time. **The Crypt** (100–65%): one phylactery guards it (break it for a 12 s window); soul fans, grave rings, the dead rising. **Soul Harvest** (65–30%): three phylacteries joined by soul beams that hurt to cross; soul sweep, bone prisons, wailing spirits. **Undeath** (30–10%): a spectre blinking from grave to grave; dead hands, a soul storm. **Final rite** below 10% |
| 40 | **Storm Herald** | A storm in three stages, one attack at a time with breathers between (2 s between stages, when the screen clears and it can't be hurt). **Gathering Storm** (100–65%): it drifts along the top, firing its 3-shot piercing volley in the breathers; **Strikes** (6 s: a 70 px strike on each player and two random spots every 1.8 s, 1.1 s warning, 25 + 0.3 × wave), **Lightning rods** (7 s: four rods fall around you; every 1.4 s an arc flickers 0.6 s between a pair, then fires along the line, 18 + 0.3 × wave), **Thunderclap** (5 s: rings of 36 with two gaps, each turned 25°). **Gale** (65–30%): a **wind** blows across the arena all stage, turning round with each attack; it pushes players 70 px/s and drifts every bullet 60 px/s, yours too. **Crosswind** (6 s: 5-shot piercing volleys aimed upwind so the wind carries them on), **Ball lightning** (7 s: twice, three 70 HP orbs drift at you on the wind; two within 200 px arc to each other, and each pops into 8 shots after 6 s), **Downburst** (6 s: rows of six 110 px strikes at your height, landing one after another in the wind's direction). **Supercell** (30–10%): the sky goes dark. The **Conductor** marks one player at a time (every 8 s, for 3 s, a 180 px ring): when it runs out the charge jumps to every ally in the ring, hurting both ends, and 10 shots burst out from them (alone, it only grounds into the shots). **Storm front** (7 s: a wall of strikes sweeps across with a two-strike gap that drifts), **Strike rain** (6 s: smaller, quicker strikes), **Rod cage** (7 s: twice, six rods ring you 240 px out and arcs join them, all but one open side). Below 10% its **last stand** in the middle: four lightning spokes turning 20°/s, a strike on everyone every 2 s, its volley |
| 45 | **The Devourer** | A hunger in three stages, one attack at a time with breathers between (2 s between stages, when the screen clears and it can't be hurt); it grows each stage (50 → 58 → 64 px). **Appetite** (100–65%): it keeps 260 px from you. **Spiral** (6 s: its double spiral, 2 shots every 0.08 s), **Snap** (5 s: three lunges at a player, a red line for 0.7 s, then a 1000 px/s dash and a bite in a 110 px half-circle, 13 + 0.6 × wave), **Hungry** (5 s: its mouth opens and your shots **heal** it for half; rings of 20 big shots every 1.2 s; it spits 4 **morsels** (60 HP) and crawls over to eat them, each healing it 1%: shoot them or walk over them first). **Gorging** (65–30%): **Inhale** (5 s: twice, a 70° cone 600 px long pulls you 200 px/s towards its mouth, 80 at the far end; whoever reaches the mouth is **swallowed**: hidden inside for 3 s, 10 damage a second, while allies' hits on it do **double** and 300 of them spit you out early, with a ring of 12 shots), **Bile** (6 s: every 1.5 s a glob at each player and one at random, a 90 px pool of acid for 6 s, 12 a second), **Burrow** (7 s: twice, it dives (can't be hurt), a ripple chases a player for 1.9 s, stops, marks 100 px, then erupts with a bite and a ring of 16). **Ravenous** (30–10%): **the Maw**, a 90° cone in front of it: shots into it heal it for half, shots on its back do ×1.25; it turns to the nearest player at 60°/s (35°/s if you're alone). **Frenzy** (6 s: four lunges, 0.55 s warning, each ending in a ring of 12), **Jaws** (6 s: twice, two rows of 7 teeth 320 px either side of a player show for 0.6 s, then close at 140 px/s), **Vomit** (5 s: a spray of bile sweeping 70° either side of you and back). Below 10%, its **last meal** in the middle: it pulls everyone in at 55 px/s, ten teeth (two gaps) turn round it 170 px out, its spiral comes back at half the rate; being swallowed here does 20 a second |
| 50 | **The Eclipse** | The final boss: **five stages** and a last stand, one attack at a time with breathers between (2.5 s between stages, when the screen clears and it can't be hurt), about twice as long as any other boss. A sun and its moon line up as it goes, and each stage keeps something from the last. **Corona** (100–80%): it orbits the middle; its 4-arm **Spiral** and 24-shot **Corona rings**, and **Solar flares** (a line at each player and one more, 1 s warning, then a 40 px lane of fire, 25 + 0.3 × wave); three sun shots in the breathers. **Penumbra** (80–60%): a **moon** orbits it 300 px out; it can't be hurt and **soaks your shots**. **Lunar wake** (the moon speeds up, trailing slow shots), **Eclipse flash** (the moon stops and the sun swells for 3 s, then everyone **outside the moon's shadow** takes 40 + 0.3 × wave), **Gravity** (its old pull and 4-way shots); two Phantoms. **Umbra** (60–40%): the arena goes **dark**; you see 140 px round you and in three drifting **sunspots**, and outside them the dark drains 6/s. **Crescents** (blades that loop out through you and come back), **Closing umbra** (rings of shadow close in with two gaps), a **Dark spiral**; two Hexers. **Echoes** (40–20%): turns borrowed from earlier bosses: the **Colossus**'s stomp rings, the **Lich**'s twin soul beams, the **Storm Herald**'s strike rain and the **Singularity**'s pull and disk; the moon shoots back. **Last Light** (20–5%): the arena's **edges burn** and close in 160 px (you can't pass, and touching burns); a frantic 5-arm spiral, **Shadow nova** (two flashes, the moon closer and jumping round between them) and a **Solar storm** of flares and crescents. **Totality** below 5%: the moon is gone; it takes **+50% damage** while a **12 s nova timer** runs, and the nova hits everyone for 70% of their health and heals it 2% before starting again |

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
boss, or **no path** (the plain arena, no bonus or prize); everyone sees the choice, and the next wave waits for it. Bosses are always the standard fight. A path changes
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
| **Burning Grounds** | 3 | Fire patches flash a warning ring, then ignite and spread for a few seconds; they burn you (10 + 0.4 per wave damage a second) and the enemies |
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

## Run modifiers

The lobby's **Modifiers** tab lists switches that change the whole run, for everyone (Practice Range too). The
host turns them on and off; the others see them, and changing one takes back everyone's Ready. Active ones show
as chips above the wave panel during the match, and session recordings list them.

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
| Hide / show the HUD | H (it slides away and fades; "Press H to show the HUD" stays faintly at the bottom while it's hidden) |
| Aim debug overlay | F3 |
| After game over / victory (host) | R = play again, L = back to lobby, C = keep going (endless, after victory) |

## The HUD

Everything sits along the bottom, so the top of the arena stays clear: the **ability bar** (with your XP strip)
in the middle, the **wave panel** left of it (wave, enemies left, score, progress; the path and modifier chips
stack above it) and **your card** right of it (health, shield, level, XP). Teammates show as slim rows above
your card: name, class, level and health (or their revive progress when downed). Boss health bars go wide across
the top middle, and the "Hold TAB" hint is in the top-right corner.

## End of a run

When a run ends (game over or victory) a results screen shows the wave reached, time, score and total
kills, plus a column per player: level, damage dealt, kills (elite and boss), biggest hit, accuracy,
healing done, allies revived, damage taken, times downed, abilities used, and the upgrades picked by
rarity. The best number in each row is highlighted, with awards like **Most damage**, **Best healer** and
**Sharpshooter**. The host can play again, go back to the lobby, or keep going after a victory.

## Saved runs and records

**Runs save themselves.** The host's game saves the run as each wave starts (once everyone has picked their
upgrades, the path is chosen and the shop is closed), and again if the match is left or the game closed between
waves. Leaving mid-wave keeps the save from that wave's start. A run that's won or lost loses its save. The
Practice Range, the lobby's loadout preview and Boss Lab fights are never saved.

**Continue a run** on the main menu lists the saves (wave, when, score, time played, each seat's player and
class, the path and modifiers), newest first, with **Continue** and **Delete** (click twice). Continuing hosts a
lobby for that run: it shows **Continuing a saved run** instead of the mode switch, and a **Seats** tab instead of
your loadout. Everyone plays the character they had: a friend who joins takes back the seat with their name (or
the first free seat, and moves aside if its owner turns up); **Take this seat** moves you to a free one. Classes,
loadouts and modifiers can't be changed. Seats nobody takes are left out. The match starts at the next wave, with
everyone's upgrades, ability keys, levels, XP, rerolls, mastery, gold, revive token, health and run stats back,
and the run's score, time, path and route. Saves are plain text in the game's data folder (`saves/*.run`),
with upgrades and abilities by name, so they keep working as the game changes.

**Records** on the main menu: this computer's player's runs started, won and lost, best wave, best endless wave,
best score, time played, totals (kills, damage, healing, downs, revives), each boss beaten, and runs and wins per
class. The host tells every player's game when a boss falls and when a run ends, so in co-op everyone's records
grow. Practice Range matches, Boss Lab fights, and runs where dev tools (F1) or the bot were used don't count.
Kept in `profile.txt` in the game's data folder.

**Unlocks** are ready to be added: each is an entry in `Unlocks.All` (`Profile.cs`) with a condition on the
records; it's earned the moment the condition holds (an **Unlocked** toast in the match, listed in Records).
There are none yet.

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
they stay in sync (`Tools/SimTest/run.sh [start-wave] [seconds] [suite]`, needs Mono). The default suite is
`core`; `features` runs every feature test, `all` runs both, and `run.sh list` shows every suite. Tests live in
`Tools/SimTest/Tests/`, one file per system, and their random rolls are seeded so a run is repeatable
(`SEED=random` for fresh rolls).
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
  plays for you, **text editing** (also F4, below), open the practice folder, and the **Boss Lab**.

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
**● REC** in the top-right corner shows a match is being recorded. The Unity editor and a built game keep
separate settings, so check it in each. While you **host**, every match (solo or with friends) is saved to a JSON file in the sessions folder (the **Open sessions folder** button shows it; on Windows it's
`%USERPROFILE%\AppData\LocalLow\<company>\<product>\sessions`). The host decides every hit, so the file
covers all players. The end-of-run screen shows where it was saved. It's rewritten after every wave and every 10 s, so
a crash keeps nearly everything. Leaving or quitting mid-run saves it too: the outcome reads "left" (Leave game,
Back to lobby), "quit" (the game was closed) or "restarted" (a new match started over it); a file whose outcome
still reads "in progress" is from a run that crashed.

Each file records:

- the team (role, class, weapon, ability, ultimate), party size, start wave, the mode (run or Practice Range),
  run modifiers, and how it ended (a run continued into endless mode records its real end, marked "(endless)")
- each player's final level and the abilities they ended with, at their mastery levels
- **per wave:** length, the path it was played on, enemies spawned and killed by type, damage the floor did to
  enemies (Burning Grounds' fire), and per player: damage dealt (and to bosses),
  kills, damage taken **broken down by source** (e.g. "The Warden (aimed)", "Brute (contact)"), healing done,
  healing received **broken down by source** (allies, own abilities, regeneration, lifesteal, heal on kill,
  orbs, Sanctuary pools, Blood Moon), health orbs, downs, revives, seconds spent under 25% health, health and level at the end, ability uses
  and parry blocks / perfect parries / breaks
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

- `Assets/Scripts/Core/`: plain C# with no Unity dependency
  - `Defs.cs`: role, enemy, bullet and weapon stats (each weapon's shot speed, lifetime, damage, reach and growth per extra projectile); tweak numbers here to rebalance
  - `Upgrades.cs`: the upgrade cards, rarities and the extra abilities
  - `EnemyAI.cs`: waves, enemy behaviour and attack patterns, boss mechanics
  - `LateGame.cs`: waves 16-50 enemies and bosses, lightning strikes, victory and endless mode
  - `StatSheet.cs`: the stats shown beside the upgrade cards
  - `Kits.cs` / `Minions.cs`: minion and zone definitions; Elementalist (code: `Kit.Mage`) and Summoner weapons, abilities and minion AI
  - `Ultimates.cs`: the nine ultimates
  - `Mastery.cs`: ability mastery levels, XP and capstone descriptions
  - `Knight.cs`: the Knight class: melee swings, bullet cutting, its abilities and ultimates
  - `Ninja.cs` / `BloodMage.cs`: the Ninja and Hemomancer classes: weapons, abilities and ultimates
  - `Stun.cs`: the Warden's legendaries: the Stun Gun ability and Warden's Guard companions
  - `Paths.cs` / `PathsUi.cs`: divergent paths (offer, choice, enemy mixes, bonus and prize; the cards, floor and chip)
  - `Shop.cs` / `ShopUi.cs`: the Bazaar's gold and shop (shelves, services, giving gold, closing, leftover gold to XP; the shop screen)
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
