# Top-Down Arena

A co-op top-down bullet hell for **1 to 3 players** over the network, made in **Unity 6**.

**Party size scaling:** solo, enemies have 85% health (Normal difficulty); with two players, 100%; with three, regular
enemies have 120% health and waves have 25% more of them, and bosses have 150% health. Bosses also have 1.2x to 5.5x
their base health, set per boss so each fight lasts about 60-120 s (`BossHpMult` in Balance.cs; the Warden and Hive
Queen 2.5x), and every boss has 10% more on top (`BossHpBonus`), and every enemy bullet hits 15% harder than its
listed damage. **Enemy damage grows 3% a wave** (Normal: 1.3x at wave 11, 2.5x at wave 50), on everything that hurts
you. The host picks a [difficulty](#difficulty) in the lobby: Easy, Normal, Hard, Nightmare or Custom. Pick a role,
then survive endless waves of enemies with a boss after every 5th wave.

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
Ninja, Hemomancer), then one of that combination's three weapons (four for the DPS Knight: the Arc Knuckles on the 4th key) and three Space abilities. Weapons and abilities are
unique to their role and class. The **Ultimate** row under them shows the combination's ultimates: key 0 is its default
ultimate and key 1 its second where it has one (so far the Tank Soldier: Juggernaut Suit, and the Tank Hemomancer: **Bloodbath**); the
other keys are locked. Click a key to pick it (inverse video, like the weapon and ability keys): the detail box and the
preview show it. The pick goes back to the default when you change role or class; bots use the default, a Wildcard's
row lists its class wheel's class, and a saved run's seats keep theirs.

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
| **Tank** (240 HP, enemies prefer you) | Weapons: Scattergun (double damage and a shove point-blank; Alt fires a shell and kicks you back), Flak Cannon (airbursts at your cursor, shredding enemy bullets; Alt lays a proximity shell), Riot Repeater (rounds suppress: enemies hit shoot slower; Alt lobs tear gas)<br>Abilities: Barrier, Roar, Bulldoze | Weapons: Flamethrower (sets enemies burning), Frost Lance (an icicle out from you that splinters sideways, slowing), Storm Hammer (a spinning hammer that electrifies what it hits)<br>Abilities: **Aeroaegis** (a wind vortex round the player your cursor is nearest: enemies can't enter, bullets are reflected), **Frost Nova** (freezes nearby enemies), **Flame Wall** (burns enemies, blocks bullets) | Weapons: Golem Staff (hold to lob a crushing boulder, + bullet-blocking Golem), Bone Guard (darts that cage enemies, + 2 orbiting skeletons that shoot the caged), Bear Totem (melee claws that cause bleeding, + Bear)<br>Abilities: **Golem Wall** (3 basalt golems that block bullets until they are broken; recasting replaces them), **Earthquake** (you slam the ground, then a Burrower breaks out at your cursor and tunnels through the enemies, biting each one it passes under), **Force Pact** (12 s cooldown: takes the regular enemy nearest your cursor (not bosses or boss adds) out of the wave; after a second of violet chains it comes back as your **thrall**: an enemy of that type on your side that fights within 240 px of you with its own attack at 75% damage, with 2.5x that type's health + 15 per wave. Enemy shots and bodies hurt it, and a fallen thrall returns 15 s later as the same type (a ring of links round your feet fills while it's away). No enemy to take: it fizzles and the cooldown is refunded. Cast again on another enemy and the pact moves to that type; mastered, you keep two thralls: the last two types you chose) |
| **Healer** (140 HP, healing aura) | Weapons: Repulsion Cannon (hits fill a meter; hold Alt to pull enemies in), Revolver, Mender's Bow<br>Abilities: Mend Pulse, **Adrenaline Shot** (instant, 14 s cooldown: injects the player closest to your cursor, you included, or just you when alone: 8 s of +30% move speed, +40% fire rate and 30% less damage taken; fire rate doesn't stack with Overdrive, the bigger wins; a heartbeat ring pulses round them while it lasts), Deployable Medkit (thrown at your cursor, 14 s cooldown: bursts into 6 healing orbs worth 15 health each that anyone can pick up for 6 s; a downed ally an orb reaches is picked up at 20% health) | Weapons: Radiant Orb (heals allies it passes), Chain Light (a cone of light, its damage shared between what's in it), Nimbus (a storm cloud follows your cursor, raining healing on allies under it; click calls lightning on the spot beneath it)<br>Abilities: **Healing Rain** (a rain at your cursor for 6 s: heals allies inside 8 HP a second, slows enemies 30%), **Energy Ward** (a bubble of energy around you and allies within 300 px that soaks 60 damage for 8 s; it shrinks and turns green as it wears, with a green core growing inside that is the heal charging up, and when it breaks or runs out half of what it soaked comes back as healing), **Lumen Ward** (20 s cooldown: a huge cone of light along your aim for 6 s; it moves with you, but keeps the direction it was cast in. Allies inside heal 9 HP/s and enemy bullets inside burn away; enemy bullets everywhere else on the map slow to a stop and fly again when it ends) | Weapons: Sprite Staff (a firefly that seeds up to three enemies, + 2 healing sprites; seeded kills free more), Treant Rod (a root lash that mends allies it touches; the Treant roots and a root to you heals allies and slows enemies crossing it), Spirit Chalk (hold and drag to draw a line of spirit ink that burns enemies, heals allies on it and destroys bullets; draw a loop round enemies to trap them)<br>Abilities: **Spirit Swarm** (5 healing sprites), **Phoenix** (fights, and instantly revives a downed ally), **Soul Lantern** (a lantern lands beside the ally nearest your cursor, or you when alone, and links to them for 6 s: it follows them, heals 8 health a second and flames the nearest enemy within 260 px for 7 every 0.6 s; 18 s cooldown) |
| **DPS** (95 HP, Momentum) | Weapons: Twin Rifle (streams cross at your cursor for 1.5×; hits load two grenades that Alt fires, left then right), Railgun (leaves its path charged for a second), Burst SMG (spins up: tighter and harder the longer you fire)<br>Abilities: **Poison Dart** (8 s: a dart at the enemy nearest your cursor hits for 20 and poisons it for 18 a second over 5 s; if it dies while still poisoned, the poison jumps to the 3 nearest unpoisoned enemies within 180 px, 80% as strong and for the full 5 s again, and on from them when they die), **Airstrike** (16 s: throw a flare at your cursor, anywhere in the arena; a red lane lights up through it across your aim, wall to wall, and 1.2 s after the cast a jet carpets it: 55 a bomb, one every 140 px, each in 85 px; then the lane burns for 3 s at 15 a second. Allies are safe), Mark for Death | Weapons: Fireball (big, rolls through enemies, burning them), Arc Lightning (hold to charge a strip of lightning), Ice Shards (bank in orbit, launching at enemies that come near)<br>Abilities: **Meteor** (a fireball streaks in over the warning circle, then 150 damage), **Blizzard** (slowing storm), **Tsunami** (a wave that soaks enemies: slowed, and lightning arcs to every wet one) | Weapons: Wolf Whistle (darts mark the prey the 2 wolves hunt; every wolf biting at once is a takedown), Wisp Lantern (3 orbiting wisps you send out as your shots), Imp Caller (imps wait by you; tap to send them, hold to merge them into one big blast)<br>Abilities: **Bat Swarm** (8 bats circle you for a moment, then peel off and dive at enemies), **Hydra** (a three-headed hydra rises at your cursor for 8 s, each head spitting venom at a different enemy), **Alpha Call** (Blood Scent: your minions frenzy, building a stack with each bite, and lunge at 5) |

**Wildcard** (a fourth role, locked until you have won a run as a Tank, a Healer and a DPS; a co-op win counts): it
has no class. You pick any class's DPS weapon, any DPS Space ability for the usual Space key, a second DPS ability
that sits on Q, E or Shift (your choice, changeable in the Tab menu like any bonus ability), and any class's DPS ultimate.
The lobby's Gear box has a class wheel (arrows, the mouse wheel or the dots turn it) with that class's DPS weapons (three, or four for the Knight: the Arc Knuckles) and
three abilities beside it, and the Ultimate row lists its ultimates. Under the wheel, the loadout shows each pick with its
class's icon (click one to turn the wheel to it); the 1st and 2nd chips choose which slot an ability click fills (the
other slot's ability swaps over), and the preview casts whichever you clicked last. 130 health, no role passive (no taunt, no aura). The price: 10% less damage
and 25% slower ability cooldowns (ultimate included). Class passives don't come with borrowed gear (a Hemomancer ability
costs its health but there is no Blood Price bonus); a weapon's own cards still appear, and borrowed abilities earn
mastery as usual. It can't be picked in the lobby or the Practice Range while locked. Bots keep their normal roles
unless you give one the Wildcard role by hand. Design notes: [`Docs/Wildcard.md`](Docs/Wildcard.md).

Generic upgrades (damage, fire rate, cooldowns, Second Charge, legendaries) work for every class; minions
use your damage and attack-speed bonuses too.

**DPS** (every class): **Momentum**, the role passive. Each kill you land adds a stack (up to 5), and each stack gives +3% move speed and +3% fire rate (+15% at full). The stacks age out together after 4 s without a kill (every kill refreshes the timer), and going down drops them. Amber pips under you show them. Only kills that give XP count (not target dummies); a Wildcard has no role passive, so no Momentum.

**Healers** (every class): the healing aura heals allies within 170 px for 9 HP/s and you for 1 HP/s; each
**Sanctuary** card adds another 9 HP/s for allies (and 40% reach) and +1 HP/s for you. A Healer's healing past
someone's full health becomes a shield (up to 20% of their max health, for 4 s; the aura's own ticks don't count).
Healer abilities work alone too: the **Adrenaline Shot** goes to you, **Spirit Ward** takes your own
damage, **Martyr** with no one down wards the most hurt ally (you, alone) for 12 s so going down puts them straight
back up at 50% (the ward is a halo of 12 blood beads turning round them, one going out each second), and a **Phoenix** brings anyone back, you included (the run doesn't end while one is out).

### The Knight

Knights fight up close. Holding fire **swings** instead of shooting: each swing hits every enemy in its arc
(the Rapier only the nearest), and **cuts small bullets out of the air** in front of you. Big orbs and mines get
through, and bosses' red aimed shots always do. Knights have 115%
health to make up for standing in the thick of it, and deal about a third more damage than the ranged
classes.

| | Weapons | Space abilities | Ultimate |
|---|---|---|---|
| **Tank** | **Warhammer** (hold to charge, let go to slam a fixed circle ahead (160 px across, the same size however long you hold): up to 80 damage and a 0.9 s stun at full charge. A faint outline shows where it lands while you charge, and a disc grows inside it as the charge builds), **Lance** (the Breach, with a lance and a heater shield: jab, jab, a Wide Sweep that bats enemies out of your lane, then a Breaching Thrust that shoves everything in front 160 px back and stuns it; shoved enemies that hit another enemy or a wall take Impact damage; the weapon-action key is Hook & Haul: hook the enemy nearest your cursor, haul it in, and your next attack is the Breaching Thrust), **Halberd** (a long, thin 18° blade swept across 80° in front of you, hitting each enemy once a sweep) | **Shield Wall** (seven steel plates across your front 120°, blocking every bullet, red aimed shots included; each shot wears down the plate it hits (400 damage shared between the plates), a broken plate leaves a gap, and the wall has no timer: it stays up until every plate is broken), **Leap Slam** (leap up to 320 px to your cursor, untouchable in the air; 50 damage and a 1 s stun where you land. With Visual effects on, the knight swells and rises with the shadow left on the floor, a hexagon closes in on the landing radius, and the landing sends out hex pressure rings, steel plates and sparks and leaves a hex rune; each stunned enemy sits in a hex cage whose ring drains over the stun), **Cavalry Call** (18 s cooldown: two lancers ride in at your sides and charge 1,100 px across the map along your aim; each enemy they meet takes 35, is shoved 150 px out of the lane and slowed 45% for 3 s, Heavy enemies take a 45 px shove and a 20% slow and bosses are only slowed; you take 20% less damage for 3 s. With Visual effects on, gold-and-ivory lancers drop in on shafts of light, with a lane band, bow wave and dust) | **Last Stand** (70 s): you plant yourself and brace nine steel plates in a crescent 120 px out (178° wide, facing your cursor; it follows your aim at 1.7 rad/s, so you commit to a side). It takes 0.5 s to plant, then it stops every enemy bullet from the front, red aimed shots included. Your teammates' shots pass out through it, and you take 10% of the damage of every shot it stops (shots from behind or the sides hit you in full). You can't move or use a dash, leap or lunge while planted. There's no timer: it stands until you go down or press the ability again, and the cooldown starts when it comes down. It also comes down between waves. With Visual effects on, three stakes drive in behind you, the plates slam down centre-first, each kicks and flashes where a shot lands, and they crack and rattle as your health runs down |
| **Healer** | **Quarterstaff** (a two-handed staff: left and right swipes that knock enemies back and bat enemy bullets back at them; each enemy hit heals you and allies near you for 2 and stores a Jab stack, up to 3, which never fade; the weapon-action key spends one on a cone jab), **Lightbringer** (a three-step chain: Dawn Cut's crescent heals the ally it passes, Mercy Cut's wards them, Noon Sunburst heals everyone in a ring round you; hits fill a light gauge, full makes the next Noon Radiant; the weapon-action key spends the light on a Sunlance that heals the ally nearest your cursor), **Censer** (a three-step rite: sweep, return sweep and a Benediction that goes all the way round; every step heals allies near you and leaves healing incense that spoils enemy aim; hits light Embers, and the weapon-action key throws the censer to your cursor for a burst and a big healing cloud that also speeds up reviving a downed ally) | **Consecrate** (a Holy Cross flies out along your aim, growing as it goes: allies it crosses heal 35 and run 30% faster for 4 s; enemies take 25, are slowed 40% for 3 s and knocked back; hits each once), **Guardian's Oath** (charge to the player nearest your cursor, downed or not, and drag them clear; a ward of light then halves the damage everyone inside takes for 5 s and revives downed allies twice as fast), **Lay on Hands** (heal the most hurt ally within 300 px for 70, or revive them at half health) | **Hallowed Blade** (80 s): a giant sword falls at your cursor for 200 damage around it, then blesses the ground for 8 s (allies heal 15 HP/s) |
| **DPS** | **Twin Blades** (the Crescent Waltz: two sabres in a five-step chain, rising cut, falling cut, double jab, pirouette, crescent shear, each step carrying you forward; the weapon-action key throws both sabres to cross at your cursor and fly back), **Greatsword** (a three-hit chain: cut, return cut, overhead rend, each leaving a bullet-stopping crescent; hold the weapon-action key to guard, stop bullets for Temper and let go to Rend), **Rapier** (thrusts at the nearest enemy, skewering up to 3 enemy bullets on its line; the first thrust is slow, and every enemy you hit adds a Tempo stack, up to 20, that makes the next thrust come sooner; Tempo holds 1.2 s after your last hit, then drains; the weapon-action key spends it on a Flourish, the blade whipping back and forth and every swing throwing a piercing crescent wave; diamonds behind you, one per stack, fill as Tempo builds and turn gold at full), **Arc Knuckles** (the 4th key: coiled blue gauntlets in a five-punch chain, jab, cross, hook, uppercut that launches, and a Twin Hammer slam whose shockwave stuns; the parry cancels a punch and starts the chain over, and a perfect parry arms a gold Counter Hammer for your next press; the weapon-action key is a Rocket Dash that throws you fist first into the first enemy ahead, and Fire as each move lands chains a Flurry, a Grab & Throw and a Rocket Punch) | **Misericorde** (12 s: wounded enemies, under 25% health from anyone's damage, show a small dagger; press Space and you leap onto the wounded enemy nearest your cursor within 200 px and execute it with the mercy dagger, then leap on to the next wounded enemy within 200 px of where you landed, and the next, untouchable, until none are left in reach. A wounded boss takes a heavy stab (120) and the chain ends there. With nobody wounded in reach the press does nothing and costs nothing. A crosshair marks the one a press would take. Look: Last Rites, a gold chain of light from each kill to the next, tolling rings, the enemy rising away as gold motes and a candle left burning, and "† 11" for the health it had left), **The Gauntlet** (9 s: throw the gauntlet at the enemy nearest your cursor along your aim, up to 380 px, flying over anything in between: it takes 12 and is Challenged for 5 s, its crest burning over it. You shoulder-charge to it, untouchable, bowling every enemy in the way aside (18 and a 55 px shove), and hit it for 30. Kill the Challenged enemy (anyone's kill counts) before the Challenge runs out and the gauntlet flies back to your hand: the cooldown resets. A miss, or a Challenge that runs out, costs the full cooldown. Look: Heraldic, a crimson and gold crest with its timer ring, a gold ring turning under the enemy and a crimson ribbon to it), **Caught Steel** (16 s: for 6 s, enemy bullets that skim past you without touching you (inside a dashed ring 14 px past your hit circle) are caught and hang round you as shards of steel, up to 12; press again, or let the time run out, and a sweeping cut hurls them out along your aim, fanned across 70°, each cutting the bullets in its way and stopping in the first enemy for 22. A hit knocks a third of them loose. The cooldown starts when you throw them. Look: Forge, shards white-hot when caught, cooling to grey steel as they orbit and reheated into molten streaks by the cut; twelve pips over your head on your own screen) | **Drawbridge** (75 s): a gatehouse rises behind you and its drawbridge falls along your aim, its shadow sweeping the lane first and its chains snapping; it lands 1.05 s after the cast: 140 to everything in the 500 x 100 px lane (bosses aren't killed outright), 45 and a 40 px shove to enemies at its edges. Then it lies there for 5 s as a raised walkway: enemies can't step onto it (they're kept to its rails and far end) and their shots stop at its rails, red aimed shots too; you and your allies walk on it. Then the planks give way from the far end. Look: Oak & Iron, oak planks with iron bands and rivets, stone towers, iron chains, dust rolling out both sides and a big screen shake |

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
| **Tank** | **Kusarigama** (a two-piece combo: a quick sickle cut, then the iron weight thrown 280 px to bite the first enemy in its lane, stun it and drag it to your feet; hold the weapon-action key to whirl the weight around you, pulling enemies within 200 px in and cutting what it sweeps, and let go for a stunning snap), **Bo Staff** (a chain that never times out: a cone slash, a jab, then a full spin, cutting small bullets; press the weapon-action key to shove a bar of staff ahead of you, which knocks enemies back and starts the chain over), **Fuma Shuriken** (a giant star thrown in an oval loop 380 px out in front of you and back, cutting through everything both ways; press the weapon-action key for the **Orbit stance**: the star circles you at 80 px, cutting small bullets, Fire lashes it out across your aim and every third press is a wide Cyclone; parry to reverse the circle and cut its ring, perfect-parry to steal shots as small stars that fly back at their shooters; parry with a star in flight to catch it into orbit) | **Shadow Clone** (a wall of 9 afterimage clones for 7 s that every enemy aims at; each soaks 50 damage and never fights; walk into them to shove the wall; capstone: double health), **Smoke Bomb** (a 140 px cloud for 5 s: enemy bullets inside slow to half speed, allies inside take 30% less damage), **Iron Body** (2 s of 80% less damage, then a 50-damage shockwave) | **Thousand Clones** (70 s): twelve violet ink clones appear all over the map and every enemy goes for the nearest one at once; each soaks 100 damage, bursts for 60 when killed, and stays until it dies or the round ends; capstone twenty clones |
| **Healer** | **Medicine Darts** (fast needles that dose what they hit: stacking poison on enemies, stacking healing over time on allies), **Spirit Fan** (alternates blowing enemies away and drawing them in; a short gust that heals allies in it and blows small enemy bullets back), **Ofuda** (charms lie at your cursor as wards that burst under enemies, healing allies near; slow homing paper charms that burst for 25 and heal allies near the burst for 8) | **Healing Dash** (sprint 70% faster for 3 s, leaving glowing footprints that last 6 s and heal 12 HP a second, you and your allies, while you stand near one; overlapping footprints don't stack; capstone 4.5 s sprint; Morning Dew makes them heal 50% more and 25% bigger), **Substitution** (swap places with the ally nearest your cursor and shield them for 60; alone, slip toward your cursor and shield yourself), **Spirit Ward** (a talisman at your cursor for 8 s that takes the damage allies near it would take, up to 150; when it breaks or ends, a quarter of what it took heals them) | **Blossom Storm** (80 s): for 5 s the whole party heals 20 HP/s and enemy bullets near every ally are erased |
| **DPS** | **Shuriken** (tight fans of 3 stars that stick in what they hit and come back after 5 s for another hit; press the weapon-action key to recall them sooner), **Katana** (two stance combos, switched with the weapon-action key; the one you leave keeps its place: **Hayate** dashes and slices and ends in a **Momentum Burst** that shoves enemies away and marks them, **Senbon** sweeps, spins and slashes and ends in a storm of **caltrops**; the Burst flings caltrops on the ground, and caltrops thrown after a Burst seek what it marked), **Kunai** (every throw leaves a mark: press the weapon-action key to blink there for a double hit; slow, heavy knives, +50% damage to enemies that aren't targeting you) | **Shadow Step** (teleport behind the enemy nearest your cursor, up to 450 px away, and strike it for 80), **Vanish** (for 3 s enemies lose track of you and shoot at the smoke bank where you vanished; your next hit deals double), **Blade Fan** (12 kunai spin around you for 4 s, cutting enemies they touch for 18, at most once every 0.5 s each, and erasing enemy bullets; press it again, or wait, and they fly out in a ring, 18 each; 12 s cooldown) | **Death Lotus** (65 s): strike up to 8 enemies near your cursor in a flurry of cuts, 100 each (the arena darkens, each target takes three white-hot crimson cuts and leaves a crimson lotus stain, and the cuts show one after another over about a second) |

### The Hemomancer

Health is the Hemomancer's resource. It has full health and normal speed, and:

- **Every Space ability costs health**, a share of your max health shown on the card (Blood Gift: of your
  current health). A cost can't take you below 1 HP: at 1 HP the ability still goes off, but anything that
  scales with what you paid is worth nothing. Cooldowns are shorter than other classes' to make up for it.
- **Blood Price:** you deal more damage the more health you're missing, up to **+40%** at 10% health.
- **All your hits heal you for 3% of the damage they deal.**

| | Weapons | Space abilities (cost) | Ultimate |
|---|---|---|---|
| **Tank** | **Blood Lash** (a 130 px, 110° whip of blood, 24: its tip cracks for double and taunts, closer hits are flung out to the tip; hits build Clot, 4% less damage taken each, and at full Clot the next lash bursts all round you), **Clot Cannon** (hold to charge, let go to lob a massive clot over enemies: 160 px on a tap, out to 520 px at full charge, the arc growing as you hold; each shot costs 6% of your max health; it splashes 34 to everything within 70 px of where it lands, the same at any range, and leaves a 130 px pool slowing enemies 40% and healing anyone in it 4 a second for 4 s; after 1 s it congeals, sticking enemies in it for 1 s; you drip blood as you walk and the clot rains drops as it flies), **Leech Tendrils** (hold fire: blood tendrils grasp up to 3 enemies in a 70° arc in front of you for as long as you hold on; they're stunned and drained slowly, and 25% of the drain heals you; spare tendrils pile onto what's held, so a lone boss takes all three) | **Blood Shell** (toggle: press to raise, press again to drop; a big bubble of blood, 96 px out, round you. It stops every enemy bullet, red aimed shots included, and enemies can't walk in; you and your allies can stand inside. Each bullet it stops costs you 7 health and heals every ally inside for 4. It also bleeds you 1.5 health a second while it stands (bullets cost far more). It stands until you press again or your health falls to a third, when it bursts and flings nearby enemies back; the 9 s cooldown starts then. Hardened Blood makes each hit cost 25% less and heal 50% more), **Blood Wall** (toggle, no upfront cost, 8 s, was Crimson Pact: press and a wall of blood is laid behind you wherever you walk, two braided veins bristling with thorns; standing still lays nothing. It stops every enemy bullet, red aimed shots included, and enemies can't walk through it: they're pushed back and bleed 12 a second while pressed against it. You and your allies walk straight through, and your shots fly over it. Each stretch stands 6 s, then crumbles. It drains 1% of your max health a second, doubling every 4 s (2% at 4 s, 4% at 8 s, 8% at 12 s, 16% at 16 s); the drain is shown over your head and in a ring round you that heats through Seep, Flow, Gush and Hemorrhage. Healing still lands, so a healer stretches it (alone it takes a full-health tank down in about 17 s), but nothing outheals the ramp. Press again to stop, or it ends when you go down; the wall already laid stands out its time, and the cooldown starts when it ends. Blood Oath: the wall stands 2 s longer and the drain doubles 1 s slower), **Exsanguinate** (10%: barbed blood whips crack out and hook every enemy within 220 px; the hook tears their own veins out toward you, the two braid, and at 0.6 s one hard yank throws them in to touching you, stunned until they land. Each drains 20 into you as it lands. Heavy enemies and bosses aren't moved, but are still drained) | **Vein Tithe** (75 s, was Blood Rite): a blood artery grows from you to every enemy on screen and holds it for 15 s, so it can't move or shoot (enemies still spawning, and any that turn up later, aren't held). You pay 0.4% of your max health a second for each enemy held (a boss costs four times that), move at 65% speed and deal 40% less damage; the drain gives no hit grace and can take you down. It ends at 15 s, or when you go down, and every artery lets go. **Or Bloodbath** (75 s, key 1 of the lobby's Ultimate row): a channel. Nothing up front: blood floods the floor 170 px round where you cast, spreading out over 0.9 s (it doesn't follow you, and you can move and shoot), and while it runs it drains 4% of your max health a second (no hit grace). It runs up to 10 s; press it again to stop sooner (no cooldown spent), and it also stops when the drain brings you to 15% health, when you go down, or between waves. Once it stops the pool does nothing and dries up over 1.2 s. Enemies wading in it move 50% slower (Heavy enemies 35%, bosses 20%) and bleed 15 a second, bosses too, scaled by your damage and Blood Price; the bleed stops the moment they step out. Every bleed tick heals you 25% of its damage wherever you are (instead of the usual 3%, not on top of it). Allies standing in it, you included, take 20% less damage. With about four enemies wading in it, the heal pays for the drain |
| **Healer** | **Transfusion Darts** (darts that give a hurt ally 20 health for 4 of yours; each enemy hit steals a sliver, 0.5), **Sanguine Orb** (orbs that home in at the last moment and circle an enemy to cover it in blood, 16 plus a bleed stack, charging a heal reserve; with 25 reserve, an orb that touches a hurt ally coats them in healing blood, 30 over 4 s), **Vein Tether** (a real beam: it hurts whatever is on your aim line, so keep it on your target; 40% of its damage heals, split between you and the nearest ally) | **Blood Gift** (25% of current health: the most hurt ally heals twice what you paid; alone, a shield for you), **Blood Freeze** (25%, 14 s: a crimson shockwave bursts out 260 px around you after a 0.55 s charge and freezes every enemy it reaches for 5 s, bosses for 3 s; frozen enemies can't move or shoot; it does no damage and allies are untouched), **Martyr** (40%: revive the downed ally nearest your cursor, anywhere, at 50%; if no one is down, the most hurt ally heals twice what you paid) | **Sanguine Rain** (80 s): the Hemomancer pulls 12% of their health into a blood orb (1.8 s), then rain circles follow every ally for 10 s, healing each 3% of their max health every 0.5 s; it drains the caster 3% a second and ends when pressed again, when the time is up, or at 15% health|
| **DPS** | **Blood Bolts** (fast bolts, 9; the fifth in a row in one enemy makes it hemorrhage, bursting for 36 within 80 px), **Implosion** (click an enemy: its blood collapses inward, 34, hurting enemies against it for 15 and pulling them in; fires only with your cursor on an enemy, up to 480 px away), **Bone Spear** (stays stuck where it stops: walk over it for its 2 health back; a heavy spear, 60, that pierces 3; each throw costs 2 health) | **Arterial Spray** (a toggle, no upfront cost, 8 s: press to open a vein and a 60° jet of blood, 200 px long, follows your aim, 30 damage a second to everything in it (Blood Price makes it hit harder as you bleed), slowing it 20%, making it bleed and pushing small enemies back; it drains 4% of your max health a second; press again to stop, or it sputters out at 15% health; the cooldown starts when it stops), **Blood Debt** (16 s: your health drops to 25% (or it costs 5% if you're already that low) and the rest becomes a debt for 8 s: a kill pays back 4% of your max health (a Heavy, an elite or a boss 10%), hurting a boss 10% of the damage, and your hits heal 15% instead of 3%; pay it all and the 15% lasts 2 s more; whatever is still owed after 8 s is lost), **Vein Rush** (10%, 6 s: rush 280 px toward your cursor as a streak of blood, untouchable; every enemy you pass through takes 25 and bleeds, and gives back 2% of your max health; you leave a trail of blood that slows enemies 30% for 2 s) | **Crimson Harvest** (65 s): blood is torn out of every enemy in the arena (20 damage each, over about 1 s, and your hits heal you as usual) into a ball floating over you; each Grunt adds 1 blood, a Heavy 3, a boss 8, and the ball's area follows the blood. You move at 60% speed while it's up. Press the ability again to throw it at your cursor (up to 900 px; it throws itself after 5 s): it lands for 60 + 12 per blood at the core, falling to half at the edge of a 150 + 50·√blood px blast, flinging enemies (not bosses) and slowing them 35% for 4.5 s. The Blood Price cap is the normal +40% |

Capstones (level 5): Blood Shell also erases bullets within 160 px · Blood Wall: enemies against the wall take
double and are slowed 40% · Exsanguinate also slows what it drains by 50% for 2 s · Blood Gift also heals others within 200 px of
them for half · Blood Freeze: frozen enemies take 25% more damage · Martyr: you're invulnerable for 2 s after · Arterial Spray also
washes away enemy bullets it touches · Blood Debt: kills repay 50% more · Vein Rush's trail bursts after 1 s for 20 · Vein Tithe lasts 20 s and costs 25% less · Bloodbath spreads to 220 px and gives back 35% ·
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
| Tank · Soldier | **Scattergun** | Short-range spread of 5 pellets (7); within 120 px each does double and shoves the enemy back. **Double Barrel** (Alt): 8 shells (red shells behind you, the next one ringed by its progress) that load back over time, one every 1.5 s. Each Alt press fires one: 9 pellets at 1.5× (still double up close) in a wide cone, and the recoil kicks you 75 px back (no invulnerability), so it works as a movement ability. Switching weapons refills it | +2 pellets and a 15% tighter cone |
| | **Flak Cannon** | Shells fly over enemies to your cursor and burst in the air (30 within 80 px), destroying enemy bullets in the burst. **Proximity Shell** (Alt, 5 s): lobs a finned shell to your cursor (as far as the cannon reaches) that arms in 0.55 s and lies there 15 s, blinking amber; an enemy within 40 px sets it off: a flak burst of 45 in 95 px that also destroys enemy bullets. Three at most (the oldest goes). A ring under your feet fills as it comes back | The burst scatters 2 bomblets that pop where they land |
| | **Riot Repeater** | Steady single shots (11) with a little spread; each round that hits suppresses the enemy: it shoots 8% slower, up to 50% (25% for bosses), fading 25% a second. **Tear Gas** (Alt, 9 s): lobs a canister to your cursor; after 0.5 s it bursts into a pale cloud 100 px round for 5.5 s. Enemies in it are suppressed to the cap at once (50%, bosses 25%) and slowed 30%. A ring under your feet fills as it comes back | Fires in bursts: +1 round per burst, then a longer pause |
| Tank · Elementalist | **Flamethrower** | A short stream of fire (about 190 px): 2 flames every 0.06 s that hit the first enemy for 3 and carry on through the rest for 1, setting them burning (5 a second for 3 s, renewed by every hit) | +1 flame a burst and 25% more reach |
| | **Frost Lance** | A 230 px icicle juts out from you (18), then splinters off its sides into enemies up to 60 px beside it (50%); both slow 30% | +30 px icicle and +25% damage, splinters fly 15 px further |
| | **Storm Hammer** | You haul a big steel hammer over your shoulder and throw it (it leaves your hand 0.22 s after the click): a 48 px hammer (40) tumbling end over end behind a storm cloud, with a bolt from the sky where it lands. It electrifies the enemy it hits: it arcs to 3 enemies round it three times over a second (5 each). **Alt (weapon action, 6 s cooldown): Thunder Slam**: you raise the hammer and drive it into the floor 52 px ahead (0.58 s), and a shockwave of lightning runs out 280 px, hurting every enemy it crosses once (40), shoving it 40 px (not bosses or heavies) and electrifying it (one arc). The hammer re-forms in your hand 0.2 s after the throw is ready, or after the slam. With Visual effects off the old small hammer is drawn | +15% hammer damage and 1 more enemy arced to (the slam hits 15% harder too) |
| Tank · Summoner | **Golem Staff** | Hold to charge, let go to lob a boulder over enemies: 140 px and 20 for a tap, 460 px and 40 at full; crushes within 75 px; plus a small Titan-like Golem (rises from rubble, wind-up slam, soaks about 400 bullet damage) | +1 Golem (up to 3) |
| | **Bone Guard** | Darts (7) that stick: the third in one enemy within 3 s cages it for 1.5 s (not bosses); 2 orbiting skeletons block bullets and shoot caged enemies | +1 Skeleton Guard |
| | **Bear Totem** | Melee: two quick claw swipes in front of you (85 px, 11 damage each), one claw on each hand (the left rakes across, then the right rakes back), that make enemies bleed (30% of the hit per second for 3 s, stacking up to five hits; each two hits leave another set of claw scars on the enemy), plus a Bear | +15 px reach, 25% more bleed, and the Bear's bites hit enemies around its target |
| Healer · Soldier | **Repulsion Cannon** | Blasts enemies away in a 70° cone (240 px, 6 damage, 90 px push; bosses and heavies don't budge); enemies it hits shed tiny health orbs (heal 6, at most every 1.5 s each). **Undertow** (Alt): every blast that hits fills a meter (shown as a violet arc behind you), more per enemy the more it hits at once (1 enemy: 5, 2: 13, 3: 24, 4: 38, 5: 55, 6: 75, out of 100; no more past 8). Hold Alt (needs 10, the notch on the arc) to pull: a 60° current 300 px long drags enemies in it toward you at 140 px/s, stopping them just in front of you, slows them 40% and stings for 4 a second, while the meter drains 35 a second. You can't fire and move at 75% while pulling; let go (or run dry) and the next blast hits the crowd you gathered, filling the meter back up. Bosses and heavies don't budge. The pull has its own sound: an inhale as it starts, a swirling hum while you hold, a soft pop as it lets go. The meter empties if you change weapons | +20° wider, +2 damage and +30 px of push; the Undertow is 20° wider and pulls 30 px/s faster |
| | **Revolver** | Six hard-hitting rounds, the last one heavy (double damage). **Tap** for perfectly aimed shots (one every 0.7 s, fast flying); **hold** to fan the hammer: after 0.2 s it empties the cylinder, a round every 0.07 s, each within 5° of your aim. Reloads in 1.2 s when empty, and slides rounds back in one at a time (every 0.25 s) once you stop shooting for 0.8 s | +2 rounds in the cylinder, loaded as heavy rounds |
| | **Mender's Bow** | **Hold to draw, release to fire** (you move at 85% while drawing). A tap looses a weak arrow (8 damage, heals 5); a full draw (0.9 s) hits for 40, flies at 950 px/s, pierces 2 and heals an ally it passes for 20. Let go within 0.25 s of full draw for a **perfect shot**: +25% (50 damage, heals 25). Holding longer keeps it at full; it never fires by itself | +1 arrow nocked: a full draw also looses an extra arrow (half damage) in a tight fan; taps stay single |
| Healer · Elementalist | **Radiant Orb** | Hold fire to cast an orb of light (20 px) out to your cursor, up to 520 px away, and keep it there while you hold: drag it through enemies (8 damage to each it touches, 5 times a second) and over allies (4 healing, 5 times a second); let go and it fades | +1 Light Orb (Barrage adds one too) that circles your orb while it's out (you otherwise), hurting enemies and healing allies it touches |
| | **Chain Light** | A 320 px cone of light (about 70° wide; Magnum Rounds widen it up to 100°, Homing Rounds narrow it 15% and strengthen it 10% a stack): 40 damage shared between every enemy in it (one alone takes it all), light arcing to each. Barrage adds cones edge to edge (60% damage each, like other weapons' Barrage copies), all within 200°: never a ring round you | +25% damage and a 15% longer cone |
| | **Nimbus** | A small storm cloud floats over your cursor, drifting after it at 600 px/s and staying within 380 px of you. Click to call lightning onto the spot beneath it (16 to each enemy in a 40 px blast, every 0.6 s). Rain falls in a 70 px circle under it and heals every ally standing in it, 8 a second | +1 lightning strike a click, landing in a ring round the cloud |
| Healer · Summoner | **Sprite Staff** | A firefly (7) that seeds the enemy it hits, then hops to the nearest enemy that isn't seeded yet (up to 2 hops within 300 px, so one shot can seed three), plus 2 healing sprites; killing a seeded enemy frees another sprite for 6 s (up to 4 more) | +1 Sprite |
| | **Treant Rod** | A thorny root whips out from you along your aim, growing from a knot of rootlets that curl round your feet (250 px, 15 damage) and snaps back, hitting everything on the line and healing allies it touches (10); plus a Treant that roots where it stands and blocks bullets; a root from you to it heals allies crossing it (8/s) and slows and stings enemies crossing it (50%, 6/s); it walks after you past 360 px; the Treant also flicks tiny thorn-dart seeds at the nearest enemy within 320 px (3 damage every 0.4 s, about 7.5 a second, your damage and fire rate apply; none while it is staggered) | The Treant grows 30%: blocks more, hits harder, reaches farther (its seeds fly 30% farther) |
| | **Spirit Chalk** | Hold and drag to draw a line of spirit ink (the chalk follows your cursor at up to 700 px/s, within 450 px of you, and the line is drawn as one smooth curve for everyone, head and all; 600 px of line at a time: **draw more and the start of the line rubs out as you go**, so you never run dry; a dab also fades slowly over 10 s). It burns enemies touching it (26 a second) and slows them 30%, and heals allies standing on it (10 a second). Enemy bullets pass through plain ink; the rare card **Warding Ink** makes it destroy bullets that cross it, but each bullet wears the dot it hit down by 1.5 s. **Close a loop** (the line meets itself, round at least about 60 x 60 px) and every enemy inside is **trapped** for 3.5 s: it can't cross the ring, and the ring glows gold. Bosses and mini-bosses can't be trapped. A hole shot through the ring (or the ring fading) lets them go | +20% ink: a longer line before the start rubs out |
| DPS · Soldier | **Twin Rifle** | Twin streams (4 per barrel) from two rifles held out wide, each stream starting at its rifle's muzzle (34 px ahead of your middle; pressed against a wall, at your edge), the barrels sharing a 60 px width (extra barrels fill it in, they don't widen it; Barrage's copies fire from a narrow 20 px cluster), angled in to cross at your cursor's distance (120 px to full range, about 575 px); hits within 45 px of the crossing are crossfire, for 1.5×. Up close the streams are still apart, so they can pass either side of a small enemy: aim at the crossing. Your aim line marks it with an X. **Grenade tubes** (Alt): a launcher under each rifle. Every 60 hits loads a grenade (a crossfire hit counts twice), two at most; they show as olive tubes on the guns and two diamonds behind you (the next one to load ringed by its progress, the next to fire lit). Alt fires one grenade a press (0.25 s apart), and the tubes take turns: with two loaded it fires the left, then the right, and that order holds even if you reload the left in between. The grenade flies down its rifle's stream to the crossing and bursts for 60 in 110 px with 8 shards. The tubes empty if you change weapons | +1 barrel |
| | **Railgun** | Slow shots (45) that pierce 3; the path stays charged for 1 s behind the round, hurting anything in it (25 a second) | Throws 2 shrapnel shards sideways at every enemy it pierces |
| | **Burst SMG** | Very fast spray (5) that spins up as you keep firing: over 2 s the spray tightens from 9° to 2° either side and damage climbs to +60%; it winds down in 0.6 s when you stop | +1 bullet per shot (half damage) and a wider spray |
| | **Double Barrel** (the 4th weapon key) | A side-by-side shotgun. Each click fires one barrel (left, then right, 0.32 s apart): 9 pellets of 7 in a tight cone from your barrel, each passing through 2 enemies. Damage is full out to 140 px and falls to 35% at 400 px, so it is a close-range weapon, and the tracers dim as they fall off. **Two shells, then a break-open reload** (0.86 s, sped up by fire rate): the barrels tip open, the hulls flip out, two new shells go in and it snaps shut; it can't fire while open. **Alt** fires both barrels at once when both are loaded: 18 pellets in a wider cone, then the reload. Each shot kicks the gun back and the barrel that fired glows and smokes for about 2 s | +4 pellets a barrel |
| | **Twin Rifle Auto** (the Twin Rifle's variant: open its Variants window in the lobby and Equip it) | The Twin Rifle with the trigger taken away. Both rifles are held out to your sides and turn on their own: each aims at the nearest enemy on its side of you (left of your facing, right of your facing; a side with nobody on it borrows the other gun's target) and fires at it (4 a round, every 0.125 s) with a clear line, out to about 380 px (the Twin Rifle reaches about 530). A thin laser from each muzzle shows its range and what it is on: it ends in a cap where the range ends and a dot on the first enemy in its way. You can't shoot, and there are no grenades. **Alt** locks both rifles onto the enemy nearest your cursor (within 260 px of it) until it dies or you press Alt again: both streams then meet on it, so every round is crossfire (1.5×). A red reticle turns round the locked enemy and a thin tether runs from each muzzle. Same guns, shots, brass and heat as the Twin Rifle. Unlocked for testing | +1 round per gun, fanned a little |
| | **Out of Ammo** (the Twin Rifle's second variant: open its Variants window in the lobby and Equip it) | Two sets of arms. The **rifle arms** out at your sides work on their own and don't shoot: each swats the nearest enemy bullet within 72 px out of the air (one each, then a half-second recovery, so two at a time; the red unblockable shots still hit), and with an enemy within 78 px in front of you (about 57° either side of your facing) they bash in turn: left (10), right (10), left (10), then a **rifle clap** (35 to everything in front, a shockwave and a hard shove), and round again. **Your own arms** hold a **knife**, by hand: hold fire for a three-hit chain, cross cut (12), reverse cut (12), lunging stab (20, a step in and a small shove), as extra damage on top of the rifles. **Alt: spin** for 1.2 s: you cut everything within 88 px every 0.2 s for 14 and shove it out, and every enemy bullet that touches the spin is swatted with no limit; 6 s cooldown. Hits throw sparks and a flash, the blade and the rifles leave ribbons, the stab leaves afterimages and the clap cracks the floor. Unlocked for testing | +50% damage on every strike and spin tick |
| DPS · Elementalist | **Fireball** | A big, slow fireball that rolls through every enemy in its path (32 each), setting them burning (8 a second for 3 s) | +1 fireball, fanned out |
| | **Arc Lightning** | Hold to charge a slight cone of lightning in front of you (36 px wide at your staff, 120 px at its far end), 160 px long for a tap out to half the arena at full; let go and everything in it is shocked (40, 0.4 s stun) | +25% damage |
| | **Ice Shards** | Each cast puts 3 shards of ice (7) into orbit round you (up to 6); they launch, homing, at enemies within 300 px, one every 0.08 s. With the orbit full, a cast flies at your aim | +1 shard a cast, +2 in orbit, and faster launches |
| DPS · Summoner | **Wolf Whistle** | Darts mark the enemy they hit as prey for 4 s and your 2 wolves hunt it (a fanned or piercing dart that clips an enemy farther from your cursor doesn't move the prey); every wolf biting it within a second is a takedown (20 per wolf, 0.6 s stun) | +1 Wolf |
| | **Wisp Lantern** | 3 wisps orbit you and are your shots: each shot sends one at the enemy nearest your cursor (22), and it comes back | +1 Wisp, and each shot sends one more |
| | **Imp Caller** | An imp every second (up to 5) waits by you; tap fire to send one charging at your cursor (45), or hold to merge them all into one big imp (blast 125% of theirs together, and wider) | +1 Imp, arriving 50% faster |
| Tank · Knight | **Warhammer** | Charge slam: hold to charge (0.9 s), let go to slam a fixed 160 px circle centred about 100 px ahead (it clears small bullets too); 20 damage and a 0.25 s stun for a tap, up to 80 damage and a 0.9 s stun at full charge | +1 aftershock: another slam 0.2 s later, 40 px further out (60% damage) |
| | **Lance** | The **Breach**, held in the right hand (the Winged War Lance: an ash shaft, a steel leaf head and a blue swallow-tail pennant) with a heater shield on the left arm. A four-step chain, each step moving you a little forward: **Jab** (14, a 178 × 32 px box straight ahead, a 30 px shove), **Second Jab** (the same, a touch wider), **Wide Sweep** (16, the shaft swung flat across 150° in front of you, batting each enemy it reaches to your left, 85 px), **Breaching Thrust** (a 0.2 s wind-up and a 34 px step in, then 40 to everything in a 205 × 88 px box straight ahead, shoved 160 px back and stunned for 1 s; not bosses, and heavy enemies aren't shoved). Hold the button and it plays on by itself; a press counts once a step is 80% through (90% for the Thrust), and the chain starts over 0.7 s after a step ends without one (one whole chain is 84 damage in about 1.3 s). **Impact:** an enemy one of these shoves is still sliding fast (34 px or more of the push left) that meets another enemy: both take 10 and the one it hits slides on with 70% of the push, so one shove can bowl a line; into the arena wall it takes 18 and is stunned 0.8 s (heavy enemies and bosses take nothing and aren't moved). Impact damage shows in orange with an IMPACT or WALL tag. **Hook & Haul** (weapon-action key, 4 s cooldown): the lance shoots out through your glove to the enemy nearest your cursor (up to 260 px), hooks it for 10, stuns it 0.8 s and hauls it to 62 px in front of you (heavy enemies and bosses stay put), and your next press is the Breaching Thrust. Four diamonds over your head show the chain; the fourth glows when the Thrust is next or a hook has primed it, and a ring under your feet fills as the hook comes back. The Lance has its own sounds (a shaft whoosh for the jabs, a wide swoosh with the pennant fluttering for the sweep, a breath and a driving thrust for the Breaching Thrust, dull steel hits, a crunch for an Impact and cracking stone for a wall, the hook rattling out and catching), and your own hits hold the picture for a moment and shake it, the Thrust most | The Breaching Thrust: +30 px reach, 24 px wider and +60 px knockback |
| | **Halberd** | Sweep: held in two hands, drawn back for 0.05 s, then an 18° blade, 200 px long, swept across 80° in 0.21 s (the first cut lands about 0.15 s after the click, the last at 0.26 s); 30 to each enemy it passes, once a sweep | +30 px reach and a return sweep (60% damage; the head turns over between the two) |
| Healer · Knight | **Quarterstaff** | Alternating left and right 130° swipes (120 px, 18) that knock enemies back 70 px; each swipe parries: enemy bullets in its arc fly back at enemies as your shots (12), big orbs too, but never red aimed shots; each enemy hit heals you and allies within 220 px for 2 (up to 8 a swipe) and adds a Jab stack (3 at most, they never fade). The weapon-action key (Left Alt, held to repeat every 0.32 s) spends one stack on a jab: a 48° cone 170 px out, 30 damage, shoves enemies back 110 px, bats bullets in the cone, heals for 3 per enemy (up to 3 enemies). Held at the ready in both hands, like the Halberd | +1 Jab stack limit |
| | **Lightbringer** | A three-step chain with a longsword, each step moving you a little forward: **Dawn Cut** (a 100° rising cut, 17, plus a crescent of light, 12, pierces 2, that heals the first hurt ally it passes for 6), **Mercy Cut** (the backhand return cut, 17, whose crescent puts a 12 HP ward on the first ally it passes for 3 s), **Noon Sunburst** (the sword planted point-first: 22 to everything within 140 px, shoving it back, and every ally in the ring heals 10, you 5). A press counts once a step is 85% through; the chain starts over 0.8 s after a step ends. Every enemy a cut hits adds a light pip (the Sunburst two), up to 12. Full, the next Noon is **Radiant**: a 220 px ring that heals three times as much (30), wards everyone in it and spends the gauge. **Sunlance** (weapon-action key, 1.5 s): with 4 pips or more, a lance of light to the ally nearest your cursor (up to 450 px): it heals them 4 a pip (a downed ally gets a quarter of their revive instead), hits every enemy on the line for 10 + 2 a pip and spends all the light | +1 crescent on Dawn and Mercy, fanned out, and +30 px on the Sunburst |
| | **Censer** | The Thurible Rite, a censer on a chain held in both hands: **Sweep** (200° in front, 13, 85 px) and **Return Sweep** (back the other way) each heal allies within 140 px for 3 and leave a puff of incense where the head ends (42 px, 2.5 s); the **Benediction** lets the chain out for a full turn (18, 115 px), heals allies within 170 px for 6 +1 per enemy it hit (up to +4) and leaves an incense cloud at your feet (75 px, 4 s); the rite starts over 0.6 s after you stop. Each enemy a step hits lights an **Ember** (up to 5, they never fade). **Censer Cast** (weapon-action key, needs an Ember, 6 s cooldown): the head is thrown to your cursor (up to 280 px), bursts for 8 per Ember within 50 px, and leaves an incense cloud for 4 s (60 px +6 per Ember) healing 2 +1 per Ember HP/s; it spends every Ember. A downed ally in the cloud gets a fifth of their revive the moment it lands and is revived on top of any teammate's help while it lasts (twice as fast). Incense heals allies in it (4/s) and enemy shots fired inside it stray; walking leaves none. The Censer has its own sounds: smoky whooshes with a rattling chain, a bronze clonk and crackling embers on a hit, a soft chime as a heal reaches an ally, a church bell for the Benediction, the chain paying out for the cast and a gong and hiss of incense where it lands | +15 px reach, +1 HP on every heal and +1 Ember |
| DPS · Knight | **Twin Blades** | The **Crescent Waltz**, a sabre in each hand. A five-step chain, each step moving you a little forward: **Rising Cut** (16, a 110° cut, right hand), **Falling Cut** (16, the left hand back), **Double Jab** (two 115 px thrusts, 12 each), **Pirouette** (a reverse turn, 13 to everything within 80 px twice; cuts small bullets), **Crescent Shear** (44, both blades raised behind and swept 200° across, shoving 18 px, and a piercing crescent wave for 30). Hold the button and it plays on by itself; a press counts once a step is most of the way through, and the chain starts over 0.7 s after a step ends without one (one whole chain is 156 damage in about 1.5 s). Five diamonds over your head show the chain; the fifth glows when the Shear is next. **Twin Fang** (weapon-action key, 4 s cooldown): both sabres are thrown out on mirrored curves to your cursor (60 to 210 px), cross there in a burst (25 within 55 px) and fly back the other way, cutting what they pass for 18 each way per sabre and small bullets too. You can't swing until you catch them (0.95 s). A ring under your feet fills as it comes back. The Twin Blades have their own sounds (light ringing cuts, a whirl for the Pirouette, a tearing Shear, a spinning throw, a clash where the sabres cross, the catch), and your own hits hold the picture for a moment and shake it, sized to the move: a short hold on each cut, a kick on the Pirouette, the longest on the Shear and Twin Fang's burst (off with Reduced effects; the shake off with Screen shake off) | +1 crescent: the Pirouette and the Crescent Shear each launch one more wave, fanned out |
| | **Greatsword** | A three-hit chain with 125 px reach: a 150° cut (40), a 150° return cut (40), then an overhead rend (90, 200°) that winds up for 0.25 s first and shoves what it hits 40 px; the chain starts over after 0.9 s without a swing. Each sweep's edge hangs as a crescent that stops enemy bullets (0.35 s for the cuts, 0.9 s for the rend). **Hold the weapon-action key (Left Alt) to guard**: a 120° arc 72 px out stops enemy bullets (not red aimed shots), you move at half speed and can't swing, and each bullet it stops adds a point of Temper (up to 6). Let go and a piercing beam fires along your aim for 30 + 15 per point (120 at six); six points also start the next chain on the overhead rend. The guard can't go back up for 0.6 s after a Rend. Held at the ready in both hands, like the Halberd | +1 sword wave: the overhead rend launches a piercing crescent (50% damage) |
| | **Rapier** | Narrow 130 px thrusts (18, always the same) at the nearest enemy in front, each skewering up to 3 enemy bullets along its line (not red aimed shots). **Tempo:** every enemy a thrust hits adds a stack, up to 20 (skewered bullets add none). The wait between thrusts goes from 0.48 s at 0 Tempo to 0.16 s at 20, evenly between (fire-rate upgrades speed it up further, never under 0.08 s); from 0 to full takes about 6.5 s of hits on one enemy. Tempo holds for 1.2 s after your last hit, then drains 2 stacks a second. **Riposte:** press the weapon-action key to stand guard (press again to drop it): you move at 40% speed, no thrusts, Tempo stops draining, and enemy bullets in a 140° arc in front of you are sent back at the enemy as your own (12 damage; not red aimed shots). Each reflected bullet adds 10% charge (a full charge from 10 bullets); the charge also creeps up on its own, very slowly (4% a second). Click to lunge along your aim, untouchable: the reach runs from 130 px to 720 px with the charge, and every enemy on the line takes 20 to 70 damage and adds a stack of Tempo. A click with almost no charge just drops the stance. A violet arc shows the guard, a gold-turning inner arc the charge. (The old Flourish is still in the code but no longer on the key.) Diamonds on an arc behind you, one per stack, fill as Tempo builds and all turn gold at 20; a thin arc under them runs down through the grace period, and the top diamond shows orange while Tempo drains. Held at the ready, point forward, like the Halberd | Skewers 1 more enemy behind the first |
| | **Arc Knuckles** (the 4th weapon key) | Coiled blue gauntlets (the Arc Coil look) in a five-punch chain: **Jab** (22, a 70 px thrust), **Cross** (26, 80 px), **Hook** (29, a 120° sweep, 72 px, knocks back 8 px), **Uppercut** (33, a 70 px thrust that launches: stunned 0.55 s, not bosses), **Twin Hammer** (55, both fists slam a 110 px shockwave centred 44 px ahead: knocks back 38 px, stuns 0.6 s, clears small bullets). Each press can come 0.24-0.38 s after the last (0.62 s after the hammer); 1.2 s without a punch starts the chain over. Raising the parry cancels a punch still winding up and starts the chain over from the Jab; a **perfect parry** arms the **Counter Hammer** (pips turn gold; it fades after 3 s unused): your next press is a gold Twin Hammer from any step (70, 135 px, knocks back 48 px, stuns 0.8 s). Five diamonds over your head show the chain. The weapon-action key is the **Rocket Dash** (3 s cooldown): it **locks onto the enemy nearest your cursor** within 300 px (a faint reticle shows which while it's ready) and the lead gauntlet's thruster throws you up to it (up to 280 px, 650 px a second), fist first and untouchable; that enemy takes 44 and a short 24 px shove that keeps it in reach, even with another in the way. With nothing in reach it flies 130 px along your aim and hits the first enemy in the lane. A ring under your feet fills as it comes back. **The dash chain:** when the dash hits, a pulsing ring and chevrons say Fire now; the chain's moves stay locked onto the dashed enemy (a bright reticle marks it): press within 0.35 s for the **Flurry** where you landed, aimed at it (eight alternating jabs, 11 each, then a straight, 37, 12 px shove; you're rooted), within 0.35 s of the straight for **Grab & Throw** (the Flurry's target, if it's within 110 px and not a boss: swung round you once and thrown along your aim at 520 px/s, up to 260 px; it takes 37 and the enemy it hits 48, a 42 px shove and a 0.5 s stun; you're rooted), and within 0.35 s of it landing for the **Rocket Punch** (the fist fires off on its chain at the thrown enemy, down a line 36 px wide that runs 80 px past it, 210-300 px long: 51 to everything on it, 18 px shove, then reels back). A missed window, a boss, or raising the parry ends the chain. Every punch that connects has its own knock, blue sparks and a ring, and on your screen the picture holds for a moment and shakes, sized to the punch (a Flurry jab barely; the hammers, the dash and a thrown enemy landing the most; off with Reduced effects, the shake off with Screen shake off) | +1 aftershock: the Twin Hammer slams again 0.2 s later, 40 px further out (60% damage) |
| Tank · Ninja | **Kusarigama** | Press once to cut with the sickle (16, a 144 degree arc), again within a second to throw the weight 280 px at the first enemy in its lane (34, stuns 0.4 s and drags it to you). Hold the weapon-action key to whirl: the weight circles you (70 to 150 px out) for up to 3 s, pulling everything within 200 px in and cutting for 12 a hit; you move at 55% speed and can't swing. Let go for a snap (30, stuns 0.6 s, knocks back 60 px), then 3 s to cool down. Drawn in ink: a curved sickle on a wooden grip, a chain of links and a small iron weight; the cut paints a lilac brush stroke along the sickle's path that dries out in half a second, the throw drags a brush line and splashes where it bites, the whirl paints an ensō (a brush circle) behind the weight and the snap bursts into splatter. The whirl's time left is a lilac ring round the weight that empties, and the hanging weight glows lilac while your next press throws it. With Visual effects off the ink is left out and the sickle, chain and weight still show | +40 px throw and +20% damage |
| | **Bo Staff** | A three-step chain with no timer, however long you wait: a 130° cone slash (16, 105 px, knocks back 14 px), a jab (22, 140 px, 28 px wide, 22 px knockback), then a full spin (30, 100 px, 20 px knockback) that starts it over; every swing cuts small bullets. Hold the button and it plays on by itself. Three pips over your head show where you are, and the third pulses when the next click is the spin. The weapon-action key **shoves**: the staff turns sideways and drives out as a bar 100 px wide and 78 px deep (6 damage, knocks enemies straight back 70 px, not bosses or heavies), once a second, and starts the chain over (so click, Alt, click is slash, shove, slash). It is held in both hands at the ready, like the Halberd. The swings glow in the staff's sage green; the shove pulls back, drives out and sends pressure arcs off its front | +20 px reach and +15% damage on every swing and the shove, and +20 px on the shove's push |
| | **Fuma Shuriken** | A giant star (30) thrown in an oval loop 380 px out in front of you and back, cutting through everything both ways. **Orbit stance** (weapon-action key, again to go back): the star circles you at 80 px (8 a pass, 0.3 s between passes on one enemy) and slices small enemy bullets out of the air; Fire **Lashes** it 190 px out across your aim, one way then back (24), and every third press is the **Cyclone**, one lap out to 160 px (20). Raising the parry: **Reversal**, the circle flips and spins 1.5x faster for 2 s, cutting everything in its ring for 30 (1 s cooldown); a **perfect parry** keeps the shot as a small star circling you (up to 5), and 1.3 s later they fly back at whoever fired them (25 each). In the Throw stance, raising the parry while a star is out catches it into orbit from where it was | +1 shuriken, looping the other way (Orbit stance: +1 star on the circle, up to 4) |
| Healer · Ninja | **Medicine Darts** | Fast needles (7) that dose what they hit: a stack of poison on enemies, a stack of healing on hurt allies (2 a second for 3 s each, up to 5 stacks) | +1 dart, fanned out |
| | **Spirit Fan** | A short gust (6) that heals allies in it for 3 and blows small bullets back; shots alternate blowing enemies in front 60 px away and drawing them 60 px in | +25% reach and +1 HP healed |
| | **Ofuda** | Charms fly to your cursor (up to 460 px) and lie as wards (up to 5, 10 s); an enemy stepping on one sets it off: 25 within 70 px and 12 healing to allies within 110 px | +1 charm, fanned out |
| DPS · Ninja | **Shuriken** | Tight fans of 3 stars (9, 0.08 rad apart: all three hit a boss out to about 650 px) that stick in what they hit. After 5 s each rips out on its own for another hit and flies back to you, cutting what it passes (50%); press the weapon-action key to recall them sooner | +1 star in the fan |
| | **Katana** | Two basic-attack combos, one per stance. The weapon-action key (Left Alt) switches stance at once (0.3 s cooldown) and stops the step you're in; the stance you leave **keeps its place** for as long as you're away (the one you're in starts over after 1.5 s without a step), so you can store a combo one step from its finisher and call it later. Pips over your head show where each combo waits. **Hayate** (blade low and trailing, moon steel): Kiri (dash-cut 120 px, 22 to all on the line, untouchable for the dash and 0.15 s after; a dash that runs past a Riot Shield or Shielder cuts it from behind, so the shield doesn't stop it), Kaeshi (the same, angled to the other side), Tsumuji (a 70 px dash into a spin, 85 px, 26), then the **Momentum Burst**: you stab the blade into the ground and a 150 px shockwave shoves enemies 120 px, deals 8, cuts the small bullets in it and marks what it hits for 4 s. A dash that kills brings the next step at once. **Senbon** (blade forward, its edge lit rose): Agari and Kaeshi (130° sweeps, 120 px, 16), Engetsu (a step into a full-moon spin, 105 px, 20), Senbon (six crossing slashes in a cone ahead, 7 each), then **Caltrops**: 16 thrown out to 150 px round you that lie 8 s; one stepped on deals 4, slows to half speed for 1.5 s and breaks. **Together:** a Burst flings every caltrop you have lying at the nearest enemy (8 each); caltrops thrown after a Burst seek the enemies it marked (8 each). Every cut flashes a line across the enemy and a kill splits it in two; the Burst cracks the ground | +1: each dash strikes once more (60%), two more caltrops and a 25% wider Momentum Burst |
| | **Kunai** | Slow, heavy knives (42 damage, one every 0.7 s, fast flying), +50% on enemies that aren't targeting you; every throw is a shadow kunai that leaves a mark where it lands (3 s, the newest replaces the old): press the weapon-action key to blink there, and your next hit lands double | +1 knife, fanned out (half damage) |
| Tank · Hemomancer | **Blood Lash** | 110° whip (24) with 130 px reach: the tip cracks for double and taunts; hits build Clot (4% less damage taken each, up to 5); full Clot makes the next lash a burst all round you | +20 px reach and +1 max Clot |
| | **Clot Cannon** | Hold to charge, let go to lob a massive blood clot over enemies (it can't be blocked by the ones under its arc): 160 px on a tap, out to 520 px at full charge (0.9 s), shown by an arc of drops that grows as you hold. Each shot costs 6% of your max health (never below 1; extra clots come free). It splashes 34 to every enemy within 70 px of where it lands, the same at any range, and leaves a 130 px pool slowing enemies 40% and healing anyone standing in it 4 a second, for 4 s; after 1 s it congeals: enemies still in it are stuck for 1 s (not bosses). Blood drips from your hand as you walk (more while charging) and from the clot as it flies | +1 clot, fanned out, landing at the same distance |
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
s; it refills when you lower it. Raise it just as a shot arrives for a **perfect parry**: for the first 0.30 s after
raising it, blocked bullets go straight back as fast white needles (900 px/s) and cost nothing (it has to have been down
for half a second, so you can't spam it). Every character gets the same flash in its own colour: the shield's whole arc
flashes white, a ring and sparks burst where the shot met it, your own picture holds for 50 ms, and the world (enemies and
every shot, not the players) slows to 30% for 0.3 s on every screen, at most once every 0.6 s so a burst slows it once. A thin blue bar curving around the shield shows how much it has left, shrinking toward your aim as
it wears and blinking red when it's nearly gone; when it breaks it cracks and shatters, and while it's lowered a faint
bar shows it refilling. Against the Warden up close it holds for about 3 seconds of fire, enough to close the gap.

**Animation cancel:** the Fuma Shuriken has its own parry moves (Reversal, stealing shots, catching a thrown star: see its row). Raising the parry sends an attack chain back to its first step, clears the wait for the next step and cancels an attack that hasn't landed yet. Today that is the Katana (the wait for its next step is cleared; both combos' buildups reset) and the Bo Staff's slash, jab, spin (a shove still drawing back never lands and costs no cooldown). Streaks you have built (the Rapier's) are kept. The Twin Blades' chain goes back to the Rising Cut too, and a step still winding up never lands.

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

**F** fires your **ultimate**. Each role and class combination has its own default one, and some have others to
pick instead on the lobby's **Ultimate** row (so far the Tank Soldier: **Juggernaut Suit** and the Tank Elementalist: **Lightning Rod**, below the table; and the
Tank Hemomancer: **Bloodbath**, in the Hemomancer table; and the Tank Summoner: **Bone Phalanx**, below the table; a
Wildcard picks any class's DPS ultimate). Ultimates have long cooldowns
that upgrades don't shorten, and start each match half charged. The HUD shows the charge in the gold slot.

**Juggernaut Suit** (Tank Soldier, picked instead of Fortress, 75 s): six steel plates clamp onto you over 0.6 s and
for 10 s you carry **300 armour**, which takes damage before your shields and health; a segmented steel bar over your
health bar shows it draining, and a plate comes off for every sixth of it gone. You move 25% slower and every enemy,
bosses too, goes for you (the taunt, without Roar's damage cut). Each enemy bullet the armour takes throws a **flak
shard** straight back at whoever fired it: 12 damage times your damage multiplier, dealt at once, with a short amber
tracer. A shot the plates take whole gives no hit grace, so a stream of fire keeps feeding the flak. It ends when the
10 s run out, the armour is gone (the rest of that hit reaches your health), you go down, or the wave is cleared (as
Last Stand), and the plates fall off. Bots that have it put it on with four or more enemies within 260 px. Mastered:
450 armour and 18 a shard. Design: [`Docs/Mockups/juggernaut-suit.html`](Docs/Mockups/juggernaut-suit.html).

| | Soldier | Elementalist | Summoner | Knight |
|---|---|---|---|---|
| **Tank** | **Fortress** (70 s): raises a fixed, impenetrable dome (240 px radius) where you stand for 10 s; only players and player bullets cross it, enemies (bosses too) and their shots can't get in or shoot through, and anything inside is pushed out. Or pick **Juggernaut Suit** (75 s) on the lobby's Ultimate row: 10 s of 300 armour, 25% slower, a taunt, and a flak shard back at the shooter for each bullet the armour takes (above) | **Glacial Tomb** (75 s): erases every enemy bullet and freezes every enemy solid for 4 s; bosses take 250 damage instead. With Visual effects on a frost lattice spreads out from you, spires cage each frozen enemy and break when the freeze ends, and a ring of spears jabs at bosses | **Titan** (80 s): a huge stone titan (140 px across) rises from rubble over 1.4 s, then stomps after enemies for 17 s, raising its fists for 0.4 s before each slam (90 damage, 190 px, every 1.4 s, shoves enemies back; bosses and Heavy enemies aren't shoved). Its whole body blocks bullets (soaks 1000 boss damage). It crumbles when time is up. Or pick **Bone Phalanx** (75 s) on the lobby's Ultimate row: a line of shield skeletons that marches 260 px, blocking shots from the front (below) | **Last Stand** (70 s): plant yourself behind a crescent of plates that stops every shot from the front while you take 10% of it (no timer; press again to cancel) |
| **Healer** | **Lifeline** (90 s): instantly revive and fully heal the whole party, plus 3 s of invulnerability. A heartbeat pulse rolls out from you and a halo and column of light drop over each player as it reaches them | **Sanctum** (80 s): a 7 s holy dome; bullets can't get in, allies inside can't go down, and a healing pulse rolls out from you every 0.9 s (7 pulses: 20 each, the last 30; each ally heals as the ring reaches them) | **World Tree** (80 s): a 100 px tree for 15 s that heals everyone under it (18 HP/s), slows and burns enemies, and sends a wave of healing butterflies, one to each ally (the most hurt first), every 1.9 s; each butterfly flutters to its ally and heals 14 on arrival | **Hallowed Blade** (80 s): a giant sword of light falls at your cursor (200 damage) and stays planted for 8 s; allies in its ring heal 15 HP/s and enemies burn 20 a second, and every 2 s a pulse crosses the whole arena (4 pulses, the last bigger) healing every ally 10 and hurting every enemy 13 as the ring reaches them |
| **DPS** | **Artillery Assault** (60 s): 6 s of triple fire rate and +2 extra projectiles, with a heat aura, speed streaks and a muzzle flash | **Bullet Rain** (70 s): a rain barrage at your cursor (330 px across) for 6 s: a blue target zone locks on, you launch rounds skyward and they rain down into it, each with a growing dot where it will land and a ring and splash when it does; it does 45 damage a second to every enemy in it, bosses too, and leaves them **wet** (as Tsunami does: slowed, and any lightning anywhere arcs to them) for 3 s after they leave | **Legion** (70 s): a blood moon rises and tints the arena for 10 s; 4 wolves and 4 bats circle you for a second, then run 40% faster, and your other minions frenzy | **Drawbridge** (75 s): a gatehouse rises behind you and its drawbridge falls along your aim, crushing the 500 x 100 px lane (260) and throwing the edges aside (45); then for 5 s it's a walkway enemies can't step onto (the rails grind any pressed against them for 80 a second), and their shots stop at its rails |

**Lightning Rod** (Tank Elementalist, picked instead of Glacial Tomb on key 1 of the lobby's Ultimate row):

| | Ultimate |
|---|---|
| **Tank Elementalist** | **Lightning Rod** (70 s): for 6 s you crackle with charge (a blue glow and small arcs on your body): you take 80% less damage and every enemy, bosses too, goes for you. Each enemy bullet that hits you calls a bolt from the sky down on the enemy that fired it (30, by your damage), and the bolt arcs on to the 2 enemies nearest it within 320 px (15 each), drawn as jagged lines; if the shooter has died the bolt still lands where it stood and arcs from there. Melee and contact hits on you call a bolt on whoever hit you. It counts as lightning: wet enemies (Tsunami, Bullet Rain) take the usual extra arcs. Each enemy can call at most one bolt every 0.25 s (a shotgun volley calls one, not five; the other pellets are still cut by 80%). A shot that touches you during your hit grace is grounded too: used up, no damage, and its bolt still falls. A small grey number shows what each grounded hit took. It ends after 6 s, or at once if you go down |

**Bone Phalanx** (Tank Summoner, the second ultimate, 75 s): a bone gate cracks open in the floor 60 px ahead of you,
across your aim (0.5 s), and eight skeletons with bone tower shields step out of it, middle first, into a line 25 px
apart (a 200 px front). Over 6 s the line marches 260 px along the aim it was cast with, even if you turn. The shields
stop every enemy shot that reaches the line from the front, red aimed shots too; shots from behind (your team's) pass
through. Enemies in front are shoved along ahead of it, and every 0.5 s each skeleton spears what it is shoving for 15
(times your damage, faster with your fire-rate bonuses, like other minions). It walks at 45% speed while it shoves a
Heavy enemy; a boss is never shoved and stops the line, and so do walls, props and an enemy pinned against one. Each
skeleton has 120 health, worn by the shots it blocks and the enemies pressing on it, and cracks as it wears; a broken
one falls into a pile of bones and leaves a gap that shots and small enemies get through. At the end it holds 1 s and
crumbles; it crumbles early if you go down or the wave ends. Mastered: 10 skeletons with 180 health each. Bots that
have it cast it at the biggest group in front of them. With Visual effects off it is drawn plainly (a disc, a bar and
a stick per skeleton).

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
| **Poison Dart** | Spreads to 5 enemies at a time, at full strength. | **Shockwave** | Also knocks nearby enemies back 120 px. |
| **Overdrive** | +1 extra projectile while it lasts. | **Blink** | Erases enemy bullets where you leave and where you land. |
| **Nova** | Fires 48 shots instead of 24. | **Turret** | Deploys two turrets. |
| **Roar** | Also knocks enemies within 250 px back and slows them for 2 s. | **Bulldoze** | Erases enemy bullets along the charge. |
| **Adrenaline Shot** | Also gives the nearest other ally within 250 px a half-length (4 s) dose. | **Deployable Medkit** | Bursts into 9 orbs worth 20 health each. |
| **Airstrike** | A second wall lands 160 px further out, half a second later. | **Mark for Death** | Marks the three enemies nearest your cursor. |
| **Aeroaegis** | Lasts 4.5 s and reflects bullets for double damage. | **Frost Nova** | Also erases enemy bullets around you. |
| **Flame Wall** | The fire also slows enemies by 40%. | **Healing Rain** | Lasts 10 s, and downed allies inside revive twice as fast. |
| **Energy Ward** | The shield soaks 120 damage. | **Lumen Ward** | Also freezes enemies outside the light for as long as it lasts (not bosses). |
| **Meteor** | Leaves burning ground for 3 s. | **Blizzard** | Lasts 10 s. |
| **Tsunami** | Wet lasts 14 s, and the wave rolls back to you. | **Golem Wall** | Raises five golems. |
| **Earthquake** | The Burrower also freezes everything it bites for 1 s (not bosses). | **Force Pact** | A second thrall: the pact keeps the last two types you chose. |
| **Spirit Swarm** | Releases 8 sprites. | **Phoenix** | Revives at full health, and the phoenix keeps fighting. |
| **Soul Lantern** | Lasts 9 s, and the linked player can't go down. | **Bat Swarm** | Releases 14 bats. |
| **Hydra** | A fourth head. | **Alpha Call** | Also summons 3 wolves for 6 s. |
| **Fortress** | Lasts 14 s. | **Juggernaut Suit** | 450 armour, and each flak shard does 18. |
| **Glacial Tomb** | Enemies stay frozen for 6 s and bosses take 500. |  |  |
| **Titan** | The titan is 30% bigger and stays for 27 s. | **Lifeline** | Everyone also gets 4 s of Overdrive. |
| **Sanctum** | The dome is half again as wide (300 px). | **World Tree** | Lasts 20 s, butterflies every 1.4 s and heal 18 each. |
| **Artillery Assault** | +4 extra projectiles instead of +2. | **Bullet Rain** | Lasts 9 s. |
| **Legion** | Summons 7 wolves and 7 bats. | **Shield Wall** | Bullets that hit the shield are reflected back as your own. |
| **Leap Slam** | The landing also erases enemy bullets within 160 px. | **Cavalry Call** | Two more lancers ride outside the first pair, and the guard lasts 5 s. |
| **Consecrate** | Two more crosses fly out 22 degrees either side (each target is hit once). | **Guardian's Oath** | Charges up to 800 px (not 600). The ward is wider, follows you, and stands a downed ally back up at 25% health. |
| **Lay on Hands** | Also heals everyone else near them for half as much. | **Misericorde** | Each execution heals you 5% of your health. |
| **The Gauntlet** | The Challenge lasts 8 s, and the charge's impact stuns for 1 s. | **Caught Steel** | Holds up to 18 shards. |
| **Last Stand** | You take 5% of the damage instead of 10%. | **Hallowed Blade** | Three swords fall: one at your cursor and one either side. |
| **Drawbridge** | The bridge stands for 8 s. | **Lightning Rod** | Lasts 8 s and each bolt arcs to 3 enemies. |
| | | **Bone Phalanx** | 10 skeletons with 180 health each (a 250 px front). |

A popup appears when an ability levels up. The **Tab** menu shows each ability's level as five pips (the next one
fills as it earns XP). What level 5 unlocks is always written in gold under each ability's description: in the lobby
(your Space ability and your ultimate), on the Tab screen's ability tiles, on the cards that give an ability (in the
level-up picks and the Bazaar). Where space is tight the text gets smaller, then shortened
with "…"; hover a Tab tile to read it in full.

In the lobby, the loadout screen's **Ultimate** row has four keys. Key 0 is your role and class's default ultimate;
the next keys hold the others you can pick instead (key 1 is **Juggernaut Suit** for the Tank Soldier, **Lightning Rod**
for the Tank Elementalist, **Bloodbath** for the Tank Hemomancer and **Bone Phalanx** for the Tank Summoner), and the rest
are locked slots for ultimates still to come. Click a key to pick it (the picked one is in inverse video, like the
weapon and ability keys): it goes to the host and the other players like your other picks, the detail box shows its
charge time, what it does and what mastering it adds, and the preview casts it. Changing role or class goes back to
key 0. The pick comes with you into the match, the Practice Range and a saved run (a continued run keeps the seat's).
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
start with 2 rerolls and everyone gets 2 more for each boss killed. Where a shop will take gold (the Bazaar, Gold Rush, the Expanding arena), a normal level-up also has a **GOLD INSTEAD** key under the buttons: skip the cards for 20 gold plus 2 per wave. Boss rewards, weapon mods, milestones and challenge prizes can't be swapped. The next wave waits until everyone have chosen.

**Repeat copies give less.** The plain stat cards you can take any number of times (Sharpened Rounds, Quick Trigger,
Light Boots, Vitality, Mending, Focus, Hollow Points, Hair Trigger, Sprinter, Fortitude, Regeneration, Clarity) are
worth a bit less with each extra copy: the 2nd 80%, the 3rd 67%, the 4th 57%, the 5th 50% (copy n: 1 / (1 + 0.25 x (n - 1))).
Fourteen Hair Triggers come to x2.3 fire rate, not x3.8; six Clarity cut cooldowns to 53%, not 38%. An offer of a card
you already have says what this copy is worth ("You have 4 · this copy 50% strength"), and the stat preview shows the
real change. Cards with a stack limit (the squares) keep full strength.

**Wounds.** 10% of every hit you take is a **wound**: it can't be healed back (heals, regeneration, orbs, lifesteal,
revives) until the wave is cleared. Your health bar shows it as a dark red end with a bright line where healing stops;
it clears with the wave. A Healer's overheal shield only takes what goes past full health. Both arenas.

| Rarity | Color | What you get |
|---|---|---|
| **Common** | white | Small stat boosts: +10% damage, +10% fire rate, 15% bigger shots (Magnum Rounds), +6% speed, +15 HP, regen, -6% cooldowns |
| **Uncommon** | green | Bigger boosts (+20% damage, +40 HP, -15% cooldowns...) and damage reduction |
| **Rare** | blue | **Second Charge** for your Space ability, an upgrade for a specific ability you have, on any key (e.g. bigger Barrier, longer Roar, double-damage Bulldoze, longer Adrenaline Shot, faster Soul Lantern, wider Airstrike bombs, harsher marks; for Knights: Tower Shield, Earthshaker, Iron Will, Sacred Ground, Bulwark, Miracle, Coup de Grace (Misericorde executes under 30% and leaps 40 px further), Iron Grip (the Gauntlet flies 40% further and its charge hits 50% harder), Forged Edge (Caught Steel's shards hit 8 harder and it catches 6 px further out); for Ninjas: Mirror Image, Thick Smoke, Tempered Steel, Morning Dew, Body Double, Sacred Seal, Assassinate, Unseen Blade, Storm of Steel; for Hemomancers: Hardened Blood, Blood Oath, Deep Veins, Lifeblood, Hungry Earth, Saint's Blood, High Pressure, Usury, Crimson Tide; for Elementalists: Eye of the Storm (Aeroaegis), Permafrost (Frost Nova), Inferno (Flame Wall), Monsoon (Healing Rain), Overcharged Ward (Energy Ward), Dawnlight (Lumen Ward), Cataclysm (Meteor), Whiteout (Blizzard), Riptide (Tsunami); for Summoners: Basalt Hide (Golem Wall), Deep Tremor (Earthquake), Kindred Bond (Force Pact), Sprite Choir (Spirit Swarm), Rebirth Flame (Phoenix), Blood Bats (Bat Swarm), Venom Glands (Hydra), Pack Leader (Alpha Call); for the Soldier's Medkit: Field Surgeon), wider aura; one card per class: Quartermaster (Soldier: orbs heal 30% more, Q/E/Shift recharge 10% faster), Arcane Surge (Elementalist: a Space cast makes your weapon fire 25% faster for 3 s), Pack Bond (Summoner: minions take 25% more before breaking and hit 15% harder), Plate Armour (Knight: 8% less damage, swings cut bullets 20% farther out), Ghost Step (Ninja: +6% speed, 30% less damage for 1 s after a dodge), Bloodthirst (Blood Mage: abilities cost 20% less health, kills heal 2); weapon cards, only while you hold that weapon: Quake Hammer (Warhammer: slam 20% wider, stuns 0.3 s longer), Breacher (Lance: Breaching Thrust shoves 30% farther, Impact +50%), Long Haft (Halberd: 15% more reach, sweeps 20° wider), Pilgrim's Staff (Quarterstaff: hits heal 1 more, one more Jab stack), Daybreak (Lightbringer: light fills 30% faster, Sunlance heals 30% more), Thick Incense (Censer: clouds 30% bigger, 2 s longer), Whirling Sabres (Twin Blades: thrown sabres fly 25% farther, hit 30% harder), Tempered Blade (Greatsword: Rend +30%, Temper builds 25% faster), Duelist's Edge (Rapier: skewers one more bullet, Tempo holds 0.5 s longer), Live Wire (Arc Knuckles: Rocket Dash reaches 25% farther, Twin Hammer stuns 0.3 s longer); for Ninjas: Heavy Weight (Kusarigama: weight flies 25% farther, stuns 0.4 s longer), Iron Bo (Bo Staff: shove knocks 40% farther, full spin +30%), Long Loop (Fuma Shuriken: loop 20% bigger, Orbit 20% wider), Potent Dose (Medicine Darts: poison and healing stacks 30% stronger), Gale Fan (Spirit Fan: gust reaches 25% farther, heals 30% more), Blessed Paper (Ofuda: ward bursts 25% wider, heal 30% more), Barbed Stars (Shuriken: stuck stars return 1.5 s sooner, return hit +40%), Honed Edge (Katana: Momentum Burst shoves 30% farther, caltrops last 2 s longer), Shadow Mark (Kunai: the hit after a blink +40%, marks last 2 s longer); for Hemomancers: Crimson Whip (Blood Lash: 20% more reach, each Clot stack cuts 1% more), Deep Pool (Clot Cannon: pool 30% bigger, 2 s longer), Hungry Tendrils (Leech Tendrils: drain 30% faster, 40% of it heals you), Generous Blood (Transfusion Darts: 30% more health given), Rich Blood (Sanguine Orb: reserve fills 30% faster, coat heals 30% more), Lifeline (Vein Tether: 55% of its damage heals), Hemophilia (Blood Bolts: hemorrhage one bolt sooner, 30% wider), Event Horizon (Implosion: crush +40%, pulls from 30% farther), Marrow (Bone Spear: pierces one more, pickup gives 3 more health); for Summoners: Granite Golem (Golem Staff: Golem takes 40% more, boulder 20% wider), Ossuary (Bone Guard: cages 0.5 s longer, skeletons shoot 30% faster), Deep Claws (Bear Totem: 15% more reach, bleed 30% stronger), Wildfire Seeds (Sprite Staff: one more firefly hop, sprites heal 20% more), Deep Roots (Treant Rod: root reaches 20% farther, heals 30% more), Alpha Pack (Wolf Whistle: takedowns 30% harder, stun 0.3 s longer), Will-o'-Wisp (Wisp Lantern: one more wisp), Imp Horde (Imp Caller: one more imp, 20% faster); plus Multishot, Barrage, Piercing Rounds and Overcharge (second charge on Q/E/Shift) |
| **Very rare** | purple | A brand-new ability on **Q**, **E** or **Shift** (you choose the key; each one only once): a universal one (Shockwave, Overdrive, Blink, Nova or Turret), or one of your **class's two other Space abilities**, the ones you didn't pick in the lobby (these count triple, so about 6 in 11 very rare cards are class abilities) |
| **Legendary** | orange | Unique weapon upgrades, **only from bosses**: after a boss dies, each player picks one of that boss's three (can't be rerolled) |
| **Weapon mod** | teal | Also **after every boss**, right after its legendary: choose any mod from the full list (each stacks up to 3 times) |
| **Milestone** | gold | Their own pool, offered **every 10 levels** (levels 10, 20, 30...) as an **extra pick** on top of that level's normal one, before it. Three cards from: **Scholar** (+15% XP), **Second Helping** (+10% chance a level-up gives an extra pick), **Wide Choice** (+1 card in every level-up offer, max 2, so up to 5 cards), **Reroll Cache** (+3 rerolls now and +1 more after every boss), **Fortune** (rarer cards 8% more often), **Iron Constitution** (+25 max HP) and **Quickening** (-4% ability cooldowns), and in the Expanding arena only **Architect** (hold 20 more vectors and gain 10 now, stacks to 3). Each card stacks to a cap (5, Wide Choice 2, Reroll Cache 4), shown as circles on the card, and a maxed card leaves the pool. Milestone picks can't be rerolled. The very rare **Milestone Compass** (stacks to 3) is a normal card that gives every level-up a 5% chance per stack of also offering a milestone pick |

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
| **Homing Rounds** | Shots bend toward an enemy within 260 px and about 45 degrees of their path, so near misses connect (they don't turn around to hunt). Costs 5% damage per stack; more stacks bend harder. **Area weapons** (Implosion, Flak Cannon, Ofuda, Storm Hammer, Clot Cannon, Vein Tether, Frost Lance, Treant Rod, Nimbus, Spirit Chalk, Golem Staff, Arc Lightning) get a bigger area instead of bending: +25% per stack to the blast, burst, splash and pool, collapse, beam width, lightning's reach, splinters, lash, lightning and rain, ink line, boulder or strip, for the same damage cost. **Cone weapons** (Chain Light, Leech Tendrils, Repulsion Cannon) condense instead: the cone, arc or blast is 15% narrower and hits 10% harder per stack (the Repulsion Cannon also pushes 10% harder), with no damage cost, so it's a choice between covering a crowd and focusing a target. **Wisp Lantern and Imp Caller:** the wisps and imps you send pick an enemy 50% farther from your cursor per stack, and one sent at empty ground (or whose target dies) turns onto an enemy within 260 px of it, for the same damage cost |
| **Ricochet** | After a hit, the shot bounces to another enemy within 300 px for 40% damage; +1 bounce per stack. Hits that can't bounce (melee, beams, cones, bursts, sent wisps and imps, and shots that pierce: Railgun, drawn arrows, Bone Spear, Fireball, flames, Fuma Shuriken) store their damage instead, and up to 4 times a second a **bounce bolt** carries 40% of it from the enemy hit to the nearest other one (+1 bounce per extra stack) |
| **Incendiary Rounds** | Hits set enemies on fire: each burns for another 40% of the hit's damage over 3 s (per stack), and burns from every hit add up, so fast and slow weapons gain about the same |
| **Cryo Rounds** | Hits slow enemies 20% (up to 60%) with a 6% chance to freeze non-bosses for 1 s |
| **Shock Rounds** | 25% chance per hit for lightning to arc to 2 more enemies at 60% damage |
| **Explosive Rounds** | Shots explode on impact for 30% damage around them (bigger blast per stack). On a fast weapon the blasts on one spot are shown at most every 0.13 s; every hit's damage still lands |
| **Velocity Rounds** | Shots fly 30% faster and farther, +25% damage |

Mods apply to your weapon's shots and your minions' shots, but not to grenades. They also ride every hit of
weapons that don't fire bullets (melee swings, beams, cones, the Repulsion Cannon's blast, Ofuda ward bursts,
sent wisps and imps). On a Knight, the elemental mods apply to every melee hit (see The Knight above).

**Piercing Rounds** let shots pass through two more enemies per stack. Weapons without bullets that run out of pierce
(melee, the Flamethrower, Frost Lance, Fuma Shuriken, Repulsion Cannon, Radiant Orb, Chain Light, Spirit Fan, Ofuda,
Arc Lightning, Wisp Lantern, Imp Caller, and the Fireball, which already rolls through everything) reach 15% farther
per stack instead.
A piercing shot can't hit the same enemy twice in a row, even inside two overlapping enemies.

**Slows** don't add up: an enemy moves at its strongest slow until that one ends, then at the next-strongest one
still running. A freeze (Frost Nova, Cryo's frozen solid) ends on time even while a weaker slow (Wet, ink, a slow
zone) keeps being renewed.

**Virulence** (rare, two stacks, with Poison Dart; it took Phase Step's place with the old Dash): the venom deals 30% more and spreads 60 px further.

Rarer cards show up a bit more often as you level. All the numbers live in
`Assets/Scripts/Core/Upgrades.cs` if you want to rebalance or add upgrades.

## Rules

- **Timed waves:** a regular wave's enemies arrive over about 45 s instead of in one burst: a ramp-up (first 15%), three **surges** (at 20%, 48% and 76% of the window, about 7 s each) with quieter lulls between them (40% of the surge rate), then a taper as the wave dies down, and the wave ends when the last one is dead (the clear-up brings it to about a minute). Spawning waits while 14 + 3 per wave (at most 60) foes are alive. Boss, Practice, designed, Zombie Infection and room-by-room waves keep their own pacing. Enemies pick the nearest breach (or the nearest valid spot on the open Rooms map, of four) to a player that is at least 350 px away, so big maps don't feel empty.
- **Spawn portals:** on a timed wave the single enemies come in **bursts** (3 on wave 1, one more every 4 waves, up to 8) through a **spawn portal**: a violet vortex that opens for 1 s where the burst comes in (an edge, a layout's enemy gate, a spot on the Rooms map, never within 160 px of a player), then lets one enemy out every 0.35 s, with chevrons pointing the way they'll come. Shoot it closed (200 health, scaling like an enemy's) before it empties and **the rest of its burst is cancelled**: +5 score each, plus 2 gold each as coins in the Expanding arena. An empty portal shrinks shut with no reward. Up to 2 portals are open at once (3 in a surge); with none open for 4 s a smaller burst comes anyway. Squads, Nests and breach waves keep their own way in.
- **Flank portals:** from wave 3, one burst in 4 (never a wave's first) opens **behind the party**, opposite the way the players are aiming: a red portal that takes 1.6 s to open, and a red arrow round your player points at it.
- The arena is 1600x900 and **wraps around for players**: walk off any edge and you come in on the opposite side.
- **Wall slams:** an enemy knocked back into the arena edge stops there and takes damage: 6 plus 0.12 per px of push the wall cut short, up to 40 (pushes that lose under 8 px do nothing). Bosses and shielded enemies still use their normal damage rules.
  Enemies and bullets don't wrap, so the edges make a good escape route.
- **Melee grace:** when a melee swing hits something, bodies you're touching can't hurt you for 0.3 s (contact damage only, not bullets or boss attacks). It keeps a melee build from being punished for standing close to land its hits, bosses' bodies included.
- Your hitbox is the small coloured dot ringed in white in the middle of your character, not the whole circle.
- **Status effects** show as a tint on the enemy plus an icon above it: **snowflake** = frozen (encased in ice, can't
  move or shoot), **blue double arrow** = slowed, **flame** = burning (flames rise off it), **blood drop** = bleeding
  (drops fall off it), **red crosshair** = marked (takes extra damage).
- At 0 HP you're **downed**. Stand next to a downed ally for 4 seconds (Normal difficulty) to revive them (or the
  healer can pulse them back). Going down plays out on screen: your model glitches into red and cyan copies and
  flickers out while flecks of its costume colours shed and fall, leaving a dim red disc with an X. A red **DOWNED** scan
  band with a vitals trace that goes flat opens near the top (it gains a mint revive bar and "<ally> is reviving you" once
  someone is on you). A 36-segment ring charges round whoever is down, with a dashed link from the ally reviving them.
  Standing back up reverses it: flecks fly in, the model returns mint-tinted under a scan line with a shockwave, and a
  **REVIVED** band flashes mint. Visual effects off keeps the old grey disc and green arc; Reduced effects drops the split
  copies, flicker and glow but keeps the fades, flecks and ring.
- After a hit you can't be hit again for 0.25 s (Normal). Small health orbs drop from 12% of kills (Normal), large
  ones from 3%.
- If everyone is downed, it's game over. Clearing a wave revives anyone downed and heals everyone a bit.
- **The Campaign is 100 waves, with a boss fight after every 10th.** Each wave of the old 50-wave track is fought
  twice (two "legs"), so enemies, new types and every number that grows by wave grow at half the pace: wave 37 plays
  like wave 19 of the old run, and the wave numbers in this README are track waves (double them, roughly, for the
  Campaign's). XP, kill gold, chests and the Expanding arena's vectors per wave are halved to match, so you reach each
  boss about as strong as before. Runs saved before this stay 50 waves; Boss Hunts stay ten.
- **A new enemy trickles in, and the last boss's fades out.** In a lesson that introduces an enemy (the Warden's and the Hive Queen's waves 2, 3, 4, 6, 7 and 8: Guards, Thugs, Security Guards, Riot Shields, Gas Grenadiers, Taser Guards, or their Hive counterparts), it arrives alone, after the wave's squads. After a boss, his faction fades over the next three waves (40%, 20%, 8% of the wave, singles only, no squads) with the tricky ones going first (Riot Shields, Gas Grenadiers and Taser Guards at once, the Security Guard after a wave, Thugs and Shivs (Bumbles and Hornets) after two, Rioters and Guards last), while the next faction's enemies come in at once. Later on, the **comeback squad** (one wave in five from wave 11) is big: an old boss's squad twice over, plus that faction's toughest enemy as an anchor, so a late Breakout is a crowd of Rioters. The Warden's first round is Rioters only (no Guards, so no Breakout either). Wave 1 has more enemies (12) and spawns faster, over a shorter window (24 s, the full 45 s by wave 6), so there's less waiting for the last few.
- **Each boss's ten Campaign waves teach its fight, in the order of the chart in `Docs/Approaches.md`.** The Warden's (waves 1-10): 1 Rioters only; 2 Guards join (Rioter packs);
  3 Thugs (Rioter packs, Guard pairs); 4 Security Guards' aimed tracers (Yard, Guard pairs); 5 a recap; 6 Riot Shields (flank them); 7 Gas Grenadiers (Yard, Cell block);
  8 Taser Guards (stuns; parry the Fork Bolt back); 9 every squad, tighter; 10 the exam, the Riot line and a big Breakout. Each wave brings one new enemy, alone and last, and
  its squads only hold enemies you have already met. The Hive Queen's (waves 11-20) follow the same ten steps with her own: Workers, Soldiers (12), Bumbles (13), Needlers (14), a recap (15),
  Comb Shields (16), Honey Lobbers (17), Stingers (18), every squad (19), the exam: the Royal guard and a big Swarm (20). Other bosses build up as before until they get theirs.
- New enemy types unlock as the waves go up (see below).
- **Knockback slides:** anything that shoves enemies (the Repulsion Cannon, Roar, Shockwave, the Scattergun up close,
  Knight swings, Bulldoze, Fortress, the Fuma Shuriken's fan) slides them the distance over 0.18 s instead of jumping
  them there, with a streak behind them, dust kicked up along the way and a puff where they were hit.
- **Daze:** a close-in hit (Bulldoze, Knight and Lance swings, the Katana's Momentum Burst, the Bo Staff, the Kusarigama's
  whirl) dazes the enemy for 0.4 s (bosses 0.2 s): it still moves, but can't shoot, use its special attacks or hurt you by
  touch, and a wind-up it was in starts over. Three gold stars wheel over its head. After a daze it can't be dazed again
  for 0.8 s, so steady swings can't lock it down. Bulldoze also keeps you invulnerable 0.35 s past the end of the charge.
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

**Job markers.** A small bar and shape under each regular enemy shows its job: orange dot = Pressure (shoots), red chevron = Hunter (runs at you), yellow ring = Zoner (takes ground), violet diamond = Disruptor (breaks your position), cyan square = Anchor (kill it first). Bosses get none.

The Lunger, Spike Block, Charger and Riot Shield all warn of a lunge the same way: a red strip on the floor that fills up over the wind-up and flashes white just before they go.

A Phantom shows a ring where it is about to blink (half a second before it does, and before its ring of shots), and a Mortar's bomb shows a ring where it will land.

**New enemy chips.** Between waves, under the results box, a small card for each enemy the next wave brings that you haven't met yet: its name, its job and one line on what to do about it (at most three; none before boss waves).

**Enemy names on hover** (Settings → Gameplay, off by default): point at any enemy to see its name, and its job for the regular ones.

**Name tags.** The first three times you see each kind of enemy it carries its name and job for a couple of seconds. Your profile remembers which ones, so you are only taught once.

A Charger shows a ring where its dash will end, which is also where its ring of shots will burst.

Shooters that fire straight at you (Sniper, Shard, Medic, Splitter, Phantom, Grunt, Taser Guard, Security Guard) share one **shooting line**: for the instant before they fire, a slim lane in their colour fills from the muzzle along the shot, brighter at its head, and caps off when the shot is due; the Grunt shows all three of its shots. The Leader and Beam Sentry keep their own blinking lines.

From wave 13, a regular wave shows at most 6 different enemy types (8 from wave 25), so each kind is easier to learn; in the Campaign the boss factions' enemies count too (they keep room for the wave's headliners). Boss waves, paths with their own mix and Zombie Infection hordes are unchanged.

The first waves are **melee rushers** (spiky, they never shoot, they hurt on contact). Shooters take over a
little more each wave: wave 1 is all melee, wave 7 about half.

**From wave 13, every enemy has a job** (design: [`Docs/EnemyRoles.md`](Docs/EnemyRoles.md)), and a wave is built to a
mix of jobs instead of an even draw from every type seen so far: about 45% **Pressure** (they shoot: weave and use
cover), 25% **Hunters** (they come for you: Swarmers, Spikers, Brutes, Lungers, Buzzsaws, Spike Blocks, Chargers, Fuse
Runners, Reavers), 12% **Zoners** (they take ground: Beam Sentries, Sappers, Scorchers), 8% **Disruptors** (the
Hookman) and 10% **Anchors** (kill them first: Medics, Nests, Warders). A job with nothing unlocked yet passes its share
on. Each wave has **2 headliners** (3 from wave 25), favouring newer types, that make up a good part of their jobs, so
waves feel different from each other. Types fade out once they've been around for 10 waves (down to 15% as often by 20
waves in), and so do old squads, so the wave-1 Grunt gives way to what came after it. Squads are half of a late wave, and
the single enemies fill whatever each job is still short of. `Tools/SimTest/run.sh 1 1 wavemix` shows the mix wave by wave
(waves 14-29 went from 81-91% shooters to about 55%).

With **Visual effects** on, a melee weapon drawn in its own hands (Halberd, Warhammer, Greatsword, Rapier, Quarterstaff, Censer, Twin Blades, Lightbringer, Lance, Bo Staff, Katana, Kusarigama, Blood Lash) is the only pair of arms you see: the character's own arms are left out (and its shoulder plates too, for weapons whose arms have their own pauldrons). The Kusarigama keeps the sickle in one hand and the weight hanging from the other; the Blood Lash's whip hangs slack and sways until it cracks. Every swing starts from the held pose and comes back into it: after the cut the weapon holds a moment, then turns back to where you are aiming and settles into the held pose, with no fading in or out.

With **Visual effects** on, regular enemies stand out from the floor: a soft dark shadow under each body and a faint glow in their own colour, much quieter than a player's halo. Bosses, spawning enemies and zombies don't get it.

With **Visual effects** on, anything that moves fast leaves an **air trail**: two thin streamlines off its shoulders that wobble and fade. It follows speed: none at or below 110 px/s (Grunts, Brutes, bosses), light for mid speeds (a Tank or Healer walking is about 10-25%), and full at 330 px/s and up (Hive Bombers, dashes, hard knockback). Players, enemies and minions all get it. Not sent over the network.

**CRT colour** (Settings > Visual effects, saved on this machine): tints the CRT screens (menus, lobby, profile, run summary) Blue (default), Green, Amber, Red, Violet or White. The role, class and rarity colours keep their own meaning and stay as they are.

With **Visual effects** on, the Ninja's **Shadow Step** is smoke: you burst into puffs that split into two halves, each following a pale eye round one side of the enemy, and meet again behind it to re-form as you strike (you aren't drawn for the first third of a second; the step and its damage are instant as before). Off, it's the plain ring and slash. The two ends of the step are sent over the network (`FxKind.ShadowStep`).

**Squads.** About 70% of a regular wave arrives as small teams that come in together from one edge and look after each
other; the rest are single enemies. Each squad unlocks at a wave, and none comes more than twice in one wave: Rush (1:
five Swarmers), Firing line (3: three Grunts, a Sniper behind), Brute pack (5: the Lunger waits for a Brute to reach
you), Splinters (7: two Splitters and a Grunt), Sword line (7: three Swordsmen), Shield line (10: two Shield Swordsmen and a Swordsman), Bombardment (8: a Charger and two Mortars), Shield wall (9: a Shielder
leading two Snipers that keep behind it), Field hospital (11: three Brutes and a Medic behind them), Hold the line
(14: a Pauser and two Grunts), Demolition (15: two Fuse Runners and a Grunt), Ambush (16: three Phantoms blink in around
you), Whalers (17: a Hookman and two Lungers waiting for what it hauls in), Minefield (18: a Sapper and two Spike Blocks), Beam battery (18: two Beam Sentries
guarded by a Shielder), Hunting party (19: two Leaders and a Sniper), Pincer (21: Hexers from opposite edges),
Seamstresses (22: two Stitchers side by side, and a Grunt), Scorched earth (24: two Scorchers and Spikers), Siege
(26: a Juggernaut and two Medics), Reaver pack (28: two Reavers and two Lungers), Hive (31: a Nest guarded by
Splitters), Bastion (36: a Warder, a Juggernaut and two Grunts). Early waves keep their share of rushers (a melee
squad as often as a melee enemy used to come). Paths with their own mix keep it.

| Wave | Melee enemy | What it does |
|---|---|---|
| 1 | **Swarmer** | The simplest enemy: a small white chomper with a wedge for a mouth and no eyes. Walks straight at you (115), 14 health, 6 per bite. Comes in packs of four, so cut them down together |
| 2 | **Spiker** | Small and fast, with a long thin spike on each face. Zig-zags toward you (swinging 40 to 65 degrees off line, flipping side every 0.2 to 0.45 s, straightening as it gets close), comes in pairs. (Called the Swarmer before the plain white one arrived) |
| 3 | **Brute** | Slow and very tough (200 health); hits hard (24) when it reaches you. **Enrages as it is shot**: the more health it has lost, the faster it walks (up to +80%) and the harder it hits (up to +60%), and it turns redder. Healing calms it down. Burst it down or back off |
| 5 | **Lunger** | Circles in close, flashes a pink line, then leaps along it. Step aside |
| 5 | **Buzzsaw** | A ring of spinning spikes. Fast (170) and it steers with momentum, so it swoops and overshoots. Each touch is only 4, but they land about eight times a second |
| 5 | **Spike Block** | A square with a big spike on each face. Rocks from side to side as it closes in; within about 140 px it stops, shakes and shows a red line (0.55 s, the line is locked), then lunges along it for 35. It rests 0.8 s afterward: shoot it then |
| 7 | **Swordsman** | A melee enemy with a long blade (85 health, never fires). It closes in until you are about 100 px away, then stops turning and raises its sword for 0.55 s while the cone in front of it fills red. The cone is exactly what the blade covers: 140 px long and 90° wide, a curved band from its hand to the tip. It then cuts across the whole cone in 0.15 s (14 damage to everyone in it, once; a perfect parry blocks it) and recovers for 0.8 s. Step out of the cone before the red reaches the edge |
| 10 | **Shield Swordsman** | A Swordsman (slower: 75 against 95) with a blue-steel shield held up in front that stops every shot from the front within 63° either side (it flashes when it blocks one, and doesn't wear). The shield drops the moment it cuts and stays down for the first 0.75 s of its 1 s recovery, so shoot it then, or from the side or behind |

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
| 36 | **Warder** | Hangs back behind its allies; every 6 s up to three allies within 320 px get a bubble worth half their health, tethered to it by a line of flowing beads. Kill it and every bubble pops. A pointed violet diamond that turns to face you, with three hex sigils orbiting it: they spin faster and its reach fills up for its last 1.4 s before a ward, and one sigil leaves for each ally it wards. A chevron bobs over it while any ward is up: shoot it first |

| Wave | Doesn't shoot | What it does |
|---|---|---|
| 15 | **Fuse Runner** | A banded orange powder keg with a lit wick (Hunter; comes in pairs). Sprints at you (200), stops within 70 px and a 0.8 s fuse runs down: a red ring shows the 90 px blast, an orange ring closes on the keg and the wick burns down. Then it blows up for 35 (plus 3% a wave on Normal), and the blast hurts **enemies** too (40, growing with enemy health; bosses take a quarter). Step out, or lead it into its friends first. Shot dead during the fuse, it blows up at once where it stands, and your shot gets the credit for what the blast hits. 50 health |
| 17 | **Hookman** | A violet diamond with a harpoon spinning round it (Disruptor). Keeps about 260 px off with its squad; every 4 s it locks a thin line on you (0.5 s, blinking faster as the throw nears) and throws the harpoon (320 px). A hit **yanks you 220 px** toward it and whatever's beside it, but does no damage. Sidestep, dodge through it (the dodge's invulnerability), or perfect-parry it; a player's shot across the cable cuts it. A miss leaves it **open** for 1 s, shimmering white: +50% damage. 150 health |
| 18 | **Sapper** | A yellow hexagon with a red core (Zoner). Circles you about 240 px out, planting a **Tripwire Pod** every 1.6 s (4 at most: the oldest fizzles). A pod takes 1 s to arm, then a red **laser** joins it to every armed pod within 180 px. Step within 40 px of an armed pod or cross a laser and it beeps for 0.3 s, then blows up (70 px, 28 plus 3% a wave on Normal); crossing a laser sets off both ends. The blast hurts enemies too, and **shooting a pod** blows it up at once (your shot gets the credit). Pods last 12 s, don't count as enemies left in the wave, and fizzle out when it's cleared. Kill the Sapper to stop new ones. 110 health |

| 12+ | **Elites** | Any enemy can spawn with a gold ring: 2.5x health, bigger, worth 3x XP, always drops an orb, and a **trait** shown as a marker over it (two traits from wave 35): **Hasted** (40% faster moving and firing), **Shielded** (a bubble worth 40% of its health that grows back 4 s after it was last hit), **Volatile** (bursts into a ring of 16 shots 0.6 s after it dies), **Splitting** (dies into two Shards, four from a Juggernaut, Nest or Brute), **Mirrored** (one in four of your shots that hit it bounce back at you), **Commander** (allies within 220 px fire 30% faster). More common each wave |

### Campaign buildup

Every regular wave is made of boss-themed enemies (design: [`Docs/Campaign.md`](Docs/Campaign.md)). The last boss's
faction **fades out** over the next five waves (35%, 25%, 15%, 5%, 0%), bosses from two fights back stay on as an
**older pool** (10% from wave 11), and the next boss's faction takes the rest, building up as before (65% to 100%;
55% to 90% once the pool exists). Only built factions count (the Warden's and the Lich's zombies); a slice for a boss
with no faction yet is the usual random mix. From wave 11, one wave in every five brings a **comeback squad**: a squad from an earlier boss's faction (the Warden's or the Lich's) arriving in place of that wave's older-pool singles. Turn it off with **Campaign buildup** in the Difficulty tab to get the
Boss approach below. Boss Hunts are unchanged.

### Boss approaches

The four waves before each boss are its **approach**: part of each wave is the boss's own kind of enemy, so you meet
its world on the way to it (design: [`Docs/Approaches.md`](Docs/Approaches.md)). How much is a lobby setting
(Difficulty tab, below): **Blend** (the default) builds up over the four waves, 34%, 51%, 68% then 85% of each wave;
**Fully themed** makes all four waves the boss's own enemies; 0% turns it off. The rest of each wave is the usual mix,
paths still apply (an enemy-mix path builds from what's left; Elite Gauntlet makes the faction elite too), and the
boss fight itself is the same. A chip above the wave panel says "Approach · The Warden · 3/4", and the banner before
the first and last waves says you're on the road to the boss, then that it's near. So far the **Warden** has a faction (waves 1-4), and the last wave before him always brings the **Riot Line**: four Riot Shields marching at you as a wall, five Guards close behind it, who break out and rush you once the wall gets within about 190 px or half its shields are down. The **Hive Queen** has one too (waves 6-9): eight bees, one for each of the Warden's roles (below), with the **Royal guard** (four Comb Shields, five Soldiers) on the last wave. The **Lich** has one too (waves 31-34): the same zombies as Zombie Infection, Shamblers first, then
Runners (in packs of three) and Spitters from wave 32, Crawlers from 33 and Menders from 34, with two squads (Graveyard
shift, Grave rush) and a last-wave **Dead march** (six Shamblers, two Menders, three Runners). They don't get back up
outside the modifier. His fight itself is unchanged. The other bosses' factions come next.

The Warden's faction pays XP like regular enemies of their health (Rioter 7, Guard 15, Riot Shield 10, Gas Grenadier 9, Taser Guard 11, Security Guard 9),
so its tougher approach waves don't leave you levels behind at his wave (about level 4 at Blend). The Rioters and Guards of
his own riot, on the boss wave, still give 2 and 4.

| From | The Warden's faction | What it does |
|---|---|---|
| 1 | **Rioter** | A prisoner on the loose: weaves at you, in pairs (contact 10). Squad: Breakout (six Rioters and a Guard) |
| 1 | **Guard** | A riot guard with a baton, straight at you (contact 14) |
| 2 | **Thug** | A prisoner brute dragging a ball and chain, with cuffed fists: moves and hits like a Brute (140 health, contact 20). Squad: Yard (a Thug, three Rioters and a Shiv) |
| 3 | **Shiv** | A prisoner with a taped shank held out front: circles in close, shows the lunge lane, then lunges like a Lunger (contact 18, 26 on the lunge) |
| 2 | **Riot Shield** | A guard behind a tall shield that stops shots and swings from the front. Up close the shield flashes (0.4 s), then it lunges and **bashes** you: 10 damage and knocked **160 px** back (you can't be bashed again for 0.8 s). It holds the shield low for a second after, when front hits land. The shield holds 4x its health and cracks as it wears; broken, the guard staggers for a second, then flails and takes 50% more damage. Go round it or break it. Squad (wave 3): Cell block (two Riot Shields with Rioters pouring past) |
| 2 | **Gas Grenadier** | Keeps its distance and lobs a tear-gas canister near you every 3.6 s. It plants its feet for a 1.07 s wind-up: a ring marks the spot (picked as the wind-up starts, so you can walk out of it) and fills while its arm goes back and the lenses glow, then it swings and the canister flies for 1 s. The 70 px cloud settles after 1 s and hurts (8 a second) for 4 s |
| 2 | **Security Guard** | A guard with a service pistol: slate uniform, hi-vis vest, peaked cap. It holds about 300 px away and strafes, keeping one side for 2-4 s, its pistol trained **straight at you** (no lead), then **locks on for 0.25 s** while still walking, slowed: the shooting line fills along the shot. Then one **tracer round** (340 px/s, 7 damage): a hot-white streak with a long tail. It only starts shooting once within about 380 px of you, then semi-auto, one every 1.5 s, and the aim doesn't follow you once locked, so a sudden turn beats it. Its pips above its head count the **seven rounds** in the magazine; empty, it drops the magazine and **reloads for 2.2 s** (a cream ring fills round it), slowing to a stop and not shooting: rush it. Squad (wave 2): Patrol (three Security Guards with two Rioters) |
| 3 | **Taser Guard** | A guard with a taser that keeps its distance (320 px), plants its feet and **charges for 0.7 s**: the shooting line and the prongs crackling, brighter as it nears the shot. Then it fires one **Fork Bolt** straight at you (260 px/s, 8 damage; it doesn't lead you, so strafe across the line): a white lightning glyph that flickers its zigzag 16 times a second, with a fork, among rounder bullets. A hit **stuns you for 1 s** (a flash and six bolts web out from you); a perfect parry sends it back like any shot. One bolt every 3.2 s |

| From | The Hive Queen's faction | Plays like | What it does |
|---|---|---|---|
| 6 | **Worker** | Warden's Rioter | A small, quick worker bee that weaves at you, in loose pairs (contact 10). Squads: Swarm (a Soldier and six Workers), Worker pack |
| 6 | **Soldier** | Guard | An armoured bee that rushes straight at you, jaws snapping as it nears (contact 14). Squad: Soldier pair |
| 7 | **Comb Shield** | Riot Shield | A bee behind a plate of seven wax cells that stops shots from the front. Up close the plate flashes (0.4 s), then it lunges and **bashes** you: 10 damage, knocked **160 px** back. The cells break as it wears (two a step, the facing ones first); broken, the bee staggers and falls fast. Squad: Honeycomb |
| 7 | **Honey Lobber** | Gas Grenadier | Keeps its distance, plants its feet and swells. A red ring marks the spot before it lobs a honey glob; the **honey puddle** (55 px, 4.5 s) slows whoever stands in it (45%) and doesn't hurt |
| 8 | **Stinger** | Taser Guard | Keeps its distance, plants its feet and **charges a venom lance for 0.7 s** (a lime shooting line, the lance crackling), then fires one **venom dart** (260 px/s, 8 damage, no lead): a lime zigzag with two white barbs. A hit **stuns** you for a second; a perfect parry sends it back |
| 7 | **Needler** | Security Guard | Hovers and strafes, aims straight at you, **locks on for 0.25 s** (the red lane), then one white **needle** (a tracer round, 7 damage). Seven to a magazine (the pips above it), then a 2.2 s reload. Squad: Picket |
| 7 | **Bumble** | Thug | A huge, furry bumblebee: moves and hits like a Brute (140 health, contact 20). Squad: Nursery (a Bumble, three Workers and a Hornet) |
| 8 | **Hornet** | Shiv | Circles in close, shows the red **lunge lane**, then darts along it like a Lunger (contact 18, 26 on the lunge) |

They are drawn as in `Docs/Mockups/hive-faction.html`: vector bees with wings, striped abdomen, thorax and head. The Hive faction pays XP like its counterparts, and after the Queen it fades the same way the Warden's does.

The **Twin Reapers' faction**, the Dusk and Dawn cult, leads up to their fight (waves 11-14; in the Long Campaign a
ten-wave lesson cycle, one Reaper mechanic a wave). Every member is sworn to **Dusk (pink) or Dawn (blue)** and drawn in
that colour. Squads: **Vigil** (an Acolyte and six Wisps), **Procession** (four Acolytes, two Lamplighters behind),
**Twin graves** (a Gravedigger and two Bound pairs), **Harvest** (two Scythe Wraiths, two Shades, three Wisps); the last
wave brings **Dusk and Dawn**: split to opposite edges, a Mourner on each side with a Bound pair and three Acolytes. (Mockup: `Docs/Mockups/reaper-faction.html`.)

| From | The Twin Reapers' faction | What it does |
|---|---|---|
| 1 | **Wisp** | A soul flame, in threes: drifts straight at you like a Swarmer (14 health, contact 6) |
| 1 | **Acolyte** | A hooded cultist with a sickle: stops short, raises it over a **pale cone** (96 px, 120°, 0.5 s), then one wide swing (12). A Swordsman's moves, shorter and wider |
| 2 | **Bound Wraiths** | Always a **pair** (60 health each), circling a point that drifts to just beside you, 75 px out, joined by a **tether** that hurts (10) as it sweeps over you. Kill one and the other **stops and channels** it back: a ring fills round the ghost for **3 s**, then it's back at 60% health. Kill the channeller first and neither comes back |
| 2 | **Lamplighter** | Keeps about 280 px away. It plants its feet, its lantern flares and **gold lines** show the volley (0.6 s), then it fires **three lines** at you, next time a **four-way cross**, streaks of four (every 3.2 s) |
| 2 | **Gravedigger** | A cultist brute with a shovel: moves and hits like a Brute (140 health, contact 20) |
| 3 | **Mourner** | Keeps about 260 px away and stands still to weep a **twin spiral**, one arm each way, for 2.5 s, then **rests** 3 s: push in then |
| 3 | **Scythe Wraith** | A Lunger that reaps: marks a long lane from up to 280 px out (0.8 s; the lane **locks after 0.25 s**), then dashes about 270 px along it and reaps **26 px either side** of its path (22). Leave the lane sideways |
| 3 | **Shade** | A shrouded Phantom: shows a ring where it will land, blinks there and fires |

**Bubbles (Shielded elites and the Warder's wards):** a bubble is twelve cells round the rim, each holding a twelfth of the shield. A hit wears down the cell facing whoever shot it (flashing, then breaking into shards) and spills over to its neighbours once that cell is gone, so the side you shoot from is the side that opens. Burning and bleeding keep chewing the weakest cell. A honeycomb inside only shows where its cell is still up, and a thin shield bar sits above the health bar. Blue is a Shielded elite, violet is the Warder's.

## Bosses

**Every boss sounds like itself.** Each mechanic has a sound for its warning and for its strike, from a palette of
synthesized boss sounds: a rising whine before lasers and aimed attacks and a buzzing beam when they fire, a crack and
deep thud for slams and landings, a rush for charges and dives, the Warden's prison siren, the Queen's swarm buzz
and honey splats, scythe swishes for the Reapers and the Duelist, an in-rush for the Singularity's pulls and the
Devourer's inhale, the Colossus's rumbles and breaking stone, cages slamming, the Devourer's jaws, the Herald's
thunder and a deep gong for rites, last stands and the Eclipse's totality. They follow the Enemies volume.
(`Tools/SoundCheck/render.sh` writes them to WAV files.)

**Melee rushers have sounds, and far sounds are quieter.** They all have footsteps (a crowd of one kind makes one step at a time, at the nearest one's volume). Swarmers chomp and pop, Spikers tick as they flip and shatter, Brutes thud, grunt and bash, Lungers zip when their pink line shows and again at the leap and the landing, and Spike Blocks clunk, rattle (the tell), lunge and give a tired hiss when they rest, which is your window to shoot. Every kind dies with its own sound, but a pack of one kind dying together makes one. A Buzzsaw has no tick per touch: a quiet whirr fades in while one touches someone. Enemy sounds and enemy shots get quieter with distance from you: full volume within 260 px, down to about an eighth at 1100 px or more. They follow the Enemies volume. (Mockup: `Docs/Mockups/melee-enemy-sounds.html`.)

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

The game has **50 regular waves** with a different boss after every 5th one. The boss is its own fight between wave 5 and 6, wave 10 and 11 and so on: it keeps the wave number of the wave before it (the HUD says BOSS FIGHT, the wave box says BOSS CLEARED) and doesn't use up a wave, so 5 regular waves lead up to each boss. Bosses after wave 20 get extra health (+10% per wave on
top of the normal scaling: x2 at wave 30, x4 at wave 50) to keep up with stacked upgrades. Their name, health bar and
warnings show at the top. Beat The Eclipse after wave 100 (50 in a run saved before the Campaign) to win; the host can then keep going in **endless** mode (C),
where the bosses come back around, tougher each time.

| Wave | Boss | Mechanic |
|---|---|---|
| 5 | **The Warden** | A prison riot in three stages, one mechanic at a time (2 s between stages, when the screen clears and it can't be hurt). **Count** (100–65%): sways through the middle — orb rings, a spiral (holding still), aimed fans (red lanes show each volley 0.35 s before it fires), then an 18 s **searchlight** from the corner tower that follows you at an outrunnable pace while the **Taser Guards** below do the stunning (the Warden fires no stun shots himself); get stunned or linger in the light and a **shackle ring** of orbs closes in on you, its gap facing away from the Warden. Turn it round: **lead the light across the Warden** and it's blinded for 3 s (**exposed**: no firing, +50% damage), and a **perfect parry** sends a Taser Guard's Fork Bolt straight back to **stun it** for 1.5 s. Each searchlight turn also sends in **two Riot Shields** (if fewer are up) that work round to your side away from the light and bash you into it, and **two Taser Guards** through the other side doors (staggered, one Fork Bolt each every 3.2 s) that go when the turn ends. **Lockdown** (65–30%): the 12 s **cross** (dash, plant, fire a static "+", four times) runs alongside three **gates** — walls of push bullets 2 rows deep, with two doors near the side walls; each slides a little way in and stops for a moment (its doors flash) so you can line up a door, then sweeps across, shoving you back the way you came (one hit a gate, pinned or not). A turn's gates all come from the same edge, and each has a **gold lock** in the middle: six hits break it (shots, blasts from grenades, boulders and flak, melee swings and beams all count), opening a third door there. With them, lobbed **tear gas** that never lands across that gate's doors. Then a **tear gas barrage**: two volleys of canisters in a pattern with clear lanes between, while it fires slow aimed bursts. **Riot** (30–0%): cell doors burst open and **rioters** pour out, nine a door (they attack whoever is nearest, the Warden too); any still standing after 10 s bring blue **guards** with batons, who beat them down and then come for you while it turns a riot shield toward you and fires a shotgun. Rioters that reach it take 0.6% of its health each, but only up to 5% a riot turn (after that it bats them away). Its **riot shield** soaks 7% of its max health from the front, cracking as it wears; **break it** and it's exposed for the rest of that riot turn, taking 50% more; then a telegraphed **baton charge**. Below 10% it **puts on armor** (a 2 s stage change: steel plates lock on, it can't be hurt; then a blue armor bar above its health, worth 47% of its max health, takes your hits first) and makes a **last stand** in the middle (about 30 s for a good duo): the lights go out (lit round you, the Warden and its searchlight, which hunts you again) under a steady red tint, with nothing flashing. It fires long **"+" bursts** of straight streams (2.2 s), turning 45° each time (+, ×, +...); in the 1 s pause between, red lanes show where the next goes and **one ring** of orbs goes out; a **Taser Guard** comes in through a bottom door, kept lit in the dark, and fires its Fork Bolts at you (perfectly parried, one stuns the Warden) |
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

## Saved loadouts

The **SAVED** key at the right of the loadout's Role heading opens **Saved loadouts** over the right column (the
preview and stats stay beside it). Up to **6** favourites, each a card with its name, class and role, weapon, ability
and ultimate; the one you're wearing says **EQUIPPED**. The pencil after a name renames it (Enter or the tick keeps it, Esc cancels). **LOAD** puts on every pick at once (and the perks it was saved
with, if they're still unlocked); **X** then **SURE?** deletes one. **SAVE CURRENT LOADOUT** asks for a name (up to 20
letters, the class and role by default; Enter saves, Esc cancels). Favourites are kept on your profile. Hidden while
continuing a saved run (its seats are fixed); **< LOADOUT** or Esc closes the drawer.

## Menu backgrounds

**Settings > Visual effects** has a **Menu background** card that switches the animated background behind the menus and
lobby. There are 28:
Spiral, Warp, Aurora, Synthwave, Bokeh, Hex Pulse, Orbits, Digital Rain, Nebula, Tides, Ember Storm, Constellations,
Radar, Blueprint, Bullet Curtain, Frost Peaks, Circuit, Event Horizon, Firefly Wood, Plotter, **Dusk Grid** (a
rank-15 reward) and seven **reactive** backgrounds that answer your pointer and clicks (frozen with STILL), all rank rewards:
**Ripples** (26: rings spread from your clicks and a soft light follows the pointer), **Boot Log** (29: a green BIOS terminal
that logs faster as you move, lights the line under the pointer and logs each click with its position), **Vector Rocks** (40:
neon asteroids drift away from a little ship on your pointer; a click fires a ring of shots that splits them) and **Life Grid**
(44: Conway's Life in amber phosphor, seeded where the pointer goes; a click launches gliders), **Phosphor Pond** (48: a dot
grid that ripples where the pointer goes, a click makes a big ring), **Vector Tunnel** (50: neon gates rushing past, the
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
| **Crate** | (With Visual effects on, crates, pots, barrels and the chest draw as glowing holograms: glowing wireframes that drop out, flicker and turn pink as they wear; off keeps the wood and clay.) Cover. Enemy shots stop at it, red aimed shots included, and each one wears it down (8 hits + 1 per 3 waves); your shots fly over it. You and enemies can't walk through it, but you can **push** it (crates and barrels slide when you lean on them: Tanks 70 px/s, Dps 50, Healers 34, enemies a crawling 3, and they walk round crates and barrels when there's any way past, shoving only when boxed in; an iron-bound crate gives at 60% of that; they stop at walls, the arena's edge and each other). Two often stand side by side as a little wall. 2 crates at wave 1, one more about every 3 waves, up to 10 |
| **Iron-bound crate** | From wave 12, about a third of the crates: twice as tough |
| **Pot** | Shoot it or walk through it: it smashes into a health orb (35%) or a little XP for everyone. 1 at first, up to 5 |
| **Barrel** | Rare (from wave 3, a 25-45% chance of one a wave, sometimes two from wave 20). Any shot or blast sets it fizzing; 0.4 s later it blows up 120 px round: 40 + 10 per wave damage to enemies, 16 to players caught in it (scaled like enemy damage), smashing crates and pots and setting off other barrels. It can be pushed like a crate, fizzing or not |
| **Health barrel** | Boss fights only: a green barrel with a white cross drops in every 18-26 s (the first after 15 s; two at most), away from the boss. Shoot it or walk into it and it breaks into 2 health orbs |
| **Treasure chest** | Rare (8% of regular waves from wave 2; always on the first wave after a boss if a player has the Contraband perk). Touch it to open it: **each player gets their own item**, a card spinning in a beam of its rarity's colour that **only they can see** (uncommon to very rare from their own pool, or a 20% chance of a legendary from a boss met so far or the next one). Walk onto it to see its card, then **G** takes it or **X** throws it away. Items never despawn, and **the wave waits** until the chest is open and every item is dealt with (anyone down gets back up to go and collect) |

## Arena layouts

Each **boss cycle** (the regular waves between two bosses) gets one of six fixed floor plans, picked at random and never
the same one twice running. Boss waves, the Practice Range, Data Gates and the loadout preview are the plain open
arena. A teal chip above the wave plate names the layout. Mockups: [`Docs/Mockups/arena-layouts.html`](Docs/Mockups/arena-layouts.html);
design notes: [`Docs/ArenaLayouts.md`](Docs/ArenaLayouts.md).

- **Walls and pillars** (glowing cyan outlines with diagonal hatching, the phosphor vector look of [`Docs/Mockups/arena-layouts-digital.html`](Docs/Mockups/arena-layouts-digital.html)) are solid: you and enemies can't walk through them, and they stop **every** shot, yours
  and theirs. Crates stay as they are: they wear away under enemy fire and your shots fly over them. Each layout
  starts the cycle with its own crates; random crates, pots and barrels still turn up, clear of the walls.
  Enemies path round walls to get at you, and so do the ones heading somewhere of their own: squads keeping
  formation, the Warden's Riot Shields (herding you or marching in a Riot Line), Guards and Rioters, the Warder
  keeping behind its allies, and the wandering Spinner (its random spots stay clear of the walls). A Phantom never
  blinks into a wall.
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

After each boss (and its rewards), the host picks one of **two paths** for the five regular waves before the next
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
325 each over waves 6-10, rising to about 1000 by wave 40); your gold shows above the wave panel. After its last
wave, everyone gets a shop before the boss:

- **One sideways carousel** holds everything: your shelf, then the services. The item in the middle is big and shows what it
  changes as the green readout on the card (old ▸▸ new). Click a side card, use the arrows, the mouse wheel or the arrow
  keys to bring one to the middle, and click the middle one to buy it. The SHELF and SERVICES tabs jump to each part.
- **Your shelf** (rolled for your class): three cards, two upgrades and a legendary (a weapon mod if no legendary is left). Prices by rarity: common
  40, uncommon 60, rare 100, very rare 160, weapon mod 200, legendary 280.
- **Services:** add stock (two more cards on the shelf, none you already see: an upgrade, then a weapon mod and a
  legendary in turn; 25, then +25 each time, no limit: past 31 cards the oldest bought ones drop off to make room),
  sell an upgrade (pays half its shelf price; one stack, and abilities it gave go too), full heal (40), and a revive token (120; one at a
  time: when you go down you get back up 2 s later at half health, and it isn't game over while it's waiting).
- **Give** sends a teammate 10, 50, 100 or all of your gold. Giving to someone who's already done reopens their
  shop so they can choose what to do with it.
- **No timer:** the shop closes when everyone has clicked **Done shopping** (click again to keep shopping until the
  last player is done). After a minute the host can close it for everyone. When it closes, each player's leftover
  gold becomes their own XP (1 gold = 1 XP).

## Expanding arena

**The Campaign's and Quick Play's arena**: every new Campaign and Quick Play is played here (the **Simple Arena** modifier keeps the open arena
instead; Boss Hunts, the Practice Range and runs saved before the Campaign use the open arena). Design:
[`Docs/ExpandingArena.md`](Docs/ExpandingArena.md). It starts as the plain Classic box and grows as you play:

**Crowds on a big map.** Enemies come in within 1500 px of a player (never a straggler across the map), in the emptiest room there, so rooms you built get their share. Waves 1-10 add up to 2.4x enemies on a big map (1.1 per extra hub of floor); later waves up to 1.4x.

- **Gold coins:** every kill drops its gold as coins (5-gold and 1-gold, at most 6; a boss's or an elite's last coin carries the rest). They never despawn, spin and glint on the floor, and drift to the nearest living player within 90 px, who gets the gold.
- **Vector pickups:** a rare drop that adds vectors (a cyan square that draws its own outline; big ones have an amber ring). A normal kill drops one 1 time in 60 (+6), an elite 1 time in 8 (+12), a boss always (+12). Like coins they never despawn and drift to the nearest living player within 90 px, but only one whose stock isn't full: it waits for you to spend some. The vectors go to whoever collects it, never past their cap.
- **Gadgets (the Magnet):** a rare drop you carry in your free hand and can **give to a teammate**. A normal kill drops a Magnet 1 time in 90, an elite 1 in 10 (never a boss), while there are fewer than one per player. Walk over it with empty hands to pick it up. **Tap B** to switch it on: every coin and vector pickup on the map flies to you (ahead of anyone else's reach); tap again to switch it off and save the rest. It has **10 s of pull** in all, then it's gone. **Hold B and let go** to throw it where you aim (up to 340 px, a dotted arc shows the landing): a teammate can catch it mid-air or pick it up, with the time it has left. Going down drops it at your feet. A pill under you shows its time and keys.
- **Vectors**: every player holds a drawing budget (chip under the wave plate, "Vectors 50 / 70"): 1 vector = 1 tile of wall.
  You start with 50 and can hold 70; each cleared wave gives 8, every 8 kills gives 1, and a cleared boss wave refills
  everyone to the cap and adds 20 on top. Two cards raise the cap, in the Expanding arena only: **Deep Inkwell** (uncommon, +20,
  stacks to 4) and the milestone card **Architect** (+40 and 20 vectors now, stacks to 3). The **vendor** also sells a **Vector pack**: +20 vectors for 50 gold, as
  many as you can afford, and it can take you past the cap.
- **Building rounds**: every cleared wave opens a round (a chip "Build rooms" under the wave plate; the countdown never waits
  for it). Open it and **draw a room by its walls** (zoom the map with the **mouse wheel**, **+ / -** or the buttons under it, pan with the **middle mouse**, **FIT** resets): **left click** corners on the zoomed-out map's grid and a wall runs from each
  to the next, straight across or straight up (a slanted click makes an L); **click the first corner again** (or press **Enter**, or
  **Propose**) to close it. You don't have to close it yourself: **end a run of walls on another room's wall** and, if the map's
  own walls close off the space on one side, that space fills in as the room and goes to the vote (the green fill shows it; if both sides
  are closed off, the smaller one). You pay only for the walls you drew. **Right click** (or Esc, Backspace, **Undo**) takes the last corner back, and with none laid goes back;
  with no corner laid, press, drag and let go draws a plain box. Any shape works (L, T, U, cross, zig-zag) as long as it is **2 tiles
  wide everywhere**, at most 144 tiles of floor and 24 tiles each way, its walls don't cross, it shares 2 tiles of one wall with a room
  and overlaps nothing (a shorter touch with another room's wall is fine, it just stays wall). The map shows it green when it fits (red,
  with the reason, when not). It is built as one room, however many boxes it takes inside, and it becomes **your proposal**; a newer
  drawing replaces it. You can only propose what you can afford. A proposal costs its **new wall length**: the perimeter
  minus every wall it shares with existing rooms, so touching rooms is cheaper and a room that touches two rooms (a loop)
  cheaper again.
- **Any size**: there are no hallways, just rooms. A dragged box can be anything from **2 to 24 tiles a side**, up to **144 tiles**
  of floor (a 2 x 24 strip, a 12 x 12 hall, a 24 x 6 gallery), and every room pays its new wall the same way.
- **Side walls**: every built room grows walls out from its own outer walls, one design rolled per room
  (the same on every machine): **Fins** (short stubs, one per ~5 tiles of wall), **Baffles** (long walls from alternate sides, a
  zig-zag with a lane at each end), **Pincers** (a pair facing each other, leaving a 3-tile choke) or **T-fins** (stubs with a
  crossbar: a pocket of cover each side). They stay 2 tiles off doors and corners and off the room's middle, avoid nodes, and
  any obstacle in their way is left out. They block shots and movement like any wall. A narrow strip (3 tiles or less) gets stubs
  from alternate sides that always leave a 1.5-tile lane; a small room gets smaller ones, and plain fins when its design won't fit past a door.
- **The draw screen's layout**: the tools down the left (**Room**, **Open wall**, **Teleporter**, **Gate**, **Trap**; keys **1-5**) with a
  key to what the map's colours mean under them; the map in the middle (scroll to zoom, middle drag to pan) drawn as the real floor: each room's
  tile grid, the walls and side walls with their door gaps, and the cover (crates, barriers, pillars, the hub's floor plan), this wave's
  breaches (red, chevrons pointing in), gates (an orange laser grid, faint while open) and portals (rings joined by a dotted line), each named in the map key; on the right your vectors with a
  bar showing what the drawing would spend, the tool's three steps (the one you're on lit gold), what's wrong or what it costs, the room
  types (Plain plus the unlocked floors, two to a row; the picked one says what it does), and **Build**/**Undo** and **Done**.
- **Room kits**: a one-use modifier the team holds (up to 6; the build chip counts them). Spend one in a building round
  (**Room kits** button on the build window): pick a kit, click a plain Combat room, and it becomes a **Shop** room or a **Floor**
  room (a path's ground and rules inside that room only, and its XP bonus). One change per room, permanent; floating rooms keep their job.
  A cleared **boss** wave gives a Floor kit (a floor not on the map yet when one is left), a won **challenge room** gives a Shop kit,
  and the **Black Market** sells a Floor kit for 200 gold.
- **Minimap**: top right, a fixed-size window that follows you (centred on you, held inside the map; the whole map when it fits). Gold coins show as small gold dots on it.
  Anything beyond its edge is clipped, and teammates off the window show as dots on its edge.
- **Who drew what**: every built room has a thin line of its drawer's colour just inside its walls, and the minimap fills it in their
  colour. While a round is open the minimap and the draw screen show everyone's proposals as pulsing ghosts in their colours.
- **Free building (the default)**: the lobby's **Free building / Team vote** pair (Difficulty tab, shown with the Expanding arena, host only) sets how it's built.
  With **Free building** there is no vote: a room, wall opening or portal pair is **built the moment you place it**, you pay for it (an opening pays you), and
  anyone can build as many as they can afford in a round (the round window is just the draw screen; **Done** closes it). **Nothing can be built mid-wave**: rooms, doors and portals all wait for the break between waves. Bots still add one room a round, and only if nobody has built that round.
  The lobby's **Team purse** toggle (any building mode, off by default) lets a build spend the whole team's vectors: the builder pays first, then the richest teammates; the chip and draw screen show the team total.
  **Team vote** keeps the rules below (proposals, one build a round, nothing mid-wave) for lobbies of strangers.
- **The vote** (Team vote only): each player backs one proposal or "No room". Drawing a room doesn't cast your vote: until you click a card your
  own proposal counts for you (the round never ends just because the last player drew). **The moment one choice is backed by more than
  half the team its room is built** (no waiting for the wave). Otherwise, when everyone has clicked a vote, or the
  next wave starts, **the most votes wins, a tie (a 50/50) is a random pick** (with "No room" as one of the
  tied options), and the winning drawer pays; nobody else does. One room is built a round, with a door to every room it
  shares a door's width of wall with (loops). Paths are off in this arena.
  **Solo players skip the vote**: your proposal is built the moment you make it.
- **Shop and vendor rooms, chest nodes and the Gatekeeper room**: every boss cycle the game places a **shop room** and a **vendor room** (tiny
  4 x 4 tile squares) and a **Gatekeeper** room (8 x 6) as sealed boxes 4 to 8 tiles off the map, plus a **chest node**: a point 10 to 16 tiles straight
  out (at least 8 tiles from other nodes). Build a room or hallway that shares 2 tiles of wall with a sealed room and its door opens: walk in to shop
  (the shop's stall or the vendor's pedestal stands in the middle). Sealed rooms don't count towards the 24-room cap and add no enemies; at most 6 wait.
  A chest node is locked until you build a **room** that encloses it (a normal combat room with enemies); a claimed chest node holds a real treasure chest (the same as the rare wave chests): touch it and every
  player gets their own item. It opens once and never holds the wave up (opened ones lie dim with the lid up). No cover stands on a node. Nothing respawns: new ones
  appear around the current map. Bots build up to the shop room first, then the vendor, then chests, then the Gatekeeper (they don't walk to chests' loot).
  Old saves with shop or vendor nodes still work: a room round one becomes that shop.
- **Relic nodes**: every other boss cycle (the first, third, ...) also places a **relic node** (an amber diamond). Build a room round it and walk up to
  it: **every player** gets its relic in their **relic slot** (one each; a new relic replaces the old, and it stays for the run, saves included). Relics are
  separate from the bosses' weapon mods. The HUD shows yours as a **RELIC** chip above the wave panel. So far there is one:
  - **Wall Bounce**: your shots bounce off walls and pillars once instead of stopping, keeping 70% of their damage (homing turns off after the bounce).
    Enemy shots, grenades and other lobbed shots, stuck shuriken and Bone Spears don't bounce.
- **Rule nodes** (in progress, Docs/RuleRooms.md): every boss cycle also places a **rule node** (an orange square, 8 x 8 tiles, off to the left or right of the
  map). Only a room that **encloses all of it** (at least 9 x 9 tiles) claims it, so it forces a big room. Each carries one rule: Dark, Frozen, Blood Moon,
  Burning, Swarm or Elite. The room you build round it takes that path's ground and rule **in that room only** (Dark: you only see about 350 px round you; Frozen: ice;
  Blood Moon: no regeneration or orbs, kills heal; Burning: fire patches; Swarm: a doubled melee crowd; Elite: a doubled all-elite crowd). Clear a wave there and it pays:
  a **chest** (Dark, Burning, Swarm, Elite), a **relic** to walk up to (Blood Moon) or **gold and vectors** at once (Frozen).
  (The Gatekeeper has its own room, below.)
- **Surprise rooms**: every time you clear a **boss**, the map gets a **free** room in one of five shapes the game picks for you: a **ring** (a block in the
  middle with a lane to run round), a **cross**, an **L hall**, a **T hall** or a **chamfer** (a box with notched corners). It goes where it closes the most loops
  (and touches a dead end), so it mends the map's shape; nobody pays. It builds in with a gold flash and scan line, a **SURPRISE ROOM** chip names it, and it stays
  gold-rimmed on the arena floor and minimap for the rest of that wave. The shapes come round in turn. It counts towards the 24-room cap and
  none is given within 2 rooms of it. ([`Docs/SurpriseRooms.md`](Docs/SurpriseRooms.md))
  **Breach events**: on about half of the breach waves from wave 7 on (so waves like 7, 15, 19, 23, ...; never the wave after a boss) one of the breaches flashes **gold** instead of red. 20 seconds into the
  wave, or when it ends if that comes first, the wall behind that breach opens onto a free surprise room. The wave's other breaches stay where they were.
  **Breach rooms**: as soon as a breach wave's breaches come up (a second and a half in) every breach left (not the gold one) opens onto a small free **Stash** (5 x 3 tiles) behind its wall. Each Stash
  holds a **vector cache** (a teal diamond marked **+10**): the first player to walk in takes **+10 vectors** (up to their cap), once. It has a block and a pillar for cover, builds in with a teal flash and
  scan line and a **STASH** chip, and stays for good (no vote, no cost; it stops within 2 rooms of the map's size cap, and a breach only opens on a wall with space behind it for a Stash). The breach itself **never closes**:
  each breach lets its share of the wave in (the wave's enemies split evenly over its breaches), then nothing more comes through it; if every breach has had its share the rest come in at the usual spots. When zoomed out, a teal edge arrow points to a cache nobody has taken.
- **Open a wall**: on the draw screen, switch to **Open wall** and click both ends of a stretch of a wall two rooms share (the cyan stretches; the outer wall can't be
  opened, nor a sealed floating room's). It goes to the vote like a room. If it wins, that stretch of wall comes down (a short stretch is a door, a long
  one joins the rooms) and its proposer gets **1 vector back for every tile that was wall** (up to their cap). At least 2 tiles, up to 40 openings.
- **Teleporters (portals)**: buy a **Teleporter pair** (150 gold) at the **Black Market** (the vendor room), then on the draw screen switch to
  **Teleporter** and click a spot in one room, then a spot in another: the pair is **built at once** from your stash (no vote, no vectors). Step
  into one end and you come out of the other (enemies use them too). It must save at least 6 tiles of walking. Each end sits inside a connected
  room, 90 px off its walls, clear of obstacles, 150 px from a door and 250 px from another portal. At most **3 pairs**, not counted as rooms.
  Bots don't build them.
- **Black Market traps (placeholders)**: the Black Market also sells a **Spike trap** (60), **Landmine** (80) and **Slow field** (90). Switch the
  draw screen to **Trap**, pick one from your stash and click a spot in a room (90 px off walls, 150 px from doors, 120 px from portals and other
  traps; at most 12). They stand on the map and have **no effect yet**. A stash holds at most 5 of a kind.
- **Shutter gates**: on the draw screen, switch to **Gate** and click a door (the gold squares): a gate goes to the vote like a room,
  for **8 vectors**. Once fitted, stand within 150 px of it and press **G** (the Take item key) to shut it: a red laser grid fills the doorway
  for **8 s**, a wall to everyone (players and enemies); shots still pass. Then it needs **30 s** to recharge (an arc fills round its
  posts). Anyone on the team can work it. Enemies (and bots) route round a shut gate through another door; with no other way in, they wait at it. Plain doors only (not an opened wall), one gate a door, at most **6** on the map. A red bar marks
  each on the minimap, bright while shut.
- **Themed rooms**: pick a theme under the map when you draw a room: a path's ground and rules (Swarm Fields, Sniper Ridge, Elite Gauntlet, Burning
  Grounds, Sanctuary, Frozen Lake, Blood Moon) inside that room only, with its kill bonus. A theme costs extra vectors (+3 / +6 / +10 for
  danger 1 / 2 / 3) and unlocks with bosses: danger 2 after the first boss, danger 3 after the second.
- **Gatekeeper room**: once a built room opens it, walking in during a wave wakes **The Gatekeeper** and the room locks behind you until it falls.
  Beating it gives everyone a prize pick (sometimes a legendary) and the room becomes an ordinary one. It's optional: if the wave ends first, the
  Gatekeeper goes, you're let out, and walking in later starts a fresh duel.
- **Shop and vendor rooms**: with one connected, kills drop gold and the shop opens every intermission without holding the countdown. Buy
  upgrades and add stock in the **shop room**; heal, revive and remove upgrades, and buy buildables, at the **Black Market** (the vendor room: a Buildables tab and a Services tab). Gold and your shelf carry over between waves
  (the shelf is new each boss cycle).
- **Breaches**: a rare event: on wave 3 and every 4th wave after it (7, 11, 15, ...), once the first room is built, enemies come in through **breaches** in the outer wall: a stretch of wall cracks open
  and a **red holographic barrier** stands in the gap (you can't go out through it; red chevrons on the floor point where they come
  from, and a red dash marks each on the minimap). 2 open on a breach wave, one more per 3 rooms built (at most 6), 400 px apart or
  more, picked by the wave and the map so everyone sees the same ones. Every 100 px of outer wall is a spot, so long outer walls
  break more often, and a **dead end** (a room with one door) more still: compact builds and loops have fewer places to be hit
  from. **Breach warning:** when the wave starts, before anything spawns, everyone's camera flies to each breach in turn (0.5 s),
  holds while it flashes with red rings (0.6 s; a siren, and a red BREACH 1/3 band at the top), then flies back to you (0.8 s), with
  letterbox bars while it's away; you can't move, shoot or use abilities until it's over (about 3 s for 2 breaches, 7.7 s for 6).
  A breach wave brings **three times** the wave's enemies (bosses not tripled), with room for 1.5x as many alive at once. An enemy
  uses a breach at least 350 px from every player when there is one. On every other regular wave enemies simply appear at a random spot in the rooms, at least 350 px from every player. Never on a sealed room, a shop, vendor or Gatekeeper room, or on boss waves (the fixed boss arena).
- **Obstacles**: a built room comes with obstacles and bars of wall (pillars, bars, an L, blocks: one per ~220,000 px2, up to 4),
  laid out from the room's own box so every machine builds the same ones. They keep off the room's walls, its doors and its middle,
  so you can always walk round them; a room 3 tiles wide or less gets a pillar or block every ~500 px, alternating sides with a 180 px lane past each, none near a door or bend. A small or narrow room with doors at both ends still gets at least one
  (pillar or block, closer to the walls and doors), and a 400 px deep room between two doors gets a small pillar to one side.
- **How it plays**: regular waves use the whole map (walled, no wrapping, a minimap top right) and bring more enemies the
  bigger it is (up to 1.4 times); the map stops at 24 rooms. Boss waves are fought in the hub alone, as always.
- Runs in this arena are saved like any other: Continue brings back the map (rooms, opened walls, portals, floating
  rooms), the team's room kits and each seat's vectors and shop shelf. A room someone drew keeps their colour for
  whoever takes their seat.

## Rooms arena

A **prototype**, kept for building generated room layouts later: the host turns it on for the next runs with **Rooms arena
prototype** in the F1 dev dock (Labs, in the lobby); otherwise the Campaign is the Expanding arena. It is still being tuned (design: [`Docs/Rooms.md`](Docs/Rooms.md), mockup:
[`Docs/Mockups/rooms.html`](Docs/Mockups/rooms.html)). What's in so far (phase 1, the map **Wings**):

- On **regular waves** the arena grows: the middle (the hub, today's 1600 x 900 arena) gets a big **room** on each
  side (900 x 900), and a **hallway** (260 px wide) runs above and below the hub, joining the two rooms into a loop
  round it. **Doors** (160 px gaps with cyan brackets) join them: one from the hub into each room, and one from each
  room into each hallway. The hub's top and bottom walls have no door.
- **Boss waves** are the hub alone (bosses are built for it). Before a boss wave, anyone out in a room or
  hallway is **beamed into the hub**: a pillar of light in their colour takes them up where they stand, a second one
  sets them down on a ring that shows in the hub from the start (about 2 s; they can't move or shoot meanwhile, and the
  boss wave waits for them; the between-waves box, the shop screen and the build window step aside so everyone sees it).
  The props out there are cleared away when it starts.
- **No wrapping**: the world has walls all round. The hub keeps the boss cycle's arena layout (walls, pillars and
  crates; its edge gates make way for the doors), and the rooms and hallways have a few pillars and short walls of
  their own. Walls stop walking and every shot, as in the layouts.
- **One open map.** You and the enemies go anywhere through the doors; there's no locking and no room-by-room
  camera. **The camera follows you** over the whole map. **Room rounds start zoomed in (1.5x)** with a "SCROLL ▼ TO
  ZOOM OUT" prompt low in the middle until you scroll out (the zoom goes from 2x in to 1x out, one hub-sized screen).
  Arrows at the edge of the screen point at enemies and teammates you can't see.
- **Shortcuts.** The hub has a door into each hallway (top and bottom, in the middle) as well as the two room doors,
  and **wraparound portals** join the far ends of the map: the West room's far wall with the East room's, and each
  hallway's two ends (each pair a swirling ring in its own colour, purple, teal or pink, with chevrons pointing the way
  out; rings on the minimap too). Step into one and you come out of its partner on the far side; enemies use them too,
  whenever that's the shorter way to you.
- **Enemies come in anywhere.** Each enemy (and each squad) appears at a random spot on the map, in the hub, a room or
  a hallway (picked by size), clear of walls, never in the vault and never within 350 px of a player, then makes its
  way to you through the doors.
- **Rooms waves hit harder**: while the rooms are up, everything that hurts you hits **1.3x** as hard (fewer
  threats at once, so each one counts). Boss waves and the Classic arena are unchanged.
- **The vault**: one regular wave in each stretch of ten (never before the first boss: waves 6-10, 12-20, 22-30, ...) opens a small gold room off the top hallway with **The
  Gatekeeper** in it (his Data Gate duel moved here: in the Rooms arena he's never in a Data Gate). He stays in the
  vault, and the vault locks behind you until he falls. Beat him and everyone gets a **prize pick** (rare or better,
  sometimes a legendary), as from his old Data Gate. It's optional: the wave ends without him (he goes with it), and
  the wave carries on outside while you're in there. His boss bar shows once you're at the vault.
- **The minimap** (top right): the hub, rooms, hallways and the vault (gold), the doors, enemies (the vault's
  Gatekeeper big and orange), the players, and the box your screen shows.
- (The room-by-room version from before, with nests that kept each room stocked, Runner spills, doors that locked
  behind you and enemies overflowing out of the rooms, is parked behind a switch, `Rooms.RoomByRoom`, off.)
- Enemies make their way through the doors and round every wall. The F1 "Bot plays for you" bot walks through doors
  too.
- Data Gates open in the hub. The rooms aren't used with **Tiny Arena** or on the **Narrow Pass** path (they wall the
  hub in themselves), in Boss Hunts, the Practice Range or the Showcase.

Measured with two Good bots over waves 1-24 (8 seeds, `Tools/SimTest/run.sh 1 1 arenabal`), Rooms against Classic:
about 4 threats in a 1x view at a time instead of 10, a third of the enemy shots on screen, waves about 45 s instead of
30, and (with the 1.3x) about 1.7x the damage taken per wave. Still to tune by playing it. Next: the power-stacking nerf
pass (phase 4). Classic stays as it is.

## Data Gates

Once in every five waves a **Data Gate** opens during the break: a dark disc of scrolling data in the challenge's colour, ringed by turning dashes, with a ring of pips that lights as the party steps in. It never opens under a wall, pillar or crate: after the 1st, 2nd or 3rd wave of each stretch
(in the first stretch after wave 2 or 3), so never right before a boss. A card over it names the challenge on the
other side. Everyone alive stands in it to go through: it fills faster the more of you are in it, but only goes
when you all are, and not while anyone still has upgrade cards to pick (it drains until they have). The break's countdown waits while it's open; ignore it for 15 s (counting only while nobody's
choosing cards) and it closes, and the run carries on as usual.

Through the Data Gate is a **challenge**: the same arena with its own ground (a tint in the challenge's colour, a
rune circle, glyphs round the walls). A title card names the challenge and what it takes, and 3 s later it starts,
with an objective plate at the top of the screen:

| Challenge | Goal |
|---|---|
| **Defend the Crystal** | Keep the crystal in the middle alive for 40 s. Its enemies are mostly melee (Swarmers, Spikers, Lungers, Brutes; about 1 in 7 is a Grunt or Sniper), so it's about cutting off the bodies rushing it: two in three go for it (their hits and shots wear it down); its health shows as a ring round it. 600 health, +30% for each extra player. |
| **The Gatekeeper** (Classic arena only; in the Rooms arena he's in the vault) | Beat a mini-boss with its own boss bar: a ring of big orbs with a gap, three aimed fans, and a charge along a red line that ends in a burst. Below half health it attacks a third faster and rings twice. It has 65% of the Warden's health at your wave. |
| **Kill Rush** | Kill 40 enemies in 40 s (+14 for each extra player). Weak enemies (half health) pour in from every side. |
| **Hold the Zones** | Stand in each of three zones in turn until it fills (8 s; half as fast with an enemy in it, and it drains slowly if you leave) within 60 s. Half the enemies crowd the zone. |
| **Mirror Match** | A shadow of each player: their class and weapon in dark violet. Each hunts its own player, keeps the distance their weapon wants, sidesteps every few seconds and fires their way (a fan as wide as their extra shots, or a slash up close). Each has 20% of the health a Warden would have against one player alone, however many of you are playing (each of you fights your own). |

**Win** and the room's enemies vanish and an **altar** rises in the middle: touch it (or wait 12 s) and everyone gets
a **Challenge prize** pick: three cards, each rare or very rare (no rerolls), and a 1 in 4 chance of a legendary
pick too. **Lose** (the crystal breaks, the clock runs out, or everyone goes down) and you're simply sent back:
it's safe to fail, the downed get back up at 30% health and nothing else is lost. Either way the break then starts
over. Developer tools (F1, Match) can take
everyone straight into any of the five.

## Run modifiers

The lobby's **Modifiers** tab lists 19 switches (tiles in two columns, each with an icon, its name, what it does and an ON/OFF key like Ready's, grouped under **Easier**, **Risky** and **Harder**
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
| Simple Arena | The open arena with its floor plans all run, instead of the Campaign's Expanding arena: no building rooms, and a path offer after each boss | Risky |
| Second Wind | Once per boss cycle (waves 1-5, 6-10, ...), each player who goes down gets back up by themselves 2 s later at half health; it's used before a revive token. The HUD chip shows whether yours is ready. While anyone holds one, everyone being down isn't game over | Easier |
| Zombie Infection | Every regular wave is a horde of green zombies, and the arena turns dark green. The count is the wave's usual x1.5 at wave 1, growing to x3 by wave 20, at most 60 alive at once (the rest come in as others fall), arriving faster so a bigger wave doesn't take longer; each zombie's XP is split so levels keep pace. **Seven kinds**: **Shambler** (wide triangle, slow, hits hard; from wave 1), **Runner** (narrow triangle, faster than you, packs of three; 3), **Spitter** (pentagon; hangs back and lobs acid that burns 10/s and slows 30%; 6, at most 4 a wave), **Bloater** (swelling circle; bursts into a spore cloud that slows 40%, on you if it reaches you; 8), **Crawler** (trapezoid; grabs and roots you for 1.5 s, 6/s, until you shoot it for 25; 9, at most 3), **Shrieker** (diamond; screams every 6 s, frenzying the zombies within 320 px: 30% faster for 4 s; 12, at most 2), **Mender** (cross; heals the zombies within 220 px by 15% every 3 s; 15, at most 2). **The dead rise:** a killed zombie has a 25% chance to get back up 3 s later (a green marker where it lies) at half health, worth no XP, and it won't rise twice; near a Mender it's 50%, in half the time. One that was burning or cut down by a melee weapon stays down (a Bloater never rises), and the wave isn't over until they're up and beaten. **Sounds:** a groan now and then from the horde, a wet rasp as the dead get back up, a spit, a grab, the Shrieker's scream, a Bloater's burst and a Mender's chime (volume: Enemies). Bosses, their adds and challenge rooms are unchanged. Not in the Practice Range (the zombies are in its picker, though) | Harder |

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
| Enemy speed | 1.1x | **1.1x** | 1.21x | 1.32x | 0.5-2x |
| Elites (chance, from wave 12) | 0.5x | **1x** (up to 35%) | 1.5x | 2x | 0-3x (at most 90%) |
| Bullet budget | 0.85x | **1x** | 1.15x | 1.3x | 0.5-2x |
| Adds' fire in boss fights (share of the budget) | 40% | **60%** | 75% | 90% | 0-100% |
| Regeneration | 0.5 HP/s | **0.25 HP/s** | 0.15 HP/s | 0 | 0-3 HP/s |
| Health orb drops (small) | 18% | **12%** | 9% | 6% | 0-50% |
| Revive time | 3 s | **4 s** | 5 s | 6 s | 1-10 s |
| Hit grace (after a hit) | 0.35 s | **0.25 s** | 0.2 s | 0.15 s | 0.05-1 s |
| Lives (retries from the last boss's checkpoint) | 3 | **2** | 1 | 0 | 0-5 |
| Boss approach (the themed share of the wave before a boss) | 85% | **85%** | 85% | 85% | 0-100% |
| Approach ramp (build up over the four waves) | On | **On** | On | On | On / Off |

The two **boss approach** numbers are flavour, not difficulty: they're the same on every preset, changing them doesn't
make the difficulty Custom, and picking a preset keeps them. **Blend** and **Fully themed** buttons under them set
both at once (60% with the ramp; 100% without).
The arena isn't picked here any more: the Campaign is the Expanding arena, the **Simple Arena** modifier keeps the open
arena with its floor plans (always on), and the Rooms arena is a prototype in the dev dock. A run saved before the
Campaign keeps the arena it was saved with.

Easy is close to how the game played before difficulties (without the per-wave damage growth). Damage growth
multiplies with Enemy damage: Nightmare at wave 11 hits 1.5 × 1.5 = 2.25x as hard as a listed shot. Modifiers stack
on top (Tough Crowd, Frenzy, Swift Foes, Glass Cannon...).

## Quick Play

**The lobby's first mode, and where a new profile starts.** In the lobby the host picks **Quick Play**, **Campaign**, **Boss Hunt** or
**Practice** on the mode carousel; the lobby opens next time on the mode the host last picked (a new profile: Quick Play).
Picking it turns the tab carousel to the lobby's **Quick Play** tab: the run's shape (ten waves, the boss, endless), its rules, your Quick Play
records (played, won, best wave, bosses), and **the boss pool**: a tile for each of the ten bosses, with its model and name once
you've beaten it anywhere, a **???** until then (on your first Quick Play the Warden shows, marked FIRST).

- **Ten waves, then a random boss** as its own fight after wave 10 (like the Campaign's boss after a wave: it doesn't use up a
  wave number). The HUD counts the waves n / 10. **Your first Quick Play always ends on the Warden**; after that the boss is
  random: every boss comes up once before any repeats, and never the same one twice in a row.
- **The waves before a boss are its own enemies** (its faction, as in a Boss Hunt: tougher members joining as they go, its set
  piece on the fifth and tenth wave). Bosses without a faction yet (all but the Warden) get the usual mixed waves.
- **Bosses play exactly as anywhere else.** Only their health changes: about a **two-minute fight** for a typical build at
  whatever wave they come (a late boss after wave 10 has much less health than at its own Campaign wave; an early one in
  endless has more). The boost for strong builds still applies on top.
- **Win, then end or keep going.** Beating the boss is **QUICK PLAY CLEAR!**; the host can **Keep going [C]** into endless: ten
  more waves and a new random boss, after wave 20, 30, ... for as long as the party lasts.
- **Room building:** Quick Play is played on the **Expanding arena** (below), as the Campaign is: vectors, a build round after
  every wave from wave 1, the vote, and a free surprise room after each boss. Vectors come in at the full rate (+8 a wave, not
  the Campaign's half), so a party builds about one room a wave. The **Simple Arena** modifier keeps the open arena.
- **The tutorial:** a profile's first Quick Play shows a prompt at the top left that ticks off as you go, in five lessons:
  **The basics** (move, aim, shoot, dodge), **Level up** (pick a card), **Build a room** (click BUILD, draw a room,
  vote it in: shown as soon as the first build round opens), **Your kit** (your ability, the parry shield: shown in the next
  wave) and **Gold and the shop** (collect gold, walk into a shop or
  vendor room once one is connected). Steps count in any order; each shows your own key. **SKIP** ends it. It's only on your
  own screen (a friend's first run shows theirs), never holds the wave, and never comes back once done, skipped, or once your
  first Quick Play is over.
- **No paths, Data Gates, lives or saving.**
- **Records:** full rank XP and Cores, bosses count as beaten (unlocks and Boss Hunts), and Quick Play keeps its own plays,
  wins, best wave and bosses beaten (not counted as runs). Falling in endless adds the stretch after the win.
  Plan: `Docs/QuickPlay.md`.

## Boss Hunt

In the lobby the host picks **Quick Play**, **Campaign**, **Boss Hunt** or **Practice** on the mode carousel at the top (arrows, the mouse wheel or a click on a side mode turn it). Over the big screen, a fixed **Loadout** key sits on the left, and the other tabs (the mode's own, Difficulty, Modifiers) are a carousel beside it: the open tab in the middle, the rest wrapping round either side. **Perks** open from the **Perks** key right of your class name on the loadout (it shows your equipped perk and slots used, e.g. 1/2); "< Loadout" or the Loadout key takes you back (in the Practice Range too, where you can set them up but the range plays without them). The lobby opens on the Loadout, and picking a mode keeps it there; the carousel turns to that mode's own tab (Quick Play, Boss hunt, Practice) for a click. A Boss Hunt is a short run that ends
with one boss: **ten waves**. Waves 1-9 bring only the boss's own enemies (its faction: for the Warden, Rioters and
Guards, then Riot Shields, Gas Grenadiers, Security Guards and Taser Guards, its Breakout, Patrol and Cell block squads, and its Riot line on waves 5 and
9); wave 10 is the boss, at wave-10 strength. Beat it and the hunt is won (**HUNT COMPLETE**); there's no endless, no
path offer, no Data Gate, and a hunt isn't saved to continue. Modifiers and the difficulty apply as in a run,
and so does the boss's health boost for strong builds. The HUD and the Tab screen count the hunt's waves (n / 10).

The **Boss hunt** tab is a dossier: a looping drum of the ten bosses (status tag: SOON, LOCKED, OPEN or WON xN; mouse wheel, PREV / NEXT or a click rolls one to the top, and the list never ends). A boss you've never beaten is a **???** row with a "?" for its portrait (and a "?" in the scope), as in the Quick Play tab's boss pool. The boss on top is the picked one: the host can scroll onto a locked or coming-soon boss to look at it, but START stays off until the top boss's hunt is open. For the top boss it shows a green-edged target scope with its live model, the wave ladder, your hunts won, best time and the hunt's rules. A boss's hunt **opens once you've beaten it in a
run** (the host's records decide, in co-op); the others say Locked or Coming soon, and the match won't start on a
locked one. **Only the Warden has a hunt so far**: a hunt's build-up waves need the boss's faction (only the
Warden has one yet), and later bosses will need the party handed a build that fits their strength. Records keep each hunt's plays, wins and best time per player
(not counted as runs). Winning a hunt unlocks that boss's perk (below). Plan: `Docs/BossHuntsAndPerks.md`.

## Perks

A perk is a rule you bring into a run, one per boss, unlocked by winning that boss's **Boss Hunt**. Pick them on the lobby's
**Perks** view (the loadout's Perks key): your **slots** on the left (one to start, a second after you win a full run, a third after you win five different
hunts), and a card for each perk on the right. Click an unlocked card to equip it, click an equipped card (or the slot's
*remove*) to take it off, and hover a card for the rules. A second perk from the same boss takes the first one's slot. Everyone
brings their own perks in co-op, and each seat shows its perk icons on its party card. Perks are off in the Practice Range, the
Boss Lab and the lobby preview. A saved run keeps each seat's perks, and when you continue it the view is read-only and shows
what the seat was saved with. Equipped perks are saved with your profile.

| Perk | Boss | Unlocked by | Effect |
|---|---|---|---|
| **Riot Gear** | The Warden | winning the Warden Hunt | A riot-shield plate blocks the next hit that would hurt you: no health lost, no damage counted, and half a second of invulnerability. Then the shield takes **30 s** to come back (the timer keeps running between waves and while you're downed). You start every run with it ready. It blocks one hit from an enemy: a shot, a swing, a blast, a boss slam. **Burning, gas, acid, lava and other damage-over-time ticks go through** and don't spend it. |
| **Contraband** | The Warden | winning the Warden Hunt on the Hard or Nightmare preset | **A treasure chest always turns up on the first wave after each boss** (waves 6, 11, 16...) when anyone in the party has it, on top of the random ones (not in Boss Hunts). **Your own item from any chest is rare or better** (never uncommon); teammates still get their own normal item from the same chest. See `Docs/Contraband.md`. |

The plate hovers at your side and swings toward the nearest incoming enemy shot (resting behind you otherwise). While the shield
recharges a ghost plate fills along its length, and it pops back when ready. A block flashes the plate white, throws sparks off
the impact and breaks the plate into three pieces that fly off (with visual effects reduced or off, only the flash). A small
**Riot Gear** chip above the ability bar's XP strip fills from the bottom and shows the seconds left, then READY. Teammates' plates
show too. A boss's perks share one slot, so equipping Contraband takes Riot Gear's place. Other bosses' perks come later: the view shows them as locked placeholders. Runs don't yet note which perks were used (a
"no perks" record variant is planned). Plan and build notes: `Docs/RiotGear.md`, `Docs/BossHuntsAndPerks.md`.

## Practice Range

In the lobby the host picks **Campaign** (100 waves in the Expanding arena), **Boss Hunt** (above) or **Practice** (the Practice Range)
at the top. Practice has two sections, as tabs on the big screen: **Practice** (build your own waves) and **Training**
(short mechanic drills). Whichever you use, it's with everyone in the lobby, with upgrade picks between waves as usual, and
**no XP counts**: a Practice run never reaches your profile (no rank XP, records, unlocks or run history), and the header
says `NO XP · RESPAWN ON`. (Level-ups inside the run still give their upgrade picks.)

**Respawn.** There's no game over in the Practice Range. When you go down, a **DOWNED** window opens in the middle of the
screen and the wave keeps running behind it:

- **RESPAWN HERE** (**R**): you're back on your feet where you fell, at full health, with the usual 1.5 s of invulnerability.
  Each player respawns on their own; teammates can still revive you instead.
- **RESTART WAVE** (the host's; **Enter**): clears the arena and starts this wave again after a short countdown, with
  everyone on their feet at full health.
- **BACK TO LOBBY** (the host's): everyone beams out, as from the pause menu.

### Practice tab

- **Waves** (left): a rack of cards, each with its number, level and what's in it (**+ Add**, **Copy**, **Remove**). The
  **level** (1-50) sets how strong its enemies are: their health and fire rate as on that wave of a normal run.
  Tick **Loop the waves** to go round again after the last one; otherwise the run ends with **PRACTICE COMPLETE**.
- **Enemies:** every one of the 17 regular enemies and 10 bosses is shown moving, as in a match, so you don't
  need to know their names. Click one to add it to the selected wave, right-click (or its corner **-**) to take
  one away; hovering shows what it does. Bosses spawn first and bring their usual adds; the Twin Reapers come
  as a pair.
- **Shared:** everyone in the lobby sees the waves change as they're edited, with the editor's name on the wave
  they're working on (players who can't edit follow along to it). The host can switch on **Let others edit** so
  friends can change the waves too.
- Everyone keeps their own loadout (the **Loadout** tab).
- **Save** writes the setup to a text file named after the name box, in the `practice` folder next to the
  sessions folder (**Folder** shows it). **Load** lists saved setups. The files are easy to edit by hand:

  ```
  # Top-Down Arena practice setup: warden drills
  loop: no
  wave 5: Grunt x4, Sniper x2
  wave 10: The Warden x1, Medic x2
  ```

  One line per wave: `wave <level>:` then each enemy as `<name> x<count>` (the count defaults to 1). Names
  ignore case and "The". Unknown names are skipped and listed when you load.

### Training tab

Small, one-mechanic drills, laid out as cartridges in rows by group (**Move & Dodge**, **Weapons**, **Abilities**,
**Enemies**, **Co-op**; four slots each, empty ones dashed). Click a drill to see its goal and steps, then **Load this drill**
(the host's): its waves go into the Practice setup (so everyone sees them on the Practice tab), and Ready and Start play it
with the rules above. This is groundwork: the built-in list has one placeholder drill marked **EXAMPLE**. Drills are
`.training` text files: put them in the `training` folder (next to `practice`; **Folder** opens it, **Reload** re-reads it).
The file is a Practice setup with a few extra lines first, and is documented in [`Docs/Training.md`](Docs/Training.md).

## Controls

| | |
|---|---|
| Move | WASD / arrow keys |
| Aim | Mouse |
| Dodge | Ctrl, or double tap a movement key (changeable). A tiny 95 px dash with 0.22 s of invulnerability; 1.1 s cooldown; a soft cloth swish plays. With no direction held it steps back from your aim |
| Shoot | Hold left click (changeable) |
| Role ability | Space |
| Parry | Hold right click (changeable) |
| Weapon secondary (Kunai blink, Twin Fang, Sunlance, Hook & Haul, Shuriken recall, Bo Staff shove, Kusarigama whirl) | Left Alt |
| Ultimate | F |
| Take / discard your chest item (standing on it) | G / X |
| Gadget: tap to switch on / off, hold and let go to throw it where you aim | B |
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
| Zoom in / out | Mouse scroll wheel during a match: up zooms in on you (up to 2x, the view follows you and stays inside the arena), down zooms back out to the whole arena. Eases back out when a run ends. While zoomed, arrows at the edge of the view point to everything off-screen: teammates in their colour with their name (pulsing red when downed), enemies in red (elites orange, bosses a big crimson arrow with a ring), and any unopened treasure chest in a pulsing gold arrow with a ring (also a gold square on the Rooms minimap); nearer ones are bigger. Settings → Gameplay → Scroll-wheel zoom turns it off (on by default) |
| Teammate shots 25–100% | Settings → Gameplay (100% by default). How solid other players' bullets are drawn, so a crowd of them doesn't hide the fight; yours and the enemies' stay full. Local only, saved on your machine |
| Aim line on / off | V (the line runs from you to the crosshair, but never past your weapon's range: as far as your shots fly, or your swing reaches; a small cap marks the end when the crosshair is beyond it) |
| Hide / show the HUD | H, or Settings > Gameplay; the mode is remembered between launches (H cycles four modes: Full, Corner text (CRT phosphor readouts in bracket corners: health with a 20-cell bar bottom-left (teammates' health in slim rows above it), each ability key over its seconds left (the ult's charge %) and its name at the bottom, the wave and enemies left bottom-right, so the minimap keeps the top right), On the player (a leader line from you to a small CRT tag with your health, a cell bar and a row per ability: its key, its name and its seconds left, green when ready, the ult's charge in gold; it switches sides near the left wall and the top, and hides while you're downed; the wave and enemies left at the top), Hidden; it slides away and fades, "Press H to show the HUD" stays faintly at the bottom while it's hidden, and a slim health bar for each boss stays at the top in every mode but Full; also a Settings choice) |
| Aim debug overlay | F3 |
| Lag chart: off / pill / chart; freeze it; copy a summary | F5; Shift+F5; Ctrl+F5 (also Settings → Gameplay; see *Lag chart*, under *Playing with a friend*) |
| DPS meter on / off (host); reset it | F2; Shift+F2 (see *DPS meter and target dummy*, below) |
| Open the folder with `Player.log` (menus, lobby, results; in a run F2 is the DPS meter) | F2 |
| Freeze time with no menu (dev) | P, once the F1 Tools tab's freeze-time key is on; everything holds, attacks and effects mid-swing included; P again resumes. Pausing is shared, so it stops everyone |
| After game over / victory (host) | R = play again, L = back to lobby, C = keep going (endless, after victory) |

### Keybinds

**Settings → Keybinds** (main menu, lobby and pause menu) lists every control: the four move keys, the role
ability, the Ultimate, the three bonus ability keys, **Fire** and **Parry** (left and right click by default; each can
be any key or mouse button), the weapon secondary (Left Alt), take and discard a chest item, reroll, and the aim line,
HUD and mute toggles. For Fire and Parry, a "Left click / Right click" pair of buttons appears while you're picking
(the page can't take a click as a key press, since the click is how you chose the row). Left and right click can't go on
any other action, since they click everything else. Click a key, press the new one (a middle or side mouse button works too), and it
changes at once; **Esc** cancels. A key already used by another action swaps with it, so nothing ends up on two. Every action also has a **second key**
(the "+ ADD" slot beside it): either key does the action; right click a second key to clear it. The
arrow keys always move too. The ↺ beside a changed key puts it back; **Reset all** puts every key back. Saved on this computer. The menu keys
(Esc, Tab, Enter, F1-F5) can't be changed, and menus are always clicked with the mouse. The number keys can be bound:
while the upgrade picker is open, the numbers of its cards pick a card instead, and while the emote bar is open, the
numbers of its slots play an emote instead (an action on one of those keys waits until it closes). The HUD, the Tab screen and the
How to play page show your own keys.

## Launching a run

When a run starts it drops you in, all within the 3 s before wave 1 (on your own screen only): the lobby's two sides
slide off left and right; a ripple runs out from the middle of the arena, bending its grid; each player beams down as
a pillar of light in their colour, a quarter second apart, landing with a ring and their class icon flashing above
them; the HUD slides in; GET READY counts down on a CRT scan band (a full-width strip with a countdown bar; gold on boss waves); and the band flashes to **WAVE 1 STARTED** as the fight starts. The same band shows between waves. You can't move,
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
A **Squad** row adds them up. The box has **three steps** (tabs along its top: **Results**, **Build**, **Ready**), in order:
**Results** (the numbers above, plus your vectors and rooms when a building round is open), then **Build** (**Continue · Build**
opens the Expanding arena's build window; its **Done** leads on to Ready; the tab is left out when there is no round: Practice, hunts, the last wave,
a full map, other arenas), then **Ready**: the enemy types the next wave draws from (new ones marked NEW; a boss wave
names its boss), the **road to the next boss**, the **Ready** key (Y), the **Auto-ready** switch, the
status and the countdown bar. Y walks the steps (Results, Build, Ready) before it readies you, and Auto-ready only counts once you reach Ready. The next wave only starts once **everyone is ready**, **3 seconds** after the last Ready, and there is no
timer while anyone is still waiting. **Auto-ready** is remembered between runs: you count as ready as soon as your upgrade
picks are done, and when everyone has it on the countdown is the usual 4 seconds, so solo play with it on moves at the
old pace (it is off by default). A small box beside each player shows who is ready (a tick, an A for auto-ready, dashed while they aren't). **Minimize** (top right of the box) folds it into a slim strip at the top so you can walk to a shop or a chest; the strip keeps the next wave, the status, Ready and **Open** (Y there readies you at once). It opens again at the next break.
**The boss gate.** In the break before a boss (Quick Play, the Campaign, a Boss Hunt) there's no Ready: a **boss gate**, a
red summoning circle with a skull in the middle, opens in the middle of the hub, and the fight starts once **every
player still standing is in it** for 1.2 s (downed players don't count). Take your time to shop, build and pick first: there's no
countdown, and Auto-ready doesn't start it. A socket under the gate lights for each player inside, a thread runs to them,
and its inner ring burns round as the team's fill rises. Its red leans toward the coming boss's colour (pink for the Warden, amber
for the Hive Queen, violet for the Singularity...). A soft low pulse hums every few seconds while it waits, a chime plays on your screen as each player steps in, and a deep gong as it
takes you. When it opens and the box isn't on your screen (shopping, building, or folded away), a toast at the top says
**THE BOSS GATE IS OPEN**, with the boss's name and how many are in, for 4 seconds. It shows as a ring on the minimap and, when you're zoomed in, as an arrow at the edge of the view. The box's last tab reads **Gate**: **Go to the gate** (Y) folds the box
away, and the status shows how many are in. Bots walk in on their own.
The first countdown of a run, the Practice Range and the dev tools' wave jump don't use ready-up. Mockup:
`Docs/Mockups/wave-cleared-ready.html` (option 1).

**Chat.** In a match, press **Enter** to open a text line at the bottom right, above your card and the teammates'
rows. Type (up to 120 characters), **Enter** sends and **Esc** cancels. While you type, movement, abilities and
the pause key are ignored. Each message is a small pill with an edge in its player's colour; lines fade away one by
one after about 6 seconds, and the box is gone when it's quiet (a line from a teammate shows it again for a moment,
without taking your keyboard). The line also closes itself after 12 seconds with no typing. The host keeps it to
about one message a second per player. Mockup: `Docs/Mockups/chat-box.html`.

## End of a run

**Checkpoints and lives.** Every boss you beat leaves a **checkpoint** (the wave box after it says **CHECKPOINT SAVED**
and the lives left). If everyone goes down and the run has a **life** left (the difficulty's Lives: Easy 3, Normal 2,
Hard 1, Nightmare 0; a hearts chip over the wave panel shows them), the GAME OVER screen says where you'd restart and the
host gets **Retry [Y]**: the run comes back exactly as it was at the checkpoint (everyone's upgrades, levels, gold, the
map), a life used and **the score cut by a quarter**. Play again, Back to lobby or Leave instead ends the run for good.
Closing the game keeps it at the checkpoint, so **Continue** retries it later (the saved-runs list shows the lives left).
Out of lives, or before the first boss, it's a plain game over. A run won without using a life is **Deathless** (on the
results screen and in Recent runs). Not in Boss Hunts or the Practice Range.

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

## Weapon variants

Each weapon has **2 variant tiers** (lobby: the button on the right of a weapon key, "Variants n/2"), bought with **Cores**, an account
currency saved in your profile. Tier I costs 10 Cores, tier II 20. The tier effects are not built yet.
Earning Cores: a won or lost **Campaign** pays 1 per boss defeated and 5 for the win (a full 100-wave win: 15); a won **Boss Hunt** pays 5;
Endless pays none. **Core Carriers** are the surprise: from wave 3, a regular enemy now and then spawns with a pulsing gold halo (one at a
time, at most 6 a run); killing it adds 1-3 Cores to the run's total, paid when the run ends and shown on the results. Design: [`Docs/Variants.md`](Docs/Variants.md).

## Rank

**Lobby profiles:** click your party card's portrait (or the card, if you are not the host) to open your profile; click a teammate's card to see their holo card, large: their frame, card background, name effect and trail, rank badge, XP, kills, best wave, bosses and what they have equipped. Esc, the close key or a click outside closes it.

A **cosmetic account rank**. Every finished run (and Boss Hunt) earns XP; XP raises your **Rank**; ranks unlock
cosmetics only, never power. It is separate from the **Level** on the results cards, which is the in-run level.
Design: [`Docs/Rank.md`](Docs/Rank.md); mockups: `Docs/Mockups/rank-screens.html`.

**XP for a run:** 20 per wave cleared, 150 per boss defeated, 40 per ally revived and 500 for a win, times
(1 + each active modifier's bonus: **Harder +25%**, **Risky +15%**, **Easier +0%**, at most x3) and the difficulty's
step (Easy x0.75, Normal x1, Hard x1.25, Nightmare x1.5, Custom x1). A hunt counts its waves and the boss, with no
win bonus. The Practice Range and runs where dev tools (F1) or the bot were used earn nothing, like Records.
**Ranks:** rank *r* to *r+1* costs 200 x *r* XP (rank 8 needs 1,600). The tiers have titles: **Recruit** (1-9),
**Challenger** (10-19), **Veteran** (20-29), **Champion** (30-39), **Legend** (40+), shown by the badge's colour (steel,
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
| 10 / 20 / 30 / 40 | Titles: Challenger, Veteran, Champion, Legend (automatic) |
| 9 | **Ember trail**: embers drift behind you as you move |
| 13 | **Steel frame**: a lit rim on your lobby party card and name chip |
| 15 | **Frost trail**: a pale chill line with ice crystals that stay where they formed, glint and melt |
| 17 | **Circuit frame**: a teal rim with bracketed corners and a pulse of light running round the edge |
| 16 | **Dusk Grid**: a 21st menu background (also pickable in Settings > Visual effects once unlocked) |
| 26 | **Ripples**: a reactive menu background (rings from your clicks, a light that follows the pointer; also pickable in Settings > Visual effects) |
| 29 | **Boot Log**: a reactive menu background (a BIOS terminal that logs your pointer and clicks) |
| 40 | **Vector Rocks**: a reactive menu background (an asteroid field with a ship on your pointer; click to fire) |
| 44 | **Life Grid**: a reactive menu background (Conway's Life in phosphor, seeded where you go; click for gliders) |
| 48 | **Phosphor Pond**: a reactive menu background (a dot grid that ripples where you go and click) |
| 50 | **Vector Tunnel**: a reactive menu background (neon gates rushing past, steered by the pointer) |
| 53 | **Signal Lost**: a reactive menu background (a test card that tears at the pointer and rolls on a click) |
| 19 | **Storm trail**: crackling static, a jagged yellow-white thread that re-rolls every few frames |
| 21 | **Ember Edge frame**: a flickering warm rim with embers lifting off the top edge |
| 23 | **Void trail**: a dark ribbon with a violet edge, motes pulled back toward you |
| 25 | **Void Pulse frame**: a violet rim with a breathing inner rim and two dark motes circling the card |
| 26 | **Gold stardust trail** |
| 31 | **Gold frame** |
| 41 | **Legend Foil frame**: gold with a bright band sweeping across the rim and twinkling corner stars |
| 36 | **Prism trail**: three ribbons cycling through the spectrum, with white glints |
| 46 | **Light Cycle trail**: a wall of light that follows where you've been but snaps to straight runs with right-angle corners; the floor tiles it crosses light up, and the wall breaks into squares that drift off as it ages (about 2.4 s). Cyan only. With Visual effects off it is one flat line |
| 51 | **Prism frame**: a rim cycling through the whole spectrum as it turns round the card |
| 56 | **Neon Tube frame**: a magenta tube with a cyan tube inside it; the outer one buzzes and drops out now and then |
| 58 | **Hazard Tape frame**: yellow and black warning tape marching round the card |
| 60 | **Frostbite frame**: an ice rim with snowflake crystals that grow and shrink, and a travelling glint |
| 62 | **Barbed Wire frame**: two twisted strands with rusty barbs |
| 64 | **Lightning Cage frame**: corner nodes with arcs of lightning jumping between them |
| 66 | **Pixel March frame**: chunky 4 px squares in four colours stepping round the card |
| 68 | **Blood Drip frame**: a crimson rim with drips forming along the bottom edge, hanging, dropping and fading |
| 70 | **Chain Link frame**: steel links running slowly round the card |
| 7 | **Dusk Grid card**: a perspective grid sliding toward you under a violet horizon (the fill of your lobby party card, behind your name) |
| 11 | **Starfield card**: three layers of drifting stars and now and then a shooting star (the fill of your lobby party card, behind your name) |
| 14 | **Blueprint card**: cyan drafting paper with a grid, a dimension line and a circle that traces itself (the fill of your lobby party card, behind your name) |
| 20 | **Ember Glow card**: a warm glow along the bottom edge with embers lifting off it (the fill of your lobby party card, behind your name) |
| 24 | **Aurora card**: slow green and violet ribbons (the fill of your lobby party card, behind your name) |
| 28 | **Circuit Board card**: dark green traces with a pulse running along each (the fill of your lobby party card, behind your name) |
| 33 | **Rain on Glass card**: cool grey-blue with thin streaks and beads running down (the fill of your lobby party card, behind your name) |
| 38 | **Void Rift card**: a slow violet vortex behind your portrait (the fill of your lobby party card, behind your name) |
| 43 | **Gilded Foil card**: brushed gold with a sheen sweeping across (the fill of your lobby party card, behind your name) |
| 54 | **Prism Shards card**: spectrum shards turning slowly and overlapping (the fill of your lobby party card, behind your name) |
| 57 | **Radar Sweep card**: a green radar scope whose beam lights the contacts it passes (the fill of your lobby party card, behind your name) |
| 59 | **Bullet Spiral card**: a five-armed spiral of slow pink and cyan bullets (the fill of your lobby party card, behind your name) |
| 61 | **Phosphor Rain card**: columns of green characters falling (the fill of your lobby party card, behind your name) |
| 63 | **Lava Veins card**: near-black rock with glowing orange veins that slide and pulse (the fill of your lobby party card, behind your name) |
| 65 | **Synthwave Sun card**: a striped sun sinking into a pink horizon over a grid floor (the fill of your lobby party card, behind your name) |
| 67 | **Snowfall card**: drifting snow at two depths on a night-blue sky (the fill of your lobby party card, behind your name) |
| 69 | **Contour Map card**: topographic rings round two drifting peaks (the fill of your lobby party card, behind your name) |
| 71 | **Honeycomb Pulse card**: a hex grid with a wave of amber light spreading through it (the fill of your lobby party card, behind your name) |
| 2 | **Wave emote**: a hand waves while you sway |
| 3 | **Thumbs up emote**: a thumb pops up, you bob |
| 4 | **GG emote** |
| 8 | **Cheer emote**: you hop, with a burst of confetti |
| 11 | **Heart emote**: small hearts floating up |
| 13 | **Laugh emote**: you shake with it |
| 16 | **Question emote** |
| 18 | **Sleep emote**: Zs drifting up while you sink |
| 22 | **Dance emote**: you sway to the beat, notes drifting off |
| 26 | **Spin emote**: two turns inside a ring of light |
| 32 | **Salute emote**: you snap upright |
| 42 | **Fireworks emote**: three bursts in turn |
| 52 | **Steam emote**: puffs of steam and a red bubble, you shake |
| 5 | **Steel Outline name**: a thin steel-blue line round white letters |
| 12 | **Frost name**: white fading to ice blue with a cold glow |
| 14 | **Ember name**: yellow fading to orange-red with sparks lifting off |
| 19 | **Neon name**: white letters with a cyan glow that breathes |
| 23 | **Wave name**: letters riding a gentle wave, cyan shading into violet |
| 28 | **Shimmer name**: silver with a white sheen gliding across |
| 34 | **Glitch name**: red and cyan fringes, and every couple of seconds a slice jumps sideways |
| 37 | **Glitchy name**: always glitching: letters jump, swap for symbols, the line splits and slides, the fringes pulse and it flickers |
| 39 | **Fire name**: yellow core to red tips, each letter flickering, flames licking up |
| 45 | **Gold Foil name**: rich gold with a sheen sweeping across |
| 49 | **Void name**: pale violet on a dark violet edge with a purple glow and circling motes |
| 55 | **Rainbow name**: every letter its own hue, turning round the spectrum |
| 72 | **Chrome name**: mirror metal with a dark horizon line and a sheen gliding across |
| 73 | **Toxic Drip name**: acid green with slime that gathers under letters, hangs and drops |
| 74 | **Hologram name**: a cyan projection with a magenta fringe, faint scanlines and a travelling bright band |
| 75 | **Nebula name**: a pink, violet and blue gradient with a drifting glow and twinkling stars |
| 76 | **Pixel name**: chunky blocks of four colours stepping along the name |
| 77 | **Terminal name**: fixed-pitch green with a blinking block cursor; every few seconds it scrambles and decodes |
| 78 | **Electric name**: warm white with a flickering blue glow and bolts jumping through the name |
| 79 | **Petals name**: soft pink with cherry-blossom petals tumbling past |

**Aim lines** change the line from your weapon to the crosshair (the V key still turns it on and off). Classic is free; Dotted Tracer (rank 6), Chevron Flow (12), Ruler (15), Ember Wisp (18), Frost Shard (22), Storm Arc (27), Pixel Beam (30), Helix (35) and Prism (47) are rank rewards. Every look stops at your weapon's range and ends in a cap when the crosshair is farther; only you see your own line.

**Cursors** change your crosshair in a match. Classic is free; Fine Cross (rank 4), Corner Sight (5), Ember Ring (12), Frost Snowflake (16), Circuit Sight (18), Storm Arc (22), Void Eye (27), Gold Diamond (32), Prism Ring (39), Light Cycle (47, it lays a corner-snapped wall of light behind the pointer), and, higher up, Radar Sweep (80), Sniper Scope (81), Chevron Snap (82), Sonar Ping (83), Pixel Plus (84), Petal Ring (85), Laser Dot (86) and Target Lock (87) are rank rewards. Each swells for a quarter second when you fire. Your equipped cursor also replaces the system pointer in the main menu and lobby (the system pointer is hidden everywhere: each look draws its own Corner pointer, a small L-bracket in the look's colours with its own flourish (Ember sparks, Storm crackle, Circuit pulse, Gold twinkle, Prism spectrum, and so on). It collapses to a dot and the crosshair swells out as a match starts, and back again when a menu, the pause screen or the end screen opens; about 0.6 s, instant with reduced effects), and the Light Cycle cursor's wall fades out in under a second. Only you see your own cursor.

**Name effects** change how your name is drawn for everyone: on the lobby party card, the Hangout name plate and the tag over your head in a match (small names keep only colour and glow, so they stay readable; with Visual effects off each is still).

**Emotes** are a short bubble over your head for 2.4 s (a gesture, a heart, GG...) that everyone sees, in the Hangout and in a match; several also move your character (a hop, a sway, a shake, two spins), the model only, never your hitbox, and not with Visual effects off. Every one your rank has is on the emote bar (hold T); nothing to equip. After one there is a 2 s pause in a match (0.75 s in the Hangout).

Everyone in the match sees your trail, and everyone in the lobby sees your rank, frame and card background; the host clamps what
players report, and only accepts a frame or trail your rank has. XP, what's equipped and the last six runs are kept in
`profile.txt` (`xp`, `equip`, `rlog` lines).

## Saved runs and records

**Runs save themselves.** The host's game saves the run as each wave starts (once everyone has picked their
upgrades, the path is chosen and the shop is closed), and again if the match is left or the game closed between
waves. Every arena saves (Classic, Rooms and Expanding, with its map). Leaving mid-wave keeps the save from that wave's start. A run that's won or lost loses its save. The
Practice Range, the lobby's loadout preview and Boss Lab fights are never saved.

**Records count the Campaign's waves** (1-100): best wave, best endless wave, Recent runs and the run charts. A profile
from before the Campaign has its waves doubled once when it loads (a 50-wave run's wave n is about the Campaign's wave
2n); Boss Hunt entries keep their ten waves. Rank XP is unchanged: it pays by bosses and progress, not the wave number.

**Play** on the main menu opens the saved-runs picker in the menu's box: a header screen (**YOUR RUNS**) with a **Back** key
beside it, and under it one scrolling screen with every save, newest first. The newest run is a tall row (wave, when,
score, time played, each seat's player and class, the path, difficulty and modifiers) with a big **Continue**, always lit; every
other run is a one-line row with its own **Continue**. Each row has a **DEL** key, dashed and quiet until the mouse is on
it (click twice: it lights red and turns into **REALLY?**); a deleted run is moved to a `deleted` folder next to the saves, not erased, so you can drag it back. Scroll with the mouse wheel or drag the scroll bar; a "more" strip at the foot of the screen counts the
runs below. **New run** is under the screen. With no saves the screen says so and offers **New run**. **Back** or Esc closes it. Continuing hosts a
lobby for that run: it shows **Continuing a saved run** instead of the mode switch, and a **Seats** tab instead of
your loadout. Everyone plays the character they had: a friend who joins takes back the seat with their name (or
the first free seat, and moves aside if its owner turns up); **Take this seat** moves you to a free one. Classes,
loadouts and modifiers can't be changed. Seats nobody takes are left out. The match starts at the next wave, with
everyone's upgrades, ability keys, levels, XP, rerolls, mastery, gold, revive token, health and run stats back,
and the run's score, time, path and route. Saves are plain text in the game's data folder (`saves/*.run`),
with upgrades and abilities by name, so they keep working as the game changes.

**Profile** (the old Records and Your look pages, merged; opened by clicking your player card on the main menu or in the lobby; the pause and lobby menus have no Profile row): the same loadout box as the pause menu's (same opening, windows and Back key): a title, a profile card (your rank badge, name with a pencil key, title and rank, XP bar), four quiet tab keys (small faded letters, the open one a small lit pill) and the open tab's options as keys; each option opens in a CRT window to the right that slides out the first time and only retypes after that (its X key, Back and Esc close it first, then the profile). **Look** is the cosmetics above. **Rank** is Progress (rank, title, XP, the next unlocks), the Reward ladder, Tiers (the five titles with their rank ranges) and XP sources. **Records** is Overview, Bosses, Classes, Challenge rooms and **Recent runs** (below). **Unlocks** has a window per kind (Frame, Trail, Background, Card background, Name effect, Aim line, Cursor, Emotes) with a check or a padlock on each cosmetic, and the run-feat unlocks. The Records tab keeps this computer's player's runs started, won and lost, best wave, best endless wave,

**Records > Recent runs** (Docs/RecentRuns.md): a strip of your last 20 runs (newest first, four at a time with arrows; each card says the mode, class, wave and won, died or quit; a run you leave before it ends counts too, with no XP), six tiles for the open run (result, time, wave, damage, damage taken, XP) and two tabs. **Overview** has three graphs: **damage per second** over the run (your line in gold with a blue team line when you played in a party, alternating bands for the waves, a red tint on boss waves, a red dot where you went down), **health** over time for everyone in the party, and **accuracy per wave**. Hover a time graph for a readout of that moment. **Trends** shows the wave reached and the damage dealt run by run (green won, red died); click a run on it to open it. Each finished run keeps a sample of everyone's totals and health every 10 seconds, in a small file in your profile's `runs` folder (the newest 20 are kept); runs from before this, Boss Hunts, Challenge rooms and Practice have no graphs (Boss Hunts and rooms keep their own records). Co-op: every player's profile keeps its own copy with the whole party's lines. Runs with dev tools or the bot are not kept, like the rest of Records.
best score, time played, totals (kills, damage, healing, downs, revives), each boss beaten, and runs and wins per
class. The host tells every player's game when a boss falls and when a run ends, so in co-op everyone's records
grow. Practice Range matches, Boss Lab fights, and runs where dev tools (F1) or the bot were used don't count.
**Profiles:** the game asks **Who's playing?** at launch: click a profile, **+ New profile** to add one (up to 3: there are always four boxes, an unused slot says Empty, and New profile is grayed out when all three are used), **Manage profiles** (a keycap like the main menu's Play; each profile is a floppy disk held in a clamp cradle on a shelf, its name written in marker pen, and an unused slot or New profile is a dashed disk outline) to rename or delete (Delete asks twice). The **eject key** (a small key with the eject symbol, right of the disk drive under the main menu's title) opens it again: the drive's cells drain, the latch clicks and your disk kicks out of the slot on a spring, hangs there (TAKE DISK), then flies back to its cradle on the shelf. Picking one plays a move into the menu (your disk lifts out of its clamps, flies up to the disk drive under the title, tips flat and slides into the slot; the drive reads it, then the menu draws in), with sounds for each step (the eject key's move has its own); any key skips it, with a quick TV-static channel flip. **Easter egg:** name a profile "San Andreas" (any spacing or case: SanAndreas, san_andreas) or GTASA, GTA:SA or SA and its disk turns into a plain blue floppy with a paper shutter and a round metal hub. Any name with "Doom" in it (Doom, DOOM, Doom 2, ...) gets a black shareware install disk with a cream label and red lettering ("REGISTERED DISK n", n is its slot). "Mission Impossible", MI, "Ethan Hunt" or "NOC list" gets a blood-spattered pale 230 MB optical disk. "Windows 95" or Win95 gets a 90s setup disk (white label, a four-pane logo). "Antaleon" gets a see-through hologram disk (cyan outline, scanlines, a sweeping glint, a flicker) that shows the profile's name. "Pacman", "Pac-Man", "Chomper" or "Waka" gets a Pac-Man disk, and while that profile is active the arena draws players as chompers, enemies as role-coloured ghosts and bullets as dots and its **Play opens a Pac-Man maze game** instead of the saved-runs picker (steer with WASD or the arrows; eat every dot, power pellets turn the four role ghosts blue; Esc pauses, Q leaves; one player for now: `Docs/Pacman.md`). The look stays when you rename the profile. Each profile has its own name (used in the lobby), records, rank, cosmetics and saved runs; an older single profile is copied into one named after your old player name, and the old files are left where they were. Kept in `profile.txt` inside the profile's folder under `profiles/` in the game's data folder. **Profile > Rank** shows your account rank (see **Rank**). Each save keeps the one before as `profile.txt.bak`; a cut-off or damaged file is set aside (`profile.corrupt-*.txt`, `profile.damaged-*.txt`) and the backup is used. If the disk refuses a save, a red "Profile not saved" popup shows.

**Unlocks**: each is an entry in `Unlocks.All` (`Profile.cs`) with a condition on the records; it's earned the
moment the condition holds (an **Unlocked** toast in the match, listed in Profile > Unlocks). So far: **Warden Hunt** (beat the
Warden in a run; see Boss Hunt). Records also keep each Boss Hunt's plays, wins and best time.

## Run stats (Tab)

Hold **Tab** during a match for the **Run stats** screen: hologram panels projected over the arena (in your CRT
colour, lit by a beam along the bottom of the screen), your stats down the left, and three tabs (click to switch; it
remembers the last one):

- **Run so far:** the wave and where you are (path and wave of it, enemies left), time played, score, the next boss
  and the run's modifiers; your totals (damage, kills, healing, accuracy, biggest hit, level, times downed, allies
  revived); the **route** (each stretch of waves with its path, then the boss that ended it, down to the one coming
  next); your fight (damage taken, elite and boss kills, shots that hit, abilities used); and each player's share of
  the party's damage.
- **Your build:** your stats, weapon, abilities (charges, cooldowns, mastery, moving them between keys) and every
  upgrade you've picked, rarest first.
- **Team:** each teammate's build: a live model, class and level, health, weapon, abilities with mastery, and
  upgrades.

The panels **switch on** from a thin line when the screen opens and when you change tab (about a third of a
second; teammates' live models appear once it settles). With **Reduced effects** or **Visual effects** off they're
simply there, without the drifting band and flicker.

**Solo, holding Tab pauses the match** (letting go resumes it). **With friends it doesn't pause** (you can still move
and shoot while it's up); to look together, pause (**Esc**) and press the small bar-chart key beside the pause menu (hover: "Current run stats"). The host sends
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
  and the difficulty, the next boss (moving), and this stretch's Data Gate (won, lost, or when the next one
  can open). The card runs down to near the bottom of the screen, with the players at its foot as compact cards
  (class, level, health; the Tab screen has the rest), stacked from the bottom up.
- **The lobby's menu** (Esc in the lobby): **Back to the lobby** (Esc again), **Settings** (the cabinet's list
  switches, like the pause menu's), **HOW TO PLAY** (in the drawer too) and **Quit game** (no Profile or Main menu
  rows: your lobby card opens the profile and the lobby's red back arrow leaves). The lobby has no Leave button or settings cog any more; its bottom
  bar is just **Ready** and **Start** (host), side by side in the middle, with who you're waiting on to their left.
- **The main menu**: the title over a small disk drive (it says DRIVE A: EMPTY on the picker and your name and READY once a profile is loaded) with a shot arcing over it on a tapered vector stroke. In a box in the middle of the screen: a big **Play** key over **Join game** and **Settings**; these are mechanical keycaps in a dark well, with a lamp in the corner that lights under the mouse, and they press down when clicked (so do **New run** and **Back** in the saved-runs picker). Under the box is
  **your player card** (your party card in small; click it to open your **Profile**, where the pencil next to your name renames you; it's remembered; hover it for **Switch
  profile**). Until you have opened your profile once (and for at most your first five launches on that profile), a small purple **YOUR PROFILE** tag beside the card bounces toward it; clicking the tag opens the profile too, and it holds still with Reduced effects; it fades out while the Join row is open. **Join game** opens a row
  under the box for the host's address and **Connect**. Hold the mouse over Play, Join game, Settings or any button in the saved-runs picker and the button turns into a worn VHS tape (blue cast, flicker, scanlines, a rolling tracking band, a tape counter); click it and it snaps into focus with a white flash and stays clean until you move off and back on (Reduced effects: tint and scanlines only, no snap). **Play** swaps the box's insides for the saved-runs picker (see
  Saved runs): **Continue** your latest run or start a **New run**, which grows the box into the lobby's loadout.
  **Quit** is a physical key with a lamp, beside a small "MAIN MENU_" CRT screen at the top of the box (like the saved-runs picker's Back). The
  first time it shows after the game starts it draws itself in: a pen writes the title and traces the arc, then the
  box opens and its buttons rise into it (2.75 s; any key or click skips it with a quick TV-static channel flip, as do skips of New run's boot and the profile disk moves; with Reduced effects or Visual effects
  off, a short fade). Starting a run grows the box into the lobby's loadout while the rest of the menu fades out and
  the lobby fades in. (How to play is in the lobby's and the pause menu's drawer.)
- **Settings** (main menu, the lobby's menu, and the pause menu): three groups, **Audio** (level-meter volume sliders: 100 thin bars that turn amber then red near the top and ripple only while you drag, for
  **Master**, **Music** (all music; the **Music player** tab picks the track), **Weapons** (your and your friend's shots and spells), **Enemies** (enemy
  fire) and **Effects** (explosions, abilities, UI), and mute), **Visual effects** (with a live preview of what they
  do: shots and a hit, its number, an ability's name, a blast and its shake) and **Gameplay** (aim line, show the
  HUD, **camera lean**, **scroll-wheel zoom**, and the **network port** hosting and joining use, 7777 unless changed; everyone in a game needs the same one).
- **Profile:** four tabs. **Look** (the cosmetics), **Rank** (Progress, Reward ladder, Tiers, XP sources), **Records** (Overview, Bosses with each one moving beside its count, Classes, **Recent runs**, **Challenge rooms** with each room played and
  won) and **Unlocks**. Mid-run and in the lobby it has no Look tab.
- **How to play** (in Settings: the drawer turns into its topics, and Back returns to the groups): Controls, The
  basics, Fighting, Between waves, Data Gates (with a little animated gate), Paths and modifiers, Playing
  together.

Esc goes back (from the pause menu's Settings, to its list). The same audio options are
under **Settings** on the main menu, and they're saved between sessions.

**Visual effects** (the right-hand column of **Settings**, saved between sessions; all drawn on your own screen only,
so they never change the fight or what your friend sees):

- **Visual effects** on/off. Off draws the arena exactly as before.
- **Screen shake:** Off, **Low** (the default) or Full. Getting hurt, a heavy enemy's death and above all a boss's
  death kick the camera; explosions only add a light rumble (several at once don't pile up).
- **Damage numbers:** damage to each enemy rises off it, merged every 0.15 s (so a fast weapon shows one total);
  big hits are larger and gold.
- **Reduced effects:** no screen shake, no hit-pause and no white flashes, for anyone they bother.

What it adds: the Tank Soldier's **Roar** (from [`Docs/Mockups/tank-abilities.html`](Docs/Mockups/tank-abilities.html)): a short pull-in, then three staggered rings with a filled blast and speed streaks, and each enemy flashes and pops a "!" as the wave reaches it; while the taunt runs, six armour plates turn round the soldier (flickering in its last second), a rim arc drains with the timer, dashed tethers run to the nearest ten enemies and a soft pulse goes out every second. **Bulldoze** shows its 60 px lane for half a second, then afterimages, speed streaks, dust off both heels, skid marks, a bow wave and a flicker for the invulnerable window; enemies in the lane flash and throw sparks (heavy ones more), and a double ring and a ring of dust mark where it ends. Both kick the camera (not in Reduced effects). Off, the old ring and arc draw as before. The Soldier DPS's abilities (from [`Docs/Mockups/soldier-dps-abilities.html`](Docs/Mockups/soldier-dps-abilities.html)): your own dashes (Leap Slam, the Royal Dive...) leave a tapered cyan comet streak that cools over 0.4 s, with sparks where you land (it was made for the old Dash); **Poison Dart** (from [`Docs/Mockups/poison-dart.html`](Docs/Mockups/poison-dart.html), look A, Toxic Drip) leaves your hand in a green puff and flies a steel needle with green fletching on a glowing green trail, splashing on where it lands, and stays stuck in the enemy it hit; poisoned enemies glow toxic green, grow teardrops that fall and splash, bubble, wear a lime rim, a green health bar and a drop-and-dart icon; a poisoned death flashes, bursts and flings droplets, leaves a bubbling puddle for a couple of seconds, and lobs a wobbling glob onto each enemy it spreads to, which splats there; the Twin Rifle's **grenades** (and Airstrike's bombs) burst in a white flash, an orange blast, eight streaking shards and smoke, the grenade showing a faint ring growing to its blast radius in flight; **Airstrike** (from [`Docs/Mockups/airstrike-rework.html`](Docs/Mockups/airstrike-rework.html), Carpet Wall) is a red smoke flare arcing to the cursor, a red lane with marching dashed edges and chevrons growing out to both walls, the jet's shadow sweeping ahead of the jet with two contrails, bombs falling with their shadows growing, craters, and flames along the lane while it burns; **Mark for Death** puts a red reticle that snaps onto the enemy and circles it, a timer arc unwinding round it, and a small skull floating overhead, all blinking for the last second; **Chain Light** as a soft fan of light blooming out of your hand, with god-rays and a beam and halo on
each enemy it hits; **Arc Lightning** as a violet halo and sparks building on your staff with a dashed outline of the cone and tendrils of lightning creeping
along it, then a whole tree of lightning (a trunk with branches three deep, and a branch to each enemy) whipping down it,
cooling to violet and staying on the ground for a second, and a crackle on each enemy it stuns; **melee attacks** drawn as the weapon itself swinging through a glowing arc that throws sparks and leaves
embers (thrusts stab through a beam, the Lance's Breaching Thrust slides out through a glowing box, and the Warhammer is raised over your
shoulder as it charges, then swung over into the floor with a glowing circle and a pillar of light); an **additive glow** on bullets, blasts, sparks, bosses and players (enemy bullets are always drawn on top of every effect, spark and glow, so a busy fight never hides what you have to dodge); enemies **flash white and bulge**
when hit (only by a real hit: burning, bleeding and poison don't flash them; under steady fire the flash softens to a
light tint and the bulge to a gentle pulse, and a boss never goes more than 70% white, so it keeps its colour and shape); every health bar gets a **damage ghost** (a pale chunk where health was lost lingers for 0.45 s, then drains
after it); **hit-pause** (the picture holds for a few frames on a heavy enemy's death or a huge hit, at most every 0.4
s, and longer for a boss); enemies **shatter** into shards of their own shape with a white flash, sparks and a
**scorch mark** that fades over 8 s; **sparks** where shots land (fewer where many hits pile into one spot) and a flash at your muzzle; **trails** behind fast
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
new shot looks, trails, sparks, bullet endings and hit flashes too (not the glow). **The Soldier DPS's guns animate** (from [`Docs/Mockups/soldier-dps-gun-animations.html`](Docs/Mockups/soldier-dps-gun-animations.html)), keeping the glowing shots: the Twin Rifle is held out wide in both hands (**Spread arms**, from [`Docs/Mockups/twin-rifle-firing.html`](Docs/Mockups/twin-rifle-firing.html)), each muzzle where its stream starts and turned toward the crossing; each gun kicks back along its own barrel and climbs outward, flicks its charging handle, flashes at its muzzle with puffs out of the brake's side ports and a warm pool of light on the floor, joins gun and round with a bright streak on the round's first frame, and flips brass out of its port that lands, bounces, slides and stays on the floor for over a second; the recoil runs through you, each elbow folding with its gun's kick and the whole body jolting back (harder for a grenade); the barrels heat from the tip (orange, then white) the longer you hold fire, shimmer when hot and smoke after a burst; the Railgun's three coils light up one after another between shots with a ring closing on the muzzle when it is ready, and a shot is a white flash, arcs between the rails, a hard kick that shoves the rails apart and steam off the coils; the Burst SMG's three barrel tubes turn faster and heat to white as it spins up, its flash grows, it drops brass and smokes once hot, and a faint cone shows the real spray tightening. The Twin Rifle's **grenades launch with Rocket assist** (from [`Docs/Mockups/twin-rifle-grenade-launch.html`](Docs/Mockups/twin-rifle-grenade-launch.html)): a backblast cone and a jet of smoke out of the back of the tube, a dull olive-white flash and a ring of smoke at its mouth, the firing gun alone kicking back and climbing outward, its breech dropping open to throw out a fat 40 mm casing behind you before it snaps shut; the grenade flies nose first with a flickering flame and a puffy smoke trail, leaving from the drawn tube and easing onto its real line. When your hits load a tube its breech drops open, a shell slides in and it snaps shut with a spark and a green ring. **Bullets end instead of
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

### Lag chart (F5)

For finding out why a match feels laggy. Anyone can turn it on, host or guest: **F5** in a match cycles **off → pill →
chart**, and **Settings → Gameplay → Lag chart** sets the same thing (saved on your machine).

- **Pill**: a small box at the top right with **Good / OK / Poor** (green, yellow, red) and your ping. *Poor* means a ping
  of 150 ms or more over the last 3 s, a stall of 300 ms or more in the last 5 s, or under 20 fps; *OK* is 80 ms, a
  shorter stall, or under 40 fps.
- **Chart**: the last 30 s, a column every quarter second. The **blue line** is your round trip to the host (pinged 4
  times a second). The **yellow line** is your frame time, so you can tell your own PC hitching from the network. The
  **bars** are the gaps between snapshots from the host (normally 50 ms); one past the **red 150 ms line** is a
  **stall**, drawn red (the game sends over TCP, so a lost packet holds up everything behind it: that's what lag feels
  like). **Ticks** mark each wave start, a boss arriving and a **bullet storm** (450+ enemy bullets), so a spike can be
  lined up with what was happening. Under it: ping now / avg / p95 / max / jitter, stalls and the worst gap, FPS and
  its 1% low, data in and out (KB/s) and the snapshot size, and **every player's ping** (the host shares them once a
  second; red at 150 ms or more).
- **The host** has no ping of its own: its blue line is the **worst guest's ping**, and its rows show its frame rate
  and upload.
- **Shift+F5** freezes the chart (the numbers keep counting underneath; again to unfreeze). **Ctrl+F5** copies a text
  summary of the whole run so far (ping min/avg/p95/max, stalls, FPS, bandwidth, everyone's ping, the version) to the
  clipboard, to paste to whoever's hosting.
- **Log lag to a file** (Settings → Gameplay) writes a CSV for each run to the `lag` folder in your profile folder: a row
  every quarter second (time, ping, snapshot gap, worst frame, fps, KB/s in and out) and a row for each wave, boss and
  storm.

## Wave Lab (wave tester, no install)

Open `Tools/WaveLab/standalone/wavelab.html` in any browser (double-click it, or open the file on a phone): one page, no server, works
offline. Build a wave set (enemy palette, squads, spawn points A-E you can drag, props, timing, level), load a built-in set or a `.waves`
file, pick a class, weapon, Space ability and gear tier, then **Play**. Play by hand (WASD, mouse, Space, F, Ctrl; on a phone two thumb
sticks and buttons) or switch the **bot** (Casual / Good / Expert) on at any time; God mode and a start wave are optional. When a run
ends, or on **Report**, a per-wave table (clear time, kills, damage dealt/taken, healing, lowest HP, downs) appears, with Copy CSV.
It is a **simplified simulation**: enemies have the real health, size, speed and colour, but move and shoot in a few generic styles
(chaser, dasher, shooter, ring, boss...), not the game's own AI. Edit `Tools/WaveLab/standalone/app.html`, then run
`python3 Tools/WaveLab/standalone/build.py` to rebuild `wavelab.html` (after enemy or wave-set changes, refresh `meta.json` first from the
faithful server below).

**Faithful version (real game simulation, needs a host):** `Tools/WaveLab/run.sh` serves the same editor on top of the real C# game
code, and `WaveLab.exe` from the repo's [wave-lab release](https://github.com/AleonSC/Top-down-arena/releases/tag/wave-lab) opens it in its
own window on Windows. Use it when the simplified enemies are not close enough.

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

Press **F1** anywhere for the developer **dock** (Esc, F1 or its X closes it). It's a slim bar across the top of the screen. In a
match the game keeps running behind it (the mouse only stops shooting while it's over the dock); in the lobby and menus it covers the
screen. On the bar: the wave, **GOD** (everyone), **FULL HEAL**, **SKIP WAVE** and **KILL ALL** pinned for one click, four keys that
open a small drawer under them (**WAVE**, **PARTY**, **VIEW**, **LABS**), and **BOT** and **REC** lamps you can click. The stat editor opens
as a drawer on the right, and the bigger tools (DPS meter and dummies, Replays, Balance test) open as one sheet. While you type in one of
its fields the game's keys are off. Outside a match only VIEW and LABS show.

- **WAVE drawer (host):** pick a wave with -10 / -5 / -1 / +1 / +5 / +10 and **Go to wave N**. It clears the arena, gets
  downed players back up and starts that wave after 2 s. The boss on that wave (or the next boss) is shown moving
  beside it. **Kill-all key (X)**: a switch (saved between sessions) that makes **X** kill every enemy on the field,
  bosses included, as your kills: XP, score, drops, gold and kill credit as usual, through shields and immunity. It
  doesn't end the wave: enemies still to come keep coming. X still throws away a chest item while you're standing on
  one. **Freeze enemies**: a switch under it that stops every enemy moving and shooting and stops new ones spawning
  (you can still kill the frozen ones; the wave won't clear while it's on; it resets when a match starts). The **PARTY drawer** has **Everyone** buttons: level up, max mastery, full heal and reset stats for every player at once (god mode and full heal are also pinned on the bar). **Challenge
  rooms** buttons (between waves): straight into Crystal, Gatekeeper, Kill Rush, Zones or Mirror.
- **WAVE > SPAWN AN ENEMY… (host, while a wave is under way):** a sheet with every enemy (and a **BOSSES** tab) shown as its live model; filter by
  name, pick a **count** (1, 3, 5, 10) and **Elite**, then click one to put them in a row in front of you. A boss comes in the middle of the
  arena as on its wave (the Twin Reapers bring both twins). They count toward the wave like any enemy. Marks the run as a dev run.
- **LABS > DEV ARENA (PLAY AS ENEMIES) (host):** a sandbox for trying enemies' attacks. From the menu or the lobby it starts a Practice
  Range match and turns it on; in a match it turns on there. The field is cleared, everyone goes into god mode, no enemies arrive and the
  wave never clears. The Spawn-an-enemy sheet opens with a row on top: **CLICK: SPAWN** puts enemies in front of you as usual, **CLICK:
  PLAY AS** spawns one and hands it to you, **LET GO**, **CLEAR ARENA** and **LEAVE DEV ARENA** (the wave then clears as usual). Playing as
  an enemy: your hero stands still and the camera follows the enemy (gold brackets, "YOU", and a controls strip at the top). **WASD** moves
  it, the **mouse** is what it goes for (its own AI aims, lunges, charges and keeps its range at the cursor), hold **Fire** to attack on its
  real cadence (it moves on its own while it attacks unless you steer), **Parry** attacks at once (skips the cooldown), **Space** (role
  ability) lets go. Its shots and touches hurt players as usual, so aim at your parked hero (or a bot) to see them land. While you don't
  attack, its AI waits (a boss's stages pause too). Some boss attacks still go for a player rather than the cursor. **Played enemy can't
  die** (a switch, on by default): its health refills and a killing blow leaves it standing; off, a dead enemy lets go by itself. Marks
  the run as a dev run.
  **Hero dummies** (up to 6): stand-in heroes for enemies to hit. **ADD HERO DUMMY** puts one in front of you; while playing an enemy,
  **Q** drops one at the cursor and **E** takes away the one nearest it; **CLEAR DUMMIES** removes them all (Clear arena keeps them).
  Enemy shots and body touches land on them (counted at what an unbuffed hero would take); each shows its last hit, then DPS (last 3 s),
  hits and total. **Enemies aim at dummies** (on by default): every other enemy goes for the nearest dummy instead of the players.
  Blasts, beams, zones and other area attacks still only hit players. Only the host sees the dummies.
- **PARTY > SPAWN AN UPGRADE… (host, in a match):** a sheet listing every upgrade (filter by name or text, or by rarity, including weapon mods and milestone
  cards; pick whose build it is for with several players). Click one and it drops a few steps in front of that player exactly as a treasure
  chest's item does: only they see it, walk onto it, then **G** takes it and **X** throws it away (the wave waits for it, like a chest's). Only
  upgrades their build could be offered are clickable (greyed: another class, a missing ability or weapon). Marks the run as a dev run.
- **PARTY > EDIT STATS… (host):** choose a player, then edit any of their stats by hand: type a number or use - / +.
  Type in **Filter** to narrow the list (e.g. "barrier"). Edited stats show in gold, are sent to every player's
  game, and stay on top of upgrades they pick later; **Reset stats** goes back to what their upgrades give.
  Also **God mode** (takes no damage), **Full heal**, **Level up** (an extra upgrade pick) and **Max mastery**
  (every ability they have to level 5). Joining players can see everyone's stats but not change them.
- **VIEW and LABS drawers:** record sessions (below) and open the sessions folder, the aim debug overlay (also F3), the **freeze-time key (P)**
  (a saved switch: P freezes the whole picture, effects and animations included, without opening the pause menu; press it again to resume;
  hide the HUD with H), a bot that plays for you, **text editing** (also F4, below), open the practice folder, and the **Boss Lab**.
  **Account rank** (PARTY drawer) sets this computer's rank without playing for it (works on the main menu too): **+1**, **+10**,
  **30** (every reward), **MAX** (999) and **RESET** (rank 1). Cosmetics the new rank hasn't reached come off.
- **VIEW > DPS DETAILS AND TARGET DUMMIES… (host, in a match):** the DPS meter and target dummies, below.
- **LABS > REPLAYS…:** record the runs you host and watch them back, below.
- **Balance (host, in a normal run):** a five-wave test with your own build. Pick your loadout as usual, start a run, press
  F1 > LABS > Balance test…, choose the **start wave** (1, 6, 11 ... 26), **who plays** (you, or the bot with your build for repeatable
  runs), whether health refills between waves and whether to sample every second, then **Launch test**. It jumps to
  that wave and starts a fresh recording; a small TEST strip with the wave pips shows at the top while it runs. When the fifth
  wave clears (or you press **End test now**, or everyone goes down) a report opens: six tiles (result, time, damage dealt,
  damage taken, downs, accuracy), tabs for **Waves**, **Weapons and abilities** (damage by source: your weapon, each ability's
  own hit, burn/bleed/poison, ability and summon shots, and the rest), **Ability use**, **Enemies** (health, time alive, damage
  to you), **Damage taken** and **Picks**, and a list of automatic **findings** (a source with most of the damage, an enemy
  that hurts far more than it appears, a long-lived enemy, low accuracy). **Copy summary** puts it on the clipboard and
  **Open logs folder** opens the `balance` folder, which holds one JSON file per test (session format 5, summarised by
  `Tools/SessionReport`). Tests never count in Records, XP or ranks. Every weapon hit (swings, beams, companions included) counts
  as the weapon; damage from ability zones, minions and other lasting effects can't be traced to the cast that made it and
  shows as "Other". Health paid for blood costs is listed as health spent, not damage taken. Design and fields: `Docs/BalanceTest.md`.

### Replays (F1)

**F1 → LABS → Replays…** records every run you host (switch **Record replays of runs I host** on first; it's saved between sessions, off by
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

- **Waves strip** (under the title): one numbered key per wave (click to open it), **+ NEW WAVE**, **DUPLICATE**,
  **DELETE**, `<` `>` to move the wave earlier or later, its **LEVEL**, and **Loop the waves**. **UNDO** / **REDO**
  (Ctrl+Z, Ctrl+Y or Ctrl+Shift+Z) cover every change, including deletes.
- **Arena map** (top left): the five **spawn points A-E** (drag a letter to move it, for every wave) and the wave's
  props. With a group selected, click a letter to send the group from it too (click it again to stop), or click empty
  ground to send it from the nearest point. **Props** mode: click to place the picked prop (crate, big crate, pot,
  barrel, chest), drag to move, right-click to remove. The map also shows the wave at the playhead: who has arrived
  and where they're heading.
- **Palette** (top middle): **Enemies** (the bosses' factions first, then everyone else; scroll for more),
  **Squads** (the regular squads and the bosses' own, like the Riot line) and **Bosses**. **Drag** one onto the
  timeline (a new group that starts where you let go), onto the map (a new group from the nearest spawn point, at the
  playhead) or onto an existing group's bar (adds that enemy to it). A plain click adds a group at the playhead;
  Shift-click an enemy to add one more to the selected group.
- **Selected group** (top right): its name, which spawn points it comes from (several take turns), when it starts,
  how it arrives (**one by one** with a **gap** between enemies, **together**, side by side in a **line**, or in a
  **ring** round a player) and how many of each enemy. A squad keeps its own members and formation. **COPY** and
  **DELETE** (or Ctrl+D and Delete).
- **Timeline** (bottom): one track per group. Drag a bar to change when it starts, drag its right edge to spread
  its enemies out; click or drag on the tracks to move the playhead; **PLAY** (or Space) runs the wave on the map.
  The line under it says what you can do next. A wave ends once every group has arrived and everyone is down.
- **SAVE** writes `<name>.waves` to the `waves` folder next to the practice folder; **OPEN** opens one (or starts a
  new set). **TEST IN PRACTICE** plays the waves in the Practice Range, hosted by you, starting on the wave you're
  editing (from the main menu, the lobby, or a match you host; it replaces that match).

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
was added or changed without running the script.

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
