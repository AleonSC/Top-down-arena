# Top-Down Arena

A co-op top-down bullet hell for **1 to 3 players** over the network, made in **Unity 6**.

**Party size scaling:** solo, enemies have 85% health (Normal difficulty); with two players, 100%; with three, regular
enemies have 120% health and waves have 25% more of them, and bosses have 150% health. Bosses also have 1.2x to 5.5x
their base health, set per boss so each fight lasts about 60-120 s (`BossHpMult` in Balance.cs; the Warden and Hive
Queen 2.5x), and every boss has 10% more on top (`BossHpBonus`), and every enemy bullet hits 15% harder than its
listed damage. **Enemy damage grows about 1.5% a wave** (3% every two Campaign waves; Normal: 1.3x at wave 22, 2.5x at wave 100), on everything that hurts
you. The host picks a [difficulty](#difficulty) in the lobby: Easy, Normal, Hard, Nightmare or Custom. Pick a role,
then pick a mode: **Quick Play** (ten waves and a boss), the **Campaign** (100 waves, a boss after every 10th, then
endless), a **Boss Hunt** or **Survival**.

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

Every weapon, ability, enemy, role and class with its numbers (health, damage, cooldowns, DPS) is in [`Docs/Reference.md`](Docs/Reference.md), generated from the game's definitions.

The lobby is one calm grey CRT screen with a 36 px margin and soft, darker edges, lit in your **role's colour** (it eases when you pick another role): the keys, tabs, text, section lines, corner brackets, a soft glow along the header and behind the loadout preview. The CRT colour setting doesn't reach it, and the mode card keeps its mode's colour. The party column is on the left, the big grey loadout screen on the right, and the tabs above it are small, faded caps with the open one a lit pill. Every option is a digital key (outlined, inverse video under the mouse or when picked; locked ones are dashed with a padlock), and the colour lives in the party cards, the live preview, the stat bars and the pixel icons. With Visual effects on (and Reduced effects off) it's alive: its pieces **holo-boot** as you come in (a scan line wipes down each, then it flickers), box frames near the cursor light up with a crisp bright line (and a faint echo just outside) where they're closest, the scanlines crawl slowly down, a faint **radar sweep** turns over the loadout preview, and readying sends a small green spark with a short tail from Ready up the gap between the party column and the big screen into that player's card, a thin ring bursts 10 px off it and the card keeps a faint thin green line while ready. Starting the run glitches the lobby (red and blue copies split off, torn bands jump sideways) and switches it off like an old CRT, squashed to a line that shrinks to a point, before the drop-in. In it each player picks a **role** (Tank, Healer, DPS), a **class** (Soldier, Elementalist, Summoner, Knight,
Ninja, Hemomancer), then one of that combination's three weapons (four for the Soldier, Elementalist, Summoner, Knight, Ninja and Hemomancer DPS and the Soldier, Elementalist, Summoner, Knight, Ninja and Hemomancer Tank, and the Healer Soldier, Elementalist, Summoner, Knight, Ninja and Hemomancer: their fourth on the 4th key) and three 1st abilities (four for a few: their fourth on the 4th key). Weapons and abilities are
unique to their role and class. The **Ultimate** row under them shows the combination's ultimates: key 0 is its default
ultimate and key 1 its second where it has one (so far the Tank Soldier: Juggernaut Suit, and on key 2 A Literal Tank and key 3 Armor Drop, the Tank Elementalist: Lightning Rod, with Stone Colossus on key 2 and Vortex on key 3, the Tank Summoner: Bone Phalanx, and on key 2 **Iron Armadillo** and key 3 **Tidal Tusker**, the Tank Ninja: Dragon's Dogma, with Spirit Avatar on key 2 and **Five Shadow Gong** on key 3, the Tank Hemomancer: **Bloodbath**, and on key 2 **Blood Golem** and key 3 **Sacrificial Ritual**, the Tank Knight: **Trojan Horse**, and on key 2 **Drawbridge** and key 3 **Aegis Standard**, the Healer Knight: **Pilgrim's Standard**, and on key 2 **Holy Lance Rain** and key 3 **Chalice of Life**, the DPS Soldier: Attack Helicopter, and on key 2 Orbital Laser and key 3 **Rocket Drop**, the DPS Elementalist: **Living Bolt**, and on key 2 **Flash Fire**, and on key 3 **Rock Gloves**, the Healer Soldier: Field Hospital, and on key 2 Supply Drop, and on key 3 Hazard Foam, and the DPS Knight: **Ballista**, and on key 2 **War Standard**, and key 3 **Excalibur**, and the DPS Ninja: **Forbidden Path**, and on key 2 **Hundred Paper Cranes** and key 3 **Oni Mask**, and the Healer Ninja: **Astral Projection** and, on key 2, **Pressure Point Press**, and on key 3 **Shadow Clone Medics**, and the Healer Elementalist: **Static Bubble** and, on key 2, **Eye of Calm**, and on key 3 **Conduit Web**, and the Healer Summoner: **Honey Raid**, and on key 2 **Tortoise Oracle** and key 3 **Sleepy Cat**, the DPS Hemomancer: **Blood Ravager**, and on key 2 **Blood Boil** and key 3 **Blood Ritual**, and the Healer Hemomancer: **Heartbeat**, and on key 2 **Heart of the Swarm** and key 3 **Blood Garden**); the
other keys are locked. Click a key to pick it (inverse video, like the weapon and ability keys): the detail box and the
preview show it. The pick goes back to the default when you change role or class; bots use the default, a Wildcard
picks its ultimate on its Gear cards, and a saved run's seats keep theirs.

The **Class**, **Weapon**, **Ability** and **Ultimate** rows show four keys each, all the same size. The **Class** row slides
sideways: click the arrow keys at its ends or roll the mouse wheel over it. It stops at its first and last class, where the
arrow on that side greys out, and the picked class slides into view. The Weapon, Ability and Ultimate rows are fixed. Click a **role** or **class** key and the detail box shows that role's or
class's description with its health and speed; a weapon, ability or ultimate key shows that as before. Role and class keys have
no hover tips.

- **Soldier**: guns and gear, full health. **Field Training**: every level-up offer shows 1 more card (4, or 5 with Wide Choice; 5 is the most), and each wave start gives a free reroll while you hold fewer than 3 (a Wildcard with Soldier gear gets neither).
- **Elementalist**: spells (fire, frost, lightning, water, wind, earth and light), 85% health.
- **Summoner**: minions that come with your weapon (they stay between waves), 90% health.
- **Knight**: melee weapons that cut small bullets out of the air, armoured: 105% health plus a 15-point Chainmail shield, and a bigger parry shield (see below).
- **Ninja**: thrown blades and quick melee with tricks and misdirection; fast (+12% move speed, a 30% shorter Dodge cooldown, a +70% sprint for 3.1 s), 85% health (see
  below).
- **Hemomancer**: blood spells that drain and heal; every 1st ability costs health, it deals 15% more damage, hits harder the lower its
  health and never takes wounds; full health (see below).
- **Engineer** (a locked class: win a run to unlock it; open to everyone for now): a builder who works in vectors; one robot weapon, one 1st ability and one ultimate per role for now (its other keys
  are locked); full health, 95% speed (see below).
- **Arcanist**: telekinesis, runes and enchantments; one weapon, one 1st ability and one ultimate per role for now (its other keys
  are locked); 90% health (see below).

| | Soldier | Elementalist | Summoner |
|---|---|---|---|
| **Tank** (170 HP + Ironhide, enemies prefer you) | Weapons: Scattergun (double damage and a shove point-blank; Alt lobs a Shock Grenade that stuns), Flak Cannon (airbursts at your cursor, shredding enemy bullets; Alt lays a proximity shell), Riot Repeater (rounds suppress: enemies hit shoot slower; Alt lobs tear gas), Grenade Launcher (4th key: HE grenades lobbed to your cursor; Alt lobs a smoke canister whose cloud jams regular enemies)<br>Abilities: Barrier, Roar, Bulldoze, **Deployable Cover** (4th choice, two charges of 12 s each: a "[" of steel plate lands 64 px in front of you along your aim, its back wall facing out and its arms reaching back toward you; from 0.4 s after the throw it stops every enemy shot that touches it and enemies can't walk through it, while your team's shots and bodies pass; it takes 400 damage (each shot wears it by what it would have hit for), up to three stand at once (another folds away the oldest), and they all fold away when the wave is cleared; its Rare card **Reinforced Plating** (two stacks) makes covers take 40% more damage and lets one more stand at once. A canister is tossed out and lands with a thump, the wall springs up, the arms swing back and bolt down; it dents, cracks and darkens as it wears, its bar going amber then red, and at 0 it bursts into tumbling plates and leaves scraps on the floor. Its sounds follow the look: a whoosh, the landing thump, a steel clang as the wall springs up, two clunks for the arms, a ratchet of bolts and a faint buzz as the light comes on; breaking is a crack, a ringing crash and plates clattering down; each shot it stops rings off it with a short ting, and folding away is two light clunks, a hiss and a click) | Weapons: Flamethrower (sets enemies burning), Frost Lance (a five-step frost chain that chills, freezes and shatters; parry cancels a step; Alt's Absolute Zero freezes everything near, then shatters it), Storm Hammer (a spinning hammer that electrifies what it hits), Wind Clash (4th key: a combo of air waves that hurt and shove; Alt's Tempest catches enemy shots round you)<br>Abilities: **Aeroaegis** (a wind vortex round the player your cursor is nearest: enemies can't enter, bullets are reflected), **Frost Nova** (freezes nearby enemies), **Flame Wall** (burns enemies, blocks bullets), **Flood** (4th choice, 16 s: you stamp and the ground floods 190 px round you for 6 s; enemies touching you take 10 and are thrown back 60 px (heavies and bosses only hurt), and every enemy wading in the water, which stays where you cast it, is slowed 40% (heavies and bosses 20%) and soaked **wet** (as Tsunami: lightning arcs to it) while it's in and 3 s after; a foam crest crashes out as it spreads, and after 6 s it drains back in, doing nothing. **Sounds:** a thud and splash as the wave crashes out, soft lapping while it floods, a gurgle as it drains; volume: Effects) | Weapons: Golem Staff (hold to lob a crushing boulder, + bullet-blocking Golem), Bone Guard (darts that cage enemies and thread them together, + 2 orbiting skeletons that shoot the caged; Alt raises an Ossuary Wall), Bear Totem (melee claws that cause bleeding, + Bear)<br>Abilities: **Golem Wall** (3 basalt golems that block bullets until they are broken; recasting replaces them), **Earthquake** (you slam the ground, then a Burrower breaks out at your cursor and tunnels through the enemies, biting each one it passes under), **Force Pact** (12 s cooldown: takes the regular enemy nearest your cursor (not bosses or boss adds) out of the wave; after a second of violet chains it comes back as your **thrall**: an enemy of that type on your side that fights within 240 px of you with its own attack at 75% damage, with 2.5x that type's health + 15 every two waves. Enemy shots and bodies hurt it, and a fallen thrall returns 15 s later as the same type (a ring of links round your feet fills while it's away). No enemy to take: it fizzles and the cooldown is refunded. Cast again on another enemy and the pact moves to that type; mastered, you keep two thralls: the last two types you chose), **Stampede** (the 4th choice, 14 s cooldown: a gate opens 60 px ahead and a line of 7 spirit beasts (bison, boars and stags) charges 320 px across your aim, trampling every enemy it meets for 20 damage (bosses and heavy enemies too) and shoving the rest along ahead of it; an enemy driven into a wall or a prop takes 15 more and is stunned for 0.5 s; bosses and heavies are run over, not carried; it does not block bullets; mastered: 9 beasts, and the herd turns and charges back over whatever stands in its path) |
| **Healer** (140 HP, healing aura) | Weapons: Repulsion Cannon (hits fill a meter; hold Alt to pull enemies in), Revolver (click to fire, click to pull the hammer; Alt: the Fan stance, a Pistol Whip that stuns, then Fan the Hammer), Mender's Bow (Alt: Scatter, five small arrows in a fan)<br>Abilities: Mend Pulse, **Adrenaline Shot** (instant, 14 s cooldown: injects the player closest to your cursor, you included, or just you when alone: 8 s of +30% move speed, +40% fire rate and 30% less damage taken; fire rate doesn't stack with Overdrive, the bigger wins; a heartbeat ring pulses round them while it lasts), Deployable Medkit (thrown at your cursor, 14 s cooldown: bursts into 6 healing orbs worth 15 health each that anyone can pick up for 6 s; a downed ally an orb reaches is picked up at 20% health), **Decoy Dummy** (4th choice, three charges of 4 s each: throws an inflatable soldier at your cursor, up to 280 px away; it lands, puffs up and from 0.7 s regular enemies within 240 px go for it and shoot at it instead of you (a boss or mini-boss only glances at a fresh one for a second). It has 40 health + 4 a wave: enemy shots that touch it stop there and wear it, and melee enemies bite it every half second and can't walk through it, while your team's shots pass. It lasts 12 s and up to three stand at once (another deflates the oldest); they all deflate when the wave is cleared. Shot down, it pops in a **confetti blast**: enemies within 90 px are stunned 0.4 s and shoved 60 px back (heavies are only stunned, bosses untouched). Its Rare card **Double-Ply** (two stacks) gives it 50% more health. It's a beige vinyl soldier with a red cross, a painted grin and a green helmet band that bobs, wobbles when hit and sags under half health; a dashed green ring shows its pull; popped, a gold ring flashes out, confetti flutters down and the empty skin zips off and lands flat. Sounds: a whoosh, a thump, a rising hiss and a rubbery boing as it puffs up, a squeak for each hit, a loud pop with a raspberry as it zips away, and a sighing hiss as it deflates) | Weapons: Radiant Orb (heals allies it passes), Chain Light (a cone of light, its damage shared between what's in it), Nimbus (a storm cloud follows your cursor, raining healing on allies under it and soaking enemies; click calls lightning on the spot beneath it; Alt spins it into a hurricane), Static Shock (4th key: hold for a cone of static that wires enemies into a shocking circuit and gives teammates Overcharge; Alt fires the Arc Cannon)<br>Abilities: **Healing Rain** (a rain at your cursor for 6 s: heals allies inside 8 HP a second, slows enemies 30% and soaks them **wet** for 2 s after they leave), **Gust Gate** (14 s cooldown: a ring of wind at your cursor, up to 340 px away, 140 px across, for 8 s. Anyone who walks through its edge, you included, gets +80% move speed for 3 s, fading over the last second; each player once per 1.2 s, and a new pass refreshes it. Enemies inside are pushed gently out (about 55 px a second; heavies half as hard, bosses not at all). Pale wind streaks circle it with dust riding them and a thin arc drains with its time; a boosted player trails speed lines and a swirl. Sounds: a rising gust on the cast and a swish for each pass), **Lumen Ward** (20 s cooldown: a huge cone of light from you toward your cursor for 6 s; it moves with you and turns as you aim. Allies inside heal 9 HP/s and enemy bullets inside it or touching its edge burn away), **Water Ward** (the 4th choice, 3 s: two drops of water fall from above and land on a ring 54 px round you, then circle you, up to 10 (at 10 the key reads FULL and a press does nothing). A drop that touches an enemy splashes: 12 damage to every enemy within 52 px, soaked **wet** for 4 s (as Tsunami: slowed 30%, lightning arcs to it). A drop that meets an enemy shot (not red aimed ones) eats it and bursts into a mist that soaks everything within 72 px, without damage. Drops last until used, or until you go down; the key shows how many are out. **Sounds:** a cool shimmer as you cast and a plop as each drop lands, a bright bloop and patter of spray for a splash, a hiss of steam and a soft puff of mist for a burst; volume: Effects) | Weapons: Sprite Staff (a firefly that seeds up to three enemies, + 2 healing sprites; seeded kills free more), Treant Rod (a root lash that mends allies it touches; the Treant roots and a root to you heals allies and slows enemies crossing it), Spirit Chalk (hold and drag to draw a line of spirit ink that burns enemies, heals allies on it (with the Warding Ink card it also blocks bullets); draw a loop round enemies to trap them), Hive Censer (4th key: hold to pour insects from your cursor that swarm enemies (damage and slow) and shell teammates in wearing chitin; Alt calls every swarm to your cursor)<br>Abilities: **Mending Web** (2 charges, 8 s each: press it, then click a point on a wall and a point on another wall, 60-420 px apart with an open line between them; the key again cancels, nothing spent. A violet spirit spider with a green cross runs from the first wall to the second laying a line, then weaves back, about 1.5 s. The web is a band 50 px wide for 10 s: allies in it heal 7 HP a second; enemies in it are slowed 45% (heavies half, bosses not at all) and poisoned for 5 a second, the poison lasting 3 s after they leave. Up to 3 webs a caster; another unravels the oldest. Mastered: heals and poisons 40% more. Its Rare card **Silk Choir** (two stacks): 140 px more reach, 3 s longer), **Phoenix** (a firebird that glides after your cursor for 10 s, shooting and sending out a healing pulse every 2 s: 8% max health to everyone within 220 px; it instantly revives a downed ally, ending itself), **Soul Lantern** (a lantern lands beside the ally nearest your cursor, or you when alone, and links to them for 6 s: it follows them, heals 8 health a second and flames the nearest enemy within 260 px for 7 every 0.6 s; 18 s cooldown), **Octopus Familiar** (the 4th choice, 40 s: press it, then click a player within 500 px, you included (alone, it goes to you at once; the key again cancels, nothing spent). A violet spirit octopus pops out of an ink splash beside them and floats round them for 60 s. Each enemy within 130 px of that player gets one of its 8 arms, so up to 8 at once: a slap of 16 that shoves (not heavies or bosses) and heals the guarded player 3; each arm rests 0.75 s, and enemies it hasn't slapped lately go first. Its body has 260 health: enemy shots stop on it and enemies touching it bite it, with a bar over it and a teal ring of its time; at 0 or when its time is up it bursts into ink. One per caster: casting again moves it. Mastered: 380 health and 22 a slap. Its Rare card **Three Hearts** (two stacks): 50% more health, 25% harder slaps) |
| **DPS** (110 HP, Momentum) | Weapons: Twin Rifle (streams cross at your cursor for 1.5×; hits load two grenades that Alt fires, left then right), Railgun (hold to charge a ricochet beam; Alt dumps the heat it builds as a cone of fire), Burst SMG (spins up: tighter and harder the longer you fire), Double Barrel (4th key: a side-by-side shotgun, one barrel a click)<br>Abilities: **Poison Dart** (8 s: a dart at the enemy nearest your cursor hits for 20 and poisons it for 18 a second over 5 s; if it dies while still poisoned, the poison jumps to the 3 nearest unpoisoned enemies within 180 px, 80% as strong and for the full 5 s again, and on from them when they die), **Airstrike** (16 s: throw a flare at your cursor, anywhere in the arena; a red lane lights up through it across your aim, wall to wall, and 1.2 s after the cast a jet carpets it: 55 a bomb, one every 140 px, each in 85 px; then the lane burns for 3 s at 15 a second. Allies are safe), **Claymore** (5 s, two charges: plant a mine at your cursor, up to 220 px away, facing away from you; it arms in 0.6 s, then the first enemy to step into the 60° cone in front of it, 150 px long, sets it off: a 0.15 s beep, then a wider 90° blast 210 px long that hits for 140 up close and 84 at its edge and shoves regular enemies 80 px; nothing behind it is hit; three out at most, a 4th removes your oldest, and they fold away at the wave's end; Rare card **Shaped Charge**: +40% blast damage), **Tactical Vest** (4th choice, 20 s: pops **3 random gadgets** off your vest, three different ones from Grenade, Smoke Bomb, Gas Grenade, Incendiary, Land Mine and EMP. They fly out and land 50-90 px round you as real gadgets with their full uses, yours alone: walk over one to take it into your hand or bag and use it like any gadget (tap or hold the Gadget key, the wheel). A full hand and bag leaves them on the floor. One left lying there fizzles out after **12 s**: a gold arc round it drains, and it blinks over the last 3 s. Mastered: 4 gadgets, and they stay until you grab them) | Weapons: Fireball (big, rolls through enemies, burning them), Arc Lightning (hold to tag up to 10 enemies in a wide cone, let go to chain lightning through them; Alt fires three forked bolts), Ice Shards (bank in orbit, launching at enemies that come near), Stonespear (slam up six hovering earth spears, throw them one by one or Alt all at one enemy)<br>Abilities: **Meteor** (a fireball streaks in over the warning circle, then 150 damage), **Blizzard** (slowing storm), **Tsunami** (a wave that soaks enemies: slowed, and lightning arcs to every wet one; a thin blue effect bar over a wet enemy drains as it dries), **Gale Wall** (the 4th choice; 18 s cooldown: a curved teal lens of wind 70 px in front of you for 5 s, staying where you cast it; enemy shots that reach it, red aimed ones too, stop and hang in it, each a charge (up to 40). Press the 1st ability again, or let it run out, to unleash a 70° cone 420 px toward your cursor: 40 damage + 6 per charge to each enemy in it (280 at full charge), shoving them back 80 px (half for Heavies, bosses aren't moved). The lens is a living plasma membrane: it boots out from its middle, every catch sends a ripple along it, held shots crackle, and as it fills it heats from violet to pink and flickers past 80%. A bar under you shows the charge, and your own screen outlines the cone. Sounds: a power-on sweep, a hum that rises with the charge, a thwip per catch, a chime when full, and a suck-in and boom on the unleash) | Weapons: Wolf Whistle (darts mark the prey your wolves hunt; every wolf biting at once is a takedown; kills grow the pack to 5, Alt merges it into an alpha, and the pack soaks 10% of your damage), Wisp Lantern (3 orbiting wisps you send out as your shots), Imp Caller (imps wait by you; tap to send them, hold to merge them into one big blast), Honeybee Staff (4th key: each click hatches a bee at your cursor that stings the nearest enemy once; hold Alt to spray slowing honey, and kills in it burst into free bees)<br>Abilities: **Bat Swarm** (8 bats circle you for a moment, then peel off and dive at enemies), **Hydra** (a three-headed hydra rises at your cursor for 8 s, each head spitting venom at a different enemy), **Alpha Call** (Blood Scent: your minions frenzy, building a stack with each bite, and lunge at 5), **Land Shark** (the 4th choice, 14 s: a puddle opens at your cursor, up to 420 px away, and spreads 100 px across while a fin circles in it; enemies wading in it are slowed 30% (not heavies or bosses). At 0.85 s a shark leaps out across the puddle along your aim and, at the top of its jump (1.1 s), bites every enemy in the puddle once for 120, bosses and heavies too, stunning each for 0.5 s; then it dives back in and the puddle drains away by 2.2 s. A gold dashed ring marks where the bite will land until it does. Mastered: a 130 px puddle and a 160 bite. Its Rare card **Great White** (two stacks) makes it bite 40% harder) |

**Wildcard** (a fourth role, locked until you have won a run as a Tank, a Healer and a DPS; a co-op win counts): it
has no class. You pick any class's DPS weapon, any DPS 1st ability, a Wildcard ability (any DPS ability)
that starts on the 2nd ability key (movable in the Tab menu like any 2nd–4th ability), and any class's DPS ultimate.
The lobby's Gear box shows four cards: Weapon, 1st ability, 2nd ability and Ultimate, each with its pick, its class's icon and its key. Click a card to open its pick window over the loadout: one column of class labels, one class open at a time (it opens on the class you already use; click another label to open that one instead). Hover an option to watch it in the preview and read it in the window's info box; click it to pick it and close the window (Esc or X closes it too). An ability already on the other key is tagged with its number, and picking it swaps the two. 125 health, no role passive (no taunt, no aura). The price: 10% less damage
and 25% slower ability cooldowns (ultimate included). Class passives don't come with borrowed gear (a Hemomancer ability
costs its health but there is no Blood Price bonus); a weapon's own cards still appear, and borrowed abilities earn
mastery as usual. It can't be picked in the lobby while locked. Bots keep their normal roles
unless you give one the Wildcard role by hand. Design notes: [`Docs/Wildcard.md`](Docs/Wildcard.md).

Generic upgrades (damage, fire rate, cooldowns, Extra Use, legendary cards) work for every class; minions
use your damage and attack-speed bonuses too.

**Tanks** (every class): **Ironhide**, the role passive. A 25-point shield takes **half of every hit** and your health takes the rest, so wounds still build but slower (it comes after an ability's shield, before a shrine's health past max). It refills at 25 a second once you've gone 3 s without taking damage; going down empties it, and you start every run with it full. Five steel plates on a ring round you show it (spent plates go faint, the one refilling grows back, and the ring flashes out when it breaks), with a steel "+25" beside your health (dim while the refill waits) and a steel stripe on the party health bars. A Wildcard has no Ironhide. Knights (every role) add their 15-point Chainmail to the same shield.

**Ironhide cards** (Tanks only):

| Card | Rarity | What it does |
|---|---|---|
| **Iron Plates** | Uncommon (x4) | Ironhide holds 15 more (25 → 85 at four) |
| **Quick Refit** | Uncommon (x3) | The refill starts 0.5 s sooner (3 → 1.5 s at three) and runs 25% faster |
| **Shared Plating** | Rare (x1) | Halves your Ironhide, so every Iron Plates (before or after) counts half. Every other player gets a fixed 25 shield that works like Ironhide (half of each hit, refills at 25/s after 3 s) and that cards never change. Several Tanks with it still give 25. Your Quick Refit stays full. |

**DPS** (every class): **Momentum**, the role passive. Every 3 kills you land add a stack (up to 5, so 15 kills to fill it), and each stack gives +3% move speed and +3% fire rate (+15% at full). The stacks age out together after 4 s without a kill (every kill refreshes the timer, and the kills toward the next stack are lost too), and going down drops them. A gold power-up aura burns round you, climbing in jagged spikes that swell smoothly with every kill, taller and brighter with each stack; at 4 its core turns white-hot, at 5 it crackles with lightning and pulses, each tier fades in rather than popping, and it gutters softly in the last second before the stacks fade, then sinks away. Only kills that give XP count (not target dummies); a Wildcard has no role passive, so no Momentum.

**Healers** (every class): the healing aura heals allies within 170 px, more the closer they stand: 17 HP/s right next to you,
falling evenly to 5 HP/s at the rim (about 9 HP/s averaged over the circle), and you for 1 HP/s. The aura's glow fades out the same way, and a tether to each ally in it is brighter the closer they are. Each
**Sanctuary** card adds the base rate again (2x, then 3x; and 40% reach) and +1 HP/s for you. A Healer's healing past
someone's full health becomes a shield (up to 20% of their max health, for 4 s; the aura's own ticks don't count).
Healer abilities work alone too: the **Adrenaline Shot** goes to you, **Chakra Reserves** goes on you at half strength, **Spirit Ward** takes your own
damage, **Martyr** with no one down wards the most hurt ally (you, alone) for 12 s so going down puts them straight
back up at 50% (the ward is a halo of 12 blood beads turning round them, one going out each second), and a **Phoenix** brings anyone back, you included (the run doesn't end while one is out).

### The Knight

Knights fight up close. Holding fire **swings** instead of shooting: each swing hits every enemy in its arc
(the Rapier only the nearest), and **cuts small bullets out of the air** in front of you. Big orbs and mines get
through, and bosses' red aimed shots always do. Knights have 105% health plus **Chainmail**: a 15-point shield that
works like Ironhide (it takes half of every hit and refills at 25 a second 3 s after the last one; on a Tank Knight it
adds to Ironhide for 40, and Shared Plating doesn't halve it). Their **parry shield** is 30% wider and sticks out
32 px instead of 20.

| | Weapons | 1st abilities | Ultimate |
|---|---|---|---|
| **Tank** | **Warhammer** (hold to charge, let go to slam a fixed circle ahead (160 px across, the same size however long you hold): up to 80 damage and a 0.9 s stun at full charge. A faint outline shows where it lands while you charge, and a disc grows inside it as the charge builds), **Lance** (the Breach, with a lance and a heater shield: jab, jab, a Wide Sweep that bats enemies out of your lane, then a Breaching Thrust that shoves everything in front 160 px back and stuns it; shoved enemies that hit another enemy or a wall take Impact damage; the weapon-action key is Hook & Haul: hook the enemy nearest your cursor, haul it in, and your next attack is the Breaching Thrust), **Halberd** (**Reaper's Wheel**, a four-step chain: a Sweep (a thin 18° blade swept across 80°, 200 px, 30, each enemy hit once), a Return Sweep (24), an **Overhead Chop** (the head raised overhead and dropped along your aim: a line 240 px long and 40 px wide, 44, stuns 0.6 s) and a **Reaping Wheel** (a full turn round you, 170 px, 28, shoves 60 px and cuts the small shots all round you); the chain starts over 0.9 s after a step, and raising the parry starts it over. Hold the weapon-action key to **Set the Pike**: you move at 35% speed and can't swing, and the pike turns on its own toward the nearest enemy within 290 px, whatever your aim, and pokes it whenever it's lined up within the point's 230 px: 22 damage, a 110 px shove straight away from you and a 0.3 s stun, at most every 0.45 s (heavies and bosses aren't shoved, bosses aren't stunned). Let go to **Pike Lunge**: the pike drives out 260 px along where it points (not your aim): 40 to everything on the line, an 80 px shove and a 0.4 s stun, once every 1.2 s (so a tap of the key lunges at the nearest enemy). With Visual effects on, the chop cracks the floor, the wheel throws a ring of dust, and a faint arc shows where the set pike's point reaches), **Bulwark Barrier** (4th key: a kite shield that always guards your front 180°: every enemy shot that hits it stops, red aimed shots included, though your back stays open, and 150° while a swing plays. Fire is a four-step chain: Rim Cut (12, 110°), Backhand (14, 160°), a Boss Slam (26, a 60° thrust that shoves 200 px and stuns 0.6 s) and a **Shield Throw**: the shield spins out 230 px along your aim and comes back, cutting through every enemy it passes for 18 on the way out and 18 on the way back (a short shove and stun) and cutting small shots out of the air; while it's away you have no guard and can't Brace, and the chain starts over when you catch it. The chain also starts over 0.9 s after a hit, and raising the parry cancels a step (or a throw still winding up) and starts it over. Hold the weapon-action key to **Brace**: you move at 38% speed, the guard widens to 220° and every shot it stops is soaked into **Ram** (five pips under you: 7 a round, 14 a red or big shot, up to 100; Ram drains 4 s after you let go). Braced, the plate also stops enemy melee from the front (bodies, sword cuts, rakes): the blow is blocked, the enemy shoved back 70 px, and it adds 4 Ram (a body leaning on the plate counts every 0.3 s). Fire while braced for a **Shield Bash**: a dash of 70 px plus 2 px a point of Ram (270 px full), untouchable, that hits everything in its lane for 20 to 110, shoves and stuns it 0.4 to 1.4 s; with no Ram it is a short shove. At full Ram the soaked shots burst out ahead of you as 8 to 16 steel shards) | **Shield Wall** (seven steel plates across your front 120°, blocking every bullet, red aimed shots included; each shot wears down the plate it hits (400 damage shared between the plates), a broken plate leaves a gap, and the wall has no timer: it stays up until every plate is broken), **Leap Slam** (leap up to 320 px to your cursor, untouchable in the air; 50 damage and a 1 s stun where you land. With Visual effects on, the knight swells and rises with the shadow left on the floor, a hexagon closes in on the landing radius, and the landing sends out hex pressure rings, steel plates and sparks and leaves a hex rune; each stunned enemy sits in a hex cage whose ring drains over the stun), **Cavalry Call** (18 s cooldown: two lancers ride in at your sides and charge 1,100 px across the map along your aim; each enemy they meet takes 35, is shoved 150 px out of the lane and slowed 45% for 3 s, Heavy enemies take a 45 px shove and a 20% slow and bosses are only slowed; you take 20% less damage for 3 s. With Visual effects on, gold-and-ivory lancers drop in on shafts of light, with a lane band, bow wave and dust), **Siege Bell** (4th key, 22 s: a ghostly bell comes down 50 px ahead of you on a shaft of light and rings three times while you stand braced (rooted, 25% less damage, about 1.4 s); tolls 1 and 2 send a 120 px rune ring (8 damage and a flinch that breaks a wind-up), the third throws a shockwave out to 260 px (20 damage, a 24 px shove and a 2.5 s stun; heavy enemies 1.5 s, bosses 0.8 s), and each stunned enemy wears a teal ring that drains with its stun; capstone: a 3.5 s stun and the shockwave erases the enemy bullets it passes; Great Bell: 25% farther and 0.5 s longer) | **Last Stand** (70 s): you plant yourself and brace nine steel plates in a crescent 120 px out (178° wide, facing your cursor; it follows your aim at 1.7 rad/s, so you commit to a side). It takes 0.5 s to plant, then it stops every enemy bullet from the front, red aimed shots included. Your teammates' shots pass out through it, and you take 10% of the damage of every shot it stops (shots from behind or the sides hit you in full). You can't move or use a dash, leap or lunge while planted. There's no timer: it stands until you go down or press the ability again, and the cooldown starts when it comes down. It also comes down between waves. With Visual effects on, three stakes drive in behind you, the plates slam down centre-first, each kicks and flashes where a shot lands, and they crack and rattle as your health runs down. Or pick **Trojan Horse** (key 1) or the **Drawbridge** (75 s, key 2 of the Ultimate row): a gatehouse rises behind you and its drawbridge falls along your aim, its shadow sweeping the lane first and its chains snapping; it lands 1.05 s after the cast: 260 to everything in the 500 x 100 px lane (bosses aren't killed outright), 45 and a 40 px shove to enemies at its edges. Then it lies there for 5 s as a raised walkway: enemies can't step onto it (they're kept to its rails and far end, and the rails grind any pressed against them for 80 a second) and their shots stop at its rails, red aimed shots too; you and your allies walk on it. Then the planks give way from the far end. Look: Oak & Iron, oak planks with iron bands and rivets, stone towers, iron chains, dust rolling out both sides and a big screen shake Or pick **Aegis Standard** (75 s, key 3): see [Ultimates](#ultimates) |
| **Healer** | **Quarterstaff** (a two-handed staff: left and right swipes that knock enemies back and bat enemy bullets back at them; each enemy hit heals you and allies near you for 2 and stores a Jab stack, up to 3, which never fade; the weapon-action key spends one on a cone jab), **Lightbringer** (a three-step chain: Dawn Cut's crescent heals the ally it passes, Mercy Cut's wards them, Noon Sunburst heals everyone in a ring round you; hits fill a light gauge, full makes the next Noon Radiant; the weapon-action key spends the light on a Sunlance that heals the ally nearest your cursor), **Censer** (a three-step rite: sweep, return sweep and a Benediction that goes all the way round; every step heals allies near you and leaves healing incense that spoils enemy aim; hits light Embers, and the weapon-action key throws the censer to your cursor for a burst and a big healing cloud that also speeds up reviving a downed ally), **Vowguard** (the 4th key: a sword and shield with two stances, Valor and Sanctuary, each playing a chain of up to 10 steps you build in the lobby; the weapon-action key switches stance) | **Consecrate** (a Holy Cross flies out along your aim, growing as it goes: allies it crosses heal 35 and run 30% faster for 4 s; enemies take 25, are slowed 40% for 3 s and knocked back; hits each once), **Guardian's Oath** (press it, then click the player to grab (up to 600 px, downed or not; Fire picks instead of shooting, the key again cancels, nothing is spent with nobody in reach): you charge to them, grab them and haul them both back to where you stood; a ward of light planted there then halves the damage everyone inside takes for 5 s and revives downed allies twice as fast), **Lay on Hands** (heal the most hurt ally within 300 px for 70, or revive them at half health), **Sanctum Glass** (the 4th choice, 16 s: a flat pane of stained glass, 220 px wide, rises 110 px ahead of you, facing your aim, and stays where you cast it for 6 s. Every enemy shot that touches it, red aimed shots too, turns into light: the shot is gone and a healing mote curves from that spot to the most hurt player within 400 px of the pane, healing 4 when it lands (10 for a big shot). It heals up to 120 in all, then shatters (it also shatters when its time runs out). With nobody hurt it still turns shots to light, but they heal nobody and don't count. Your own shots pass through it. Mastered: 40% wider, and each mote heals 50% more. Each pane flares as a shot turns in it, cracks spread as it nears 120, and a gold line (time) and a green line (healing left) run along its back) | **Hallowed Blade** (80 s): a giant sword falls at your cursor for 200 damage around it, then blesses the ground for 8 s (allies heal 15 HP/s) Or pick **Pilgrim's Standard** (80 s, key 1 of the Ultimate row) or **Holy Lance Rain** (80 s, key 2) or **Chalice of Life** (90 s, key 3; see [Ultimates](#ultimates)) |
| **DPS** | **Twin Blades** (the Crescent Waltz: two sabres in a five-step chain, rising cut, falling cut, double jab, pirouette, crescent shear, each step carrying you forward; the weapon-action key throws both sabres to cross at your cursor and fly back), **Greatsword** (a ten-step chain: Undertow, Pommel Bash, Cross Cut, Pommel Bash, Backstep Reap, Comet Leap, Pommel Bash, Undertow, Full Moon, Eclipse; the weapon-action key throws the sword spinning to your cursor and back, the Moonwheel), **Rapier** (thrusts at the nearest enemy, skewering up to 3 enemy bullets on its line; the first thrust is slow, and every enemy you hit adds a Tempo stack, up to 20, that makes the next thrust come sooner; Tempo holds 1.2 s after your last hit, then drains; the weapon-action key raises the Riposte guard: you move slowly, bullets in front are sent back as your own and fill a charge, and a click lunges along your aim, untouchable, farther the fuller the charge; diamonds behind you, one per stack, fill as Tempo builds and turn gold at full), **Arc Knuckles** (the 4th key: coiled blue gauntlets with two stances, swapped on the weapon-action key: **Brawl**, a Pull Punch that opens a click wheel, then punches and dashes sent the way you click on it, a Pin Punch, a Barrage and a charged Quake Punch; **Rocket**, the Rocket Dash, Flurry, Grab & Throw and Rocket Punch, a click each; the parry starts a stance over, and a perfect parry arms a gold Counter Hammer for your next click) | **Bladed Dive** (10 s: rush toward your cursor, up to 240 px, untouchable (nothing hurts you until 0.3 s after you land), your blade spinning round you: four cuts land along the way, each hitting every enemy within 64 px of you for 18. Kill anything with it and the cooldown resets), **The Gauntlet** (9 s: throw the gauntlet at the enemy nearest your cursor along your aim, up to 380 px, flying over anything in between: it takes 12 and is Challenged for 5 s, its crest burning over it. You shoulder-charge to it, untouchable, bowling every enemy in the way aside (18 and a 55 px shove), and hit it for 30. Kill the Challenged enemy (anyone's kill counts) before the Challenge runs out and the gauntlet flies back to your hand: the cooldown resets. A miss, or a Challenge that runs out, costs the full cooldown. Look: Heraldic, a crimson and gold crest with its timer ring, a gold ring turning under the enemy and a crimson ribbon to it), **Caltrop Run** (12 s: for 4 s you run 20% faster and a caltrop bounces out behind your heels every 34 px you move, up to 16 (none while you stand still; the run ends early once the pouch is empty). Each lands about 16 px back along your path, a little to one side, and is armed once it has landed. The first 2 enemies to step on one each take 12, bleed 4 a second (the shared bleed) and are slowed 40% for 1.5 s (heavy enemies 20%, bosses only the damage); then it breaks. Unused, it rusts away after 6 s. The cooldown starts when the run ends. Look: Iron Scatter, dark-iron four-point caltrops with a steel rim and a pulsing glint, a bent spike tipped red once one has bitten, a white pop and red sparks on each bite, gold speed lines off your heels and a pouch at your hip while you run), **Cannon Call** (4th key, 12 s: fire a flare at your cursor, up to 420 px; a red mark shows the 130 px blast ring with a core ring closing in as the countdown, and 1.25 s after the cast an iron ball drops out of the sky onto it: 70 to enemies inside the core (70 px), 35 out to the edge, everything shoved outward (140 px at the middle down to 40 at the edge; Heavy enemies and bosses aren't shoved) and stunned 0.6 s (not bosses), and ordinary enemy shots in the ring are knocked out of the air (red aimed shots fly on). Kill 3 or more with one call and half the cooldown comes back. Allies are never hurt. Look: Iron Round, a black iron ball with a sputtering fuse on a smoke trail, its shadow growing on the mark, then a white flash, shock and dust rings, stone chunks and sparks, and a smoking crater with cracks for 3 s) | **Warhorn Volley** (70 s): blow a war horn and a front of fire arrows crosses the field along your aim, burning and pinning every enemy it reaches. Or pick **Ballista** (75 s) or **War Standard** (70 s) on the lobby's Ultimate row (see [Ultimates](#ultimates)) |

How shot upgrades work on a melee weapon (the cards say so when you're a Knight):

- **Damage, fire rate (swing speed), crits, lifesteal, legendary cards** work as usual.
- **Piercing Rounds**: +15% reach per stack (sword waves already pierce everything).
- **Barrage**: every swing also throws a **sword wave** per stack, fanned out to the sides.
- **Incendiary, Cryo, Shock and Explosive Rounds**, Swarm Rounds, Collapse Rounds and Graviton Rounds apply to
  every melee hit. **Ricochet** flings bounce bolts off your hits (see the mods table). **Homing Rounds**: +10% reach
  per stack (and your sword waves bend). **Velocity Rounds** speed up your sword waves, and their +25% damage counts for everything.

### The Ninja

Ninjas are quick (+12% move speed; their Dodge comes back 30% sooner, 0.77 s, and their sprint is +70% over 3.1 s) and lightly protected (85% health). They mix thrown weapons with fast melee
(the Kusarigama, Bo Staff and Katana swing like a Knight's weapons; the Bo Staff and Katana cut small bullets).

| | Weapons | 1st abilities | Ultimate |
|---|---|---|---|
| **Tank** | **Kusarigama** (a two-piece combo: a quick sickle cut, then the iron weight thrown 280 px to bite the first enemy in its lane, stun it and drag it to your feet; hold the weapon-action key to whirl the weight around you, pulling enemies within 200 px in and cutting what it sweeps, and let go for a stunning snap), **Bo Staff** (a chain that never times out: a cone slash, a jab, then a full spin, cutting small bullets; press the weapon-action key to shove a bar of staff ahead of you, which knocks enemies back and starts the chain over), **Fuma Shuriken** (a giant star thrown in an oval loop 380 px out in front of you and back, cutting through everything both ways; press the weapon-action key for the **Orbit stance**: the star circles you at 80 px, cutting small bullets, Fire lashes it out across your aim and every third press is a wide Cyclone; parry to reverse the circle and cut its ring, perfect-parry to steal shots as small stars that fly back at their shooters; parry with a star in flight to catch it into orbit), **Nodachi** (the 4th weapon key: a katana as tall as you with two stances on the weapon-action key, each a five-step combo that keeps its place with no timer, even while you're in the other stance; **Arashi** sweeps wide round you with armoured wind-ups and **Mamori** jabs fast, blocking shots in front of you; raising the parry starts both over) | **Shadow Clone** (a wall of 9 afterimage clones for 7 s that every enemy aims at; each soaks 50 damage and never fights; walk into them to shove the wall; capstone: double health), **Smoke Bomb** (a 140 px cloud for 5 s: enemy bullets inside slow to half speed, allies inside take 30% less damage), **Ceremonial Armor** (2.5 s cooldown: summons one lacquered armour plate, up to 10, that circles you until it breaks: 4 spin fast on an inner ring, 6 turn the other way further out; each has 45 health, stops the enemy bullets it touches and pushes enemies away; all 10 up: 20% less damage; capstone: a breaking plate bursts for 30 within 60 px; Tempered Steel: plates have 50% more health), **Bamboo Grove** (4th key, 14 s: palm to the ground, 0.35 s of cracks and a closing ring, then six rings of bamboo burst out to 140 px; each enemy the wave reaches takes 40 and is stunned (1.2 s, heavy enemies 0.6 s, bosses 0.3 s), then bleeds 8/s for 4 s once the stun ends; the outer ring stands 3.2 s as a fence that holds enemies outside and stops enemy shots (heavy enemies and bosses push past); capstone: the fence lasts 6 s; Rhizome: 30% farther, bleed 50% longer) | **Thousand Clones** (70 s): twelve violet ink clones appear all over the map and every enemy goes for the nearest one at once; each soaks 100 damage, bursts for 60 when killed, and stays until it dies or the round ends; capstone twenty clones. Or pick **Dragon's Dogma** (70 s) on the lobby's Ultimate row: a spirit dragon that hunts for 5 s, devouring regular enemies whole and biting heavies and bosses for 150 (see [Ultimates](#ultimates)) |
| **Healer** | **Acupuncture Needles** (healing needles that fly through enemies and stick in teammates; Alt: Bloodletting needles that cut and bleed enemies), **Spirit Fan** (alternates blowing enemies away and drawing them in; a short gust that heals allies in it and blows small enemy bullets back), **Ofuda** (Healing Talismans that bend toward a hurt teammate at the end of their flight and burst, healing, shoving enemies and erasing small bullets; Alt: a Blast Ofuda that sticks to an enemy and blows up), **Twin Tessen** (4th key: a five-step dance of two war fans that heals the allies it brushes, sheds healing petals and ends in a petal wave; hold the weapon-action key for the Kamikaze Gust, a massive cone that throws enemies back, turns shots round and heals allies) | **Healing Dash** (sprint 70% faster for 3 s, leaving glowing footprints that last 6 s and heal 12 HP a second, you and your allies, while you stand near one; overlapping footprints don't stack; capstone 4.5 s sprint; Morning Dew makes them heal 50% more and 25% bigger), **Substitution** (swap places with the ally nearest your cursor and shield them for 60; alone, slip toward your cursor and shield yourself), **Spirit Ward** (a talisman at your cursor for 8 s that takes the damage allies near it would take, up to 150; when it breaks or ends, a quarter of what it took heals them), **Chakra Reserves** (4th key, 28 s: pour chakra into the teammate nearest your cursor within 460 px: it heals 40 plus 15% of their missing health, gives them a **Chakra Surge** for 5 s (25% faster moving, 35% faster attacks; a jade ring under their feet empties as it runs out) and refreshes every ability they have but the ultimate, each back to full charges; a jade thread runs to them and a lotus opens at their feet. With nobody in reach it goes on you at half the heal and Surge, with no refresh; capstone: a second thread reaches the teammate nearest the first, for half the heal and the full Surge; Deep Reserves: the Surge lasts 1.5 s longer and it heals 10 more) | **Blossom Storm** (80 s): for 5 s the whole party heals 20 HP/s and enemy bullets near every ally are erased |
| **DPS** | **Shuriken** (tight fans of 3 stars that stick in what they hit and come back after 5 s for another hit; the weapon-action key switches to the **Great Star** stance, recalling them: hold Fire to draw a route, let go to throw one huge star along it and home, cutting everything both ways), **Katana** (two stance combos, switched with the weapon-action key; the one you leave keeps its place: **Hayate** dashes and slices and ends in a **Momentum Burst** that shoves enemies away and marks them, **Senbon** sweeps, spins and slashes and ends in a storm of **caltrops**; the Burst flings caltrops on the ground, and caltrops thrown after a Burst seek what it marked), **Dragon Fang Kunai** (one huge kunai that bites one enemy, and with Dragon's Hunger cards up to 12 in turn, 10% harder each bite, a fire dragon growing behind it; the last bite bursts; press the weapon-action key to blink to where it bit last for a double hit), **Nunchaku** (the 4th weapon key; Twin Storm, a chuck in each hand: a four-step combo of Snap, Backhand, Double Figure-8 and a stunning Twin Crush; the weapon-action key raises the **Twin Windmill** guard, whose two spinning discs swat small shots back, and Fire in it is a **Twin Counter** that hits harder for every shot swatted) | **Shadow Step** (teleport behind the enemy nearest your cursor, up to 450 px away, and strike it for 80; like Blink, it never lands in a wall or off the map: the spot backs up toward you onto the floor), **Vanish** (for 3 s enemies lose track of you and shoot at the smoke bank where you vanished; your next hit deals double), **Blade Fan** (12 kunai spin around you for 4 s, cutting enemies they touch for 18, at most once every 0.5 s each, and erasing enemy bullets; press it again, or wait, and they fly out in a ring, 18 each; 12 s cooldown), **Senbon Seekers** (4th key, 6 s: three poisoned needles school round you in a loose, breathing ring, up to 9 (the key does nothing at 9); they have no timer; when an enemy comes within 200 px, a needle leaves the ring and curves in on it (one every 0.12 s, at most two on one enemy) for 20 and a stack of poison (5/s for 4 s, renewed by each hit, up to 5 stacks); a needle is used up when it hits, and one whose enemy dies takes another in range or flies back into the ring; going down loses them; a row of pips under you counts them; capstone: holds 12; Hidden Needles: 30% harder, poison stacks 2 higher) | **Death Lotus** (65 s): strike up to 8 enemies near your cursor in a flurry of cuts, 100 each (the arena darkens, each target takes three white-hot crimson cuts and leaves a crimson lotus stain, and the cuts show one after another over about a second) |

### The Hemomancer

Health is the Hemomancer's resource. It has full health and normal speed, and:

- **Every 1st ability costs health**, a share of your max health shown on the card (Blood Gift: of your
  current health). Cooldowns are shorter than other classes' to make up for it.
- **The blood floor (20%):** no health cost, toggle or drain takes you below **20% of your max health**; only
  enemies can. Spending health never downs you, and a crimson tick on your health bar marks the floor. At the
  floor an ability still goes off, but whatever cost it can't pay goes on its **cooldown** (nothing paid: double
  the cooldown; half paid: half again), and anything that scales with what you paid is worth less.
- **Blood Price:** you deal **15% more damage** always, and more still the more health you're missing, up to **+40%** more at the blood floor (a Wildcard with blood gear gets neither).
- **No wounds:** hits never leave a wound on you, so every heal can bring you back to full.
- **No lifesteal:** your hits don't heal you. Instead, 15% of your kills drop a crimson **blood orb** with a white
  drop on it: a small health orb (15 HP) that **only you** see and drink. It lasts 20 s and drifts to you from up to 600 px away. None from a boss's own drop or where health orbs are off (Famine, Bloodlust, Blood Moon).

| | Weapons | 1st abilities (cost) | Ultimate |
|---|---|---|---|
| **Tank** | **Blood Lash** (a whip of blood, a four-step string: **Crack** (a 130 px, 110° sweep, 24; the outer 30% cracks for double and taunts), **Backhand** (the same sweep back), **Snap** (a narrow 36° lash out to 190 px, 36, that taunts) and **Reap**. Press again within 0.8 s of a lash ending to move on, or it starts over at Crack. Every lash that lands hooks a blood chain on the enemy: +1 link, +2 from the cracking tip or the Snap, up to 4 links each. A chain holds 3 s after its last hit, then drops a link every 0.5 s, and snaps past 400 px. Reap yanks every chained enemy within 340 px in to touching you, hitting each for 20 + 12 a link and stunning it 0.6 s (heavy enemies and bosses aren't moved, only hit), and each link spent becomes a Clot stack: 4% less damage taken each, up to 5, holding 3 s then fading one every 0.5 s. **Alt, Martyr's Leash:** hold the weapon-action key (up to 3 s, 2% of your max health a second, then a 3 s cooldown): every chained enemy is taunted, its chain doesn't fade, and it's dragged back if it strays more than 160 px from you; 40% of every hit you take is taken off and runs down the chains into them, three times over, split by their links), **Gore Mortar** (hold to charge, let go to lob a massive glob of gore over enemies: 160 px on a tap, out to 520 px at full charge, the arc growing as you hold; each shot costs 3% of your max health; it splashes 34 to everything within 70 px of where it lands, the same at any range, and leaves a 130 px pool slowing enemies 40% for 4 s (it doesn't heal); after 1 s it congeals, sticking enemies in it for 1 s; you drip blood as you walk and the glob rains drops as it flies), **Leech Tendrils** (hold fire: blood tendrils grasp up to 3 enemies in a 70° arc in front of you for as long as you hold on; they're stunned and drained slowly, and 25% of the drain heals you; spare tendrils pile onto what's held, so a lone boss takes all three), **Blood Blast** (4th key: your blood shaped into a blade, a six-step chain that never times out: **Crimson Slash** (22, a 120° sweep out to 115 px), **Backcut** (22, 130° back, 108 px), **Thorn Flurry** (three quick 9s across 100°, 85 px), **Vein Whirl** (18, a full spin, 95 px), **Sanguine Cleave** (40, a 70 px circle 105 px ahead that shoves 130 px) and **Crescent Flight** (26, an 80° crescent flung out 260 px), then Crimson Slash again. Each attack costs 1.3, 1.3, 1.7, 2, 3 and 2.3% of your max health, and puts one **blood stack** on every enemy it hits, however many of its cuts land, up to 10 an enemy. When a stacked enemy dies, whoever kills it, its blood flies back to you and heals you 2 a stack as it arrives, about a second later. **Alt, Crimson Tide:** 12% of your max health and an 8 s cooldown (a ring under you fills back up): the blood gathers for 0.65 s, then a 100° crescent rolls out 400 px along your aim, hitting every enemy it reaches once for 60, shoving it 150 px and adding a blood stack. Its Rare card **Blood Harvest** (two stacks): each stack heals 1 more, and an enemy holds 5 more) | **Blood Shell** (toggle: press to raise, press again to drop; a big bubble of blood, 96 px out, round you. It stops every enemy bullet, red aimed shots included, and enemies can't walk in; you and your allies can stand inside. Each bullet it stops costs you 4% of your max health and heals every ally inside for 4. It also bleeds you 1% of your max health a second while it stands. It stands until you press again or your health falls to a third, when it bursts and flings nearby enemies back; the 9 s cooldown starts then. Hardened Blood makes each hit cost 25% less and heal 50% more), **Blood Wall** (toggle, no upfront cost, 8 s, was Crimson Pact: press and a wall of blood is laid behind you wherever you walk, two braided veins bristling with thorns; standing still lays nothing. It stops every enemy bullet, red aimed shots included, and enemies can't walk through it: they're pushed back and bleed 12 a second while pressed against it. You and your allies walk straight through, and your shots fly over it. Each stretch stands 6 s, then crumbles. It drains 1% of your max health a second, doubling every 4 s (2% at 4 s, 4% at 8 s, 8% at 12 s, 16% at 16 s); the drain is shown over your head and in a ring round you that heats through Seep, Flow, Gush and Torrent. Healing still lands, so a healer stretches it (alone it takes a full-health tank to the blood floor in about 16 s), but nothing outheals the ramp. Press again to stop, or it ends at the blood floor (20%) or when you go down; the wall already laid stands out its time, and the cooldown starts when it ends. Blood Oath: the wall stands 2 s longer and the drain doubles 1 s slower), **Exsanguinate** (10%: barbed blood whips crack out and hook every enemy within 220 px; the hook tears their own veins out toward you, the two braid, and at 0.6 s one hard yank throws them in to touching you, stunned until they land. Each drains 20 into you as it lands. Heavy enemies and bosses aren't moved, but are still drained), **Blood Totem** (4th key, 6%, 2 s: plant a totem of bone and clot at your cursor, up to 420 px away. For 14 s a vein reaches out of its beating heart to each enemy within 180 px (up to 5 a totem, nearest first) and wraps round it: gripped enemies move at 40% speed, bosses at 70%. Up to 3 totems at once; a 4th bursts the oldest. An enemy two totems reach is held by one only, so they don't stack. Mastered: the veins also drain 6 health a second. Bloodroot: 20% more reach and 3 s longer) | **Vein Tithe** (75 s, was Blood Rite): a blood artery grows from you to every enemy within 380 px and holds it for 15 s; enemies that come inside that radius while it lasts are tethered too, so it can't move or shoot (enemies still spawning aren't held until they finish). You pay 0.4% of your max health a second for each enemy held (a boss costs four times that), move at 65% speed and deal 40% less damage; the drain gives no hit grace. It ends at 15 s, at the blood floor (20%), or when you go down, and every artery lets go. **Or Bloodbath** (75 s, key 1 of the lobby's Ultimate row): a channel. Nothing up front: blood floods the floor 170 px round where you cast, spreading out over 0.9 s (it doesn't follow you, and you can move and shoot), and while it runs it drains 4% of your max health a second (no hit grace). It runs up to 10 s; press it again to stop sooner (no cooldown spent), and it also stops when the drain brings you to the blood floor (20%), when you go down, or between waves. Once it stops the pool does nothing and dries up over 1.2 s. Enemies wading in it move 50% slower (Heavy enemies 35%, bosses 20%) and bleed 15 a second, bosses too, scaled by your damage and Blood Price; the bleed stops the moment they step out. Every bleed tick heals you 25% of its damage wherever you are. Allies standing in it, you included, take 20% less damage. With about four enemies wading in it, the heal pays for the drain **Or Blood Golem** (80 s, key 2 of the Ultimate row): for 2 s you gather (walking at half speed, and every enemy goes for you): every blood stack and bleed stack on an enemy within 300 px is torn off one at a time (6 damage each) and becomes a **coat** on you, and the coats it doesn't cover are paid from your health, 2.5% of your max health each, up to **12 coats** (a cost you can't pay at the blood floor adds nothing). Then you stand up as a golem of blood for **10 s**: each coat is **8% of your max health** in blood armour that takes every hit before your health, and coats shot off fall away as chunks; you walk at 75%. **Fire, Golem Fist** (your weapon is put away): left, right, then a **Slam**; a fist hits 45 in a 100° cone out to 120 px (0.42 s apart), the Slam 70 to everything within 120 px, stunning 0.5 s; every hit adds a Blood Blast stack. **Alt, Hurl Clot** (2 s): tear off a coat and throw it up to 400 px: 60 within 80 px where it lands and a Gore Mortar pool there for 4 s. An enemy the golem kills grows a coat back (up to 12). It ends after 10 s, when the armour is gone, when you go down or between waves, and **collapses**: each coat left bursts for 20 within 180 px, gives you back 2% of your max health, and allies in the burst 1% of theirs. Design: [`Docs/Mockups/blood-golem.html`](Docs/Mockups/blood-golem.html). **Or Sacrificial Ritual** (90 s, key 3 of the Ultimate row): a pentagram bleeds under you and five spikes close in over 1 s (you're rooted, and nothing can hurt you). You **die**: your body stays on the sigil and you rise as a **blood spirit** at 1 HP for **10 s**, moving 40% faster. Enemies can't see the spirit: none goes for it, and they go for your body instead (it can't be hurt). Nothing hurts the spirit, enemy shots pass through it, and you can't go down. **Fire, spirit claws** (your weapon and its Alt are put away): a scythe of blood sweeps a 150° arc out to 70 px for 40, three times a second (more at 1 HP, from the Hemomancer's blood bonus). Each enemy you kill as the spirit stores **10% of your max health** (a boss counts as 5), its blood streaming back to your body; a ring round the body shows the time left and ten pips the kills. When time runs out (or the wave ends) the spirit flies back and you stand up at your body with the stored health, up to full, or with **1 HP** if you got no kills. Ended during the rite, nothing happens. Design: [`Docs/Mockups/sacrificial-ritual.html`](Docs/Mockups/sacrificial-ritual.html). |
| **Healer** | **Transfusion Darts** (darts that give a hurt ally 20 health for 3% of your max health; each enemy hit steals a sliver, 0.5), **Sanguine Orb** (orbs that home in at the last moment and circle an enemy to cover it in blood, 16 plus a bleed stack, charging a heal reserve; with 25 reserve, an orb that touches a hurt ally coats them in healing blood, 30 over 4 s), **Vein Tether** (a real beam: it hurts whatever is on your aim line, so keep it on your target; 40% of its damage heals, split between you and the nearest ally), **Heart Flail** (4th key: a living heart on a chain of veins; click to swing it, hold to spin it, Alt throws it to root and heal allies near it; hits quicken its beat to a Cardiac Burst; see below) | **Blood Gift** (25% of current health: the most hurt ally heals twice what you paid; alone, a shield for you), **Blood Freeze** (25%, 14 s: a crimson shockwave bursts out 260 px around you after a 0.55 s charge and freezes every enemy it reaches for 5 s, bosses for 3 s; frozen enemies can't move or shoot; it does no damage and allies are untouched; an enemy that dies frozen, whoever kills it, heals you 3% of your max health), **Martyr** (40%: revive the downed ally nearest your cursor, anywhere, at 50%; if no one is down, the most hurt ally (you, alone) is warded for 12 s: going down puts them straight back up at 50%), **Clot Crystals** (4th key, 15%, 18 s: three crystals of hardened blood grow round you and circle 46 px out for 12 s. Each goes for the nearest enemy shot within 150 px of you that no other crystal is after: it shudders, jerks at where the shot will be and eats it; a shot that flies into one is eaten too. Each has 150 health and a shot takes its real damage off it (about a dozen shots), then it shatters. At the end they melt back into you, giving back 10% of the health they have left. Mastered: a 4th crystal. Hard Clot: 30% more health and 3 s longer) | **Sanguine Rain** (80 s): the Hemomancer pulls 12% of their health into a blood orb (1.8 s), then rain circles follow every ally for 10 s, healing each 3% of their max health every 0.5 s; it drains the caster 3% a second and ends when pressed again, when the time is up, or at the blood floor (20%)|
| **DPS** | **Needle Swarm** (160 blood needles circle you, blocking enemy shots (each breaks a needle; red aimed shots go through); missing ones are siphoned back, 32 a second for 0.14% max health each, never under the blood floor (20%); hold Fire to grow a tendril (140 px/s, 260 px reach) to the enemy nearest your cursor that wraps it in up to 30 needles, 4 damage a second each, slowing it 50% and adding a bleed stack every 0.5 s (the stack that brings it to five, from any source, bursts it for 36 within 80 px), until it dies; then the next, three at once; Alt throws 24 needles as a fan, 14 each, 2.5 s), **Implosion** (click an enemy: its blood collapses inward, 34, hurting enemies against it for 15 and pulling them in; fires only with your cursor on an enemy, up to 480 px away), **Blood Brawler** (click an enemy up to 520 px away and work its blood from range, a four-step string: Pulse 18 shoves it, Reel 15 pulls it 110 px in, Heart Lift 27 throws it in the air 0.7 s, stunned (hits on it airborne +25%), Marrow Burst 36 + 12 per hit landed earlier in the string bursts it into 8 bone shards, 24 each, 120 px; 1% of your max health a move, 2.5% for the Burst; Alt, Sympathy (8 s): binds up to 4 enemies within 170 px of the marked one, and for 4 s every move on it lands on them too at 50%), **Embolus Spears** (the 4th key: a blood spear in each hand, thrown left then right for 12% health each, they pierce and stick in walls; Fire again recalls both, piercing again, for the health back; Alt bursts them for half back) | **Arterial Spray** (a toggle, no upfront cost, 8 s: press to open a vein and a 60° jet of blood, 200 px long, follows your aim, 30 damage a second to everything in it (Blood Price makes it hit harder as you bleed), slowing it 20%, making it bleed and pushing small enemies back; it drains 4% of your max health a second; press again to stop, or it sputters out at the blood floor (20%); the cooldown starts when it stops), **Blood Debt** (16 s: your health drops to 25% (or it costs 5% if you're already that low) and the rest becomes a debt for 8 s: a kill pays back 4% of your max health (a Heavy, an elite or a boss 10%), hurting a boss 10% of the damage, and your hits heal you 15% of their damage; pay it all and the 15% lasts 2 s more; whatever is still owed after 8 s is lost), **Vein Rush** (10%, 6 s: rush 280 px toward your cursor as a streak of blood, untouchable; every enemy you pass through takes 25 and bleeds, and gives back 2% of your max health; you leave a trail of blood that slows enemies 30% for 2 s), **Thorn Bloom** (4th key, 12%, 8 s: twelve blood needles burst out of you all round, 240 px; each stops on the first enemy it meets for 45 and a bleed stack, and whips a tendril on to the nearest enemy no tendril has reached yet (180 px) for 30, which chains on: 3 jumps a needle, 20% less each jump; each needle that strikes heals you 1% of your max health; an enemy a needle struck is never a tendril's target) | **Crimson Harvest** (65 s): blood is torn out of every enemy in the arena (20 damage each, over about 1 s, and your hits heal you as usual) into a ball floating over you; each Grunt adds 1 blood, a Heavy 3, a boss 8, and the ball's area follows the blood. You move at 60% speed while it's up. Press the ability again to throw it at your cursor (up to 900 px; it throws itself after 5 s): it lands for 60 + 12 per blood at the core, falling to half at the edge of a 150 + 50·√blood px blast, flinging enemies (not bosses) and slowing them 35% for 4.5 s. The Blood Price cap is the normal +40% |

Capstones (mastery 5): Blood Shell also erases bullets within 160 px · Blood Wall: enemies against the wall take
double and are slowed 40% · Exsanguinate also slows what it drains by 50% for 2 s · Blood Gift also heals others within 200 px of
them for half · Blood Freeze: frozen enemies take 25% more damage · Martyr: you're invulnerable for 2 s after · Arterial Spray also
washes away enemy bullets it touches · Blood Debt: kills repay 50% more · Vein Rush's trail bursts after 1 s for 20 · Thorn Bloom's tendrils jump 5 times · Vein Tithe lasts 20 s and costs 25% less · Bloodbath spreads to 220 px and gives back 35% ·
Blood Golem lasts 13 s and each coat left bursts for 30 · Sacrificial Ritual's spirit lasts 12 s and each kill gives back 15% · Blood Ritual's portal stays open 10 s and pours out 50 imps ·
Sanguine Rain lasts 14 s and heals 4% a tick · Heartbeat's waves hit 40% harder · Heart of the Swarm's beats heal 11% · Blood Garden flings 16 seeds · Crimson Harvest holds the ball 8 s and tears out 25% more blood.

Players can pick the same role if you want. There is **no passive regeneration** by default (owner Nov 20; every preset is 0 HP/s, a Custom difficulty can add it, see
[Difficulty](#difficulty)): only upgrades such as Regeneration give it.

Enemies sometimes drop **health orbs** (in co-op each player gets their own copy, and you only see and drink yours; the Healer's
orbs, Medkits' and the arena's are shared): small green ones heal 15 HP (12% chance on Normal; 18% on Easy), and rare large ones
with a gold ring heal 60 HP (3% chance). Bosses always drop a large orb plus four small ones. Orbs drift
toward a hurt player who gets close, are left alone by players at full health (so your ally can grab
them), and blink before vanishing after 12 seconds.

### The Engineer

A builder whose constructions draw themselves in the cyan ink of the Expanding arena's walls (Docs/Engineer.md). Each
role has one robot weapon (below), one 1st ability and one ultimate for now, and the DPS also has the Bumble Remote; the lobby locks its other keys. It wears navy coveralls,
an orange safety vest and a yellow hard hat with a headlamp, in its role's trim. The DPS kit:

- **Draftsman** (passive): every vector gain is 25% bigger for an Engineer (part-vectors add up across gains).
- **Class card**: **Wide Field** (rare, x2, with Drag Pylon: the field 25% wider and slowing 10% more).
- **Drag Pylon** (1st ability, 6 s): plants a pylon at your cursor (up to 320 px away). Its violet floor grid (130 px) slows
  enemies inside by 45% (bosses 20%), and they trail drag rings. It stays until you plant the next one, which folds the
  old one up. Mastered: 40% wider.
- **Breach** (ultimate, 70 s): a vector hammer smashes the wall you aim at (up to 220 px): 80 damage and a shove within
  140 px. In the **Expanding arena** a free random room (Stash, Cross, L or T hall, Ring or Chamfer) opens behind the
  wall with one thing in it: **vectors** (+10), **gold** (40) or an **ambush** (two elite enemies of the wave's own types, shown as "?"). The loot
  waits in the middle of the room in a pulsing ring: the first player to walk up to it takes it (a Stash holds its usual
  vector cache instead). Elsewhere, or with no wall in reach, the blast and the loot (no ambush) land at your cursor at
  once. Mastered: the blast is twice as wide.

The **Tank** Engineer:

- **Vector Wall** (1st ability, 10 s): five glowing tiles across your aim, centred on the cursor (60-220 px away). They stop
  enemy shots, each soaking 400 damage, for 8 s. Mastered: seven tiles for 12 s.
- **Bastion** (ultimate, 75 s): a ring of 12 tiles round you (130 px) for 10 s, with four gaps to shoot out of. Mastered:
  the gaps are closed for the first 3 s and it lasts 14 s.

The **Healer** Engineer:

- **Med Pylon** (1st ability, 6 s): the Drag Pylon's green twin: its field (130 px) heals allies inside for 10 a second, with
  plus marks rising off the floor; one at a time. Mastered: 40% wider.
- **Safe Room** (ultimate, 80 s): the Breach hammer (half its damage) opens the wall you aim at; in the Expanding arena
  the room behind it heals everyone inside for 20 a second and cuts the damage they take by 30% for 12 s. With no room,
  a 160 px dome at your cursor does the same. Mastered: 18 s.

**Robot weapons** (each role's one weapon: DPS Vector Blade, Tank Iron Fist, Healer Stasis Gun): the first click (Fire) summons a robot Engineer of your role at your cursor
(up to 320 px away; it builds in over 0.8 s). It does the fighting: it walks into range of the enemy nearest you (within
360 px) and shoots it, and enemies near it go for it instead of you. A click on an enemy sends it at that enemy until it dies.
With the robot out, Fire is a basic attack. A broken robot can be summoned again 8 s later (until then Fire just attacks).
Its health grows 8% a wave and it is patched up between waves.

- **Iron Fist** (Tank): the **Tank Bot** (850 health, draws enemies from 240 px) fires a five-nail shotgun (6 a nail, 0.75 s).
  The basic attack is a robotic fist: a shove in an arc in front of you (95 px) for 10 damage that pushes enemies 180 px
  (heavy ones a third of that, bosses not at all), every 0.7 s. Each extra projectile: the arc reaches 15 px further.
- **Stasis Gun** (Healer): the **Medic Bot** (420 health, draws enemies from 150 px) fires rivets (9, 0.4 s) and heals allies
  within 110 px for 9 a second; with nothing to shoot it goes to the most hurt ally. The basic attack: hold Fire for a stasis
  field at your cursor (60 px, up to 300 px away). Enemies in it are held in place (bosses and heavy enemies slowed by half)
  and take 14 damage a second; letting go ends it. Each extra projectile: the field 8 px wider.
- **Vector Blade** (DPS): the **Assault Bot** (380 health, draws enemies from 150 px) fires a rapid rivet gun (11, 0.22 s).
  The basic attack is a melee swipe of a cyan blade in front of you (88 px) for 24, every 0.4 s. Each extra projectile: the
  swipe reaches 12 px further.

**Bumble Remote** (the DPS Engineer's 2nd weapon): you can't attack at all, only move. A giant robot bee, the **Bumble Bot**,
follows your cursor on a springy flight (up to 460 px from you) and does all the fighting; it can't be hurt, and enemies
ignore it and keep coming for you. While you're down it hovers over you and holds its fire.

- **Stinger Stream**: it shoots stingers (7 each) at the enemy nearest it (within 340 px) every 0.24 s on its own; hold Fire
  to double the rate (every 0.12 s), and with nothing near, holding Fire sprays them the way it faces.
- **Swarm Burst** (Alt, 6 s): it dives to your cursor and lands with a shockwave: 30 damage within 95 px, enemies shoved
  120 px out (heavy ones a third, bosses not at all), and 6 bees burst out of it to sting the enemies nearest them (8 each).
- Each extra projectile: one more stinger a shot (fanned) and one more bee in the Swarm Burst.

### The Arcanist

A mage of telekinesis, runes and enchantments: violet force and gold glyphs (Docs/Arcanist.md). Each role has one weapon, one
1st ability and one ultimate for now; the lobby locks its other keys. It wears indigo robes and a deep hood with a gold
circlet, small runes drifting round its head. 90% health, normal speed.

- **Kinetic Field** (passive): enemy bullets within 90 px of an Arcanist fly at 75% speed (a faint dashed ring shows it).

The **Tank** Arcanist:

- **Orbit Shards** (weapon): four violet crystal shards circle you (44 px), cutting small enemy bullets out of the air. Fire
  flings the shard most in front of you at your cursor (up to 360 px; Piercing Rounds send it farther): it cuts everything it
  passes for 18 on the way out and again on the way back. Hold **Alt** for **Shard Guard**: the shards pull in tight and spin
  fast, every bullet that reaches their ring (46 px) stops (not bosses' aimed shots), and you move at 55% speed and can't fling
  them. Each extra projectile: +1 shard, and one more flung with each click.
- **Kinetic Grasp** (1st ability, 10 s): lifts the biggest enemy (not a boss) within 150 px of your cursor (and 450 px of you)
  and holds it in front of you, stunned, as a shield: each bullet that hits it is stopped and hurts it for 10. Press again, or
  wait 4 s, to throw it along your aim (up to 380 px): it and the enemy it hits take 50, the one hit is knocked back and
  stunned (0.6 s). Nothing in reach: 80% of the cooldown comes back. Mastered: the throw hits 50% harder and stuns twice as long.
- **Grand Sigil** (ultimate, 75 s): a 200 px rune circle round you for 8 s. Enemies inside are bound for 1 s (not bosses),
  then slowed by half (bosses 20%) and drawn to you. Enemy bullets freeze where they meet its rim; when it ends, every frozen
  bullet (up to 80) flies straight back out as a spectral blade (14). Mastered: 12 s.

The **Healer** Arcanist:

- **Glyph Stylus** (weapon): writes gold rune bolts (10, 0.26 s) that go through two enemies; one that passes through an
  ally heals them for 7. Each extra projectile: +1 bolt, fanned out.
- **Arcane Tether** (1st ability, 12 s): pulls the ally nearest your cursor (up to 480 px, downed or not) to your side. They
  can't be hurt on the way and are healed for 30. Nobody in reach: 80% of the cooldown comes back. Mastered: heals 60.
- **Sanctum Sigil** (ultimate, 80 s): a 170 px healing circle follows you for 8 s: allies inside heal 25 a second. Six runes
  turn on its rim; each bursts on the first enemy to touch it (50 damage within 70 px and a shove) and is spent. Mastered: 12 s.

The **DPS** Arcanist:

- **Dancing Blades** (weapon): three enchanted swords hover behind you. Fire launches the one most in front at your cursor
  (up to 380 px; it homes onto the enemy nearest the cursor, within 70 px): it cuts what it passes (5), then dances on the
  spot for 1 s, slashing everything within 42 px four times (10 each; it dashes back and forth through the enemy in a star
  of crossing cuts, one through the middle on each slash), and flies back. **Alt** calls every blade home at
  once, cutting on the way. Each extra projectile: +1 blade.
- **Telekinetic Crush** (1st ability, a toggle on a **force meter**, no cooldown): press to open a 70 px grip at your cursor.
  It follows the cursor on a springy lag (up to 420 px from you) and lifts the regular enemies it touches, up to 6: they're
  carried round inside it, can't move or shoot, and take 6 damage a second. Heavy enemies are only slowed 60%; bosses aren't
  touched. Enemy shots that enter it (not unblockable ones) are caught and orbit inside, up to 10. Press again to let go:
  with the grip nearly still (under 220 px/s) it **slams** everything held together for 30, +10 for each other enemy (up to
  +40), and a 0.6 s stun; on the move it **flings** them on at the grip's speed, and one that hits a wall, a prop or another
  enemy at 220 px/s or more takes 25 and the enemy it hits 15, both stunned. Either way the caught shots fly back out as
  spectral blades (8 each): along the throw, or all round on a slam. The meter drains 22% a second, +3% for each enemy held
  (an empty grip lasts about 4.5 s), and refills 0.8 s after the grip closes, empty to full in 8.5 s (cooldown cards speed it
  up); it takes 25% to open. Run it dry and the grip lets go on its own and burns out for 1.5 s. The grip is a violet ring
  with a turning gold halo of runes and the meter round it, a tether of motes back to your hand, flickering red under 25%;
  the meter also rings your feet, and the key shows it as a percent ("ON" while open). Mastered: the grip is 40% wider.
- **Runic Array** (ultimate, 65 s): four gold sigils open behind you for 6 s. Every Fire also sends a spectral blade from each
  at your cursor (16, through one enemy), even while your blades are out. Mastered: 9 s.

### Weapons

Each role picks one of three starting weapons in the lobby. Damage, fire rate and Piercing upgrades work
with all of them.

**Extra projectiles** (Multishot) upgrade each weapon in its own
way instead of adding more bullets in a line. The Multishot card shows what it'll do for
your weapon. Per extra projectile:

| Class | Weapon | Base | Each extra projectile |
|---|---|---|---|
| Tank · Soldier | **Scattergun** | Short-range spread of 5 pellets (7); within 120 px each does double and shoves the enemy back. **Shock Grenade** (Alt): holds 3 grenades (the next one ringed by its progress) that load back over time, one every 5 s. Each Alt press lobs one to your cursor (up to 380 px); half a second later it bursts: every enemy within 110 px takes 30 damage and is stunned 1.8 s (0.7 s on bosses), with bolts jumping to each. Switching weapons refills it | +2 pellets and a 15% tighter cone |
| | **Flak Cannon** | Shells fly over enemies to your cursor and burst in the air (30 within 80 px), destroying enemy bullets in the burst. **Proximity Shell** (Alt, 5 s): lobs a finned shell to your cursor (as far as the cannon reaches) that arms in 0.55 s and lies there 15 s, blinking amber; an enemy within 40 px sets it off: a flak burst of 45 in 95 px that also destroys enemy bullets. Three at most (the oldest goes). A ring under your feet fills as it comes back | The burst scatters 2 bomblets that pop where they land |
| | **Riot Repeater** | Steady single shots (11) with a little spread; each round that hits suppresses the enemy: it shoots 8% slower, up to 50% (25% for bosses), fading 25% a second. **Tear Gas** (Alt, 9 s): lobs a canister to your cursor; after 0.5 s it bursts into a pale cloud 100 px round for 5.5 s. Enemies in it are suppressed to the cap at once (50%, bosses 25%) and slowed 30%. A ring under your feet fills as it comes back | Fires in bursts: +1 round per burst, then a longer pause |
| | **Grenade Launcher** (4th key) | A revolving drum of 6 HE grenades, 0.42 s apart, lobbed over enemies to your cursor (60 to 380 px): each spins in on an arc, hops once and bursts, 34 in 72 px (half at the edge) with a small shove; then a 1.5 s reload (the drum swings out and the hulls spill). **Smoke canister** (Alt, 0.55 s apart, while the HE drum keeps firing): each press lobs a canister that bounces twice and opens into a cloud 105 px round for 6 s. Regular enemies in it are slowed 45% and can't attack (a jammed-gun icon over them); bosses and mini-bosses are only slowed 30%. 3 canisters, one back every 6 s. Behind you: the drum's rounds and the canisters. Putting it away refills everything | +1 grenade a shot, fanned out |
| Tank · Elementalist | **Flamethrower** | A short stream of fire (about 190 px): 2 flames every 0.06 s that hit the first enemy for 3 and carry on through the rest for 1, setting them burning (5 a second for 3 s, renewed by every hit) | +1 flame a burst and 25% more reach |
| | **Water Gun** (the Flamethrower's variant: open its Variants window in the lobby and Equip it) | A long, narrow spray of water (about 370 px): a jet every 0.07 s (5 each, about 60 a second) that stops on the first enemy and **splashes**: that enemy and every enemy within 55 px of it are soaked **wet** for 3 s (slowed 30%, lightning arcs to them; they turn to steam if burning, and wind chills them). Sets nothing burning. A pump water gun with a cyan tank; the jets are cyan streaks that break into droplets at the end | +1 jet a burst and 25% more reach |
| | **Gale Gun** (the Flamethrower's second variant) | An air blower (about 230 px): 2 gusts every 0.06 s that pass through enemies like flames (2.5 to the first, a third behind) and **slice** them: a bleed of 60% of the hit a second (the shared bleed: its stacks, the Needle Swarm's burst and Cauterize all apply), a plain **slow** (30% for 0.6 s: Slowed, never Chilled), a light push (not heavies) and **wind** (it chills wet enemies and fans burning ones: Wind reactions). About 82 a second with the bleed. A grey leaf-blower with a spinning fan; the gusts are pale curls of wind | +1 gust a burst and 25% more reach |
| | **Frost Lance** | Hold Fire for a five-step chain that flows from one move into the next: **Shiver Crescent** (a 158° sweep ahead, 178 px, 80%), **Backhand Rime** (the sweep back, rimes the floor, 80%), **Frost Waltz** (a full spin, 170 px all round, 70%), **Crystal Fan** (five crystals erupt from the floor in a fan, 50% each) and the **Avalanche Slam** (an overhead slam 140 px ahead, a 112 px blast, 135%); stop for 1 s and it starts over. Every hit **chills** (the pips under the enemy, slowed 30%; one chill a step, two for the slam); 3 chill **freezes** a regular enemy for 2.4 s (heavies 1.2 s; bosses only slow). The slam **shatters** a frozen enemy for 2.5x. **Parry** at any moment breaks the step in your hands (its hit never lands) but keeps your place in the chain; a **perfect parry** primes the **Rime Counter** (a gold glow): the next step skips its wind-up and adds one more chill. **Alt: Absolute Zero** (14 s): a 0.7 s charge (you move at half speed and can't attack) as a snowflake rune draws itself under you, then a white wave freezes everything within 290 px (67%), and 0.7 s later a second wave shatters it (100%, 2.5x on the frozen). **Sounds:** glassy chimes on a cold E minor chord over airy rushes: the crescent sweeping up, the backhand sweeping down with rime hissing, the waltz wheeling with a rising run, five crystals crunching up out of the floor, the slam's deep swing and its heavy crash of ice; a creaking crackle and a tink as an enemy freezes, a burst of glass on each shatter, a brittle snap when a parry breaks the lance; Absolute Zero's charge swells like a cold choir, its wave booms with the whole chord, its shatter cascades (Weapons volume for the swings, Effects for the rest) | +20% damage and 10% more reach |
| | **Storm Hammer** | You haul a big steel hammer over your shoulder and throw it (it leaves your hand 0.22 s after the click): a 48 px hammer (40) tumbling end over end behind a storm cloud, with a bolt from the sky where it lands. It electrifies the enemy it hits: it arcs to 3 enemies round it three times over a second (5 each). **Alt (weapon action, 6 s cooldown): Thunder Slam**: you raise the hammer and drive it into the floor 52 px ahead (0.58 s), and a shockwave of lightning runs out 280 px, hurting every enemy it crosses once (40), shoving it 40 px (not bosses or heavies) and electrifying it (one arc). The hammer re-forms in your hand 0.2 s after the throw is ready, or after the slam. With Visual effects off the old small hammer is drawn | +15% hammer damage and 1 more enemy arced to (the slam hits 15% harder too) |
| | **Wind Clash** (4th key) | Hold Fire for a four-step combo of air waves, each hitting an enemy once and shoving it away (not heavies): a **Crescent** (110° arc that sweeps down across your aim as it flies out to 170 px, 18, 45 px shove), a **Back Crescent** (the same sweeping back up), a **Gale Lance** (a 28 px line of wind straight out to 330 px, 29, 70 px shove) and a **Clash Ring** bursting round you (to 100 px, 22, 85 px shove); 0.34 / 0.34 / 0.44 / 0.62 s between steps, and a 0.9 s pause starts it over. **Tempest** (Alt, 9 s): for 3 s a 120 px storm of wind blows round you: 9 to every enemy in it 5 times a second with a light shove, and every enemy shot that comes in is caught, spins round you and bursts; you move 30% slower and can't attack while it blows (its cooldown fills a ring under your feet) | +20% damage and 10% bigger waves |
| Tank · Summoner | **Golem Staff** | Hold to charge, let go to lob a boulder over enemies: 140 px and 20 for a tap, 460 px and 40 at full; crushes within 75 px; plus a small Titan-like Golem (rises from rubble, wind-up slam, soaks about 400 bullet damage) | +1 Golem (up to 3) |
| | **Bone Guard** | Darts (7) that stick: the third in one enemy within 3 s cages it for 1.5 s (not bosses); 2 orbiting skeletons block bullets and shoot caged enemies. **Marrow Tether**: a dart that hits a different enemy within 1 s of your last links the two with a marrow thread for 3 s; when one is caged, every enemy threaded to it is reeled 40 px toward the cage (not bosses or heavies) and slowed 30% while the cage holds. **Ossuary Wall** (Alt, 8 s): lobs a bone to your cursor (up to 320 px); a 110 px palisade of femurs rises across your aim for 3 s. Enemy shots stop on it, enemies can't walk through it (bosses can), and one pressed against it takes a dart (7) every 0.6 s, so it gets caged. A ring under your feet fills as it comes back | +1 Skeleton Guard |
| | **Bear Totem** | Melee: two quick claw swipes in front of you (85 px, 11 damage each), one claw on each hand (the left rakes across, then the right rakes back), that make enemies bleed (30% of the hit per second for 3 s, stacking up to five hits; each two hits leave another set of claw scars on the enemy), plus a Bear | +15 px reach, 25% more bleed, and the Bear's bites hit enemies around its target |
| | **Wyvern Staff** (the 4th weapon key) | A wind **Wyvern** hovers over you (it comes with the staff; after Fire or a slam it drifts slowly back to you). Its **gale** (95 px round it) slows the enemies in it 30% (bosses 15%), shoves them away (34 px a second; not bosses or heavies) and slows enemy shots 30%; every enemy in it feeds your **Cinder** meter. **Fire** (hold): the Wyvern flies out to 120 px short of your cursor (no farther than 420 px from you) and streams fire at it for as long as you hold and have Cinder: 22 damage a second and burning (4 a second for 2 s). It costs 30 Cinder a second (the meter holds 100 and refills 6 a second, plus 1.5 for each enemy in the gale) and needs 15 to start. **Alt (7 s): Wyrm Slam**: the Wyvern dives to your cursor and slams the ground: 30 damage to every enemy within 130 px, stunned 1.2 s (heavies 0.6 s, bosses 0.4 s), small enemy shots there cleared, and 10 Cinder back for each enemy stunned (up to 40). A ring under your feet fills as the slam comes back | +25% breath damage and a 10% bigger gale and slam |
| Healer · Soldier | **Repulsion Cannon** | Blasts enemies away in a 70° cone (240 px, 6 damage, 90 px push; bosses and heavies don't budge); enemies it hits shed tiny health orbs (heal 6, at most every 1.5 s each). **Undertow** (Alt): every blast that hits fills a meter (shown as a violet arc behind you), more per enemy the more it hits at once (1 enemy: 5, 2: 13, 3: 24, 4: 38, 5: 55, 6: 75, out of 100; no more past 8). Hold Alt (needs 10, the notch on the arc) to pull: a 60° current 300 px long drags enemies in it toward you at 140 px/s, stopping them just in front of you, slows them 40% and stings for 4 a second, while the meter drains 35 a second. You can't fire and move at 75% while pulling; let go (or run dry) and the next blast hits the crowd you gathered, filling the meter back up. Bosses and heavies don't budge. The pull has its own sound: an inhale as it starts, a swirling hum while you hold, a soft pop as it lets go. The meter empties if you change weapons | +20° wider, +2 damage and +30 px of push; the Undertow is 20° wider and pulls 30 px/s faster |
| | **Revolver** | A single-action gun with two stances (the weapon-action key switches, 0.25 s). **Gunslinger** (the one you start in): a **click fires** one hard, perfectly aimed round (50), and the **next click pulls the hammer back** (0.15 s), so it shoots exactly as fast as you click: about 100 damage a second at four clicks a second, up to about 4 shots a second. Holding Fire does nothing more, and a click while the gun is busy is kept and used the moment it can be. **Fan stance**: a click is the **Pistol Whip**: the gun swung across a 110° arc in front of you (120 px, 25 damage, a short shove) that **stuns everything it hits for 1 s** (bosses aren't stunned) and cuts small enemy shots in it; it doesn't need aim, so a tight pack all goes down together. **Click again** within 0.5 s for **Fan the Hammer**: 6 rounds in a 30° cone, 0.07 s apart (16 each, **20 on a stunned enemy**, 280 px), then the hammer reloads 1.2 s (a ring under you). **Hold the parry and click** to fan at any time, the one shot you can take with the shield up. **Raise the parry after a whip** and the combo starts over (your next click whips again: whip, parry, whip keeps a pack stunned), and raising it during a fan you started with a click cuts the rest (half the reload). Under you: one pip for the hammer (gold: cocked, the next click fires; an arc while it's pulled back), or in the Fan stance two diamonds (the whip, the fan) lit by the step | The shot goes through one more enemy, and Fan the Hammer fires 2 more rounds |
| | **Mender's Bow** | **Hold to draw, release to fire** (you move at 85% while drawing). A tap looses a weak arrow (8 damage, heals 5); a full draw (0.9 s) hits for 40, flies at 950 px/s, pierces 2 and heals an ally it passes for 20. Holding longer keeps it at full; it never fires by itself. **Focus meter**: as you draw, your aim line closes from a wedge to a single line; at full draw it turns into a meter with a gold zone at its far end (the last fifth) and a marker running out along it and back (every 1.3 s). Let go with the marker in the zone for a **Focus shot**: a gold arrow for 90 damage that goes through everything, heals an ally it passes for 30, flies at 1150 px/s and carries a gust of wind that shoves every enemy within 34 px of its path 70 px on and to the side (heavies half as far, bosses not at all); the bar bursts, FOCUS! pops up and your screen holds and shakes. Let go anywhere else and it's a plain full shot. **Alt: Scatter** (3 s cooldown, a teal arc under you while it cools): five small arrows in a 40° fan, 12 damage each, each heals an ally it passes for 6 and stops in the first enemy it meets, out to about 400 px; it drops a draw in progress. **Camera** (your own bow): as you draw, the view eases out (5% at full draw) and slides back against your aim, like riding the arrow back on the string; let go and it snaps forward past rest and settles in about a quarter second, kicking harder the fuller the draw (a tap barely, a Focus shot most, Scatter a medium kick). A Focus arrow's first hit holds the picture for a frame or two. The cursor stays on what it's over. Stronger with Screen shake on Full, off with it off or Reduced effects (every charge weapon gets this camera: see Screen shake under Settings) | +1 arrow nocked: a full draw also looses an extra arrow (half damage) in a tight fan; taps stay single |
| | **Twin Pistols** (4th key) | A pistol in each hand, **taking turns**: left, right, left (a round every 0.16 s, 10 damage, 780 px/s, within 1.5° of your aim). A hit can knock a **tiny health orb** loose (15% of hits, at most one per enemy every 1.5 s). **Alt: Laser Sights**: two thin red beams snap on from the muzzles and meet at your cursor (never closer than 120 px). For 4 s **both guns fire together** down the beams, a pair every 0.22 s, each round 14 damage (+40%), knocking orbs loose more often (25%); you move at 85%. 12 s cooldown from the press: a red bar over your head drains while the lasers are on, then a faint arc fills until it's ready | +1 round a shot, fanned a little, and Laser Sights lasts 1.5 s longer |
| Healer · Elementalist | **Radiant Orb** | Hold fire to cast an orb of light (20 px) out to your cursor, up to 520 px away, and keep it there while you hold: drag it through enemies (8 damage to each it touches, 5 times a second) and over allies (4 healing, 5 times a second); let go and it fades | +1 Light Orb (Barrage adds one too) that circles your orb while it's out (you otherwise), hurting enemies and healing allies it touches |
| | **Chain Light** | A 320 px cone of light (about 70° wide; Magnum Rounds widen it up to 100°, Homing Rounds narrow it 15% and strengthen it 10% a stack): 40 damage shared between every enemy in it (one alone takes it all), light arcing to each. Barrage adds cones edge to edge (60% damage each, like other weapons' Barrage copies), all within 200°: never a ring round you | +25% damage and a 15% longer cone |
| | **Nimbus** | A towering storm cloud floats over your cursor, drifting after it at 600 px/s and staying within 380 px of you. Click to call a forked bolt onto the spot beneath it (16 to each enemy in a 40 px blast, every 0.6 s; it leaves a scorch mark). Rain falls in a 70 px circle under it: it heals every ally standing in it, 8 a second, and leaves every enemy in it **wet** (as Tsunami: slowed 30%, and lightning arcs to it) while there and 1.5 s after. **Eye of the Storm** (Alt, 12 s cooldown): the cloud spins into a 140 px hurricane for 4 s, drifting after your cursor at half speed. Its arms drag enemies round and in (not heavies or bosses), hurt them 20 a second and soak them, and a bolt lashes an enemy in it every 0.33 s (10); enemy shots that enter it are torn apart; allies in its calm eye heal 16 a second. No strikes while it blows; it ends in a burst that shoves enemies out 70 px | +1 lightning strike a click, landing in a ring round the cloud |
| | **Static Shock** (4th key) | Hold fire for a short cone of constant static out of your hand (200 px, 64° across). Up to 6 enemies in it are wired into one circuit, drawn as jagged bolts: the nearest to your hand, each of the rest to the closest point already in it. Each is shocked 10 times a second, 6 a second at first, growing steadily to 24 a second after 4 s in the cone (it starts over once it has been out of every cone for 0.3 s); the bolt to it thickens and runs whiter as it grows. Room turrets in the cone take the same shock, and the Arc Cannon hits them too. Teammates the cone touches get **Overcharge** for 10 s (refreshed, never stacked; a gold glow, a gold ring draining round them with the seconds under it, and an OVERCHARGE pop): +25% move speed, +30% fire rate and shots 35% faster (same reach). **Alt: Arc Cannon** (5 s cooldown): a giant ball of lightning (24 px) rolls at 330 px/s to your cursor (120 to 560 px out), through everyone: 12 to each enemy it passes and Overcharge for each teammate; at the end it bursts (120 px): 30 to each enemy, a 0.8 s stun (not bosses or heavies), Overcharge for teammates in it | +1 enemy in the circuit and 15% more shock |
| Healer · Summoner | **Sprite Staff** | A firefly (7) that seeds the enemy it hits, then hops to the nearest enemy that isn't seeded yet (up to 2 hops within 300 px, so one shot can seed three), plus 2 healing sprites; killing a seeded enemy frees another sprite for 6 s (up to 4 more) | +1 Sprite |
| | **Treant Rod** | A thorny root whips out from you along your aim, growing from a knot of rootlets that curl round your feet (250 px, 15 damage) and snaps back, hitting everything on the line and healing allies it touches (10); plus a Treant that roots where it stands and blocks bullets; a root from you to it heals allies crossing it (8/s) and slows and stings enemies crossing it (50%, 6/s); it walks after you past 360 px; the Treant also flicks tiny thorn-dart seeds at the nearest enemy within 320 px (3 damage every 0.4 s, about 7.5 a second, your damage and fire rate apply; none while it is staggered) | The Treant grows 30%: blocks more, hits harder, reaches farther (its seeds fly 30% farther) |
| | **Spirit Chalk** | Hold and drag to draw a line of spirit ink (the chalk follows your cursor at up to 700 px/s, within 450 px of you, and the line is drawn as one smooth curve for everyone, head and all; 600 px of line at a time: **draw more and the start of the line rubs out as you go**, so you never run dry; a dab also fades slowly over 10 s). It burns enemies touching it (26 a second) and slows them 30%, and heals allies standing on it (10 a second). Enemy bullets pass through plain ink; the rare card **Warding Ink** makes it destroy bullets that cross it, but each bullet wears the dot it hit down by 1.5 s. **Close a loop** (the line meets itself, round at least about 60 x 60 px) and every enemy inside is **trapped** for 3.5 s: it can't cross the ring, and the ring glows gold. Bosses and mini-bosses can't be trapped. A hole shot through the ring (or the ring fading) lets them go. **Alt (your bound alt-fire key): Ignite.** Sets every sealed loop of yours that holds enemies alight: they burn for 40 a second for 3 s, the ring flares orange and holds for those 3 s, then lets them go. Needs a loop with enemies in it; 6 s cooldown (the ring under your feet) | +20% ink: a longer line before the start rubs out |
| | **Hive Censer** (the 4th weapon key) | Hold Fire to pour insects out of your cursor like powder (150 a second, up to 420 px from you). They carry on with the flick of your mouse, settle for half a second, then join your nearest swarm within 140 px or start a new one (up to 4). Each insect lives 9 s and you keep 900 at most (the oldest go first). A swarm goes for the nearest enemy within 260 px, else the nearest teammate (you too) whose chitin is under 80%, else hovers round you; it flies at 110 px/s. **On an enemy** it wraps it: 18 a second for a full swarm of 150 (scaled by its size) and a 40% slow (20% on bosses), with SLOWED under it. **Chitin**: a gold shell of plates round a teammate (and you), up to 40, that takes hits before any shield and wears off 5 a second; standing within 100 px of any swarm (a faint gold ring) builds it 22 a second for a full swarm. **Hive Call** (Alt, 6 s): every swarm of yours rushes to your cursor (320 px/s for 1.2 s), so you can drop a shell on a teammate in trouble or bury an enemy | +1 swarm and 20% more insects (poured and kept) |
| DPS · Soldier | **Twin Rifle** | Twin streams (4 per barrel) from two rifles held out wide, each stream starting at its rifle's muzzle (34 px ahead of your middle; pressed against a wall, at your edge), the barrels sharing a 60 px width (extra barrels fill it in, they don't widen it; Barrage's copies fire from a narrow 20 px cluster), angled in to cross at your cursor's distance (120 px to full range, about 575 px); hits within 45 px of the crossing are crossfire, for 1.5×. Up close the streams are still apart, so they can pass either side of a small enemy: aim at the crossing. Your aim line marks it with an X. **Grenade tubes** (Alt): a launcher under each rifle. Every 60 hits loads a grenade (a crossfire hit counts twice), two at most; they show as olive tubes on the guns and two diamonds behind you (the next one to load ringed by its progress, the next to fire lit). Alt fires one grenade a press (0.25 s apart), and the tubes take turns: with two loaded it fires the left, then the right, and that order holds even if you reload the left in between. The grenade flies down its rifle's stream to the crossing and bursts for 60 in 110 px with 8 shards. Firing one gives your screen a quick shake. The tubes empty if you change weapons. Each volley kicks your screen: a 5 px push the way you fire and a 1.5% zoom pulse round you (stronger with Screen shake on Full, off with it off or Reduced effects) | +1 barrel |
| | **Railgun** | **Hold to charge, let go to fire** (full charge 1.1 s; you move at 85% while charging) an instant **beam that ricochets** off the arena's edge, walls and pillars, hitting each enemy once along the whole line. A tap: 24 damage, 4 px wide, passes 2 enemies, 1 bounce. Half (0.5 s): 61, 11 px, passes 3, 2 bounces. Full: 120, 26 px, passes every enemy, 3 bounces. Let go within 0.25 s of full for a gold **Perfect** shot (+25%). From a quarter charge the line stays charged (1 s at half, 1.5 s full, 2 s perfect; 15 to 30 a second; paths don't stack: an enemy in several burns in the hottest). While charging you see the beam's width and bounces. Every shot **heats the gun** (+5 a tap, +8 half, +20 full, +25 perfect; a gauge behind you, up to 100; it stays until you dump it). **Alt: Heat Dump** (10 heat or more, 0.8 s cooldown) empties the meter into a 70° cone of fire: reach 140 px + 180 at full heat, 15 + 75 damage, a burn of 6 + 24 a second for 3 s that slows to 60%, and a shove of 60 + 200 px (at 100 heat: 320 px, 90 damage, 30/s, 260 px) | +1 bounce and 12% more damage |
| | **Burst SMG** | Very fast spray (5) that spins up as you keep firing: over 2 s the spray tightens from 9° to 2° either side and damage climbs to +60%; it winds down in 0.6 s when you stop | +1 bullet per shot (half damage) and a wider spray |
| | **Double Barrel** (the 4th weapon key) | A side-by-side shotgun. Each click fires one barrel (left, then right, 0.32 s apart): 9 pellets of 7 in a tight cone from your barrel, each passing through 2 enemies. Damage is full out to 140 px and falls to 35% at 400 px, so it is a close-range weapon, and the tracers dim as they fall off. **Two shells, then a break-open reload** (0.86 s, sped up by fire rate): the barrels tip open, the hulls flip out, two new shells go in and it snaps shut; it can't fire while open. **Alt** fires both barrels at once when both are loaded: 18 pellets in a wider cone, then the reload. Each shot kicks the gun back and the barrel that fired glows and smokes for about 2 s | +4 pellets a barrel |
| | **Twin Rifle Auto** (the Twin Rifle's variant: open its Variants window in the lobby and Equip it) | The Twin Rifle with the trigger taken away. Both rifles are held out to your sides and turn on their own: each aims at the nearest enemy on its side of you (left of your facing, right of your facing; a side with nobody on it borrows the other gun's target) and fires at it (4 a round, every 0.125 s) with a clear line, out to about 380 px (the Twin Rifle reaches about 530). A thin laser from each muzzle shows its range and what it is on: it ends in a cap where the range ends and a dot on the first enemy in its way. You can't shoot, and there are no grenades. **Alt** locks both rifles onto the enemy nearest your cursor (within 260 px of it) until it dies or you press Alt again: both streams then meet on it, so every round is crossfire (1.5×). A red reticle turns round the locked enemy and a thin tether runs from each muzzle. Same guns, shots, brass and heat as the Twin Rifle. Unlocked for testing | +1 round per gun, fanned a little |
| | **Out of Ammo** (the Twin Rifle's second variant: open its Variants window in the lobby and Equip it) | Two sets of arms. The **rifle arms** out at your sides work on their own and don't shoot: each swats the nearest enemy bullet within 72 px out of the air (one each, then a half-second recovery, so two at a time; the red unblockable shots still hit), and with an enemy within 78 px in front of you (about 57° either side of your facing) they bash in turn: left (10), right (10), left (10), then a **rifle clap** (35 to everything in front, a shockwave and a hard shove), and round again. **Your own arms** hold a **knife**, by hand: hold fire for a three-hit chain, cross cut (12), reverse cut (12), lunging stab (20, a step in and a small shove), as extra damage on top of the rifles. **Alt: spin** for 1.2 s: you cut everything within 88 px every 0.2 s for 14 and shove it out, and every enemy bullet that touches the spin is swatted with no limit; 6 s cooldown. Hits throw sparks and a flash, the blade and the rifles leave ribbons, the stab leaves afterimages and the clap cracks the floor. Unlocked for testing | +50% damage on every strike and spin tick |
| DPS · Elementalist | **Fireball** | A big, slow fireball that rolls through every enemy in its path (32 each), setting them burning (8 a second for 3 s) | +1 fireball, fanned out |
| | **Waterball** (the Fireball's variant: open its Variants window in the lobby and Equip it) | A big, slow ball of water that rolls through every enemy in its path (28 each) and soaks them: **wet** for 4 s (slowed 30%, 15% on heavies; lightning arcs to wet enemies). No burn | +1 waterball, fanned out |
| | **Earthball** (the Fireball's second variant) | A heavy ball of rock (slower, 0.6 s apart) that **stops on the first enemy**: 48 damage and a **stun** (0.8 s; 0.4 s on heavies, 0.2 s on bosses), then it breaks into rubble that hits everything within 70 px for 60%. Piercing Rounds let it carry on through | +1 earthball, fanned out |
| | **Arc Lightning** | **Lock-on chain.** Hold fire to scan a wide cone in front of you (100° across, 360 px long): every 0.14 s the enemy in it nearest your cursor is **tagged**, up to 10 (full in 1.4 s). A tag sticks if the enemy walks out of the cone; it's lost only if the enemy dies or the draw is cancelled. Each tag wears a crackling violet brand with a live wire back to your staff. Let go: lightning **chains** from your staff through every tag in the order you tagged them, one hop every 0.06 s, each hit for 16 + 4 per tag (10 tags: 56 each, 560 in all) and a 0.4 s stun (daze stars, not ice; not bosses). Let go with nothing tagged and it fizzles. **Alt: Forked Volley** (1.4 s cooldown): three quick forked bolts, 0.07 s apart, curve into the three enemies nearest your cursor (within 200 px of it), 14 each; one enemy alone takes all three; none there and they strike the ground. Sounds: a charge hum that rises with every tag, a crackle per tag that climbs a step each time, a thunder crack on letting go and a snap per hop. Barrage adds 2 tags a stack (up to 16), Homing Rounds widen the cone, Velocity and Piercing Rounds stretch it, fire rate tags faster | +25% damage |
| | **Ice Shards** | Each cast puts 3 shards of ice (7) into orbit round you (up to 6); they launch, homing, at enemies within 300 px, one every 0.08 s. With the orbit full, a cast flies at your aim | +1 shard a cast, +2 in orbit, and faster launches |
| | **Stonespear** (the 4th weapon key) | Fire with no spears up **slams** the ground: a 0.3 s wind-up (a dashed amber ring closes in round you), then 18 to every enemy within 110 px with a small shove (not bosses or heavies), the floor cracks and **6 earth spears** (3 a side) tear out of it and lift to hover fanned out at your sides, pointing at your cursor; they ease after you, so they trail behind when you move. Each **Fire** throws the risen spear nearest your cursor at it (24, 900 px/s, passes through 1 enemy; holding fire throws one every 0.18 s); a miss sticks in the ground and crumbles. **Alt** locks on to the enemy nearest your cursor (within 220 px; a ring marks it) and hurls every spear left at it, 0.06 s apart: each peels out to its side, then curves in on it, 30 each (on to the nearest other if it dies first; straight at your cursor with no enemy there). 0.6 s after the last spear leaves, the next Fire slams again. Sounds (Basalt): a low rumble swelling over the wind-up, a deep thump and the spears grinding out of the ground, a soft whoosh per throw, a dull thud and settling rubble on a hit, a short thump on a miss | +1 spear a side, and each click throws one more |
| DPS · Summoner | **Wolf Whistle** | Darts mark the enemy they hit as prey for 4 s and your wolves hunt it (a fanned or piercing dart that clips an enemy farther from your cursor doesn't move the prey); every wolf biting it within a second is a takedown (20 per wolf, an alpha counting as the wolves in it, 0.6 s stun; a lone wolf needs two bites). **The pack**: you start with 1 wolf; every 10 kills another joins, up to 5 slots (pips behind you, the next one ringed by your kills). With all 5 slots taken, the next wolf replaces your most wounded one (or heals the alpha by a wolf). **Soak**: 10% of every hit you take goes to the pack instead, the most wounded wolf first; a wolf at 0 health is gone (a wolf has 20 health, growing with the enemies' damage each wave; Pack Bond adds to it). A hurt wolf shows a health bar. **Alpha** (Alt): every small wolf merges into one big alpha that takes a single slot: bites as hard as its wolves ×1.15, runs 8% faster and is 15% bigger per extra wolf, and has their health. Alt again folds new wolves into it, up to 8 wolves' worth | +1 starting wolf |
| | **Wisp Lantern** | 3 wisps orbit you and are your shots: each shot sends one at the enemy nearest your cursor (22), and it comes back | +1 Wisp, and each shot sends one more |
| | **Imp Caller** | An imp every second (up to 5) waits by you; tap fire to send one charging at your cursor (45), or hold to merge them all into one big imp (blast 125% of theirs together, and wider) | +1 Imp, arriving 50% faster |
| | **Honeybee Staff** (the 4th weapon key) | Each click hatches a **bee** out of a wax cell at your cursor (no farther than 380 px from you; up to 8 out): it buzzes on the spot for 0.45 s, then darts at the enemy nearest it (within 320 px), **stings it once for 22 and dies**; with nothing to sting it hangs about and fizzles after 4 s. **Swarm**: three stings on one enemy, each within 0.5 s of the last, and from the third on they hit 40% harder (SWARM pops over it). **Alt (hold): Honey Spray**: the staff sprays honey globs in a short cone at your cursor (110-160 px out) while the honey meter behind you lasts (about 2.6 s; it refills 0.6 s after you let go, and a dry staff waits for you to let go). They land as **honey puddles** (3 s) that **slow enemies 40%**; globs landing in a puddle grow it. **Harvest**: an enemy that dies in your honey bursts open and **2 free bees** fly out of it at the nearest enemies (they don't count against the 8). Barrage hatches one more bee a click | +1 bee a click (fanned round the cursor), and 2 more can be out |
| Tank · Knight | **Warhammer** | Charge slam: hold to charge (0.9 s), let go to slam a fixed 160 px circle centred about 100 px ahead (it clears small bullets too); 20 damage and a 0.25 s stun for a tap, up to 80 damage and a 0.9 s stun at full charge; the slam sends a shockwave that shoves enemies 30 px (55 px at full charge) away from its centre | +1 aftershock: another slam 0.2 s later, 40 px further out (60% damage) |
| | **Lance** | The **Breach**, held in the right hand (the Winged War Lance: an ash shaft, a steel leaf head and a blue swallow-tail pennant) with a heater shield on the left arm. A four-step chain, each step moving you a little forward: **Jab** (14, a 178 × 32 px box straight ahead, a 30 px shove), **Second Jab** (the same, a touch wider), **Wide Sweep** (16, the shaft swung flat across 150° in front of you, batting each enemy it reaches to your left, 85 px), **Breaching Thrust** (a 0.2 s wind-up and a 34 px step in, then 40 to everything in a 205 × 88 px box straight ahead, shoved 160 px back and stunned for 1 s; not bosses, and heavy enemies aren't shoved). Hold the button and it plays on by itself; a press counts once a step is 80% through (90% for the Thrust), and the chain starts over 0.7 s after a step ends without one (one whole chain is 84 damage in about 1.4 s). **Impact:** an enemy one of these shoves is still sliding fast (34 px or more of the push left) that meets another enemy: both take 10 and the one it hits slides on with 70% of the push, so one shove can bowl a line; into the arena wall it takes 18 and is stunned 0.8 s (heavy enemies and bosses take nothing and aren't moved). Impact damage shows in orange with an IMPACT or WALL tag. **Hook & Haul** (weapon-action key, 4 s cooldown): the lance shoots out through your glove to the enemy nearest your cursor (up to 260 px), hooks it for 10, stuns it 0.8 s and hauls it to 62 px in front of you (heavy enemies and bosses stay put), and your next press is the Breaching Thrust. Four diamonds over your head show the chain; the fourth glows when the Thrust is next or a hook has primed it, and a ring under your feet fills as the hook comes back. The Lance has its own sounds (a shaft whoosh for the jabs, a wide swoosh with the pennant fluttering for the sweep, a breath and a driving thrust for the Breaching Thrust, dull steel hits, a crunch for an Impact and cracking stone for a wall, the hook rattling out and catching), and your own hits hold the picture for a moment and shake it, the Thrust most | The Breaching Thrust: +30 px reach, 24 px wider and +60 px knockback |
| | **Halberd** | Reaper's Wheel: Sweep (held in two hands, drawn back for 0.05 s, then an 18° blade, 200 px long, swept across 80° in 0.21 s; 30 to each enemy it passes, once a sweep), Return Sweep (24, the head turns over first), Overhead Chop (44 on a 240 × 40 px line along your aim, stuns 0.6 s) and Reaping Wheel (28 all round you out to 170 px, shoves 60 px), then over; the chain starts over 0.9 s after a step. Weapon-action key, held: Set the Pike (35% speed, no swings; the pike turns to the nearest enemy on its own and pokes it for 22, shoving it 110 px away and stunning it 0.3 s, every 0.45 s at most); let go for a Pike Lunge (40 on a 260 px line along the pike, shoves 80 px, stuns 0.4 s, every 1.2 s at most) | +30 px reach and +10% damage on every step and poke |
| | **Bulwark Barrier** (the 4th weapon key) | A kite shield that always guards your front; a four-step chain (Rim Cut, Backhand, Boss Slam, Shield Throw) and a Brace on the weapon-action key that fills Ram for a Shield Bash: see the Knight summary above | +15 px reach and +10% damage on the chain, and +20% Shield Bash damage |
| Healer · Knight | **Quarterstaff** | Alternating left and right 130° swipes (120 px, 18) that knock enemies back 70 px; each swipe parries: enemy bullets in its arc fly back at enemies as your shots (12), big orbs too, but never red aimed shots; each enemy hit heals you and allies within 220 px for 2 (up to 8 a swipe) and adds a Jab stack (3 at most, they never fade). The weapon-action key (Left Alt, held to repeat every 0.32 s) spends one stack on a jab: a 48° cone 170 px out, 30 damage, shoves enemies back 110 px, bats bullets in the cone, heals for 3 per enemy (up to 3 enemies). Held at the ready in both hands, like the Halberd | +1 Jab stack limit |
| | **Lightbringer** | A three-step chain with a longsword, each step moving you a little forward: **Dawn Cut** (a 100° rising cut, 17, plus a crescent of light, 12, pierces 2, that heals the first hurt ally it passes for 6), **Mercy Cut** (the backhand return cut, 17, whose crescent puts a 12 HP ward on the first ally it passes for 3 s), **Noon Sunburst** (the sword planted point-first: 22 to everything within 140 px, shoving it back, and every ally in the ring heals 10, you 5). A press counts once a step is 85% through; the chain starts over 0.8 s after a step ends. Every enemy a cut hits adds a light pip (the Sunburst two), up to 12. Full, the next Noon is **Radiant**: a 220 px ring that heals three times as much (30), wards everyone in it and spends the gauge. **Sunlance** (weapon-action key, 1.5 s): with 4 pips or more, a lance of light to the ally nearest your cursor (up to 450 px): it heals them 4 a pip (a downed ally gets a quarter of their revive instead), hits every enemy on the line for 10 + 2 a pip and spends all the light | +1 crescent on Dawn and Mercy, fanned out, and +30 px on the Sunburst |
| | **Censer** | The Thurible Rite, a censer on a chain held in both hands: **Sweep** (200° in front, 13, 85 px) and **Return Sweep** (back the other way) each heal allies within 140 px for 3 and leave a puff of incense where the head ends (42 px, 2.5 s); the **Benediction** lets the chain out for a full turn (18, 115 px), heals allies within 170 px for 6 +1 per enemy it hit (up to +4) and leaves an incense cloud at your feet (75 px, 4 s); the rite starts over 0.6 s after you stop. Each enemy a step hits lights an **Ember** (up to 5, they never fade). **Censer Cast** (weapon-action key, needs an Ember, 6 s cooldown): the head is thrown to your cursor (up to 280 px), bursts for 8 per Ember within 50 px, and leaves an incense cloud for 4 s (60 px +6 per Ember) healing 2 +1 per Ember HP/s; it spends every Ember. A downed ally in the cloud gets a fifth of their revive the moment it lands and is revived on top of any teammate's help while it lasts (twice as fast). Incense heals allies in it (4/s) and enemy shots fired inside it stray; walking leaves none. The Censer has its own sounds: smoky whooshes with a rattling chain, a bronze clonk and crackling embers on a hit, a soft chime as a heal reaches an ally, a church bell for the Benediction, the chain paying out for the cast and a gong and hiss of incense where it lands | +15 px reach, +1 HP on every heal and +1 Ember |
| | **Vowguard** (the 4th weapon key) | An arming sword and a heater shield with two stances (the Radiant look: a white blade edged in the stance's colour, a faceted shield round a glowing core that throws a lattice of light while it guards; strikes wind up, hits hold a moment, and a fast blade leaves afterimages); the weapon-action key switches between them (0.25 s; each keeps its own place). Each stance plays a **chain of up to 10 steps** you build from its nine moves: with the Vowguard picked, **EDIT CHAINS** in the weapon's box opens the chain editor over the loadout (click a move to add it to the end, click a step to select it and the next move replaces it, the arrows move it, **X** removes it, **CLEAR** leaves one step; presets Crusader, Shepherd, Bastion and Marathon fill both); the preview plays your chains, and they are saved with your profile. Fire plays the next step; hold it to play on. A press counts once a step is 75% through; the chain starts over 0.8 s after a step ends without one, and after its last step. **Valor** (gold): full speed; the shield only blocks during shield moves. Links: **Edge Cut** (16, a 110° cut, 115 px), **Rim Bash** (12, 75 px, 0.4 s stun; blocks while out), **Lunge Thrust** (20 down a 165 px line), **Shield Hook** (10, 95 px, drags 50 px in), **Twin Strike** (a cut, 10, then a bash, 10 and a 0.3 s stun), **Mercy Spin** (13 all round within 95 px, heals you 3). Finishers: **Judgment Cleave** (34 down a 360 px line; allies ahead of you within 300 px heal 8), **Shield Charge** (a 140 px dash shield first, untouchable: 30 and an 80 px shove), **Hallowed Slam** (28 within 120 px, 0.6 s stun; allies within 160 px heal 10). **Sanctuary** (jade): 80% speed; small enemy shots (not red aimed ones) in a 100° arc in front stop on the shield. Links: **Raise Guard** (a 160° guard for 0.4 s; each shot it stops heals the most hurt ally 2), **Guarded Jab** (10, 140 px), **Deflect Sweep** (8, 140°, 80 px; small shots in the arc fly back as yours), **Sidestep** (a 60 px hop to the left, untouchable 0.2 s), **Cover Ally** (a dash in front of the ally nearest your aim within 220 px: they heal 4 and take 30% less damage for 2 s), **Rally Tap** (allies within 160 px heal 4). Finishers: **Bulwark Absorb** (soaks every shot in front for 0.7 s, then a 170 px pulse: 12 to enemies, allies heal 6 + 2 a shot soaked, up to +16), **Aegis Dome** (plants a 110 px dome for 2 s that stops enemy shots at its rim; allies inside heal 3 a second), **Sanctified Ward** (every ally within 180 px gets a 15 HP ward for 3 s and heals 4). **Faith** (up to 6, dots under your feet): Valor links light one per enemy hit, Sanctuary one per shot the shield stops, a perfect parry fills it; a finisher spends it all for +3 damage and +2 healing a Faith. Raising the parry starts both chains over. Two rows of diamonds over your head show both chains (the stance you're in on top; finishers edged white) | +15% damage and healing on every step |
| DPS · Knight | **Twin Blades** | The **Crescent Waltz**, a sabre in each hand. A five-step chain, each step moving you a little forward: **Rising Cut** (16, a 110° cut, right hand, shoving 18 px), **Falling Cut** (16, the left hand back, shoving 18 px), **Double Jab** (two 130 px thrusts, 12 each, shoving 18 px each), **Pirouette** (a reverse turn, 13 to everything within 92 px twice, shoving 18 px each turn; cuts small bullets), **Crescent Shear** (44, both blades raised behind and swept 200° across, shoving 18 px, and a piercing crescent wave for 30). The shoves keep bodies off you as you step in; heavy enemies and bosses aren't moved. Hold the button and it plays on by itself; a press counts once a step is most of the way through, and the chain starts over 0.7 s after a step ends without one (one whole chain is 156 damage in about 1.5 s). Five diamonds over your head show the chain; the fifth glows when the Shear is next. **Twin Fang** (weapon-action key, 3.5 s cooldown from the catch): both sabres are thrown as one spinning pinwheel that flies to your cursor and follows it for 2.2 s, cutting every enemy within 44 px for 12 (each again every 0.2 s) and small bullets too. Enemies don't stop it; a wall, pillar or the arena's edge does (a clang), and it flies back to your hand. You can't swing until you catch it. A ring under your feet fills as it comes back. The Twin Blades have their own sounds (light ringing cuts, a whirl for the Pirouette, a tearing Shear, a spinning throw, a clang when the pair strikes a wall, the catch), and your own hits hold the picture for a moment and shake it, sized to the move: a short hold on each cut, a kick on the Pirouette, the longest on the Shear and Twin Fang striking a wall (off with Reduced effects; the shake off with Screen shake off) | +1 crescent: the Pirouette and the Crescent Shear each launch one more wave, fanned out |
| | **Greatsword** | A ten-step chain, played in order; the chain starts over 0.7 s after a step ends without a press. **Undertow** (22, 210°, 155 px) drags what it hits 55 px toward you · **Pommel Bash** (13, 58 px) stuns for 0.8 s, quick · **Cross Cut** two diagonal cuts (26 each, 112 px) · Pommel Bash · **Backstep Reap** (31, 190°) hops you 70 px back and leaves a 0.7 s crescent that stops enemy bullets · **Comet Leap** jumps you 150 px forward (untouchable in the air) and lands in an 88 px ring (62, 60 px shove) · Pommel Bash · Undertow · **Full Moon** a full spin (48, 108 px) that leaves a ring of steel for 0.5 s · **Eclipse** winds up 0.45 s, then a golden lance 290 px long (170) and a 120 px ring where it strikes (26). Each hit lands when the blade gets there. **Moonwheel** (weapon-action key, 4 s from the catch): the sword spins out to your cursor (140-290 px) and back, cutting each enemy it passes once each way (35) and small shots; a wall turns it back; no swings until you catch it. Ten diamonds behind you show the chain (the tenth, Eclipse, gold). Held at the ready in both hands | +1 sword wave: Eclipse launches a piercing crescent |
| | **Rapier** | Narrow 130 px thrusts (18, always the same) at the nearest enemy in front, each skewering up to 3 enemy bullets along its line (not red aimed shots). **Tempo:** every enemy a thrust hits adds a stack, up to 20 (skewered bullets add none). The wait between thrusts goes from 0.48 s at 0 Tempo to 0.16 s at 20, evenly between (fire-rate upgrades speed it up further, never under 0.08 s); from 0 to full takes about 6.5 s of hits on one enemy. Tempo holds for 1.2 s after your last hit, then drains 2 stacks a second. **Riposte:** press the weapon-action key to stand guard (press again to drop it): you move at 40% speed, no thrusts, Tempo stops draining, and enemy bullets in a 140° arc in front of you are sent back at the enemy as your own (12 damage; not red aimed shots). Each reflected bullet adds 10% charge (a full charge from 10 bullets); the charge also creeps up on its own, very slowly (4% a second). Click to lunge along your aim, untouchable: the reach runs from 130 px to 720 px with the charge, and every enemy on the line takes 20 to 70 damage and adds a stack of Tempo. A click with almost no charge just drops the stance. A violet arc shows the guard, a gold-turning inner arc the charge. (The old Flourish is still in the code but no longer on the key.) Diamonds on an arc behind you, one per stack, fill as Tempo builds and all turn gold at 20; a thin arc under them runs down through the grace period, and the top diamond shows orange while Tempo drains. Held at the ready, point forward, like the Halberd | Skewers 1 more enemy behind the first |
| | **Arc Knuckles** (the 4th weapon key) | Coiled blue gauntlets (the Arc Coil look) with **two stances**; the weapon-action key swaps them (each starts at its first move). **Brawl** (the default): each click is one move (held Fire repeats; a click during a move waits 0.3 s for it). **Pull Punch**: click an enemy up to 230 px away (within 70 px of the cursor; a faint reticle marks it): you're pulled up to it and punch it (18; everything within 50 px is stunned 0.5 s). It opens the **click wheel**, fixed on the screen where you clicked: four sides, the one under your cursor lit; from now on the side you click on is the way the punches send enemies and the dashes go (its centre is no direction), and the movement keys only move you. **Drive Punch** (22): the enemy nearest you within 150 px (you step in if it's out of reach) and everything within 60 px of it fly 190 px the clicked way (with no direction: on away from you). An enemy a Drive Punch sends into the arena's wall takes a **Wall Slam** (11 and a 0.6 s stun). **Slip Dash**: 130 px the clicked way (no direction: at the cursor), untouchable. Then a second **Drive Punch** (25) and **Slip Dash**, the **Pin Punch** (35: stuns the group within 85 px for 1.8 s and stops it where it is), the **Barrage** (eight jabs, 8 each, and a straight, 15, on everything within 75 px of the enemy nearest you; you're rooted; a stunned enemy stays stunned through it) and the **Quake Punch**: hold Fire to charge (a ring fills round you, gold when full at 1.2 s; it lets go on its own 0.6 s after), let go to punch the ground: 31-66 to everything within 90-180 px of you, thrown back 60-160 px. Then it's back to the Pull Punch. A punch with no enemy in reach swings at the air and keeps your place; 1.6 s without a move starts the combo over (the wheel closes). Eight blue diamonds over your head show the combo; the wheel names the next move. **Rocket** (four ember diamonds): one click per move, no windows to hit. The **Rocket Dash** locks onto the enemy nearest your cursor within 300 px (a reticle shows which) and the lead gauntlet's thruster throws you up to it (up to 280 px, 650 px a second), fist first and untouchable; that enemy takes 44 and a short 24 px shove, even with another in the way. With nothing in reach it flies 130 px along your aim and hits the first enemy in the lane (a dash that hits nothing can't be thrown again for 0.6 s). When it hits, the chain waits for your clicks (a pulsing ring and chevrons; up to 4 s, then it's back to the dash), its moves locked onto the dashed enemy: the **Flurry** where you landed (eight alternating jabs, 11 each, then a straight, 37, 12 px shove; you're rooted), **Grab & Throw** (the Flurry's target, if it's within 110 px and not a boss: swung round you once and thrown along your aim at 520 px/s, up to 260 px; it takes 37 and the enemy it hits 48, a 42 px shove and a 0.5 s stun; a wall, pillar or crate stops it, and a held enemy is never pulled through one; you're rooted), and the **Rocket Punch** (the fist fires off on its chain at the thrown enemy, down a line 56 px wide that runs 80 px past it, 210-300 px long: 51 to everything on it, 18 px shove, then reels back). A boss or raising the parry ends the chain. In either stance raising the parry cancels a punch still winding up and starts the stance over; a **perfect parry** arms the **Counter Hammer** (the diamonds turn gold; it fades after 3 s unused): your next click is a gold slam 44 px ahead (70, 165 px, knocks back 48 px, stuns 0.8 s, clears small bullets). Their stuns are stuns, not freezes: no ice, just stars over the head. Each punch flashes where it lands (a lane of arc light and its blast's ring, gold for the Pin Punch), as do the Barrage's and the Flurry's jabs and the Rocket Punch's whole line. Every punch that connects has its own knock, sparks and a ring, and on your screen the picture holds for a moment and shakes, sized to the punch (a jab of a Barrage or Flurry barely; the slams, the dash and a thrown enemy landing the most; off with Reduced effects, the shake off with Screen shake off) | +1 aftershock: the Quake Punch and the Counter Hammer slam again 0.2 s later, 40 px wider (60% damage) |
| Tank · Ninja | **Kusarigama** | Press once to cut with the sickle (16, a 144 degree arc), again within a second to throw the weight 280 px at the first enemy in its lane (34, stuns 0.4 s and drags it to you). Hold the weapon-action key to whirl: the weight circles you (70 to 150 px out) for up to 3 s, pulling everything within 200 px in and cutting for 12 a hit; you move at 55% speed and can't swing. Let go for a snap (30, stuns 0.6 s, knocks back 60 px), then 3 s to cool down. Drawn in ink: a curved sickle on a wooden grip, a chain of links and a small iron weight; the cut paints a lilac brush stroke along the sickle's path that dries out in half a second, the throw drags a brush line and splashes where it bites, the whirl paints an ensō (a brush circle) behind the weight and the snap bursts into splatter. The whirl's time left is a lilac ring round the weight that empties, and the hanging weight glows lilac while your next press throws it. With Visual effects off the ink is left out and the sickle, chain and weight still show | +40 px throw and +20% damage |
| | **Bo Staff** | A three-step chain with no timer, however long you wait: a 130° cone slash (16, 105 px, knocks back 18 px), a jab (22, 140 px, 28 px wide, 18 px knockback), then a full spin (30, 100 px, 18 px knockback) that starts it over; every swing cuts small bullets. Hold the button and it plays on by itself. Three pips over your head show where you are, and the third pulses when the next click is the spin. The weapon-action key **shoves**: the staff turns sideways and drives out as a bar 100 px wide and 78 px deep (6 damage, knocks enemies straight back 70 px, not bosses or heavies), once a second, and starts the chain over (so click, Alt, click is slash, shove, slash). It is held in both hands at the ready, like the Halberd. The swings glow in the staff's sage green; the shove pulls back, drives out and sends pressure arcs off its front | +20 px reach and +15% damage on every swing and the shove, and +20 px on the shove's push |
| | **Fuma Shuriken** | A giant star (30) thrown in an oval loop 380 px out in front of you and back, cutting through everything both ways. **Orbit stance** (weapon-action key, again to go back): the star circles you at 80 px (8 a pass, 0.3 s between passes on one enemy) and slices small enemy bullets out of the air; Fire **Lashes** it 190 px out across your aim, one way then back (24), and every third press is the **Cyclone**, one lap out to 160 px (20). Raising the parry: **Reversal**, the circle flips and spins 1.5x faster for 2 s, cutting everything in its ring for 30 (1 s cooldown); a **perfect parry** keeps the shot as a small star circling you (up to 5), and 1.3 s later they fly back at whoever fired them (25 each). In the Throw stance, raising the parry while a star is out catches it into orbit from where it was | +1 shuriken, looping the other way (Orbit stance: +1 star on the circle, up to 4) |
| | **Nodachi** (the 4th weapon key) | A katana as tall as you, held in two hands, with two stances. The weapon-action key switches stance (0.3 s); each stance has its own five-step Fire combo, and **neither ever goes back down**: no timer resets it, and switching keeps both places (the step playing stops). Raising the parry sends both back to their first step. Two rows of pips over your head show both places: the stance you're in on top, the other dim under it. Each step eases out of wherever the last left the blade, so the chain flows. **Arashi** (Storm, the blade low and trailing; steel and vermilion): every wind-up is armoured (40% less damage taken), and the sweeps cut small bullets. **Nukitsuke** (a 200° draw cut, 150 px, 20), **Kesagiri** (a 150° diagonal back across, 165 px, 25), **Engetsu** (a full circle with the tip, 210 px, 30), **Tenchi** ◆ (a spin, 150 px, 22, carried round into a two-handed slam: a 190 px ring, 58, stuns 0.8 s, cuts the small bullets in it), **Nagi** (two 300° sweeps there and back, 185 px, 18 each, walking you 100 px forward). **Mamori** (Guard, the point at their eyes; jade): you walk at 85%, and while a jab is out small enemy shots in a 90° cone in front (30-110 px) are blocked (not red aimed shots). **Kasumi-zuki** (a 175 px jab, 12), **Nidan-zuki** (two jabs, 150 and 190 px, 9 each), **Senbon-zuki** ◆ (seven jabs scattered over a 34° cone, 185 px, 7 each), **Tsuka-ate** (the pommel driven into whoever is in your face: 70 px, 15, stuns 0.5 s, shoves 50 px), **Hiki-zuki** (a hop 70 px back, untouchable, then a 190 px jab, 17). Its sounds are its own: heavy air under each sweep with the blade ringing after, a creak on the armoured wind-ups, Tenchi's boom, hard whistles for the jabs, a tink for each shot blocked and a shing on each switch. About 50 damage a second on one target in either stance | +1 ring after Tenchi's slam, 45 px farther out (60%) |
| Healer · Ninja | **Acupuncture Needles** | **Healing needles** (900 px/s, every 0.18 s) fly through enemies and stick in the first hurt teammate they touch (full-health ones are passed by). Each needle in them heals 3 a second; up to 5 at once, all falling out 4 s after the last went in. The fifth **opens the meridian**: all five come out at once in a jade burst that heals 15. **Alt: Bloodletting** (0.9 s cooldown): three long needles in a tight fan (760 px/s, 420 px), each hitting for 10, sticking in the enemy and adding a stack of bleed (3 a second). The fifth needle in one enemy bursts at the **pressure point**: 25 more (15 on a boss) and the bleed renewed. **Needles Return**: an enemy that dies with needles in it sends them flying back to the most hurt player (you too), healing 4 each. Drawn as steel needles with jade (healing) or blood-red (Bloodletting) beads, stuck where they hit, five pips under a teammate counting toward the meridian; a red ring under your feet fills while Bloodletting cools down | +1 healing needle, fanned out, and +1 Bloodletting needle |
| | **Spirit Fan** | A short gust (6) that heals allies in it for 3 and blows small bullets back; shots alternate blowing enemies in front 60 px away and drawing them 60 px in | +25% reach and +1 HP healed |
| | **Ofuda** | **Healing Talisman**: a paper charm flies straight (380 px/s, 420 px, every 0.45 s); over the last 35% of its flight it bends toward the most hurt teammate within 150 px. It bursts on a teammate (+10 to them), on an enemy, or where its flight ends: 55 px round it, teammates (you too) heal 5, enemies are shoved 90 px (not heavies) and small enemy bullets are erased. No damage. **Alt: Blast Ofuda** (1.1 s cooldown): a charm flies straight (460 px/s, 380 px, no homing) and sticks to the first enemy it hits, blowing up 0.8 s later; one that hits nothing lands and blows up 1.5 s later. The blast: 45 within 75 px (60% at the rim; full to the enemy it's stuck to). An enemy that dies under a charm drops it, still lit | +1 talisman and +1 Blast Ofuda, fanned out |
| | **Twin Tessen** (the 4th weapon key) | An iron war fan in each hand, melee. **Fire: the Crane Dance**, five steps (press within 0.7 s of a step ending to chain, or it starts over): **Left Flick** and **Right Flick** (110° sweeps, 105 px, 10; allies they brush heal 4), **Scissor Cut** (both fans cross in front: 70°, 125 px, 16, cuts small bullets), **Petal Spin** (a full twirl, 115 px, 8 twice, cuts small bullets; 6 petals fly to the most hurt player within 420 px, you too, and heal 5 each), **Crane Bloom** (both fans fly open: a 60° petal wave 240 px long, 28, shoves 80 px and bats enemy shots back as yours; allies in it heal 18 and get **Tailwind**, +20% speed for 2 s, you too). Five diamonds over your head count the steps (the fifth, red, pulses when the Crane Bloom is next). Hits fill **Breeze** (6 an enemy, 3 a heal; the arc over your head). **Alt, held: Kamikaze Gust.** Holding pulls the fans back (you move 40% slower and can't attack; a dashed cone shows its reach); letting go sweeps them forward in one massive gust: a 120° cone 460 px long, 12 damage, throws enemies back up to 260 px (harder the longer you held, 0.4 to 1.2 s; not bosses or heavies), turns every enemy shot in it round as yours, heals allies in it 20 with 3 s of Tailwind. 9 s cooldown (the ring under your feet). With a full Breeze bar the gust is a gold **Typhoon** (uses the bar): 150° wide, stuns what it hits for 1 s, heals 35 | +15% damage, +2 petals a Petal Spin and a 10% wider gust |
| DPS · Ninja | **Shuriken** | Tight fans of 3 stars (9, 0.08 rad apart: all three hit a boss out to about 650 px) that stick in what they hit. After 5 s each rips out on its own for another hit and flies back to you, cutting what it passes (50%). The weapon-action key switches to the **Great Star** stance (and back), pulling stuck stars out on the way in: hold Fire to draw a route of ink from you through the cursor (up to 900 px), let go and one huge star flies it at 1150 px/s cutting everything on it (60), then flies home cutting again (30); catch it to throw again. A click throws it 600 px straight at the cursor | +1 star in the fan (Great Star: +15% damage) |
| | **Katana** | Two basic-attack combos, one per stance. The weapon-action key (Left Alt) switches stance at once (0.3 s cooldown) and stops the step you're in; the stance you leave **keeps its place** for as long as you're away (the one you're in starts over after 1.5 s without a step), so you can store a combo one step from its finisher and call it later. Pips over your head show where each combo waits. **Hayate** (blade low and trailing, moon steel): Kiri (dash-cut 120 px, 22 to all on the line, untouchable for the dash and 0.15 s after; a dash that runs past a Riot Shield or Shielder cuts it from behind, so the shield doesn't stop it), Kaeshi (the same, angled to the other side), Tsumuji (a 70 px dash into a spin, 85 px, 26), then the **Momentum Burst**: you stab the blade into the ground and a 150 px shockwave shoves enemies 120 px, deals 8, cuts the small bullets in it and marks what it hits for 4 s. A dash that kills brings the next step at once. **Senbon** (blade forward, its edge lit rose): Agari and Kaeshi (130° sweeps, 120 px, 16), Engetsu (a step into a full-moon spin, 105 px, 20), Senbon (six crossing slashes in a cone ahead, 7 each), then **Caltrops**: 16 thrown out to 150 px round you that lie 8 s; one stepped on deals 4, slows to half speed for 1.5 s and breaks. **Together:** a Burst flings every caltrop you have lying at the nearest enemy (8 each); caltrops thrown after a Burst seek the enemies it marked (8 each). Every cut flashes a line across the enemy and a kill splits it in two; the Burst cracks the ground. Each step that lands rings a chime, a note higher the further it is in its combo (the Burst rings the top note, after the blade is drawn back with a click) | +1: each dash strikes once more (60%), two more caltrops and a 25% wider Momentum Burst |
| | **Dragon Fang Kunai** | One huge kunai at a time (60 damage, 950 px/s, 760 px out at the cursor). With no cards it **bites one enemy** and is gone; each **Dragon's Hunger** card (Rare, up to 3) lets it bite **4 more**, and each extra projectile 2 more, **up to 12**. Each enemy it bites sends it on to the nearest one it hasn't bitten within 300 px, **10% harder each bite** (60, 66, 73 … 171; 1,283 for all 12). With bites left and nobody new in reach it flies on and **bounces off the arena's edges, walls and pillars**, then heads for the nearest enemy, which it may bite again, so a lone boss still takes the whole chain (two bounces in a row with nothing bitten and it fades). The **12th bite bursts**: half of it to everything within 120 px. The next throw is ready 0.5 s after it's gone. It leaves the Kunai's mark where it bites last (3 s): press the weapon-action key to blink there, and your next hit lands double. Drawn as the fang in a **fire dragon's jaws**: the dragon's body traces the kunai's real path, so the chain is drawn as the serpent, and it grows with every bite: longer, wider and hotter, legs from the 4th bite, a fire mane and a red-hot blade from the 8th, white-hot at the 12th; each bite flashes and the jaws snap shut, and when it's gone the dragon fades in embers. With Visual effects off: the kunai, a short orange trail and a ring a bite | +2 bites a throw (up to 12) |
| | **Nunchaku** (the 4th weapon key) | Twin Storm: a black chuck with crimson bands in each hand, the swinging stick whipping behind the handle. Fire is a four-step combo (a second without a step and it starts over at the Snap): **Snap** (20, the right chuck whips straight ahead: a 95 px line, 22 px either side), **Backhand** (22, the left sweeps back across: 140°, 90 px), **Double Figure-8** (both cross in front twice: four hits of 12, 110°, 85 px), **Twin Crush** (68, both spin overhead and slam: a 100 px shockwave 40 px ahead that stuns for 0.7 s, not bosses). About 95 damage a second on one target. Raising the parry sends the combo back to the Snap. The weapon-action key raises the **Twin Windmill** guard (again to drop it; the combo starts over either way): both chucks spin a disc at each side in front of you (46 px), you move at 75%, an enemy touching a disc takes 6 and is shoved 36 px out (each enemy once every 0.3 s; bosses and heavies aren't shoved), and every small enemy shot that touches a disc is **swatted** back at the nearest enemy (6) and counted (up to 8, ticks over your head). Fire in the guard is the **Twin Counter**: the discs drop as both chucks lash 175 px straight ahead (22 px either side) for 20 + 6 a swat (up to 68), which uses the swats up (nothing is swatted while it plays); then the chucks go back to the windmill, which spins up again | +1 aftershock ring after the Twin Crush, 40 px farther out (60%) |
| Tank · Hemomancer | **Blood Lash** | Crack, Backhand (110°, 130 px, 24; the tip cracks for double and taunts), Snap (36°, 190 px, 36), Reap: each hit hooks a chain (+1 link, +2 tip or Snap, up to 4); Reap yanks every chained enemy in for 20 + 12 a link and turns links into Clot (4% less damage taken each, up to 5). Alt: Martyr's Leash (hold: chained enemies taunted and held, 40% of hits you take run down the chains, 3x) | +20 px reach, +1 link per enemy and +1 max Clot |
| | **Gore Mortar** | Hold to charge, let go to lob a massive glob of gore over enemies (it can't be blocked by the ones under its arc): 160 px on a tap, out to 520 px at full charge (0.9 s), shown by an arc of drops that grows as you hold. Each shot costs 3% of your max health (never below the blood floor; extra globs come free). It splashes 34 to every enemy within 70 px of where it lands, the same at any range, and leaves a 130 px pool slowing enemies 40% for 4 s (it doesn't heal); after 1 s it congeals (it only slows: it never stops enemies). Blood drips from your hand as you walk (more while charging) and from the glob as it flies | +1 glob, fanned out, landing at the same distance |
| | **Leech Tendrils** | Hold fire: tendrils lash out across a 70° arc (160 px from your edge) and grasp the nearest 3 enemies (Barrage adds a tendril a stack; shields turn them away; a held enemy slips free past 130% of the reach, losing its build-up). Held enemies can't move or shoot (bosses are held but not stunned) and blood builds up in them for 5 s, then they burst for 80 per tendril on them; spare tendrils pile onto the enemies already held (nearest first), so a lone enemy or boss takes all 3 (240). Let go early and each bursts for the share built up so far. Holding costs 2% of your max health a second, nothing heals you, and after any burst or release the tendrils need 1 s before they grasp again | +1 tendril and +20 px reach |
| | **Blood Blast** | 4th key. A six-step chain that never times out: Crimson Slash (22, 120°, 115 px), Backcut (22, 130°, 108 px), Thorn Flurry (3 × 9, 100°, 85 px), Vein Whirl (18, 360°, 95 px), Sanguine Cleave (40, a 70 px circle 105 px ahead, shoves 130 px), Crescent Flight (26, 80°, out to 260 px). Each costs 1.3-3% of max health and adds 1 blood stack to each enemy hit (up to 10); a stacked enemy's death heals you 2 a stack when its blood reaches you. Alt, Crimson Tide (12% health, 8 s): after 0.65 s a 100° crescent rolls out 400 px, 60 to each enemy, a 150 px shove and a stack. Rare card Blood Harvest: +1 health a stack, +5 stacks an enemy | +15% damage and reach (the Tide's too) |
| Healer · Hemomancer | **Transfusion Darts** | Darts (7): one that reaches a hurt ally gives them 20 health for 3% of your max health; each enemy hit steals 0.5 back | +1 dart, fanned out |
| | **Sanguine Orb** | Orbs that home in at the last moment and cover an enemy in blood (16 plus a bleed stack), charging a heal reserve; with 25 reserve, an orb touching a hurt ally coats them in healing blood (30 over 4 s) | +1 orb, fanned out |
| | **Vein Tether** | An instant beam, 350 px: 4 damage every 0.08 s to the first enemy on your aim line (Pierce passes through more, Barrage adds fanned beams; shields stop it). Only what you hold it on gets hurt. 40% of its damage heals you and the nearest ally | +25% beam length and +25% damage |
| | **Heart Flail** | 4th key. A living heart on a chain of veins that beats at 60 a minute; each enemy hit quickens it 10 (5 while spinning) and it slows 15 a second. **Click: Swing**, a whip-crack 150° arc 105 px out, alternating sides: 18 to each enemy the heart passes, a bleed (4 a second, 3 s), a 40 px shove, and 3 blood drops that fly to the most hurt player within 320 px of the heart (you too) and heal 4 each. **Hold: Spin**, the heart whirls round you 115 px out: 9 to each enemy it passes (once every 0.4 s each), and each heartbeat heals the allies within 150 px 5. **Cardiac Burst**: once the beat reaches 180 the next beat heals everyone within 220 px 30 (you too), shoves regular enemies 90 px and the beat starts over at 60. **Alt: Open Heart** (12 s): throw the heart to your cursor (up to 360 px); it lands for 23 within 70 px (bleed and drops as a swing), then roots there for 3 s, running a vein to every ally within 200 px that heals 8 a second, and reels back; no swinging while it's out. At or under 30% health every heal of the flail is 50% stronger. Its Rare card **Pacemaker** (two stacks): the beat quickens 50% faster and its pulses and Cardiac Burst heal 50% more. Look: a dark red heart with aorta stubs and coronary veins, a lub-dub swell and glow on every beat, a vein chain with iron rings that pulses blood out to the heart, a red crescent behind a swing, a beat arc round your feet that fills red to gold, veins beading blood to each ally while it's rooted, a gold shock ring for the Cardiac Burst | +1 blood drop a hit and 10% more reach |
| DPS · Hemomancer | **Needle Swarm** | A thick ring of 160 blood needles (+20 per extra projectile; also +5 wrapped needles per tendril, 10% harder cuts and +4 in the Alt's fan) circles you all the time, swirling and trailing as you move (mockup [`Docs/Mockups/needle-swarm.html`](Docs/Mockups/needle-swarm.html)). It comes out full; a missing needle is siphoned out of you, 32 a second for 0.14% of your max health each, never while you're under the blood floor (20%). An enemy shot coming in through the ring stops on a needle and breaks it (a thin swarm lets some through; red aimed shots always pass). **Hold Fire**: a tendril grows from the ring (140 px/s) to the enemy nearest your cursor within 260 px; let go before it arrives and it pulls back. Once it arrives it wraps the enemy for good in up to 30 needles (at most 45% of the swarm between all tendrils), each cutting for 4 a second, slowing it 50%, and every 0.5 s adding a bleed stack: the stack that brings it to five (from any source) bursts it for 36 within 80 px. It lets go when the enemy dies or gets 390 px away; holding on, the next enemy nearest the cursor gets the next tendril, three at once. **Alt**: 24 needles (+4 per extra projectile) leave the ring as a fan at your aim, 14 each (2.5 s cooldown); the swarm siphons them back. Going down or putting the weapon away loses the swarm. |
| | **Implosion** | No projectile: click an enemy (up to 480 px away) and its blood collapses inward (34), hurting enemies within 55 px of it (15) and pulling them 18 px in; ignores shields. Holding fire with nothing under the cursor waits | +1 implosion: another enemy near your target collapses too (60% damage; the target again if no one is near) |
| | **Blood Brawler** | Click an enemy (up to 520 px away) to mark it and land the next step of a string from range: Pulse (18, shoves 24 px), Reel (15, pulls 110 px in), Heart Lift (27, airborne and stunned 0.7 s; hits on it airborne +25%), Marrow Burst (36 + 12 per earlier hit, 8 bone shards for 24). A press within 0.9 s on the same enemy moves the string on. 1% of your max health a move, 2.5% for the Burst. Alt, Sympathy (8 s): up to 4 enemies within 170 px of the mark take every move on it at 50% for 4 s | +1 echo: every move also lands on the next enemy nearest the mark (50% damage) |
| | **Embolus Spears** (the 4th weapon key) | Arms out, a hardened blood spear in each hand. Fire throws the **left** spear, then the **right**, at your cursor, each for **12% of your max health** (never below the blood floor): they fly at 1100 px/s through every enemy (30 each) and **stick in the first wall, pillar or arena edge** they reach. A **third Fire recalls both**: they tear back to your hands, hitting everything again on the way (30), and each one caught **gives back what it cost**. Holding Fire throws both, waits for them to stick, then recalls. **Alt bursts** every spear out where it is: 90 at the middle down to 45 at the edge of 120 px, giving back **only half** the health; new spears harden in your hands in 0.45 s. Going down or switching weapon with spears out loses them (and their health). Rare card **Burst Vessels**: the burst 30% wider and 30% harder (2 stacks) | +20% spear and burst damage |

With your own Revolver, the hammer shows as a pip under your character (or the Fan stance's two diamonds, see above). The
held gun shows its hammer pulled back, gold-tipped when it's cocked. With **Visual effects** on, the gun kicks back and its
cylinder clicks round on every shot, a flash and drifting smoke leave the muzzle and a brass casing flips out (the
Gunslinger's shot is a bigger, orange flash). The Mender's
Bow is drawn 30% bigger (a dark-outlined limb with gold tips and a green grip wrap) with a quiver on your back: its limbs
bend as you draw, its arrowhead glints near full draw, a gold ring pops at full draw and the bow glows gold and trembles
while the Focus meter is up; on release the string snaps and a ring of light flies off (gold for a Focus shot), and your
hand pulls a fresh arrow from the quiver and swings it over your shoulder onto the string. A Focus shot flashes a gold
ring and three wind crescents at the bow; its arrow is wrapped in two twisting wind ribbons, sheds streaks of wind,
bursts gold on every enemy it goes through and ends in a gust. Scatter snaps a fan of light, and a Scatter arrow that
hits nothing sticks in the floor a moment. Drawing only.

Want a **wall of bullets** instead? The **Barrage** card (Rare, stacks to 3) makes every shot also fire a copy fanned
out to the side (8° steps, alternating sides), at 60% damage. It stacks with the weapon upgrades above, so a Twin
Rifle with Multishot and Barrage fires several side-by-side barrels in a fan. One Revolver click fires the round and
its copies.

### Graze

An enemy shot that **skims past you** without hitting (within 16 px of your hit circle) charges your **ultimate**: 0.3 s off
its recharge per graze. Each shot grazes once, and at most one graze counts every 0.2 s, so weaving through a dense
pattern tops out around 1.5 s of ultimate a second. It doesn't count while you're invulnerable (a dodge, the moment after
a hit), so dodging through a shot isn't a graze: brushing past it is. A pale crescent flares round you where the shot went
by, with a soft "tsip" on your screen, and your ultimate's key flashes pale blue.

### Parry

Hold **right click** (or your Parry key) to raise a small shield on the side facing your cursor (a Knight's is 30% wider and reaches further out). It blocks enemy bullets from that side
(including the red aimed shots that barriers can't stop) and melee enemies that run into it, sliding them back 45 px.
A weapon strike (an Acolyte's sickle, a Scythe Wraith's reap) is blocked whenever the shield faces the
striker, however far its blade reaches, and throws the striker back 120 px (armoured enemies and bosses stand firm). While
it's up you **can't shoot** and you move at 70% speed. It soaks **150 damage**, then breaks and can't be raised for 3
s; it refills when you lower it. Raise it just as a shot arrives for a **perfect parry**: for the first 0.30 s after
raising it, blocked bullets go straight back as fast white needles (900 px/s) and cost nothing (it has to have been down
for half a second, so you can't spam it). Every character gets the same flash in its own colour: the shield's whole arc
flashes white, a ring and sparks burst where the shot met it, your own picture holds for 50 ms, and the world (enemies and
every shot, not the players) slows to 30% for 0.3 s on every screen, at most once every 0.6 s so a burst slows it once. A thin blue bar curving around the shield shows how much it has left, shrinking toward your aim as
it wears and blinking red when it's nearly gone; when it breaks it cracks and shatters, and while it's lowered a faint
bar shows it refilling. Against the Warden up close it holds for about 3 seconds of fire, enough to close the gap.

**Animation cancel:** the Fuma Shuriken has its own parry moves (Reversal, stealing shots, catching a thrown star: see its row). Raising the parry sends an attack chain back to its first step, clears the wait for the next step and cancels an attack that hasn't landed yet. Today that is the Katana (the wait for its next step is cleared; both combos' buildups reset) and the Bo Staff's slash, jab, spin (a shove still drawing back never lands and costs no cooldown). Streaks you have built (the Rapier's) are kept. The Twin Blades' chain goes back to the Rising Cut too, and a step still winding up never lands. So do the chains of the Lance, Lightbringer, Censer, Greatsword, Arc Knuckles, Nunchaku, Bulwark Barrier and Fuma Shuriken (Lash and Cyclone).

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

**Q** fires your **ultimate**. Each role and class combination has its own default one, and some have others to
pick instead on the lobby's **Ultimate** row (so far the Tank Soldier: **Juggernaut Suit**, **A Literal Tank** and **Armor Drop** and the Tank Elementalist: **Lightning Rod**, **Stone Colossus** and **Vortex**, below the table; and the DPS Elementalist: **Living Bolt**, **Flash Fire** and **Rock Gloves**, below; and the
Tank Hemomancer: **Bloodbath** and, on key 2, **Blood Golem** and, on key 3, **Sacrificial Ritual**, in the Hemomancer table; and the Tank Summoner: **Bone Phalanx** and, on key 2, **Iron Armadillo** and, on key 3, **Tidal Tusker**, below the table; and the
Tank Ninja: **Dragon's Dogma** and, on key 2, **Spirit Avatar**, and on key 3 **Five Shadow Gong**, below; and the DPS Soldier: **Attack Helicopter**, **Orbital Laser** and **Rocket Drop**, below; and the Healer Soldier: **Field Hospital** and on key 2 **Supply Drop** and on key 3 **Hazard Foam**, below; and the DPS Knight: **Ballista** and, on key 2, **War Standard**, and on key 3 **Excalibur**, below; and the Tank Knight: **Trojan Horse**, on key 2 **Drawbridge** and on key 3 **Aegis Standard**, below; and the Healer Knight: **Pilgrim's Standard** and, on key 2, **Holy Lance Rain**, and on key 3 **Chalice of Life**, below; and the DPS Hemomancer: **Blood Ravager** and, on key 2, **Blood Boil**, and on key 3 **Blood Ritual**, below; and the DPS Summoner: **Rodent Minigun** and, on key 2, **Mammoth Fall**, and on key 3 **Brood Queen**, below; and the DPS Ninja: **Forbidden Path** and, on key 2, **Hundred Paper Cranes**, and on key 3 **Oni Mask**, below; and the Healer Ninja: **Astral Projection** and, on key 2, **Pressure Point Press**, and on key 3 **Shadow Clone Medics**, below; and the Healer Elementalist: **Static Bubble** and, on key 2, **Eye of Calm**, and on key 3 **Conduit Web**, below; and the Healer Summoner: **Honey Raid**, **Tortoise Oracle** and, on key 3, **Sleepy Cat**, below; and the Healer Hemomancer: **Heartbeat** and, on key 2, **Heart of the Swarm**, and on key 3 **Blood Garden**, below; a Wildcard picks any class's DPS ultimate). Ultimates have long cooldowns
that upgrades don't shorten, and start each run half charged. The HUD shows the charge in the gold slot.

**Juggernaut Suit** (Tank Soldier, picked instead of Fortress, 75 s): six steel plates clamp onto you over 0.6 s and
for 10 s you carry **300 armour**, which takes damage before your shields and health; a segmented steel bar over your
health bar shows it draining, and a plate comes off for every sixth of it gone. You move 25% slower and every enemy,
bosses too, goes for you (the taunt, without Roar's damage cut). Each enemy bullet the armour takes throws a **flak
shard** straight back at whoever fired it: 12 damage times your damage multiplier, dealt at once, with a short amber
tracer. A shot the plates take whole gives no hit grace, so a stream of fire keeps feeding the flak. It ends when the
10 s run out, the armour is gone (the rest of that hit reaches your health), you go down, or the wave is cleared (as
Last Stand), and the plates fall off. Bots that have it put it on with four or more enemies within 260 px. Mastered:
450 armour and 18 a shard. Design: [`Docs/Mockups/juggernaut-suit.html`](Docs/Mockups/juggernaut-suit.html).

**A Literal Tank** (Tank Soldier, key 2 of the Ultimate row, picked instead of Fortress, 80 s): an olive tank drops on
you out of the sky (its shadow grows under you for 0.6 s, enemies within 120 px are shoved off as it lands) and you
drive it for **15 s**. Its **400 armour** takes damage before your shields and health (a steel bar over your health bar,
with the time left as a hairline under it). You drive at 75% speed: the hull turns toward where you drive (it backs up
rather than spinning round) and leaves tread marks, and the turret follows your cursor. **Fire** shoots a big explosive
**shell** every 0.9 s (900 px/s): 75 to what it strikes and 45 to everything else within 95 px; a shell that hits
nobody bursts at your cursor (120 to 620 px out) for 45. Small enemies you drive into are **run over** (killed outright);
heavies and bosses take 25 and heavies are shoved 90 px, at most every 0.5 s each. Your weapon, its Alt, your abilities
and the Dodge are locked while you're inside. The last 3 s the hull blinks red. It ends when the time runs out, the
armour is gone, you go down or the wave is over: you hop out and the empty tank **blows up** (90 within 150 px), leaving
a charred wreck for a moment. Bots drive it out with an enemy within 480 px. Mastered: 550 armour, and the shells blast
30% wider. Design: [`Docs/Mockups/a-literal-tank.html`](Docs/Mockups/a-literal-tank.html). Sounds (LitTankSoundBank.cs): a falling whistle as it drops, a crunching
thud and a clank of steel as it lands, a diesel engine with clattering treads that idles low and revs up as you drive,
a deep rolling boom for each shell, a double beep every half second in your own tank's last 3 s, and a hatch clunk as
you hop out before the wreck blows.

**Armor Drop** (Tank Soldier, key 3 of the Ultimate row, picked instead of Fortress, 80 s): throw a red smoke flare at
your cursor (up to 520 px away). For 1.6 s a pulsing red ring marks where a gunmetal crate lands as it falls under its
chute; then it slams down for **60** (scaled by your damage) to every enemy within 110 px, staggers them 0.4 s and shoves
them 40 px (not bosses). Its riveted sides fall open and it kicks out **3 ballistic vests** 70-110 px out. Walk over a
vest to wear it: **50 ballistic armor**. Armor takes damage before your shields and health (after the Juggernaut Suit's),
**never regenerates** and stays between waves until it's shot off; another vest refills it to 50 (it never stacks past
50, so at full armor you leave a vest for someone else). Wearing it shows a steel half-shell round you, five steel
segments over your health bar, and ARMOR with steel cells on your own health panel. Unclaimed vests blink their last 3 s
and are gone after 20 s; the crate folds away once every vest is taken or gone. Bots throw it with an enemy within 520 px
and walk to a vest when they're short of armor. Mastered: the crate holds a 4th vest. Design:
[mockup](https://claude.ai/artifact/4CUNVxkwG8EgAe3tNViHpC). Preview: `ARMORDROP=<s since the cast> [ARMORCAP=1]
[ARMORHIT=<armor>] ZOOM=2 CAMAT=860,450 Tools/Preview/shot.sh x hud 6 1 3`; test suite `armordrop`.

**Trojan Horse** (Tank Knight, key 1 of the Ultimate row, picked instead of Last Stand, 75 s): a wooden horse on wheels
rolls out from you along your aim at **90 px/s**, shoving regular enemies out of its path, and parks where the arena's
edge, a wall or a prop stops it (or after 1,400 px). It has its own health (**1,200** with one player, +200 for each more:
1,400 for two, 1,600 for three), shown as a bar over it with one pip per seat lit in each rider's colour and its time arc.
Any player within **80 px** presses **Interact** (F) to **climb in**, and again to drop out beside it. Inside you go where
it goes, nothing can hurt you, enemies don't pick you, and your weapon, its Alt, abilities and the Dodge are put away:
**Fire** shoots **arrows** out of the side slit nearer your aim (**16** each, every 0.35 s, each piercing one enemy; the
slit glows gold). Its body stops **every enemy shot**, red aimed ones too, and enemies can't walk through it; each shot
and bite wears it. While **everyone still up is inside**, enemies have nobody to pick and go for the horse (it pulses
red). At 0 health it **breaks**: planks fly and the riders are thrown out, untouchable for 1 s. After **20 s** it springs
open, **Sally Forth**: its sides swing down, regular enemies within 120 px are shoved 110 px, and everyone who was inside
deals **25% more damage for 4 s** (a gold ring round them). It also folds away between waves. The cooldown starts when it
comes down. Bots walk to a horse within 420 px and climb in. Mastered: 40% more health, and the arrows pierce one more
enemy.

**Attack Helicopter** (DPS Soldier, picked instead of the Deployable Turret, 70 s): a gold radio ring goes out from you and
an olive gunship drops in from off-screen over 0.9 s (high and big, its shadow tightening), then for **10 s** it circles
you 230 px out and its nose chaingun works through the enemies one at a time: it locks the enemy **nearest you** within
**560 px** (amber brackets close on it over 0.25 s and turn red), the barrels spin up, and it fires **20 rounds a second
of 8 damage** (160 a second, times your damage multiplier) until that enemy dies or leaves the 560 px, then turns on the
next. The rounds hit at once (it flies over walls and props and can't be hit); the tracers, muzzle flash, brass falling
to the floor and sparks on the target are only the look. It leaves (climbing away) when the time runs out, you go down,
or the wave is cleared. You hear its rotor (a deep blade whop over a turbine whine) swell in as it arrives and fade as it
climbs away, the barrels whine up on a fresh target, and every round cracks (all on the Weapons volume). Mastered: a
second gun on the other wing, so two targets at once. Design:
[`Docs/Mockups/attack-helicopter.html`](Docs/Mockups/attack-helicopter.html) (the Orbit pattern).

**Orbital Laser** (DPS Soldier, key 2 of the Ultimate row, picked instead of the Deployable Turret, 90 s): calls a laser
down from orbit on your **cursor**, anywhere in the arena. A red spotting line drops out of the sky onto the point, then
for **1.9 s** a red **260 px** circle locks on: gold brackets spin round its rim, a pale timer ring closes in on the
point, three gold arcs count down outside it, and every enemy inside is marked with gold corners. From 1.5 s the screen
dims and a thin white pilot beam comes down. Then a white **flash**, a screen shake and a column of blue-white light
for **1.1 s**: every regular and heavy enemy it touches **dies**, however tough; a **boss** loses **20%** of its health,
once; enemy shots inside are erased; allies are never hurt. A shockwave rolls out and sparks fly up. It leaves a crater
of molten glass for **4 s** that burns enemies in it for **25 a second** (times your damage multiplier) and slows them
30%, glowing cracks and embers cooling as it goes. Fast enemies can walk out during the lock-on. Bots call it with four
or more enemies near them. Sound (OrbitalSoundBank.cs, Effects volume): an uplink chirp, three rising lock-on beeps, a
charging whine, a deep boom with a crack, the beam's roar and the glass hissing as it cools. Mastered: a **340 px**
circle, and the beam ploughs **200 px** on, away from you, while it fires. Design:
[`Docs/Mockups/orbital-laser.html`](Docs/Mockups/orbital-laser.html).

**Rocket Drop** (DPS Soldier, key 3 of the Ultimate row, picked instead of the Deployable Turret, 80 s): calls a supply pod
onto your **cursor** (up to **220 px** away). A red circle locks on with gold brackets turning round it while a steel
pod streaks in on a trail of fire; after **1.1 s** it slams down for **120** to every enemy within **90 px** (small ones
are shoved off). Its four petals open and a heavy olive **rocket launcher** rises on a rack inside, ringed in gold (with
a gold arrow over it on your screen). Only you can take it: step on the pod. Left there 15 s, it folds away with it.
Taken, the launcher replaces your weapon for **10 s** (a gold ring at your feet counts down, a gold **∞** shows over
your head): **infinite ammo**, one rocket every **0.6 s**, no reload; you move at 85% speed, your weapon's Alt is
locked, and your abilities and Dodge still work. Each rocket flies at 820 px/s and bursts on the **first enemy** it
touches, a wall, the arena's edge, or at your **cursor**: a **thermobaric** blast **190 px** wide that does **150** at
the middle falling to **60** at the rim (times your damage), shoves small enemies out, and leaves the ground **burning**
for **2.6 s** (118 px, **35 a second**). Allies and you are never hurt. On your screen the blast's reach is dashed round
the cursor. The look: the launcher on your right shoulder kicks back with each shot and shows a fresh warhead once
reloaded; rockets trail flame and smoke; each blast is a white flash, a fireball, a shockwave ring, debris and smoke,
and shakes the screen. It ends when the time runs out, you go down or the wave is over. Bots call it with an enemy
within 520 px and walk to the pod. Mastered: the launcher lasts **13 s** and each blast throws **3 bomblets** (60 px,
40 each). Design: [`Docs/Mockups/rocket-drop.html`](Docs/Mockups/rocket-drop.html) (look B, the drop pod, with the
Thermobaric warhead).

**Field Hospital** (Healer Soldier, picked instead of Lifeline, 150 s): in a Grid run, pressing it starts a pick. Every
free door spot within 900 px of you lights up as a green hologram door with a white **+** (the mystery door's look in
medic green) and a dashed outline of the cell behind it; a spot whose cell is taken (a built room, a mystery door on it,
a sealed mini-boss room beside it, or a shrine, chest or relic waiting in it) shows a small red cross. Click a lit door
(or anywhere in its cell) and that one cell opens behind it as a real room; the key again cancels, and nothing is spent
while no door is lit. Inside: a **health shrine** in the middle (each player once: clears wounds, fills your health and
adds 25% of your max past it), **6 healing orbs** of 20 each that come back one every 4 s (they only drift to players
inside, never out through the walls; one also picks a downed ally up), and a **green laser gate** across its door:
allies walk through, enemies are put back out and their shots burn away inside. It has no door spots of its own
(nothing is built off it) and isn't counted as a built room. It lasts **1 minute** from when it opens, through the break between waves too (a map reset takes it with the
rest of the map); then a bar
over the door runs out and **EJECT 3, 2, 1** counts down over it: everyone still inside is put out at its door, the
orbs go, and the cell comes off the map. The **cooldown starts when it comes down**, not when you cast it. Outside a Grid run it is a **tent** at your cursor (up to 360 px away): the
same shrine and orbs inside a 150 px ring of green beams that keeps enemies and their shots out. Mastered: 8 orbs, one
back every 3 s. Bots use it when in danger or hurt beside a hurt ally, on the free door nearest them. Design:
[`Docs/Mockups/field-hospital.html`](Docs/Mockups/field-hospital.html).

**Supply Drop** (Healer Soldier, key 2 of the Ultimate row, 110 s): throw a red smoke flare at your cursor (up to 520 px
away). For 1.5 s a pulsing red ring marks where the crate lands as it falls under its chute; then it slams down for
**60** (scaled by your damage) to every enemy within 90 px and staggers them 0.4 s (not bosses). It opens into a **170 px
green zone for 12 s**: everyone inside moves **20% faster**, fires **30% faster** and has their weapon and ability
cooldowns recharge **40% faster** (not the ultimate); a green ring under each player shows it, and it ends a quarter
second after you step out. Every 2 s the crate throws out a **health pack** (heals 25, lands 60-150 px out, lasts 8 s,
4 at most on the floor). Mastered: the zone lasts 18 s and packs heal 40. Bots use it when an ally is hurt or an enemy
is within 460 px. Preview: `SUPPLY=<s since the cast> [SUPPLYCAP=1] Tools/Preview/shot.sh x hud 3 1 3`; test suite `supplydrop`.

**Hazard Foam** (Healer Soldier, key 3 of the Ultimate row, 100 s): strap on a foam sprayer for **12 s**. It replaces your
weapon (a striped hazard canister on your back, a yellow wand in your hands); you move at 85% speed, your weapon's Alt is
locked, abilities and the Dodge still work. **Hold Fire** to spray: the **100 tank** drains 14 a second (about 7 s of
spraying, shown as a striped bar under you) and lobs 16 globs a second at your cursor, 50 to 300 px out (a dashed yellow
ring shows where they land). Globs set into **lumps of foam** (up to 24 px across each, growing as foam piles on; topping
one up starts its life again). Foam is **solid**: nobody walks through it, allies and you included, and it **stops every
shot**, yours too. Enemy shots wear it (10 each, 30 from a boss) and enemies pressed against it chew it (8 a second) and
slow down; a lump worn through breaks apart. Bosses aren't held: they plough through, eating 80 a second (a boss's
unblockable attacks go through too). Each lump lasts **60 s**, shrinking and fizzing over its last 6 s, and all foam goes
when the wave ends; 80 lumps at most in the arena. The sprayer comes off when the time or tank runs out, you go down or
the wave ends. Mastered: a 130 tank and the foam lasts 90 s. Bots use it with an enemy within 380 px. Preview:
`FOAM=<s since the cast> [FOAMCAP=1] [FOAMFIZZ=1] ZOOM=1.3 CAMAT=700,520 Tools/Preview/shot.sh x hud 1 6 3`; test suite
`hazardfoam`. Mockup: [`Docs/Mockups/hazard-foam.html`](Docs/Mockups/hazard-foam.html).

**Dragon's Dogma** (Tank Ninja, picked instead of Thousand Clones, 70 s): a gold sigil opens under you, the arena dims
and a great azure spirit dragon (680 px long, gold spine and antlers, a shadow on the floor) pours out of you head first.
For **5 s** it hunts across the whole arena: its head flies at 760 px/s and swoops after the enemy **nearest it** (it
turns harder as it closes in, so it never circles its prey; with nobody about it circles you). Every regular enemy its
jaws reach (30 px past its edge) is **devoured**: killed outright, whatever its health; the jaws spread as it closes in,
a ring bursts where it bit, the enemy's soul spirals into the maw and a glowing lump runs down the body. **Heavy
enemies and bosses** are only bitten, for **150** (times your damage), at most once every 1.2 s each, and it hunts
someone else meanwhile. Its head also **swallows enemy bullets** within 40 px. In its last 0.7 s it turns for home and
coils back into you (the body drains in after its head). It ends early if you go down or the wave is cleared. Bots that
have it cast it with four or more enemies near. Mastered: hunts for 7 s and every enemy it devours heals you 3%.
You hear a rush of wind swell into a deep, fluttering dragon roar with spirit bells as it comes out; each enemy it
devours gives a snap of the jaws, a deep gulp and a thin shimmer of its soul; a heavy or boss bitten gives a snap, a crunch
and a thud; and it coiling back in gives a rush of wind pulled into you, settling into a low hum and a bell.

**Warhorn Volley** (DPS Knight, the default ultimate, 70 s): you blow a black war horn (you keep moving and
fighting) and 0.6 s later burning arrows start landing 150 px behind you; their front sweeps along your aim at 700 px/s,
**1,400 px** ahead and **700 px** wide. Every enemy it reaches takes **120** (times your damage), **burns** for 20 a second
for 5 s and is **pinned**, 40% slower for 3 s (bosses burn but aren't pinned). Ordinary enemy shots in its path are
knocked out of the air; red aimed shots fly on. Mastered: the volley is 40% wider and the fire burns for 8 s. The rare card **Bodkin Points** (2 stacks) makes the arrows hit 25% harder and the volley 20% wider. Look: Fire
Arrows, the volley's shadow riding ahead of the front, arrows arcing in from behind you with ember trails, shafts left
quivering in the floor with small fires at their feet. One sound: the horn's long low blast, then the hiss, whistles,
thuds and crackle of the volley. Design: [`Docs/Mockups/warhorn-volley.html`](Docs/Mockups/warhorn-volley.html).

**Ballista** (DPS Knight, key 1 of the Ultimate row, 75 s): a giant ballista drops **70 px** in front of you along your
aim. Its shadow grows for 0.6 s, then it lands: **80** to every enemy within 90 px (a red ring marks the reach). It stands
for **25 s**. **Anyone** (you or a teammate) standing by it presses **Interact (F)** to climb on, and Interact again to get
off; one rider at a time. The rider sits in the seat behind the bow, rooted (no walking, dodging or moving abilities, the
weapon put away), and takes **30% less damage**: that share wears the ballista's **600** health instead. The bow turns onto
the rider's aim (220° a second); holding Fire looses a giant bolt every **0.9 s**: **260** times the rider's damage, through
**every** enemy in a **1,600 px** line, knocking each one 90 px along it (heavies half, bosses not at all) and knocking
ordinary enemy shots in its path out of the air. It holds **8 bolts**; it falls apart 0.6 s after the last, or when its
time or health runs out, and folds away when the wave ends. Mastered: **10 bolts**, and each bolt pins what it hits (40%
slower for 3 s; not bosses). The rare card **Windlass** (2 stacks) adds 2 bolts and makes it reload 25% faster. Look: an
oak platform with iron corners, a long stock, two steel-shod arms and the string drawn back by the windlass with a giant
bolt laid on it while loaded; a gold pip per bolt left, its time (steel) and health (gold to red) under it, a gold reload
arc; each bolt kicks the bow back with a flash; the planks fly apart when it breaks. A "F MOUNT BALLISTA" tag shows by a
free one ("F LEAVE · n BOLTS" while you ride it). Design: the "Knight Ballista Ultimate" mockup.

**Excalibur** (DPS Knight, key 3 of the Ultimate row, 70 s): a shaft of light comes down on you and a giant glowing sword
grows in your hands over **0.5 s** (you walk at half speed and can't swing). Then for **10 s** your weapon is put away and
Fire swings the sword, left and right by turns, once every **0.38 s** while held (you move at 90%). Each swing cuts
**60** into everything in a **150°** arc out to **230 px**, then throws a **shockwave**: a crescent of light that flies
out at 900 px/s to **700 px**, hitting every enemy it reaches once for **90** and cutting the small enemy shots it
passes. Every **4th** swing is a **Judgement**: an overhead chop for **140**, then a taller, wider wave that flies
**1,000 px** for **180**, shoves enemies 120 px back and cuts every enemy shot it passes, red aimed shots too (the next
swing waits 0.5 s). When the time runs out the sword bursts into light: **120** to everything within **200 px**; going
down or the wave ending puts it away with no burst. Shoves skip heavies and bosses; all damage scales with your damage.
Mastered: **13 s** and the waves fly **30%** farther. Look: a blade of white light with gold edges, guard and pommel and
a blue gem, glowing and shedding motes; a gold fan behind each swing; a Judgement lifts it overhead and drives it down
your aim with a flash; under you a pool of gold light, a gold ring draining with the time left and four diamonds counting
to the next Judgement. Design: the "Excalibur Ability Mockup" artifact.

**Knight Banners** (one per role: **Aegis Standard**, Tank Knight key 3, 75 s; **Pilgrim's Standard**, Healer Knight key 1, 80 s; **War Standard**, DPS Knight key 2, 70 s): you throw a standard up to **160 px** toward your cursor; it drops in and slams down 0.35 s later and its ring grows out. It stands for **60 s**, or until you press the ultimate again; it also comes down if you go down or the wave ends. The cooldown starts when it comes down. Allies standing in its ring (you too) get its effect, and lose it when they step out:
- **Aegis Standard** (220 px ring): an **80**-point shield that fills as you step in and takes every hit before your health and any other shield; 1.5 s after the last hit it regenerates **25 a second**. Out of the ring it fades over 1.5 s. Mastered: 120 and it regenerates after 0.75 s.
- **Pilgrim's Standard** (230 px ring): heals **4%** of your max health a second, and downed allies inside are stood back up 50% faster. Mastered: 5% a second.
- **War Standard** (210 px ring): **40%** faster attacks and **25%** more damage. Mastered: +55% and +35%.
Look: a dark oak pole with a gold finial and a swallowtail cloth swaying in the wind (Aegis: a silver kite shield on steel blue; Pilgrim's: a gold sun on ivory; War: crossed gold swords on crimson), a time arc draining round the ring, hex plates turning round the Aegis ring and a hex bubble on each shielded ally, soft gold light, rising motes and a light thread to each ally in the Pilgrim's, crimson chevrons, embers and a red ring under each ally in the War Standard. Design: [`Docs/Mockups/knight-banners.html`](Docs/Mockups/knight-banners.html).

**Holy Lance Rain** (Healer Knight, key 2 of the Ultimate row, picked instead of Hallowed Blade, 80 s): spears of light rain down around you for **3 s**, **24** of them, each at a random spot within **340 px** of you (the circle follows you as you walk; spots are spread out so the ground fills evenly). Each spot shows a gold ring for 0.35 s, then the spear lands: enemies within **55 px** take **30** (times your damage), are stunned 0.3 s (not bosses) and shoved 30 px (not Heavy enemies or bosses). The spear leaves a small mint healing glow (40 px) for **4 s**: anyone standing in a glow heals **5 HP/s** (glows don't stack). Mastered: 32 spears, and the glows last 6 s. Look: a dashed gold circle round you, spears of light dropping out of the sky with a gold trail, a white flash, shock ring and gold cracks, then the glow with rising motes and the spear left standing in it.

**Chalice of Life** (Healer Knight, key 3 of the Ultimate row, picked instead of Hallowed Blade, 90 s): a big gold chalice drops in up to **140 px** toward your cursor and stands for **12 s**. Every player can drink from it **once** by walking within **50 px** of it: it clears their wounds, fills their health, adds **20** health past max (like a health shrine's: damage takes it after shields, it doesn't decay, going down loses it) and puts up a **40** shield for **4 s**. Downed players can't drink until they're back up. Once everyone has drunk, the chalice fades. Mastered: the shield is 60 and lasts 6 s. Look: the cup drops out of the sky with a shock ring, then stands in a dashed gold circle with its time left as a gold arc; its mint liquid sinks with each sip, a mint stream arcs to the drinker and a ring and motes rise off them.

| | Soldier | Elementalist | Summoner | Knight |
|---|---|---|---|---|
| **Tank** | **Fortress** (70 s): raises a fixed, impenetrable dome (240 px radius) where you stand for 10 s; only players and player bullets cross it, enemies (bosses too) and their shots can't get in or shoot through, and anything inside is pushed out. Or pick **Juggernaut Suit** (75 s) on the lobby's Ultimate row: 10 s of 300 armour, 25% slower, a taunt, and a flak shard back at the shooter for each bullet the armour takes (above) | **Glacial Tomb** (75 s): erases every enemy bullet and freezes every enemy solid for 4 s; bosses take 250 damage instead. With Visual effects on a frost lattice spreads out from you, spires cage each frozen enemy and break when the freeze ends, and a ring of spears jabs at bosses | **Titan** (80 s): a huge stone titan (280 px across) rises from rubble over 1.4 s, then stomps after enemies for 17 s, raising its fists for 0.4 s before each slam (90 damage, 190 px, every 1.4 s, shoves enemies back; bosses and Heavy enemies aren't shoved). Its whole body blocks bullets (soaks 1000 boss damage). It crumbles when time is up. Or pick **Bone Phalanx** (75 s) on the lobby's Ultimate row: a line of shield skeletons that marches 260 px, blocking shots from the front (below) | **Last Stand** (70 s): plant yourself behind a crescent of plates that stops every shot from the front while you take 10% of it (no timer; press again to cancel). Or pick **Drawbridge** (75 s, key 2): a drawbridge falls along your aim, crushing the 500 x 100 px lane (260) and throwing the edges aside (45); then for 5 s it's a walkway enemies can't step onto (the rails grind any pressed against them for 80 a second), and their shots stop at its rails. Or **Aegis Standard** (75 s, key 3): a standard planted for 60 s whose ring gives allies an 80-point regenerating shield |
| **Healer** | **Lifeline** (90 s): instantly revive and fully heal the whole party, plus 3 s of invulnerability. A heartbeat pulse rolls out from you and a halo and column of light drop over each player as it reaches them | **Sanctum** (80 s): a 7 s dome of light; bullets can't get in, allies inside can't go down, and a healing pulse rolls out from you every 0.9 s (7 pulses: 20 each, the last 30; each ally heals as the ring reaches them) | **World Tree** (80 s): a 100 px tree for 15 s that heals everyone under it (18 HP/s), slows and burns enemies, and sends a wave of healing butterflies, one to each ally (the most hurt first), every 1.9 s; each butterfly flutters to its ally and heals 14 on arrival. Or pick **Honey Raid** (80 s, key 1), **Tortoise Oracle** (90 s, key 2) or **Sleepy Cat** (85 s, key 3) on the lobby's Ultimate row (see [Ultimates](#ultimates)) | **Hallowed Blade** (80 s): a giant sword of light falls at your cursor (200 damage) and stays planted for 8 s; allies in its ring heal 15 HP/s and enemies burn 20 a second, and every 2 s a pulse crosses the whole arena (4 pulses, the last bigger) healing every ally 10 and hurting every enemy 13 as the ring reaches them. Or pick **Pilgrim's Standard** (80 s, key 1): a standard planted for 60 s (press again to lift it) whose ring heals allies 4% of their max health a second. Or **Holy Lance Rain** (80 s, key 2): 24 spears of light fall around you over 3 s; each hits enemies near where it lands for 30 and leaves a healing glow (5 HP/s for 4 s). Or **Chalice of Life** (90 s, key 3): a chalice stands 12 s; each player drinks once for a full heal that clears wounds, 20 health past max and a 40 shield for 4 s |
| **DPS** | **Deployable Turret** (60 s; took Artillery Assault's place): a crate drops on your spot and unfolds, and you climb in for 20 s: you can't move or dodge, and the turret fires twin barrels instead of your weapon. Hold Fire and it turns onto your cursor, spooling up from 7 to 30 rounds a second over 1.2 s; each round is 22 damage straight down the lane through the first 2 enemies, with a muzzle flash, gold tracers, casings and shakes that grow with the spool, and the barrels glow from steel to white-hot. The turret takes the damage meant for you (350 health, a bar under it that reddens as it wears); if it is wrecked you are thrown clear and it blows early. For the last 0.8 s it overheats and can't fire, then blows for 80 within 160 px. Press the key again to step out early: you are free at once and it overheats and blows after 0.8 s, so run. Heavy gunfire, a spin-up whine, an overheat alarm and a blast boom play with it. It folds away with no blast if you go down or the wave ends) | **Bullet Rain** (70 s): a rain barrage at your cursor (a 520 px square) for 6 s: a blue square target zone locks on, you launch rounds skyward and they rain down into it, each with a growing dot where it will land and a ring and splash when it does; it does 45 damage a second to every enemy in it, bosses too, and leaves them **wet** (as Tsunami does: slowed, and any lightning anywhere arcs to them) for 3 s after they leave | **Legion** (70 s): a blood moon rises and tints the arena for 10 s; 4 wolves and 4 bats circle you for a second, then run 40% faster, and your other minions frenzy | **Warhorn Volley** (70 s): a front of fire arrows crosses the field along your aim, burning and pinning every enemy it reaches. Or pick **Ballista** (75 s, key 1) or **War Standard** (70 s, key 2): a standard planted for 60 s whose ring gives allies 40% faster attacks and 25% more damage |

**Lightning Rod** (Tank Elementalist, picked instead of Glacial Tomb on key 1 of the lobby's Ultimate row):

| | Ultimate |
|---|---|
| **Tank Elementalist** | **Lightning Rod** (70 s): for 6 s you crackle with charge (a blue glow and small arcs on your body): you take 80% less damage and every enemy, bosses too, goes for you. Each enemy bullet that hits you calls a bolt from the sky down on the enemy that fired it (30, by your damage), and the bolt arcs on to the 2 enemies nearest it within 320 px (15 each), drawn as jagged lines; if the shooter has died the bolt still lands where it stood and arcs from there. Melee and contact hits on you call a bolt on whoever hit you. It counts as lightning: wet enemies (Tsunami, Bullet Rain) take the usual extra arcs. Each enemy can call at most one bolt every 0.25 s (a shotgun volley calls one, not five; the other pellets are still cut by 80%). A shot that touches you during your hit grace is grounded too: used up, no damage, and its bolt still falls. A small grey number shows what each grounded hit took. It ends after 6 s, or at once if you go down |

**Rodent Minigun** (DPS Summoner, key 1 of the lobby's Ultimate row, picked instead of Legion, 70 s): a small violet portal
tears open 46 px in front of you (0.4 s) and spins up like a minigun: 6 rodents a second rising to **36 a second** over 1 s,
5 s at full rate, then a 0.6 s wind-down and the portal pinches shut (about **207 rodents** a cast). The portal turns after
your aim at 240° a second, so it drags like a heavy barrel, and the spread widens from 4° to 10° as it heats. While it's
open your weapon is put away and you move at 75% speed. The ammo is 74% **mice** (5), 16% **rats** (9, and a 20 px shove;
not heavies or bosses) and 10% **hamsters** (6, rolling through one enemy into a second), all times your damage: about
210 damage a second at full rate, 1,200 a cast. Weapon mods (Piercing Rounds, Split Shots, ...) don't apply to rodents.
Going down or the wave ending shuts it at once. Mastered: 2 s longer at full rate. The rare card **Plague Rats** (2
stacks) makes 14 points more of the rodents rats (30% with one) and every rat bite bleeds. The look: gold studs spin
round the portal's rim like barrels, teal swirls turn faster as it heats, a rodent that hits tumbles back off and runs away,
and cheese crumbs flick out of the portal's side like spent cases; a squeak per rodent and a whoosh as it opens.

**Blood Ravager** (DPS Hemomancer, key 1 of the lobby's Ultimate row, picked instead of Crimson Harvest, 70 s): eight thin
blood legs burst out of you like a spider's for **9 s**. You move **1.8x** as fast (432 for a DPS) and walk on them: the
legs step in two diagonal halves, each foot planting ahead of you as you run. **Trample**: every foot that lands stomps for
**7** in a 24 px circle with a small shove, and running into an enemy at speed rams it for **12** (once every 0.3 s each).
**Lash**: your weapon is put away and every click sends the leg whose claw is nearest an enemy within **140 px** out at it:
the claw follows the enemy, lands **20** (and 20 to anything within 16 px of it) with a shove and comes back, 0.2 s all told.
Click as fast as you like (0.07 s apart), **up to 3 legs lashing at once**, each new one going for an enemy no other leg is
already lashing; a click with nothing in reach does nothing. All damage is times your damage. Going down or the wave ending
folds the legs at once. Mastered: lasts 12 s. The look: dark red legs with a wet sheen, barbed thighs and knee knobs, a
shadow under each foot that shrinks as it lifts, blood stains where feet land, drops falling off the knees and a pulsing red
glow under you; on your own screen a dashed ring shows the 140 px reach, each enemy inside it gets a ring, and three pips
under you light for each leg lashing.

**Blood Boil** (DPS Hemomancer, key 2 of the lobby's Ultimate row, picked instead of Crimson Harvest, 70 s): your blood
boils for **10 s**. It drains **7%** of your max health a second, but never below the blood floor (**20%**), so it can't down you (Bloodthirst
makes it cheaper). The lower your health, the harder it boils: nothing at full health, fully at **20%**. At full boil you
move **1.7x** as fast, fire **2x** as fast and hit **60%** harder, on top of Blood Price (so up to 2.24x at 20% health); half
way (60% health) it's half that. Blood orbs from your kills slow the drain. Press it again to end it early.
When it ends (time up, pressed again, going down or the wave ending) you keep the health you're at and speed, fire rate and
damage go straight back to normal. From full health it reaches the floor in about 11.5 s, so the 10 s take you to 30%. Mastered: up to +90% damage at full boil. The
look: a hot orange glow and three wobbling heat rings round you that swell on a heartbeat (faster as it boils), bubbles
rising off you and popping, wisps of steam, boiling blood left where you run, and on your own screen a thin arc of the time
left; it ends in a burst of steam and a pale ring. Mockup: claude.ai artifact "Blood Boil".

**Blood Ritual** (DPS Hemomancer, key 3 of the lobby's Ultimate row, picked instead of Crimson Harvest, 80 s): a pentagram is bled onto the floor at your cursor (up to 320 px away, 90 px across) over 1.2 s, costing 15% of your max health as it's drawn (never below the blood floor; going down while it's drawn breaks it). Then it flares and caves into a portal, and while it's open enemies on it are slowed 40%. After 0.4 s **blood imps** pour out: 8 at once, then 4 a second for 8 s, 40 in all and at most 24 out at once. Each climbs out, flies at the nearest enemy with the fewest imps on it and circles it, clawing for 6 every 0.4 s; each claw adds a bleed stack and **taunts** that enemy onto the imp for 1 s (bosses ignore the taunt), so it walks and shoots at the swarm instead of you. An imp stops enemy shots (30 health), and the enemy it clings to swats it (8 a second, a boss 16); a worn-out imp pops for 10 to enemies within 40 px. When the portal shuts, every imp still out flies home into it, and each one that makes it heals you 1% of your max health. Design: [`Docs/Mockups/blood-ritual.html`](Docs/Mockups/blood-ritual.html)

**Mammoth Fall** (DPS Summoner, key 2 of the lobby's Ultimate row, picked instead of Legion, 75 s): a violet rift tears open
over you and a woolly mammoth forms in it over **1.6 s** (motes spiral in, a glowing outline fills with wool, it rears at
the end), with a rune circle under your feet. While it forms, a gold ring on the ground follows your cursor (up to 520 px
away): the dashed outer ring is the shockwave, the solid inner one the crush, and an arc round it fills as the summon
completes. Where the ring is when the mammoth is whole is where it lands: it dives there in 0.5 s, its shadow growing.
The slam **crushes** whatever is under it (70 px) for **120**, and a cracked **shockwave** hits everything within **200 px**
for 25, shoves it out 40 px (not heavies or bosses) and **stuns** it for 1.5 s (bosses 0.5 s). After a beat it
**rampages** for 3 s: it charges the nearest enemy within 300 px of the crater (gold brackets mark it), turning hard, and
every enemy it runs into is trampled (18, a 0.3 s stun, knocked aside; at most once every 0.4 s each). It won't charge
an enemy it trampled in the last 1.2 s, so it turns on the next one, and with nothing in reach it circles the crater.
Then it charges off and fades into violet motes. All damage scales with your damage. Going down while it forms cancels
it; once it has launched it finishes. Mastered: rampages 1.5 s longer. The rare card **Ice Age** (2 stacks): the shockwave
reaches 25% farther and stuns 0.5 s longer.

**Brood Queen** (DPS Summoner, key 3 of the lobby's Ultimate row, picked instead of Legion, 75 s): a giant violet spider
lowers herself on a silk thread at your cursor (up to 360 px away, 0.8 s; her shadow grows under her) and lands for **40**
in 70 px, leaving a web there (2 s). For **10 s** she hunts: she walks (120 px/s) at the nearest enemy within 280 px (gold
brackets mark it) and bites it for **30** every 0.6 s, each bite adding a venom bleed (3 a second a stack, up to five).
Regular enemies within 120 px go for her, walking and shooting at her; she soaks 900 of their damage and is gone early if
it runs out. Every 1.2 s she sends out **2 broodlings** (at most 8 out): each runs (240 px/s) at an enemy she isn't
biting, the one with the fewest broodlings on it first, latches on for 3 s and bites for **4** every 0.25 s; if its prey
dies it hops to another within 120 px, or pops. Every 3 s she spits a **web** (80 px, 3 s) at the biggest pack within
360 px: enemies on a web are 50% slower (Heavy enemies and bosses wade). Then she climbs her thread back up (0.9 s) and
every broodling still latched bursts for 6. All damage is times your damage: about 150 a second with a full brood. Going
down while she drops cancels it. Mastered: she stays 13 s. The rare card **Hatchery** (2 stacks): a broodling's kill
hatches 1 more broodling where the enemy fell (+1 a stack, up to 12 out). The look: eight long jointed legs that step in
turn, a dark violet abdomen with a gold hourglass, a gold crown, green fangs that snap forward on a bite and pink eyes;
broodlings are the same at a few px; webs are ten silk spokes and four rings that pop out and fade. Mockup: claude.ai
artifact "Spider Queen" (renamed Brood Queen so it doesn't clash with the Spider Calling's card).

**Forbidden Path** (DPS Ninja, key 1 of the lobby's Ultimate row, picked instead of Death Lotus, 75 s): smoke and a red
seal swallow you (0.6 s: rooted, can't be hit) and you drop to all fours as a **shadow beast** for **8 s**: black, red-rimmed,
red claws and eyes, smoke trailing off its mane and tail. The beast runs at **135% speed**, your weapon is put away, and it
**drains 3% of your max health a second** (never below 1); each enemy the combo kills heals 5% of your max health.
It **locks onto the healthiest enemy within 420 px** (spinning red fang brackets on it, a dashed red chain to you); the lock
holds while it lives and stays within 520 px, then jumps to the next healthiest in reach. **Fire** (held or clicked) runs the
next stage of a five-stage area combo on the lock; you're rooted while a stage runs, and 2.5 s without one starts it over:
1. **Rending Pounce** (0.55 s): a leap to the target's side (untouchable in the air), three claw slashes and a 120 px burst: 30.
2. **Crescent Rake** (0.7 s): two crossing black crescents, 160° wide and 150 px long, toward it: 18 each.
3. **Gnashing Spiral** (1.4 s, untouchable): the beast circles it leaving a red trail, drags everything within 200 px in
   (not heavies) and bites six times: 8 to the target and to anything at its jaws.
4. **Shadow Rend** (1 s): three red shades of the beast run out to the three nearest other enemies within 420 px and tear
   through them: 25 each, 12 to anything else on their path (alone, one shade tears the target).
5. **Forbidden Gate** (1.4 s): the beast rises over a rune circle (untouchable), then slams a **black sun** with a red rim
   and cracks on the target: 90 to everything within 260 px; then every **Blood Brand** within 420 px bursts for 10 each.

**Hundred Paper Cranes** (DPS Ninja, key 2 of the lobby's Ultimate row, picked instead of Death Lotus, 75 s): **100 folded
paper cranes** fly out of you over 0.8 s and spiral out into a **swirling ring 60 to 130 px round you** that follows you,
over a faint lilac ink vortex. For the first **1 s** they only gather. Then, over **3 s**, **one crane every 0.03 s** peels
off the ring along its orbit and **homes in** (540 px/s, turning harder the longer it flies). Each picks the enemy within
**700 px** of you with the **most health left after the cranes already flying at it**, so the first ones pile onto the
healthiest and later ones spread across the wave; one whose enemy dies picks another. Each crane hits for **10** (1000 in
all) and unfolds into paper scraps. With no enemy in reach the cranes wait in the ring; any still there at **4 s** scatter
harmlessly. A gold arc round you counts the 4 s down. It stops if you go down or the wave ends (cranes already flying fly
on). Mastered: **130 cranes**, released in the same 3 s. Drawn as cream origami cranes with a red seal dot, flapping as
they bank, each in flight trailing lilac ink.

**Oni Mask** (DPS Ninja, key 3 of the lobby's Ultimate row, picked instead of Death Lotus, 75 s): a red demon mask drops onto
your face (0.4 s, you can't be hit) and for **10 s** you move **30% faster**, every hit you land does **40% more damage** and
your **max health is 50% higher** (the gain comes as health). Each enemy **you** kill adds **0.5 s** (an elite, heavy or boss
**2 s**), up to **25 s** in all. When it ends your max health drops back and your health is kept up to it (never below 1).
It also ends if you go down or the wave ends. Mastered: **0.75 s** a kill, up to **30 s**. Drawn as a hannya mask (red face,
cream horns, glowing gold eyes, fangs) leaning toward your aim, over a crimson flame aura with red afterimages; a red ring
round you drains with the time left and flashes gold where a kill put time back on, and a gold streak runs from the enemy to
you. In the last 2 s the mask cracks and the flames sputter; at the end it shatters.

**Static Bubble** (Healer Elementalist, key 1 of the lobby's Ultimate row, picked instead of Sanctum, 80 s): a crackling
**44 px bubble of static** wraps you for up to **10 s** and moves with you. Enemies can't be inside it, heavies and bosses
too: one touching it is put out at its edge and **shoved 60 px**, takes 4 and is stunned 0.35 s (not bosses), and
**feeds it 3** (once per 0.6 s each). **Enemy shots** touching it are **eaten**, feeding it 1 each. **Allies inside can't
be hurt.** It **grows to 118 px** as it fills (40 charge), running from cyan to white-violet and crackling harder; a gold
arc round it shows the charge, a thin grey one the time left. **Full, it bursts**: 70 damage to everything within 190 px,
a 0.6 s stun (not bosses), a 110 px shove, every enemy shot in reach erased, and **Static Rush** for you and every ally in
the blast: **6 s of +40% move speed and +60% fire rate** (a gold ring draining round them). If time runs out first it pops
smaller: its reach, damage and Static Rush scale with how full it was (at least a quarter). Going down or the wave ending
ends it with no burst. Mastered: the burst reaches 240 px and Static Rush lasts 8 s. Damage is times your damage.

**Eye of Calm** (Healer Elementalist, key 2 of the lobby's Ultimate row, picked instead of Sanctum, 85 s): a **hurricane**
round you for **10 s** that moves with you; you walk at 80% and can still shoot. Its wall grows out from the eye over 0.8 s
to **280 px**. The **calm eye** (90 px round you) heals every ally standing in it, you too, **12 HP a second**; enemies
can't walk into it, and every enemy shot that reaches it, red aimed shots too, is gone. Enemies in the **wall** take
**22 a second** (bosses too), are slowed 45% and drift round it (heavies and bosses aren't moved); ordinary enemy shots
entering it are swept away, while red aimed shots fly on to the eye. It ends (on time, when you go down or between waves)
in a **final gust**: everything in the wall takes 40 and is flung 200 px out (not heavies or bosses). Mastered: the wall
reaches 335 px and the gust hits for 80. Design: [`Docs/Mockups/eye-of-calm.html`](Docs/Mockups/eye-of-calm.html)

**Conduit Web** (Healer Elementalist, key 3 of the lobby's Ultimate row, picked instead of Sanctum, 80 s): a **field of static**
bursts out round you to **330 px** in 0.35 s and moves with you for **4 s**. Every enemy in it, and any that walk in, is wired
into one **circuit**, drawn like Static Shock's: **three conduits** leave your hand and each enemy joins the closest point
already in the circuit, so it branches out in three arms. A wired enemy takes **12** when it's first wired, is **held stunned**
while it stays wired (bosses are shocked but not stunned) and is **shocked every 0.5 s for 8**, growing to **16** after 3 s
wired; the bolts to it thicken and run whiter as it grows. Every 0.5 s each teammate in the field (not you) heals **5**, with a
gold bolt from your hand. When it ends (on time, when you go down or between waves) the enemies still wired stay stunned **1 s**
more. Mastered: the field reaches 420 px and lasts 6 s. Damage scales by your damage. Design:
[`Docs/Mockups/conduit-web.html`](Docs/Mockups/conduit-web.html)

**Honey Raid** (Healer Summoner, key 1 of the lobby's Ultimate row, picked instead of World Tree, 80 s): a **giant bee** comes in
from 900 px behind you and flies straight over your cursor (kept within **600 px**) in 1.6 s, dripping **honey puddles** on the
way (one every 70 px, 32 px wide, 4 s, the usual 40% slow). Over the spot it dumps its whole load: a dashed gold ring shows where,
and 0.4 s later the glob lands, splashing **40** on every enemy within 120 px, and spreads into a **240 px honey pool** for
**8 s** (a gold arc round its rim shows the time left). Enemies wading in it are **slowed 55%** (Heavy enemies 35%, bosses 20%)
and take **12 a second**; **allies standing in it heal 10 a second**. In its **last 1.5 s the honey sets**, paling to cracked
wax: every enemy in it but bosses is **stuck for 1 s**. The bee flies on off the map. Mastered: the pool is **300 px**. Damage is
times your damage.

**Tortoise Oracle** (Healer Summoner, key 2 of the lobby's Ultimate row, picked instead of World Tree, 90 s): an **old
tortoise** settles at your cursor (kept within **500 px**) and slows time in a **240 px circle** for **10 s** (a clock face
of ticks round the rim, a hand sweeping once round, the time left as a gold arc). **Enemies inside act at half speed**
(moving, turning, winding up and firing; **bosses at 0.75**), and **enemy shots over it fly at half speed** (keeping their
reach). **Allies standing in it run at double tempo**: heals on them count **double**, they **fire twice as fast** and
their **abilities recharge twice as fast** (not the ultimate). Summoned minions aren't sped up. Slowed enemies wear a
dashed amber ring, sped-up allies a pulsing jade one. Casting again replaces your circle. Mastered: the circle is **300 px**.

**Sleepy Cat** (Healer Summoner, key 3 of the lobby's Ultimate row, picked instead of World Tree, 85 s): a lilac **cat** runs
after your cursor for **5 s** (up to **560 px/s**, easing in as it gets there), a dashed gold ring round it showing its reach.
Your weapon is put away: **each click is a meow** (**4 meows**, 0.35 s apart; a paw pip under the cat for each left) that
**marks every enemy within 95 px** of the cat with a gold paw. When the 5 s are up (or 0.6 s after the last meow, or if you go
down) the cat **curls up for a nap** and **every marked enemy falls asleep for 10 s**: no moving, no shooting, a lilac ring of
its sleep left and Zs drifting off its head. **Hits don't wake them.** Bosses only **doze for 3 s**. While it naps the cat
**purrs**: **allies within 140 px of it heal 15 a second** (a dashed green ring). Mastered: **5 meows** and a **120 px** reach.

**Heartbeat** (Healer Hemomancer, key 1 of the lobby's Ultimate row, picked instead of Sanguine Rain, 80 s): your heart
**beats three times**, 0.35 s after the cast and then every 0.9 s. Each beat costs **8% of your max health** (never
below the blood floor, 20%, so it can't down you; Bloodthirst makes it cheaper) and sends a **wave of blood** out of you to **320 px**
in 0.6 s. Each wave hits every enemy it reaches once for **45** and shoves it 26 px. **Every enemy a wave kills drops a
blood orb** (the Hemomancer's own: a small health orb only you drink, drifting to you), except where health
orbs are off (Famine, Bloodlust, Blood Moon). Veins pull in toward you before each beat; the heart on your chest swells
white on the beat and flings blood that dries on the floor; a dashed ring shows the reach, and three pips under you
light one a beat. Going down or the wave ending stops it. Mastered: the waves hit 40% harder. Damage is times your damage.

**Blood Garden** (Healer Hemomancer, key 3 of the lobby's Ultimate row, picked instead of Sanguine Rain, 80 s): pay **30% of
your current health** to fling **12 blood seeds** round you, 60-260 px out. Each lands after 0.5-0.85 s and sprouts a **blood
sapling** with a **90 px thorn aura** that lasts **12 s**. Enemies in an aura are **slowed 40%** (heavy enemies and bosses wade)
and **siphoned 12 HP a second** (a hit from you every 0.5 s, so your damage multiplier scales it); a blood tether runs from each
into the sapling. The siphoned health fills the sapling's **bulb**; at **60** it bursts and drops a **blood orb anyone can drink
that heals 25**, and the sapling is gone. An unfilled sapling withers. Saplings stay when you go down. Mastered: 16 seeds.

**Heart of the Swarm** (Healer Hemomancer, key 2 of the lobby's Ultimate row, picked instead of Sanguine Rain, 80 s): a
**giant heart** appears at your cursor and follows it for 8.45 s. It **beats six times**, 0.35 s after the cast and then every
**1.5 s**. Each beat **heals every ally within 300 px of the heart 8%** of their max health (not you) and **pulls every enemy in
reach 70 px toward the heart** (never past it; heavy enemies and bosses don't move). It costs no health. The heart squeezes
twice on each beat (lub-dub) and glows; a dashed ring shows the reach, a gold arc fills toward the next beat, a dotted
tether runs from you to the heart, tendrils reach for the enemies it will pull, and on each beat rings roll out and veins
carry drops of blood to the allies it heals. Going down or the wave ending stops it. Mastered: each beat heals 11%.

**Astral Projection** (Healer Ninja, key 1 of the lobby's Ultimate row, picked instead of Blossom Storm, 70 s): for **8 s**
your body stays where it is behind a slowly turning gold hex ward: it **can't be hurt, can't move or dodge, and enemies
ignore it** (any already after it look for someone else). Your **spirit**, a pale glowing ninja, rides the **nearest
teammate** on a swaying cyan tether (a dotted silver cord still runs back to your body, and the camera follows the spirit).
It leans toward your aim, at most 110 px from them. **Fire** shoots **astral bolts** from the spirit (9 damage, 6 a
second; your weapon is put away), and the spirit can't be hurt. The teammate it rides **heals 7 HP a second** and **moves
35% faster** while the tether holds. **Right click** (the parry key) sends the spirit to the **next teammate**, round the
team (0.4 s apart). If the host goes down the spirit moves on; with nobody left, it rides your own body, which then heals
and runs 35% faster itself. It ends on time, when you go down, or when the wave ends. Mastered: 10 s and the heal is 40%
stronger. The Rare card **Astral Bond**: 2 s longer and astral bolts hit 25% harder (a stack, up to 2).

**Pressure Point Press** (Healer Ninja, key 2 of the lobby's Ultimate row, picked instead of Blossom Storm, 70 s): a jade
ring sweeps out **360 px** round you and **marks the 8 nearest enemies** (a pulsing jade reticle; they're stunned where they
stand). Then you **blink to each in turn**, nearest next, **0.15 s apart**, and can't be hurt for the whole cast. Each
**press** hits for **30**, **stuns 2 s** and **poisons** (8 a second for 5 s); bosses and heavies are slowed 50% instead of
stunned. Each press also sends a **jade wisp** that **heals your most hurt teammate 10** (you included). Then the **Seal**:
you blink back where you cast and every pressed enemy still standing is **stunned 1 s more** (jade threads to each). It stops
if you go down or the wave ends. Mastered: marks 12. The Rare card **Hundred Meridians**: 2 more marks and presses hit 20%
harder (a stack, up to 2).

**Shadow Clone Medics** (Healer Ninja, key 3 of the lobby's Ultimate row, picked instead of Blossom Storm, 80 s): a hand
seal, and a **shadow clone medic** steps out of a puff of ink smoke beside **every player, you included**, for **10 s**.
Each clone trails its player 26 px away on the side of the nearest enemy and **heals them 4 every 0.5 s** (8 HP a second;
a jade ring pulses over them with each heal, and a dotted jade thread joins them). When an enemy shot is **heading for its
player** (within 260 px), the clone dashes onto its line and **stops it**, red aimed shots too; shots aimed at anyone else
pass through it, as do your team's, and melee enemies ignore it. A clone has **120 health + 6 a wave** (50% more when
you're alone), shown as a violet ring round it; each shot it stops takes its damage off, and at 0 it pops in smoke. When a
clone goes (broken, on time, or when the wave ends) its player heals **a fifth of the damage it soaked**. A new cast
replaces your old clones. Mastered: 12 s and 40% more health. Each clone is a violet ink ninja with a jade rim, headband
and tails and a pale medic cross; it flashes pale when it takes a shot and flickers over its last second, then vanishes
in smoke with a falling jade leaf. Design: [`Docs/Mockups/shadow-clone-medics.html`](Docs/Mockups/shadow-clone-medics.html).

**Blood Brand**: every combo hit brands what it hits (red pips under it, up to 5; they fade 6 s after the last). All damage
is times your damage. A stage running when the time's up finishes first. Going down or the wave ending ends it at once.
Mastered: the beast lasts 11 s. The rare card **Forbidden Hunger** (2 stacks): the combo hits 20% harder and kills heal
twice as much. With Visual effects off the beast has no smoke or glow and the moments are plain rings and lines.

**Stone Colossus** (Tank Elementalist, key 2 of the lobby's Ultimate row):

| | Ultimate |
|---|---|
| **Tank Elementalist** | **Stone Colossus** (80 s): the earth piles onto you and you stand up as a golem of brown stone with glowing lava veins, about three times your size, for 15 s. **Rise** (1 s): rock pillars erupt round you (20 to enemies within 95 px, a shove) while rocks fly in and build the body; you can't be hurt while it forms and move at 40%. **Golem**: you take 70% less damage, walk at 80%, and every enemy, bosses too, goes for you. **Stomp**: every 40 px you walk a foot comes down beside you, 25 to enemies under it and a 0.15 s stun, with a dust puff and small cracks. **Slam**: Fire is the slam while you're the golem (your weapon is put away): both fists rise, then come down 110 px ahead (0.22 s), 60 to everything within 110 px, a 0.6 s stun and a shove, with a ring, flying chunks and hot cracks; one a second while you hold Fire (your own golem shows a faint ring where they'll land). **Crumble**: when it ends it shakes for its last 1.5 s, then breaks into rubble and 8 boulders roll 260 px outward, 40 to each enemy they run over. It also ends at once if you go down or the wave ends. All damage is by your damage; stuns and shoves skip bosses and heavies. Mastered: lasts 18 s and slams reach 25% wider. Rare card **Bedrock** (up to 2): 3 s longer and slams 25% wider a stack. A bot uses it with 4 enemies within 260 px |

**Vortex** (Tank Elementalist, key 3 of the lobby's Ultimate row, 80 s): a ring of dust closes in on you for 0.4 s, then a
**240 px** whirlwind spins up round you for **5 s** and follows you while you move at full speed. You stand in its calm
60 px eye. Every enemy inside is **swept round** it at 280 px/s and pulled in 50 px/s (never into the eye), and enemies within
60 px outside its edge are drawn in. Heavies, bosses and enemies that never move stay put. Everything inside, bosses too,
takes **9 every 0.2 s** (45 a second, times your damage multiplier: 225 over the storm). **Enemy shots** that enter it are
swept away (they curl off round it as a wisp and are gone). When it ends (time, going down, the wave over) everyone inside
is **flung** 160 px outward (not heavies or bosses) and takes 30. The look: a pale field deepening toward the rim, six
trailing spiral arms, wind lines going round faster near the eye, leaves and grit thrown round the outside, a cloud wall
with two dashed rings, and the time left draining round the edge. You hear the Undertow's inhale at the cast, its
swirling hum while it blows, and its pop at the fling. Bots cast it with four or more enemies within 260 px. Mastered:
300 px across and 55 a second. Design: [`Docs/Mockups/vortex.html`](Docs/Mockups/vortex.html).

**Living Bolt** (DPS Elementalist, key 1 of the lobby's Ultimate row, picked instead of Bullet Rain, 80 s): for **0.5 s**
arcs gather onto you (you can't move or attack), then you're **electrified for 10 s**. Your weapon is put away and Fire is
the dash: each **click blinks you to the cursor**, up to **260 px** (walls and props stop it short), as often as you like
with only **0.12 s** between dashes (a click in that gap goes as soon as it ends), and you can't be hurt for the blink.
Every enemy within 30 px of the path is **shocked**: **40** and a 0.3 s stun (heavies and bosses: the damage only); it's
lightning, so wet enemies arc on. Each path stays live as **static residue** for **3 s**: **12 a second** to enemies
touching it (once a tick, however many trails cross). When the 10 s run out you **discharge**: 60 to every enemy within
130 px. Going down or the wave ending stops it at once, with no discharge. All damage is by your damage. The look: violet
arcs jumping inward for the channel; then a cyan halo with arcs on your body and the time left draining round you in
gold, flickering violet over the last 2 s; each dash is a jagged violet-and-white bolt with branches, afterimages along
it and a ring where you land; the residue is a faint lane with a thin bolt crackling along it. Sounds: a chain-arc
crackle at the cast, a zap for each dash, a boom for the discharge. Bots cast it with a target within 380 px. Mastered:
13 s, and the residue stays 5 s.

**Flash Fire** (DPS Elementalist, key 2 of the lobby's Ultimate row, picked instead of Bullet Rain, 80 s): a **0.5 s**
kindle at your feet (rune arcs close round the edge, embers stream into you), then a ring of fire rolls out to **260 px**
in 0.3 s: **100** to every enemy it passes, bosses too. The fire then **stays for 8 s**: a wall of flame round the edge
and fire on the ground inside, **25 a second** to every enemy in it, slowing them **40%** (heavies and bosses 20%). When it
goes out, every enemy still in it is left **burning, 12 a second for 5 s**. It stays where you cast it, and all damage is
by your damage. The look is take C "Pyre" of `Docs/Mockups/flash-fire.html`: a white-hot ring, then flame tongues along
the edge, small flames flaring all over the scorched ground and embers rising; smoke puffs as it goes out. Bots cast it
with 4 enemies near and a target within 260 px. Mastered: the fire burns 11 s. Rare card **Backdraft** (up to 2): 20%
farther, and what it leaves burning burns 50% hotter.

**Rock Gloves** (DPS Elementalist, key 3 of the lobby's Ultimate row, picked instead of Bullet Rain, 80 s): a **0.6 s**
gather (lava cracks open round you, shards of earth rise from them and fly in; you walk at half speed and can't attack),
then two **giant floating fists of earth** hover at your sides for **10 s**. Your weapon is put away and Fire punches: hold
it for **rapid jabs** at the cursor, one every **0.09 s**, the fists taking turns: **26** to every enemy along the fist's
path (out to **130 px**, 46 px wide) and a small shove. Every **8th** punch is the **Haymaker**: both fists drive in
120 px ahead, **80** to everything within **90 px**, a **0.5 s** stun and a shove; the next jab waits 0.32 s. When the
time runs out the fists burst into **8 shards** that fly 260 px outward, **20** to each enemy they hit. Going down or the
wave ending stops it at once, with no shards. It's earth: every hit leaves enemies brittle. Stuns and shoves skip heavies
and bosses, and all damage is by your damage. The look: the fists are Stone Colossus's stone with glowing lava seams,
bobbing over their shadows; a jab drives one out with dust streaks behind it and white-hot knuckles, a Haymaker both; a
gold ring drains round you with the time left, eight pips under you count to the Haymaker, and over the last 1.5 s the
fists shake and shed chips. Bots cast it with 3 enemies near and a target within 200 px. Mastered: 13 s, and the Haymaker
hits 25% wider. Rare card **Granite Knuckles** (up to 2): punches 20% harder and reach 15% farther.

**Spirit Avatar** (Tank Ninja, key 2 of the lobby's Ultimate row, 70 s): a giant spirit brushed in ink, with a red seal on
its back and a paper mask, wraps you for **12 s**. **Rise** (0.8 s): ink drops pull in round you; you can't be hurt, and
enemies within 95 px take 30 and a shove. **Avatar**: you take **60% less damage**, walk at 85%, every enemy (bosses too)
goes for you, and **enemy shots** that reach the spirit's body (64 px round you) are swallowed with a splash of ink.
**Moon Cleave**: Fire is the cleave while it's out (your weapon is put away). The giant draws its brush back for **0.9 s**
while a red arc fills in on the floor (you can still turn, slowly), then sweeps it **200°** across your front out to
**170 px**: **80** to every enemy it passes, a 0.5 s stun and a push of about 150 px; heavies are pushed half as far and
not stunned, bosses only take the damage. It leaves a stroke of ink on the floor for 2 s. One every 1.6 s while you hold
Fire. **Release**: in its last second it shudders, then bursts: 40 and a shove to enemies within 130 px. It also ends at
once if you go down or the wave ends. All damage is by your damage. Mastered: lasts 15 s and the cleave reaches 20%
further. Rare card **Kami Vessel** (up to 2): 2 s longer and the wind-up 0.2 s faster a stack. Bots cast it with four or
more enemies within 260 px. Design: [`Docs/SpiritAvatar.md`](Docs/SpiritAvatar.md), mockup look C of
[`Docs/Mockups/spirit-avatar.html`](Docs/Mockups/spirit-avatar.html).

**Five Shadow Gong** (Tank Ninja, key 3 of the lobby's Ultimate row, 70 s): a bronze gong on a red-lacquered frame drops
in 95 px ahead of you (0.55 s) and you wind back a mallet. At **0.95 s** you strike it: a gold shockwave, the arena dims,
and every enemy within **900 px** that isn't a boss or heavy is stunned until the sheathe. The **15** enemies nearest you
are split by direction into five slices, and a **shadow ninja** appears 100 px out toward each (1.05 s). From 1.3 s every
shadow flashes through its 3 enemies, nearest first, 0.13 s a cut, leaving gold slash lines and a cut mark on each. About
0.4 s after the last cut they **sheathe**: every cut lands at once, **120** each (**60** on bosses and heavies), times
your damage, and the enemies split in half and burst in ink. With fewer than 15 enemies in reach the spare cuts go round
them again. Then each shadow drops a **smoke grenade** and vanishes as it pops: five clouds, 90 px round, for 4 s, where
enemies move and shoot **40% slower**. It plays out even if you go down; it stops if the wave ends. Mastered: each shadow
cuts **4** (20 cuts). Rare card **Temple Bronze** (up to 2): cuts 20% harder and the smoke 25% wider a stack. Bots cast it
with five or more enemies near them.

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

**Iron Armadillo** (Tank Summoner, the third ultimate, key 2, 75 s): a giant spirit armadillo wraps round you and curls
into an armoured ball 140 px across (0.6 s, rooted, spirit arcs closing in), then you roll for 10 s. The ball steers toward
your cursor (or where WASD points) in wide turns and builds speed up to 520 px/s; hold **Fire** to brake and aim (a gold
dashed arrow and a ring fill over 0.8 s), let go to launch at up to 780 px/s, dropping back to 520 over about half a
second. Nothing hurts you while you're curled up: enemy shots that reach the shell ricochet off it as your arcane bolts (15
each), and red aimed shots just stop. Each enemy you roll into takes 20 at rest to 80 at full speed (up to 100 at the
launch's top speed), times your damage, at most once every 0.4 s: regular enemies are flung, and one flung fast hurts
what it hits (as a Lance shove); Heavies are stunned 0.6 s and bounce you off, and so do bosses. Hitting a wall or a prop
faster than 312 px/s bashes it for 50 within 120 px, and you bounce (the plain arena wraps round, so walls are the
Expanding arena's, Tiny Arena's and the like). Your weapon, its Alt, your abilities and the Dodge are locked; your
minions keep fighting. On time the ball uncurls with a slam: 90 within 180 px, shoving regular enemies back; going down
or the wave ending uncurls it quietly. Mastered: rolls 13 s and the slam hits for 130. Bots cast it with an enemy within
420 px. The banded plates roll over the top as it goes, with a teal spirit rim, a dust trail and speed lines (gold while
launched); a thin bar over it is the time left. In the lobby preview it rolls a little slower at the dummies, bouncing round the picture's edges, and flings them less far.

**Tidal Tusker** (Tank Summoner, the fourth ultimate, key 3, 75 s): a puddle opens beside you and a teal spirit elephant
rises out of it (0.8 s), then follows you for 12 s, plodding 85 px behind you (behind the way you walk). Regular enemies
within 300 px go for it and shoot at it instead of you (bosses and mini-bosses don't). Enemy shots that reach it stop there
and wear its health (600, +30 a wave), and bodies pressing on it bite it every 0.5 s and can't walk through it. Its trunk
hoses a **steady stream** at the nearest enemy within 260 px: the stream stops on the first enemy in its line, 40 a second
(times your damage), keeps it **wet** for 3 s (slowed 30%, lightning arcs to it) and pushes it back 60 px a second (heavies
and bosses aren't pushed). At the end, or as soon as its health runs out or you go down, it rears and stomps: 60 to every
enemy within 160 px, and a splash that soaks and shoves back everything within 220 px. The wave ending puts it away
quietly. Mastered: it stays 16 s and sprays two streams at two enemies. Rare card **Deep Trunk** (two stacks): the stream
reaches 25% farther and keeps enemies wet 1 s longer. Bots cast it with an enemy within 420 px. The elephant steps, swishes
its tail and flaps its ears (hard when it's bitten), its ringed trunk swings after what it sprays, and the stream is a
rippling hose of water with pulses racing down it and foam where it hits, leaving puddles that dry; a health bar and a time
bar float over it, and the stomp floods the ground round it and shakes the screen. With Visual effects off it is drawn
plainly (an ellipse and a line per stream).

### Ability mastery

Every ability (role abilities, the extra ones from upgrades, and ultimates) **gains mastery from 1 to 5 as you use
it** during a run. Each cast during a fight earns XP equal to the ability's cooldown in seconds, so fast and
slow abilities gain mastery at about the same pace. Instant abilities (Shockwave, Frost Nova, Mend Pulse, ...)
earn by what they did: half for a cast that hit nothing, up to 1.5x for one that hit, healed or erased a
lot. Casting between waves or with no enemies around earns nothing. Mastery 2-5 need 100 / 300 / 550 / 850 XP in
total: about mastery 3 by wave 16-20 and mastery 5 by wave 40-50 if you use it whenever it's ready. Under
each ability key, four cells (one per mastery step, the current one filling; gold once mastered) and the mastery digit
("M" at 5) show its mastery. Each cast that earns XP floats "+24" off the key; a new mastery step flashes brackets round
the key, pops "MASTERY 3" over it and shows a line above the keys with what it gave (+8% power, -4% cooldown), or
"MASTERED" and the capstone. The Tab menu shows the mastery too.

- **Each mastery step:** +8% to everything the ability does (damage, healing, shields, buff duration, and the zones,
  minions and shots it creates) and 4% off its cooldown. Mastery 5 is +32% and -16%.
- **Mastery 5 unlocks its capstone**, a change to how it works:

| Ability | Capstone | Ability | Capstone |
|---|---|---|---|
| **Barrier** | Bullets that hit the barrier are reflected back as your own. | **Mend Pulse** | Also erases enemy bullets within its range. |
| **Poison Dart** | Spreads to 5 enemies at a time, at full strength. | **Shockwave** | The wave runs out 300 px. |
| **Overdrive** | +1 extra projectile while it lasts. | **Blink** | Erases enemy bullets where you leave and where you land. |
| **Nova** | Fires 48 shots instead of 24. | **Turret** | Deploys two turrets. |
| **Roar** | Also knocks enemies within 250 px back and slows them for 2 s. | **Bulldoze** | Erases enemy bullets along the charge. |
| **Adrenaline Shot** | Also gives the nearest other ally within 250 px a half-length (4 s) dose. | **Deployable Medkit** | Bursts into 9 orbs worth 20 health each. |
| **Airstrike** | A second wall lands 160 px further out, half a second later. | **Claymore** | Each claymore re-arms once after its first blast: it goes off twice. |
| **Aeroaegis** | Lasts 4.5 s and reflects bullets for double damage. | **Frost Nova** | Also erases enemy bullets around you. |
| **Flame Wall** | The fire also slows enemies by 40%. | **Healing Rain** | Lasts 10 s, and downed allies inside revive twice as fast. |
| **Gust Gate** | The ring is 40% wider and pushes enemies twice as hard. | **Lumen Ward** | Also freezes enemies outside the light for as long as it lasts (not bosses). |
| **Meteor** | Leaves burning ground for 3 s. | **Blizzard** | Lasts 10 s. |
| **Tsunami** | Wet lasts 14 s, and the wave rolls back to you. | **Golem Wall** | Raises five golems. |
| **Gale Wall** | The wall is 40% wider and holds up to 60 charges. | **Water Ward** | Splashes and bursts reach 40% farther. |
| **Deployable Cover** | Covers take 600 damage, and each charge comes back 25% sooner. | **Stampede** | 9 beasts, and the herd turns and charges back. |
| **Decoy Dummy** | Dummies have 50% more health, and the confetti blast reaches 130 px. | **Tactical Vest** | Pops 4 gadgets, and they stay on the floor until you grab them. |
| **Earthquake** | The Burrower also freezes everything it bites for 1 s (not bosses). | **Force Pact** | A second thrall: the pact keeps the last two types you chose. |
| **Mending Web** | Webs heal and poison 40% more. | **Phoenix** | Revives at full health, and the phoenix keeps fighting. |
| **Soul Lantern** | Lasts 9 s, and the linked player can't go down. | **Bat Swarm** | Releases 14 bats. |
| **Hydra** | A fourth head. | **Alpha Call** | Also summons 3 wolves for 6 s. |
| **Flash Fire** | The fire burns 11 s. | **Rock Gloves** | Lasts 13 s and the Haymaker hits 25% wider. |
| **Fortress** | Lasts 14 s. | **Juggernaut Suit** | 450 armour, and each flak shard does 18. |
| **Glacial Tomb** | Enemies stay frozen for 6 s and bosses take 500. | **A Literal Tank** | 550 armour, and the shells blast 30% wider. |
| | | **Armor Drop** | The crate holds a 4th vest. |
| | | **Rocket Drop** | The launcher lasts 13 s, and each blast throws 3 bomblets (60 px, 40 each). |
| **Titan** | The titan is 30% bigger and stays for 27 s. | **Lifeline** | Everyone also gets 4 s of Overdrive. |
| **Sanctum** | The dome is half again as wide (300 px). | **World Tree** | Lasts 20 s, butterflies every 1.4 s and heal 18 each. |
| **Deployable Turret** | Fires 26 s, rounds pierce 4, and the blast does double. | **Bullet Rain** | Lasts 9 s. |
| **Legion** | Summons 7 wolves and 7 bats. | **Shield Wall** | Bullets that hit the shield are reflected back as your own. |
| **Leap Slam** | The landing also erases enemy bullets within 160 px. | **Cavalry Call** | Two more lancers ride outside the first pair, and the guard lasts 5 s. |
| **Consecrate** | Two more crosses fly out 22 degrees either side (each target is hit once). | **Guardian's Oath** | Charges up to 800 px (not 600). The ward is wider, follows you, and stands a downed ally back up at 25% health. |
| **Lay on Hands** | Also heals everyone else near them for half as much. | **Bladed Dive** | Each kill heals you 5% of your health. |
| **The Gauntlet** | The Challenge lasts 8 s, and the charge's impact stuns for 1 s. | **Caltrop Run** | Caltrops last 9 s and each catches 3 enemies. |
| **Trojan Horse** | 40% more health, and the arrows pierce one more enemy. | **Cannon Call** | Two smaller balls (70% of the blast and damage) land 115 px behind and beyond the mark along your aim, 0.22 s apart. |
| **Last Stand** | You take 5% of the damage instead of 10%. | **Hallowed Blade** | Three swords fall: one at your cursor and one either side. |
| **Drawbridge** | The bridge stands for 8 s. | **Lightning Rod** | Lasts 8 s and each bolt arcs to 3 enemies. |
| **Flood** | Deluge: the flood is 30% wider and lasts 9 s. | **Bone Phalanx** | 10 skeletons with 180 health each (a 250 px front). |
| **Dragon's Dogma** | Hunts for 7 s, and every enemy it devours heals you 3%. | **Warhorn Volley** | The volley is 40% wider and the fire burns for 8 s. |
| **Attack Helicopter** | A second gun on the wing: two targets at once. | **Field Hospital** | 8 orbs, one back every 3 s. |
| **Spirit Avatar** | Lasts 15 s and the cleave reaches 20% further. | **Rodent Minigun** | Fires 2 s longer (7 s at full rate). |
| **Orbital Laser** | The circle is 340 px wide instead of 260, and the beam ploughs 200 px on while it fires. | **Mammoth Fall** | Rampages 1.5 s longer (4.5 s). |
| **Land Shark** | A 30% wider puddle, and the bite hits for 160. | **Senbon Seekers** | Holds up to 12 needles. |
| **Octopus Familiar** | The octopus has 380 health and slaps for 22. | **Shadow Clone Medics** | Lasts 12 s and clones have 40% more health. |
| **Blood Ravager** | Lasts 12 s. | **Forbidden Path** | The beast lasts 11 s. |
| **Astral Projection** | Lasts 10 s and heals 40% more. | **Iron Armadillo** | Rolls 13 s and the uncurl slam hits for 130. |
| **Pressure Point Press** | Marks 12 enemies. | **Brood Queen** | She stays 13 s. |
| **Blood Boil** | Up to +90% damage at full boil. | **Ballista** | 10 bolts, and each one pins what it hits. |
| **Aegis Standard** | A 120-point shield that regenerates after 0.75 s. | **Pilgrim's Standard** | Allies inside heal 5% a second. |
| **War Standard** | +55% attack speed and +35% damage. | **Holy Lance Rain** | 32 spears, and the glows last 6 s. |
| **Excalibur** | Lasts 13 s and the shockwaves fly 30% farther. | **Chalice of Life** | The shield is 60 and lasts 6 s. |

A popup appears when an ability gains mastery. The **Tab** menu shows each ability's mastery as five pips (the next one
fills as it earns XP). What mastery 5 unlocks is always written in gold under each ability's description: in the lobby
(your 1st ability and your ultimate), on the Tab screen's ability tiles, on the cards that give an ability (in the
level-up picks and the Bazaar). Where space is tight the text gets smaller, then shortened
with "…"; hover a Tab tile to read it in full.

In the lobby, the loadout screen's **Ultimate** row has four keys. Key 0 is your role and class's default ultimate;
the next keys hold the others you can pick instead (key 1 is **Juggernaut Suit** for the Tank Soldier, with **A Literal Tank** on key 2 and **Armor Drop** on key 3, **Trojan Horse** for the Tank Knight (key 2: **Drawbridge**, key 3: **Aegis Standard**), **Pilgrim's Standard** for the Healer Knight (key 2: **Holy Lance Rain**, key 3: **Chalice of Life**), **Lightning Rod**
for the Tank Elementalist (key 2: **Stone Colossus**, key 3: **Vortex**), **Bloodbath** for the Tank Hemomancer, **Bone Phalanx** for the Tank Summoner (key 2: **Iron Armadillo**, key 3: **Tidal Tusker**), **Dragon's Dogma** for the Tank Ninja, **Attack Helicopter** for the DPS Soldier (key 2: **Orbital Laser**, key 3: **Rocket Drop**), **Living Bolt** for the DPS Elementalist (key 2: **Flash Fire**, key 3: **Rock Gloves**), **Field Hospital** (key 2: **Supply Drop**, key 3: **Hazard Foam**) for the Healer Soldier and **Ballista** for the DPS Knight (key 2: **War Standard**), **Forbidden Path** for the DPS Ninja (key 2: **Hundred Paper Cranes**), **Astral Projection** for the Healer Ninja, **Heartbeat** for the Healer Hemomancer), and the rest
are locked slots for ultimates still to come. Click a key to pick it (the picked one is in inverse video, like the
weapon and ability keys): it goes to the host and the other players like your other picks, the detail box shows its
charge time, what it does and what mastering it adds, and the preview casts it. Changing role or class goes back to
key 0. The pick comes with you into the run and a saved run (a continued run keeps the seat's).
Mastery resets each run.

## Levels and upgrades

Every enemy killed gives **both** players XP. Level up during a wave and you'll see **LEVEL UP!**; when the wave is
cleared, each player picks one of **3 upgrades per level gained** (a level gained between waves, in a woken room, a duel or an extra room, keeps its cards back while an awake enemy is within about a screen of you: a gold **LEVEL UP · CARDS AFTER THE FIGHT** plate shows instead, since co-op can't pause): click (or press 1/2/3) to select a card, then
**Choose Upgrade** (or Enter). The screen wears the lobby's blue CRT look: a glowing VT323 title, boxed cards with corner
brackets, and the lobby's digital keys for **Reroll** and **Choose Upgrade** (Choose is the dashed, locked key until you pick a
card, then it lights up with a green lamp). Each card has its rarity in a small key-like label on top, the icon (in its own
colours) in an outlined tile, and a soft glow (blue common, green uncommon, blue rare, purple very rare, orange legendary)
while it bobs slowly (rare and better ones also shine); the one you pick lifts, glows stronger and outlines in white-blue.
Upgrades with a stack cap show squares that fill with how many you hold (the next one is ringed on the picked card). What the pick changes ("Regeneration 0 → 3 HP/s", or a new
ability and its key) shows on the card itself. Hold **Tab** for all your stats. Boss rewards say **BOSS REWARD!** (legendary cards)
or **WEAPON MOD** (a grid of every mod you can take). Don't like the cards? **Reroll** (or R) for a fresh set: the first click
(or press) arms it (the button turns orange and reads CLICK AGAIN TO REROLL for 3 seconds), the second rerolls. You
start with 2 rerolls and everyone gets 2 more for each boss killed. Where a shop will take gold (the Bazaar, the Expanding arena), a normal level-up also has a **GOLD INSTEAD** key under the buttons: skip the cards for 20 gold plus 2 every two waves. Boss rewards, weapon mods, milestones and challenge prizes can't be swapped. The next wave waits until everyone have chosen.

**Repeat copies give less.** The plain stat cards you can take any number of times (Sharpened Rounds, Quick Trigger,
Light Boots, Vitality, Mending, Focus, Hollow Points, Hair Trigger, Sprinter, Fortitude, Regeneration, Clarity) are
worth a bit less with each extra copy: the 2nd 80%, the 3rd 67%, the 4th 57%, the 5th 50% (copy n: 1 / (1 + 0.25 x (n - 1))).
Fourteen Hair Triggers come to x2.3 fire rate, not x3.8; six Clarity cut cooldowns to 53%, not 38%. An offer of a card
you already have says what this copy is worth ("You have 4 · this copy 50% strength"), and the stat preview shows the
real change. Cards with a stack limit (the squares) keep full strength.

**Wounds.** 20% of every hit you take is a **wound**: it can't be healed back (heals, regeneration, orbs, lifesteal,
revives). Wounds **carry across waves** but never take more than **70% of your max health**. They clear when a **boss falls**, at a **health shrine**, with the **Medic's heal** (now buyable when only wounded) or at a Boss Hunt **Rest**. Saves keep them. Health bars show it as a striped red end with a light tick where healing stops (it flashes white when the wound grows), and your HUD names it (WOUND -52). A Healer's overheal shield only takes what goes past full health. Health orbs skip you once only your wound is left. Both arenas.

| Rarity | Color | What you get |
|---|---|---|
| **Common** | white | Small stat boosts: +10% damage, +10% fire rate, 15% bigger shots (Magnum Rounds), +6% speed, +15 HP, regen, -6% cooldowns, Reinforced Guard (parry) |
| **Uncommon** | green | Bigger boosts (+20% damage, +40 HP, -15% cooldowns...), damage reduction and Wide Guard (parry) |
| **Rare** | blue | **Extra Use**: +1 use for one ability, you pick the 1st, 2nd, 3rd or 4th ability, an upgrade for a specific ability you have, in any slot (e.g. bigger Barrier, longer Roar, double-damage Bulldoze, tougher Deployable Cover, longer Adrenaline Shot, faster Soul Lantern, wider Airstrike bombs, harsher marks, Greater Mending (Mend Pulse heals 60% more, reaches 25% farther); for Knights: Tower Shield, Earthshaker, Iron Will, Sacred Ground, Bulwark, Miracle, Rose Window (Sanctum Glass heals 40% more before it shatters), Coup de Grace (Bladed Dive's cuts deal 25% more damage and it rushes 40 px further), Iron Grip (the Gauntlet flies 40% further and its charge hits 50% harder), Barbed Iron (Caltrop Run drops 4 more caltrops and each hits 4 harder), Siege Powder (Cannon Call hits 25% harder and its blast is 15% wider), Bodkin Points (Warhorn Volley, when it's your ultimate: the arrows hit 25% harder and the volley is 20% wider), Windlass (Ballista, when it's your ultimate: 2 more bolts and a 25% faster reload); for Ninjas: Mirror Image, Thick Smoke, Tempered Steel, Morning Dew, Body Double, Sacred Seal, Assassinate, Unseen Blade, Storm of Steel, Kami Vessel (Spirit Avatar), Temple Bronze (Five Shadow Gong: cuts 20% harder, smoke 25% wider), Hidden Needles (Senbon Seekers: 30% harder, poison stacks 2 higher), Forbidden Hunger (Forbidden Path: its combo hits 20% harder, kills heal twice as much), Thousand Folds (Hundred Paper Cranes: 20 more cranes, 15% harder), Astral Bond (Astral Projection: 2 s longer, astral bolts 25% harder); for Hemomancers: Hardened Blood, Blood Oath, Deep Veins, Lifeblood, Hungry Earth, Saint's Blood, High Pressure, Usury, Crimson Tide, Thornwork (Thorn Bloom hits 20% harder and each tendril jumps once more), Bloodroot (Blood Totem: the veins reach 20% farther, each totem stands 3 s longer), Hard Clot (Clot Crystals: 30% more health, 3 s longer); for Elementalists: Eye of the Storm (Aeroaegis), Permafrost (Frost Nova), Inferno (Flame Wall), Monsoon (Healing Rain), Jet Stream (Gust Gate: the ring lasts 3 s longer, the boost 1 s longer), Dawnlight (Lumen Ward), Cataclysm (Meteor), Whiteout (Blizzard), Riptide (Tsunami), High Tide (Flood: 25% wider, 2 s longer), Momentum Bank (Gale Wall: each caught shot adds 40% more), Deep Current (Water Ward: splashes 40% harder, wet 2 s longer); for Summoners: Basalt Hide (Golem Wall), Deep Tremor (Earthquake), Kindred Bond (Force Pact), Silk Choir (Mending Web), Rebirth Flame (Phoenix), Blood Bats (Bat Swarm), Venom Glands (Hydra), Pack Leader (Alpha Call), Great White (Land Shark: bites 40% harder), Three Hearts (Octopus Familiar: 50% more health, slaps 25% harder), Plague Rats (Rodent Minigun: 30% rats, and rats bleed what they bite), Ice Age (Mammoth Fall: the shockwave 25% wider, stuns 0.5 s longer), Hatchery (Brood Queen: a broodling's kill hatches 1 more broodling, +1 a stack), Deep Trunk (Tidal Tusker: the stream reaches 25% farther, wet 1 s longer); for the Soldier's Medkit: Field Surgeon), wider aura; one card per class: Quartermaster (Soldier: orbs heal 30% more, 2nd, 3rd and 4th abilities recharge 10% faster), Arcane Surge (Elementalist: a 1st ability cast makes your weapon fire 25% faster for 3 s), Pack Bond (Summoner: minions take 25% more before breaking and hit 15% harder), Plate Armour (Knight: 8% less damage, swings cut bullets 20% farther out), Ghost Step (Ninja: +6% speed, 30% less damage for 1 s after a dodge), Bloodthirst (Hemomancer: abilities cost 20% less health, kills heal 2); weapon cards, only while you hold that weapon: Quake Hammer (Warhammer: slam 20% wider, stuns 0.3 s longer), Breacher (Lance: Breaching Thrust shoves 30% farther, Impact +50%), Long Haft (Halberd: 15% more reach, sweeps 20° wider), Iron Boss (Bulwark Barrier: Ram fills 30% faster, the Shield Bash and its shards hit 30% harder), Pilgrim's Staff (Quarterstaff: hits heal 1 more, one more Jab stack), Daybreak (Lightbringer: light fills 30% faster, Sunlance heals 30% more), Thick Incense (Censer: clouds 30% bigger, 2 s longer), Whirling Sabres (Twin Blades: thrown sabres follow the cursor 25% longer, hit 30% harder), Lunar Arc (Greatsword: the thrown sword flies 25% farther, hits 30% harder), Duelist's Edge (Rapier: skewers one more bullet, Tempo holds 0.5 s longer), Live Wire (Arc Knuckles: the Rocket Dash and Pull Punch reach 25% farther, the Pin Punch and Counter Hammer stun 0.3 s longer); for Ninjas: Heavy Weight (Kusarigama: weight flies 25% farther, stuns 0.4 s longer), Iron Bo (Bo Staff: shove knocks 40% farther, full spin +30%), Long Loop (Fuma Shuriken: loop 20% bigger, Orbit 20% wider), Potent Dose (Acupuncture Needles: needles heal 30% more, Bloodletting bleeds 30% harder), Gale Fan (Spirit Fan: gust reaches 25% farther, heals 30% more), Blessed Paper (Ofuda: talisman bursts and Blast Ofudas 25% wider, talismans heal 30% more), Barbed Stars (Shuriken: stuck stars return 1.5 s sooner, return hit +40%), Honed Edge (Katana: Momentum Burst shoves 30% farther, caltrops last 2 s longer), Dragon's Hunger (Dragon Fang Kunai: 4 more bites a throw, up to 12), Shadow Mark (Dragon Fang Kunai: the hit after a blink +40%, marks last 2 s longer), Iron Links (Nunchaku: Twin Windmill discs 25% wider, each swatted shot adds 2 more to the Twin Counter), Divine Wind (Twin Tessen: the Kamikaze Gust reaches 25% farther, it and the petals heal 50% more); for Hemomancers: Crimson Whip (Blood Lash: 20% more reach, one more link per enemy, each Clot stack cuts 1% more), Deep Pool (Gore Mortar: pool 30% bigger, 2 s longer), Hungry Tendrils (Leech Tendrils: charge 30% faster, bursts 15% harder), Generous Blood (Transfusion Darts: 30% more health given), Rich Blood (Sanguine Orb: reserve fills 30% faster, coat heals 30% more), Lifeline (Vein Tether: 55% of its damage heals), Hemophilia (Needle Swarm: burst one bleed stack sooner, 30% wider), Event Horizon (Implosion: crush +40%, pulls from 30% farther), Marrow (Blood Brawler: Marrow Burst +2 shards, Sympathy binds one more), Blood Harvest (Blood Blast: stacks heal 1 more each, an enemy holds 5 more), Burst Vessels (Embolus Spears: burst 30% wider, 30% harder); for Summoners: Granite Golem (Golem Staff: Golem takes 40% more, boulder 20% wider), Ossuary (Bone Guard: cages 0.5 s longer, skeletons shoot 30% faster), Deep Claws (Bear Totem: 15% more reach, bleed 30% stronger), Wildfire Seeds (Sprite Staff: one more firefly hop, sprites heal 20% more), Deep Roots (Treant Rod: root reaches 20% farther, heals 30% more), Alpha Pack (Wolf Whistle: takedowns 30% harder, stun 0.3 s longer), Will-o'-Wisp (Wisp Lantern: one more wisp), Imp Horde (Imp Caller: one more imp, 20% faster), Bigger Hive (Honeybee Staff: one more harvest bee, puddles 20% wider), Warding Ink (Spirit Chalk: blocks enemy bullets, but each one wears the ink away fast); for Engineers: Wide Field (Drag Pylon: field 25% wider, slows 10% more); for Soldiers: Point Blank (Scattergun: double damage reaches 30% farther, shove +30%), Wide Burst (Flak Cannon: bursts 25% wider, Proximity Shell 1 s sooner), Crowd Control (Riot Repeater: suppression 50% faster, Tear Gas 25% bigger), Heavy Drum (Grenade Launcher: +2 HE a drum, smoke clouds 20% bigger), Undertow Pump (Repulsion Cannon: Undertow fills 30% faster, its orbs heal 30% more), Hot Lead (Revolver: the hammer comes back 25% faster, and Fan the Hammer reloads faster; the shot hits 25% harder), Steady Draw (Mender's Bow: full draw 20% sooner, pierces one more), Kill Zone (Twin Rifle and Auto: crossfire zone 40% wider, 1.75x), Supercharge (Railgun: charged path 0.5 s longer, 40% hotter), Spin-Up (Burst SMG: spins up 30% sooner, winds down slower), Quick Loader (Double Barrel: reload 25% faster, full damage 30% farther), Gun Kata (Out of Ammo: rifle arms swat bullets 25% farther out, recover 30% faster); for Elementalists: Napalm (Flamethrower: reaches 15% farther, burns 40% hotter), Gale Force (Wind Clash: waves reach 20% farther, shove 30% harder), Glacier (Frost Lance: reaches 20% farther, freezes last 0.6 s longer), Stormcaller (Storm Hammer: arcs to one more enemy, 40% harder), Sunstone (Radiant Orb: 30% bigger, heals 30% more), Prism (Chain Light: cone 20% farther, 20° wider), Cloudburst (Nimbus: rain 30% wider, heals 30% more), Wildfire (Fireball: burns 50% hotter, 1 s longer), Static Field (Arc Lightning: tags 25% faster, stun 0.2 s longer), Frozen Halo (Ice Shards: 2 more in orbit, launch 25% farther out), Tectonic (Stonespear: one more spear a side, slam 20% wider), Bedrock (Stone Colossus: 3 s longer, slams 25% wider), Backdraft (Flash Fire, when it's your ultimate: 20% farther, what it leaves burning burns 50% hotter), Granite Knuckles (Rock Gloves, when it's your ultimate: punches 20% harder, 15% farther), Kami Vessel (Spirit Avatar: 2 s longer, the cleave winds up 0.2 s faster); plus Multishot, Barrage and Piercing Rounds, and the parry cards (see Parry) |
| **Very rare** | purple | A brand-new **2nd, 3rd or 4th ability** (you choose the slot; each one only once): a universal one (Shockwave, Overdrive, Blink, Starburst (a ring of 24 lilac shots) or Turret (a sentry pod drops at your feet, unfolds on three legs and fires twin cyan tracers for 10 s; ten segments round its feet count down and blink amber near the end, then it folds away)), or one of your **class's two other 1st abilities**, the ones you didn't pick in the lobby (these count triple, so about 6 in 11 very rare cards are class abilities) |
| **Legendary** | orange | Unique weapon upgrades, **only from bosses**: after a boss dies, each player picks one of that boss's three (can't be rerolled) |
| **Weapon mod** | teal | Also **after every boss**, right after its legendary: choose any mod from the full list (each stacks up to 3 times). In the Expanding arena the boss gives the **card pick** below instead, and mods are bought at the **Arms Dealer** |
| **Milestone** | gold | Their own pool, offered **every 10 levels** (levels 10, 20, 30...) as an **extra pick** on top of that level's normal one, before it. Three cards from: **Scholar** (+15% XP), **Second Helping** (+10% chance a level-up gives an extra pick), **Wide Choice** (+1 card in every level-up offer, max 2, so up to 5 cards), **Reroll Cache** (+3 rerolls now and +1 more after every boss), **Fortune** (rarer cards 8% more often), **Iron Constitution** (+25 max HP) and **Quickening** (-4% ability cooldowns), and in the Expanding arena only **Architect** (hold 40 more vectors and gain 20 now, stacks to 3). Each card stacks to a cap (5, Wide Choice 2, Reroll Cache 4), shown as circles on the card, and a maxed card leaves the pool. Milestone picks can't be rerolled. The very rare **Milestone Compass** (stacks to 3) is a normal card that gives every level-up a 5% chance per stack of also offering a milestone pick |

| Boss | Legendary weapon upgrades |
|---|---|
| The Warden | **Warden's Guard** (one of the Warden's guards walks at your side and fights enemies within 240 px of you with its baton, 14 damage every 0.7 s; it has 150 health + 15 every two waves, enemy shots and bodies hurt it, and a fallen guard is replaced 15 s later; +1 guard per stack) · **Stun Gun** (a new 2nd, 3rd or 4th ability, 14 s: draw a stun gun with 6 shots, 15 damage each, that stun enemies for 1.5 s (bosses are slowed 50% instead); your weapon is away until they're spent, and only then does the cooldown start; capstone: 9 shots) · **Searchlight** (a pool of light, 110 px, chases your cursor at 1100 px/s; enemies in it take 25% more damage from every player, and one held in it for 2 s is stunned for 1 s, bosses slowed 40% instead, then can't be locked again for 3 s; the 2nd stack widens it to 140 px, the 3rd makes it +40%) |
| Hive Queen | **Hive Drones** (two of her drones circle you and block bullets; after your kills one flies out to sting the nearest enemy 3 × 20 and comes home to heal you 4; every 3 s while you hit a boss; +1 drone a stack, and one more flies out each time) · **Royal Dive** (new ability, 12 s: her dive, untouchable, crashing at your cursor for 120 within 110 px with knockback, leaving a honey pool that slows enemies 40% and heals allies 6/s for 4 s) · **Bomber Escort** (every 5 s one of her bombers flies from you at the toughest enemy within 400 px and bursts for 55 within 70 px; +1 bomber a stack) |
| (no longer offered) | **Swarm Rounds**, **Royal Jelly** and **Brood Mother** (the Queen's old cards) and **Executioner**, **Twin Fang** and **Soul Harvest** (the Twin Reapers') are still in the game (old saves, dev tools) but no boss offers them |
| Twin Reapers | **Twin Scythes** (two of their scythes circle you in opposite directions, 60 and 100 px out, cutting what they pass for 16 and slicing small enemy bullets; every 1.6 s each reaps out at the nearest enemy within 240 px and flies back; 2nd stack every 1.1 s, 3rd a third scythe) · **Final Reap** (a hit that leaves a regular enemy below 25% reaps it and heals you 3; hits on a boss below 40% deal double; stacks: 30%, 35%; x2.5, x3) · **Scythe Throw** (new ability, 9 s: a giant scythe out to your cursor, up to 380 px, and back, cutting 45 each way and slicing small bullets; catch it to take 40% off the cooldown) |
| The Singularity | **Collapse Rounds** (shots implode on impact; the enemy hit takes half) · **Graviton Rounds** (hits slow enemies 40%; slowed enemies take 15% more damage from you) · **Dark Matter** (+1 pierce, +35% damage) |

**Callings** (Summoner): a Calling is a Rare card that commits your run to one minion. Take one and the other Callings are never offered again; that minion's own cards join your pool instead, and its Very Rare capstone once you hold both of them. All three are open to every Summoner; the one that suits your role turns up twice as often. If you reach level 5 without one, that level's offer always shows a Calling (you can still turn it down).

| Calling | The minion | Its cards | Capstone (Very Rare) |
|---|---|---|---|
| **Call the Spider** (Tank) | A spider fights at your side for the rest of the run, leaping on enemies within 260 px of you: bites for 14 every 0.8 s that web the enemy (40% slower for 1.5 s). It has 120 health + 12 a wave and stops enemy bullets with its body; when it falls it comes back after 8 s | **Silk Snare** (webs hold 1 s longer and slow 15% more; 2 stacks) · **Thick Carapace** (+50% health, back 2 s sooner; 2 stacks) | **Spider Queen**: every 6 s it lays 3 spiderlings that leap on an enemy, bite it once (half damage, webbing it) and are gone |
| **Call the Fairy** (Healer) | A fairy tends the most hurt ally (you when nobody is), healing them 4 a second within 120 px. Nothing can hit it | **Pixie Dust** (enemies within 90 px of the ally it tends burn for 10 a second; 2 stacks) · **Second Wind** (heals 30% more, and 50% more again an ally under 30% health; 2 stacks) | **Fairy Ring**: once every 45 s it flies to an ally who is down and stands them up at 40% health |
| **Call the Demon** (DPS) | A demon hovers at your shoulder hurling fireballs at the nearest enemy within 460 px: 20 every 1.2 s, bursting 46 px wide. Nothing can hit it | **Soul Feast** (each kill within 300 px of it makes it 5% stronger, up to 40%; the hunger fades 6 s after its last kill; 2 stacks) · **Hellfire** (fireballs leave the ground burning for 2 s; 2 stacks: a wider fire) | **Infernal Pact**: every 12 s it goes berserk for 4 s, throwing twice as fast with fireballs that burst 50% wider |

Calling minions hit harder with your damage upgrades and Pack Bond, and fight in frenzies (Alpha Call) like your other minions.

**Callings** (Soldier): the same rules, but each role has its own three, and each is a weapon that exists only for that Calling. One per role of each kind: **Replace** (a massive weapon takes your lobby weapon's place for the rest of the run), **Second slot** (you keep your weapon and **Swap weapon**, E by default, switches to the Calling's and back) and **Auto-fire** (a shoulder mount that works on its own beside your weapon).

| Calling | The weapon | Its cards (Rare, 2 stacks) | Capstone (Very Rare) |
|---|---|---|---|
| **Call the Siege Cannon** (Tank, replace) | Huge shells (55, every 1.25 s) that burst 84 px wide and knock enemies back; you move 12% slower | **Bigger Bore** (blasts 25% wider, 15% harder) · **Aftershock** (blasts stun 0.5 s) | **Bombardment**: every 3rd shell scatters 4 bomblets |
| **Call the Slug Breacher** (Tank, second slot) | One heavy slug (38, every 0.85 s) through 3 enemies, stunning 0.35 s and shoving them | **Door Kicker** (+1 enemy, 20% harder) · **Concussive** (0.4 s more stun) | **Breach and Clear**: swapping to it loads 3 slugs that explode where they stop |
| **Call the Flak Mount** (Tank, auto-fire) | A shoulder flak gun shoots down the nearest enemy bullet within 170 px every 0.6 s | **Quick Traverse** (30% faster) · **Wide Arc** (60 px further) | **Return Fire**: each bullet shot down sends a flak round (18) at the nearest enemy |
| **Call the Defib Cannon** (Healer, replace) | Shocks (26, every 0.9 s) through 2 enemies that stun them 0.6 s and heal allies they pass 10; every 5th is a full charge: twice as hard, a 1.2 s stun, and it stands up a downed ally it touches at 30% health | **Overvolt** (25% more damage and healing) · **Quick Paddles** (a full charge 1 shock sooner) | **Clear!**: a full charge arcs to 3 more enemies and heals every ally within 140 px of what it hits for 20 |
| **Call the Nano Sprayer** (Healer, second slot) | A 170 px mist cone: allies in it heal about 13 a second, enemies take about 18 | **Dense Mist** (30% further) · **Nanite Boost** (heals and corrodes 30% more) | **Gray Goo**: enemies that die in the mist burst, healing allies within 90 px for 15 |
| **Call the Lifeline Tether** (Healer, auto-fire) | A beam from your shoulder heals the most hurt ally within 320 px 6 a second (you, half that, when nobody is hurt) | **Strong Link** (40% more) · **Long Line** (120 px further) | **Second Chance**: the tether reaches for a downed ally first and stands them up after 3 s on the beam at 30% health; once every 30 s |
| **Call the Railspike** (DPS, replace) | Hold to charge (1.1 s): a spike (50 to 180) through everything across the arena; a full charge pins what it passes 0.5 s | **Capacitors** (charges 25% faster) · **Tungsten Core** (25% harder) | **Skewer**: a full charge pins for 1 s and blasts each enemy it passes for 40% of the hit |
| **Call the Rocket Pod** (DPS, second slot) | A fanned salvo of 6 homing rockets (14 each, 38 px bursts) every 1.8 s | **Extra Tubes** (+2 rockets) · **Warheads** (blasts 30% wider, 15% harder) | **Cluster Payload**: each rocket bursts into 2 bomblets |
| **Call the Shoulder Minigun** (DPS, auto-fire) | Spins up (0.6 s) when an enemy is within 420 px and sprays the nearest one: 4 a round, 11 rounds a second | **Belt Feed** (25% faster) · **Tracer Rounds** (25% harder, 80 px further) | **Bullet Storm**: after 3 s of non-stop fire it fires two streams |

**Weapon mods** change how your shots behave, and you can see them on your bullets:

| Mod | Per stack |
|---|---|
| **Homing Rounds** | Shots bend toward an enemy within 260 px and about 45 degrees of their path, so near misses connect (they don't turn around to hunt). Costs 5% damage per stack; more stacks bend harder. **Area weapons** (Implosion, Flak Cannon, Ofuda, Storm Hammer, Gore Mortar, Vein Tether, Frost Lance, Treant Rod, Nimbus, Spirit Chalk, Golem Staff, Arc Lightning) get a bigger area instead of bending: +25% per stack to the blast, burst, splash and pool, collapse, beam width, lightning's reach, sweeps, lash, lightning and rain, ink line, boulder or Arc Lightning's tagging cone (wider, up to 137° across), for the same damage cost. **Cone weapons** (Chain Light, Leech Tendrils, Repulsion Cannon) condense instead: the cone, arc or blast is 15% narrower and hits 10% harder per stack (the Repulsion Cannon also pushes 10% harder), with no damage cost, so it's a choice between covering a crowd and focusing a target. **Wisp Lantern and Imp Caller:** the wisps and imps you send pick an enemy 50% farther from your cursor per stack, and one sent at empty ground (or whose target dies) turns onto an enemy within 260 px of it, for the same damage cost. **Melee weapons** reach 10% farther per stack instead (swings and sword waves; the waves still bend), for near misses the same way; the Heart Flail and Twin Tessen, whose hits land in an area, get the bigger area |
| **Ricochet** | After a hit, the shot bounces to another enemy within 300 px for 40% damage; +1 bounce per stack. Hits that can't bounce (melee, beams, cones, bursts, sent wisps and imps, and shots that pierce: drawn arrows, Fireball, flames, Fuma Shuriken) store their damage instead, and up to 4 times a second a **bounce bolt** carries 40% of it from the enemy hit to the nearest other one (+1 bounce per extra stack) |
| **Incendiary Rounds** | Hits set enemies on fire: each burns for another 40% of the hit's damage over 3 s (per stack), and burns from every hit add up, so fast and slow weapons gain about the same |
| **Cryo Rounds** | Hits slow enemies 20% (up to 60%) with a 6% chance to freeze non-bosses for 1 s |
| **Shock Rounds** | 25% chance per hit for lightning to arc to 2 more enemies at 60% damage |
| **Explosive Rounds** | Shots explode on impact for 30% damage around them (bigger blast per stack). On a fast weapon the blasts on one spot are shown at most every 0.13 s; every hit's damage still lands |
| **Velocity Rounds** | Shots fly 30% faster and farther, +25% damage |

Mods apply to your weapon's shots and your minions' shots, but not to grenades. They also ride every hit of
weapons that don't fire bullets (melee swings, beams, cones, the Repulsion Cannon's blast, Blast Ofudas,
sent wisps and imps). On a Knight, the elemental mods apply to every melee hit (see The Knight above).
Each card says what it does for **your** weapon (its kind: shots, area, cone or melee, or the weapon's own text), and a
mod that would do nothing for your weapon is never offered.

**Piercing Rounds** let shots pass through two more enemies per stack. Weapons without bullets that run out of pierce
(melee, the Flamethrower, Fuma Shuriken, Repulsion Cannon, Radiant Orb, Chain Light, Spirit Fan, Ofuda,
Arc Lightning, Wisp Lantern, Imp Caller, and the Fireball, which already rolls through everything) reach 15% farther
per stack instead.
A piercing shot can't hit the same enemy twice in a row, even inside two overlapping enemies.

**Slowed and Chilled** are two statuses. **Slowed** is any slow (wind, honey, gas, ink, Graviton, ice...): it only slows
and never builds to a freeze. **Chilled** is ice only: every ice attack (Frost Lance, Frost Nova, Blizzard, Cryo Rounds,
Ice Shards at 25% for 1.5 s, Wind Chill) slows the enemy *and* adds a chill (steady ice: Blizzard, Cryo Rounds and Ice
Shards add one a second at most). Chill wears off 3 s after the last one; the third freezes it (2.4 s, heavies 1.2 s),
and the freeze lifts its slows, since the ice holds it still. Bosses are only slowed. Frozen means real ice only (can't
move or shoot); Frost Nova's and Cryo's deep 0.1x slows show as chilled. **Slows** don't add up: an enemy moves at its
strongest slow until that one ends, then at the next-strongest one still running. A deep slow (Frost Nova, Cryo's
"frozen solid") ends on time even while a weaker slow (Wet, ink, a slow zone) keeps being renewed.

**Virulence** (rare, two stacks, with Poison Dart; it took Phase Step's place with the old Dash): the venom deals 30% more and spreads 60 px further.

Rarer cards show up a bit more often as you level. All the numbers live in
`Assets/Scripts/Core/Upgrades.cs` if you want to rebalance or add upgrades.

## Rules

- **Timed waves:** a regular wave's enemies arrive over about 45 s instead of in one burst: a ramp-up (first 15%), three **surges** (at 20%, 48% and 76% of the window, about 7 s each) with quieter lulls between them (40% of the surge rate), then a taper as the wave dies down, and the wave ends when the last one is dead (the clear-up brings it to about a minute). Spawning waits while 14 + 3 every two waves (at most 60) foes are alive. Boss, Zombie Infection and room-by-room waves keep their own pacing. Enemies pick the nearest breach (or the nearest valid spot in the Expanding arena's rooms, of four) to a player that is at least 350 px away, so big maps don't feel empty.
- **Spawn portals:** on a timed wave the single enemies come in **bursts** (3 on wave 1, one more every 8 waves, up to 8) through a **spawn portal**: a violet digital gate whose segmented ring fills like a loading bar while it opens (1 s) where the burst comes in (an edge, a layout's enemy gate, a spot in the Expanding arena's rooms, never within 160 px of a player, and always with its whole ring clear of walls and pillars), then lets one enemy out every 0.35 s, with chevrons pointing the way they'll come. Shoot it closed (200 health, scaling like an enemy's) before it empties and it blows apart in a white flash: **the rest of its burst is cancelled**: +5 score each, plus 2 gold each as coins in the Expanding arena. An empty portal shrinks shut with no reward. Up to 2 portals are open at once (3 in a surge, when their rings chase faster and more bits pour in); with none open for 4 s a smaller burst comes anyway. Squads, Nests and breach waves keep their own way in.
- **Recycling:** in the Expanding arena and Survival, a regular enemy that stays more than 1800 px from every standing player for 4 s comes back in at a fresh spawn spot near the party (the usual spawn-in, 0.6 s), so stragglers left behind on a big map keep the pressure on. Bosses, mini-bosses, portals, Nests, a duel room's enemies and Bound Wraiths stay where they are.
- **Flank portals:** from wave 5, one burst in 4 (never a wave's first) opens **behind the party**, opposite the way the players are aiming: a red portal that takes 1.6 s to open, and a red arrow round your player points at it.
- The arena is 1600x900 and **wraps around for players**: walk off any edge and you come in on the opposite side.
- **Wall slams:** an enemy knocked back into the arena edge stops there and takes damage: 6 plus 0.12 per px of push the wall cut short, up to 40 (pushes that lose under 8 px do nothing). Bosses and shielded enemies still use their normal damage rules.
  Enemies and bullets don't wrap, so the edges make a good escape route.
- **Melee grace:** when a melee swing hits something, bodies you're touching can't hurt you for 0.3 s (contact damage only, not bullets or boss attacks). It keeps a melee build from being punished for standing close to land its hits.
- Your hitbox is the small coloured dot ringed in white in the middle of your character, not the whole circle.
- **Status effects** show as a tint on the enemy plus an icon above it: **white snowflake in a block of ice** = frozen (encased in ice, can't
  move or shoot), **blue snowflake** = chilled (ice; its stacks are pips under it), **grey-white double arrow** = slowed (anything), **blue drop over a wave** = wet (a blue bar
  over its health bar drains as it dries), **flame** = burning (flames rise off it), **blood drop** = bleeding
  (drops fall off it), **red crosshair** = marked (takes extra damage), **yellow zigzag bolt** = charged (small arcs
  crawl over it), **cracked stone** = brittle (amber-seamed cracks across it), **small sun** = dazzled (gold glints
  wheel round its head).
- **Brittle (earth):** an earth hit (Stonespear's spears, the Earthball and every enemy in its rubble, Meteor, Stone
  Colossus's rise, stomps, slams and boulders) cracks what it hits for 5 s (refreshed, never stacked; enemies only): it
  takes 20% more damage (bosses 10%). Meteor cracks first, so its own blast lands 20% harder. **Shatter (Earth + Ice):**
  an enemy brittle and frozen at once (a third chill, Frost Nova, Glacial Tomb, Blood Freeze...) shatters: the ice and the
  cracks break, it takes 60 (+3 a wave) and every enemy within 90 px takes half that, credited to whoever cracked it.
- **Dazzled (light):** a light hit (the Radiant Orb, Chain Light's cone, standing in a Lumen Ward's light and 1 s after)
  dazzles what it hits for 3 s (enemies only): its shots stray up to 25° either way (bosses 12°).
- **Charged (lightning):** any lightning hit (Arc Lightning, Chain Light, Storm Hammer pulses, Lightning Rod, the Nimbus,
  Static Shock's circuit and the Arc Cannon, the Arc Knuckles' punches, Rocket Dash and throw, the EMP and Shock Grenades) charges what it hits for 4 s (refreshed, never stacked; enemies only).
  Charged alone doesn't conduct: only wet enemies take arcs from other lightning hits (a wet, charged one still does; the
  arcs don't charge it). **Wet and charged** it shorts out (its arcs turn blue-white and flicker): it takes 6 damage a second
  (+0.3 a wave), and every 2.5 s an arc jumps from it to the 3 nearest enemies within 220 px for 25 (+1.25 a wave),
  credited to whoever charged it.
- **Steam (Fire + Water):** an enemy that is wet and burning at once (any fire: the Flamethrower, Fireball, Flame Wall and
  every burning ground (it keeps burning 2 s after it leaves), Flash Fire, Spirit Chalk, Warhorn Volley, Incendiary Rounds...) flashes the water to steam: the wet and the burn are both used up, and a steam
  cloud (90 px, 3 s) billows up where it stands, with a hiss. Enemies inside take 8 damage a second (+0.4 a wave) and
  their weapons are jammed (no attacking, like the Grenade Launcher's smoke; bosses and mini-bosses only take the scald),
  credited to whoever lit the fire. Steam near an existing cloud refreshes it instead of stacking, and an enemy can't steam
  again for 1 s. White wisps keep rising through it over a faint warm glow.
- **Meltwater (Ice → Water):** when a freeze ends (Glacial Tomb, a Frost Lance freeze, Blood Freeze, a shatter...; not a
  stun), the melted ice leaves the enemy **wet** for 6 s. While it's frozen, fire just burns as usual; if it's still
  burning when the ice melts, the water and fire turn to steam.
- **Wind reactions:** wind has no status of its own; it reacts with the others. Every wind source counts: Wind Clash's
  waves and Tempest, Gale Wall's blast, Gust Gate (the cast and its steady push), Vortex's swirl, the Spirit Fan's gust, the Wyvern's gale
  and the Nimbus's hurricane burst. An enemy reacts to wind at most once a second.
  - **Wind Chill (Wind + Water):** wind on a **wet** enemy chills it like a Frost Lance strike (+1 chill, slowed 30% for
    3 s); the third chill freezes it (2.4 s, heavies 1.2 s; bosses only get the slow). After a wind freeze, wind can't chill
    that enemy for 6 s, so a steady wind can't keep it frozen through its meltwater.
  - **Fan the Flames (Wind + Fire):** wind on a **burning** enemy spreads its fire, at 60% strength and for the time it has
    left, to the 3 nearest enemies within 120 px downwind (the side the wind blows toward), credited to whoever lit it.
- **Cauterize (Bleed + Burn):** fire landing on a bleeding enemy (the Flamethrower, the Fireball, Incendiary Rounds)
  sears the wound shut: all the bleed it had left lands at once, times 1.25, and the bleed ends. Whoever made it bleed
  is credited the bleed's share, whoever brought the fire the extra quarter, so fire never wastes an ally's bleed. A
  white-hot flash, a searing ring and a hiss of steam; an enemy can't be cauterized again for 1 s.
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
  like wave 19 of the old run. **Wave numbers in this README are the Campaign's** (1-100). Quick Play, Boss Hunts and the
  Boss Lab have no legs: their wave n plays like Campaign waves 2n-1 and 2n (Quick Play's wave 10
  is the Campaign's 19-20), so halve a number here for them. XP, kill gold, chests and the Expanding arena's vectors per wave are halved to match, so you reach each
  boss about as strong as before. Runs saved before this stay 50 waves; Boss Hunts stay ten.
- **A new enemy trickles in, and the last boss's fades out.** In a lesson that introduces an enemy (the Warden's and the Hive Queen's waves 2, 3, 4, 6, 7 and 8: Guards, Thugs, Security Guards, Riot Shields, Gas Grenadiers, Taser Guards, or their Hive counterparts), it arrives alone, after the wave's squads. After a boss, his faction fades over the next three waves (40%, 20%, 8% of the wave, singles only, no squads) with the tricky ones going first (Riot Shields, Gas Grenadiers and Taser Guards at once, the Security Guard after a wave, Thugs and Shivs (Bumbles and Hornets) after two, Rioters and Guards last), while the next faction's enemies come in at once. Later on, the **comeback squad** (two waves in every ten from wave 21) is big: an old boss's squad twice over, plus that faction's toughest enemy as an anchor, so a late Breakout is a crowd of Rioters. The Warden's first round is Rioters only (no Guards, so no Breakout either). Waves 1-2 have more enemies (12) and spawn faster, over a shorter window (24 s, the full 45 s by wave 11), so there's less waiting for the last few.
- **Each boss's ten Campaign waves teach its fight, in the order of the chart in `Docs/Approaches.md`.** The Warden's (waves 1-10): 1 Rioters only; 2 Guards join (Rioter packs);
  3 Thugs (Rioter packs, Guard pairs); 4 Security Guards' aimed tracers (Yard, Guard pairs); 5 a recap; 6 Riot Shields (flank them); 7 Gas Grenadiers (Yard, Cell block);
  8 Taser Guards (stuns; parry the Fork Bolt back); 9 every squad, tighter; 10 the exam, the Riot line and a big Breakout. Each wave brings one new enemy, alone and last, and
  its squads only hold enemies you have already met. The Hive Queen's (waves 11-20) follow the same ten steps with her own: Workers, Soldiers (12), Bumbles (13), Needlers (14), a recap (15),
  Comb Shields (16), Honey Lobbers (17), Stingers (18), every squad (19), the exam: the Royal guard and a big Swarm (20). The Singularity's (waves 31-40): Debris (31), Void Acolytes' bent orbs (32), Lenses (33), Gravitons' wells (34), Pulsars (35), a recap with Collapsars (36), Orbiters (37), White Dwarfs (38), every squad (39), the exam: the Accretion ring with everything (40). Every other boss's ten waves follow the same steps with its own faction (see each under Enemies).
- New enemy types unlock as the waves go up (see below).
- **Knockback slides:** anything that shoves enemies (the Repulsion Cannon, Roar, Shockwave, the Scattergun up close,
  Knight swings, Bulldoze, Fortress, the Fuma Shuriken's fan) slides them the distance over 0.18 s instead of jumping
  them there, with a streak behind them, dust kicked up along the way and a puff where they were hit.
- **Melee hits:** every melee hit (every swing, Out of Ammo's bashes and knife) dazes and shoves the same: 18 px back
  (heavies aren't moved). Finishers and special moves with their own push keep it (the Quarterstaff swipe, the Greatsword's
  Undertow, Comet Leap and Eclipse, the Lance, the rifle clap, Alts and abilities).
- **Daze:** a close-in hit (every melee hit, Bulldoze, the Katana's Momentum Burst, the Bo Staff's shove, the Kusarigama's
  whirl) dazes the enemy for 0.4 s (bosses 0.2 s): it still moves, but can't shoot, use its special attacks or hurt you by
  touch, and a wind-up it was in starts over. Three gold stars wheel over its head. After a daze it can't be dazed again
  for 0.8 s, so steady swings can't lock it down. Bulldoze also keeps you invulnerable 0.35 s past the end of the charge.
- **Boss bodies:** touching a boss or mini-boss doesn't hurt, so melee can stand close; only a telegraphed charge or lunge hurts on contact (the Warden's baton charge, the Colossus's Charge, the Duelist's dashes, the Devourer's lunges, the Gatekeeper's charge: 25, plus the difficulty's 3% every two waves, at most once a second). A boss still shoves you about 100 px back
  out; it can't hurt you again for 1 s.
- **Bullet budget:** enemy bullets on screen are capped at 40 + 3 every two waves, up to 85 from wave 29 (20% more per extra
  player; times the difficulty's Bullet budget). When the screen is over the cap, regular enemies hold their fire and
  try again a moment later. Bosses always fire, and while a boss is up the other enemies get 60% of the cap (Normal).
  Later waves get harder through tougher enemies, not more bullets.
- **Taking turns:** only so many regular shooters fire at once: 4, one more every 12 waves up to 8 (wave 48), a quarter
  more for each extra player (times the difficulty's **Shooters at once**). Each gets a 5 s turn, then rests 1.5 s
  while whoever has waited longest takes over; the rest keep moving and hold fire. Bosses don't take turns.
- **Reading the shots:** a regular shooter glows (a white core, a ring closing in) for 0.3 s before each volley. Enemy
  bullets show what they'll do: a shot aimed at a player is a **dart** pointing where it flies, a curving shot is a
  **crescent**, and rings and sprays (6 or more shots at once) are **round**. Rings are sparse and slow enough to slip
  through: a Shade's 8, Mortar bombs 7 (10 elite) at 125 px/s, Volatile 12.

## Enemies

**The basic faction.** Outside the boss factions, the waves use eight plain enemies, one per role: **Swarmer** (rusher, in
fours), **Brute** (tank), **Lunger** (charger), **Grunt** (gunner), **Sniper**, **Spinner** (spray), **Mortar** (artillery)
and **Shielder** (guard). They fill the Campaign's mix around the factions, and in Quick Play and Boss Hunt they stand in as
the faction of any boss that has no faction yet (squads Rush, Firing line, Brute pack, Shield wall and Barrage, and the
**Last stand** set piece before the boss). These eight are the only plain enemies: the old ones no faction used were removed
(Nov 18).

**Job markers.** A small bar and shape under each regular enemy shows its job: orange dot = Pressure (shoots), red chevron = Hunter (runs at you), yellow ring = Zoner (takes ground), violet diamond = Disruptor (breaks your position), cyan square = Anchor (kill it first). Bosses get none.

The Lunger (and its kin: Shiv, Scythe Wraith) and the Riot Shield warn of a lunge the same way: a red strip on the floor that fills up over the wind-up and flashes white just before they go.

A Shade shows a ring where it is about to blink (half a second before it does, and before its ring of shots), and a Mortar's bomb shows a ring where it will land.

**New enemy chips.** Between waves, under the results box, a small card for each enemy the next wave brings that you haven't met yet: its name, its job and one line on what to do about it (at most three; none before boss waves).

**Enemy names on hover** (Settings → Gameplay, off by default): point at any enemy to see its name, and its job for the regular ones.

**Name tags.** The first three times you see each kind of enemy it carries its name and job for a couple of seconds (one at a time per kind, so a squad of three costs one showing). Tags never overlap: one that would sits just above the other, and a tag over the bottom HUD fades out. Your profile remembers which ones, so you are only taught once.

Shooters that fire straight at you (Sniper, Shard, Shade, Grunt, Taser Guard, Security Guard) share one **shooting line**: for the instant before they fire, a slim lane in their colour fills from the muzzle along the shot, brighter at its head, and caps off when the shot is due; the Grunt shows all three of its shots.

From wave 25, a regular wave shows at most 6 different enemy types (8 from wave 49), so each kind is easier to learn; in the Campaign the boss factions' enemies count too (they keep room for the wave's headliners). Boss waves, paths with their own mix and Zombie Infection hordes are unchanged.

The first waves are **melee rushers** (spiky, they never shoot, they hurt on contact). Shooters take over a
little more each wave: waves 1-2 are all melee, wave 13 about half.

**From wave 25, every enemy has a job** (design: [`Docs/EnemyRoles.md`](Docs/EnemyRoles.md)), and a wave is built to a
mix of jobs instead of an even draw from every type seen so far: about 45% **Pressure** (they shoot: weave and use
cover), 25% **Hunters** (they come for you: Swarmers, Brutes, Lungers), 12% **Zoners** (they take ground), 8%
**Disruptors** and 10% **Anchors** (kill them first). The plain enemies are all Pressure or Hunters, so the other jobs'
shares pass on (Disruptor to Hunter, the rest to Pressure); the boss factions bring their own Zoners, Disruptors and
Anchors. Each wave has **2 headliners** (3 from wave 49), favouring newer types, that make up a good part of their jobs, so
waves feel different from each other. Types fade out once they've been around for 20 waves (down to 15% as often by 40
waves in), and so do old squads, so the wave-1 Grunt gives way to what came after it. Squads are half of a late wave, and
the single enemies fill whatever each job is still short of. `Tools/SimTest/run.sh 1 1 wavemix` shows the mix track wave by track wave
(waves 27-58 went from 81-91% shooters to about 55%).

With **Visual effects** on, a melee weapon drawn in its own hands (Halberd, Warhammer, Greatsword, Rapier, Quarterstaff, Censer, Twin Blades, Lightbringer, Lance, Bo Staff, Katana, Kusarigama, Blood Lash) is the only pair of arms you see: the character's own arms are left out (and its shoulder plates too, for weapons whose arms have their own pauldrons). The Kusarigama keeps the sickle in one hand and the weight hanging from the other; the Blood Lash's whip hangs slack and sways until it cracks; its swing bends like a real whip, the tip trailing the hand, then cracks with a white flash, flinging blood and leaving a crimson streak behind it (the Snap shoots straight out with a wave running down it). Every swing starts from the held pose and comes back into it: after the cut the weapon holds a moment, then turns back to where you are aiming and settles into the held pose, with no fading in or out.

With **Visual effects** on, regular enemies stand out from the floor: a soft dark shadow under each body and a faint glow in their own colour, much quieter than a player's halo. Bosses, spawning enemies and zombies don't get it.

With **Visual effects** on, anything that moves fast leaves an **air trail**: two thin streamlines off its shoulders that wobble and fade. It follows speed: none at or below 110 px/s (Grunts, Brutes, bosses), light for mid speeds (a Tank or Healer walking is about 10-25%), and full at 330 px/s and up (Hive Bombers, dashes, hard knockback). Players, enemies and minions all get it. Not sent over the network.

**CRT colour:** the CRT screens (menus, profile, run summary) are always dark blue; there's no setting for it. The lobby and Game Mode screen are grey in the role's and mode's colours instead. The role, class and rarity colours keep their own meaning.

With **Visual effects** on, the Ninja's **Shadow Step** is smoke: you burst into puffs that split into two halves, each following a pale eye round one side of the enemy, and meet again behind it to re-form as you strike (you aren't drawn for the first third of a second; the step and its damage are instant as before). Off, it's the plain ring and slash. The two ends of the step are sent over the network (`FxKind.ShadowStep`).

**Squads.** About 70% of a regular wave arrives as small teams that come in together from one edge and look after each
other; the rest are single enemies. Each squad unlocks at a wave, and none comes more than twice in one wave: Rush (1:
five Swarmers), Firing line (5: three Grunts, a Sniper behind), Brute pack (9: the Lunger waits for a Brute to reach
you) and Shield wall (17: a Shielder leading two Snipers that keep behind it); the boss factions add their own. Early
waves keep their share of rushers (a melee squad as often as a melee enemy used to come). Paths with their own mix keep it.

| Wave | Melee enemy | What it does |
|---|---|---|
| 1 | **Swarmer** | The simplest enemy: a small white chomper with a wedge for a mouth and no eyes. Walks straight at you (115), 14 health, 6 per bite. Comes in packs of four, so cut them down together |
| 5 | **Brute** | Slow and very tough (200 health); hits hard (24) when it reaches you. **Enrages as it is shot**: the more health it has lost, the faster it walks (up to +80%) and the harder it hits (up to +60%), and it turns redder. Healing calms it down. Burst it down or back off |
| 9 | **Lunger** | Circles in close, flashes a pink line, then leaps along it. Step aside |


| Wave | Shooter | What it does |
|---|---|---|
| 1 | Grunt | Aimed 3-shot spreads |
| 5 | Sniper | Fast, accurate shots from far away |
| 7 | Spinner | Wanders and sprays spirals in **bursts** (about 1.3 s on, 2 s off); it glows and spins up while bursting |
| 15 | **Mortar** | Lobs bombs at where you're standing; they burst into a ring when they land. Keep moving |
| 17 | **Shielder** | A shield on its front blocks your shots. It turns slowly, so flank it. The shield (60% of its health) wears down as it blocks and crumbles on its own (about 33 s), cracking as it goes, then shatters |

| Wave | Any enemy | What it does |
|---|---|---|
| 3-11 | **Hasted mutation** | A regular enemy can carry the Hasted trait (its marker, +40% speed and fire rate) before elites begin: 3% a wave from wave 3, up to 20%; never bosses or spawned children (`Mutations` in `Variety.cs`) |
| 12+ | **Elites** | Any enemy can spawn with a gold ring: 2.5x health, bigger, worth 3x XP, always drops an orb, and a **trait** shown as a marker over it (two traits from wave 35): **Hasted** (40% faster moving and firing), **Shielded** (a bubble worth 40% of its health that grows back 4 s after it was last hit), **Volatile** (bursts into a ring of 12 shots 0.6 s after it dies), **Splitting** (dies into two Shards, four from a Brute), **Mirrored** (one in four of your shots that hit it bounce back at you), **Commander** (allies within 220 px fire 30% faster). More common each wave |

### Campaign buildup

Every regular wave is made of boss-themed enemies (design: [`Docs/Campaign.md`](Docs/Campaign.md)). The last boss's
faction **fades out** over the next waves (in the 100-wave Campaign: three, 40%, 20%, 8%, see Rules), bosses from two fights back stay on as an
**older pool** (10% from wave 21), and the next boss's faction takes the rest, building up as before (65% to 100%;
55% to 90% once the pool exists). Every boss has a faction now. From wave 21, two waves in every ten bring a **comeback squad**: a squad from an earlier boss's faction arriving in place of that wave's older-pool singles. Turn it off with **Campaign buildup** in the Difficulty tab to get the
Boss approach below. Boss Hunts are unchanged.

### Boss approaches

Only with **Campaign buildup** off (above), and in runs saved before the 100-wave Campaign (there, halve the wave numbers
here). The eight waves before each boss are its **approach**, in four steps of two waves: part of each wave is the boss's own kind of enemy, so you meet
its world on the way to it (design: [`Docs/Approaches.md`](Docs/Approaches.md)). It's always **Blend** (no lobby picker since Nov 18):
it builds up over the four steps, 34%, 51%, 68% then 85% of each wave. The rest of each wave is the usual mix,
paths still apply (an enemy-mix path builds from what's left; Elite Gauntlet makes the faction elite too), and the
boss fight itself is the same. A line at the top of the screen says "The Warden  in 2 waves", and the banner before
the first and last waves says you're on the road to the boss, then that it's near. The **Warden**'s faction (waves 1-8), and the last two waves before him always brings the **Riot Line**: four Riot Shields marching at you as a wall, five Guards close behind it, who break out and rush you once the wall gets within about 190 px or half its shields are down. The **Hive Queen** has one too (waves 11-18): eight bees, one for each of the Warden's roles (below), with the **Royal guard** (four Comb Shields, five Soldiers) on the last two waves. The **Singularity** has one too (waves 31-38): the **Infall**, eight enemies with four squads (below), and on the last two waves the **Accretion ring**: eight Debris and four Orbiters in a ring round you, 320 px out, with **two gaps**. It turns slowly and closes in to 110 px over about 10 s while the Orbiters sling their orbs inward: walk out through a gap. A faint violet **band** runs along the ring, with the two gaps left dark, edged in green and marked with chevrons pointing out. Bots walk out through a gap, keep clear of a White Dwarf's flash, Small Wells and Implosions. It breaks up after 12 s or once half of it is down. The **Lich** has one too (waves 61-68): eight, all seven of Zombie Infection's zombies and his own **Grave Caller**.
Shamblers first, then Runners (in packs of three) and Spitters from wave 63, Crawlers, Bloaters and Shriekers from 65,
Menders and Grave Callers from 67 (at most two Shriekers and two Grave Callers a wave), with six squads (Graveyard shift,
Grave rush, Gravediggers: two Crawlers in a crowd, Plague cart: a Bloater with Spitters, Wake: a Shrieker behind a Runner
pack, Wake the dead: a Grave Caller with Spitters) and a last-wave **Dead march** (six Shamblers, two Menders, three
Runners, a Shrieker and two Grave Callers). The **Grave Caller** (60 health, a hooded cross in the Lich's soul cyan)
keeps about 320 px back; every 7 s it raises its hands and **three graves** crack open in a ring 120 px round you, and
1.6 s later a **Shambler** claws out of each, at half health, worth no XP, its eyes cyan and a faint thread back to its
Caller. At most four of its Shamblers are up at once; **kill the Caller and they crumble** (graves still opening close).
Step out of the ring, then dig through to it. In the Campaign the Lich's ten waves teach one zombie each: Shamblers,
Runners, Spitters, Crawlers, Bloaters, a recap with Shriekers, Menders, Grave Callers, every squad, then the exam with
the Dead march. Zombies don't get back up outside the modifier, only out of a Caller's graves. His fight itself is unchanged.

Guards **rally** the Rioters: a Rioter within 150 px of a Guard runs 25% faster (it doesn't stack), so kill the Guards first to slow the crowd; an amber ring marks the rallied. From wave 2, about 3 in 10 Rioters carry a **bottle**: from 110-300 px away they slow and cock an arm for 0.6 s while a dashed green line marks the throw, then throw it at you (10 damage, flies about 330 px). A bottle that hits also **cuts**: you bleed 4 a second for 3 s (refreshed by another cut; like gas it goes through Riot Gear). Parry it and it just **smashes** on the shield, never sent back. Each has only the one bottle. Rioters **yell** like a mob (each shout two or three voices at once): when one first runs within earshot, when it hits you, as it cocks a bottle and when it dies; the bottle whooshes as it tumbles and shatters where it lands, and you can hear their running feet. Rioters also **crowd** you: each one within a few pixels of you slows your walk by 4%, compounding (about 12% with three on you, 22% with six, 34% with ten, never more than half), so a pile pins you down.

With Visual effects on, the Warden's faction glows: Rioters, Shivs, the Turnkey and the five officers get a breathing halo in their own colour; a Thug's is a faint red that grows bigger, brighter and faster as it enrages (as it loses health); the officers also have lit kit (the Guard's baton tip and cap badge, the Riot Shield's face, the Gas Grenadier's green mask lenses and canister, the Taser Guard's crackling prongs, the Security Guard's pistol light) and a blinking red radio light on the shoulder.

The Warden's faction pays XP like regular enemies of their health (Rioter 7, Guard 15, Riot Shield 10, Gas Grenadier 9, Taser Guard 11, Security Guard 9, Turnkey 16),
so its tougher approach waves don't leave you levels behind at his wave (about level 4 at Blend). The Rioters and Guards of
his own riot, on the boss wave, still give 2 and 4.

| From | The Warden's faction | What it does |
|---|---|---|
| 1 | **Rioter** | A prisoner on the loose in an orange jumpsuit: a Brawler (broad shoulders, cuffed fists throwing punches) that weaves at you, in pairs (contact 15). **Variant, Ball and Chain** (30% of Rioters, from wave 3): hunched, dragging an iron ball on its ankle, the same moves but **25% slower**. Squad: Breakout (six Rioters and a Guard) |
| 1 | **Guard** | A corrections officer (khaki shirt, peaked cap, gold badge) with a side-handle nightstick, straight at you (contact 14). As it closes in (from about 190 px) its whole swing arc fades in on the floor ahead of it: a dashed amber rim at its 80 px reach, the arc's edges and a curved arrow showing which way the next swing sweeps, while it brings the stick up on that side; with you inside the reach it pulses. Within about 60 px it swings the stick without stopping, forehand then backhand: a 0.16 s draw-back flashes its 120° arc amber on the floor, then the swing hits everyone in it out to 80 px for 7; one every 0.6 s |
| 3 | **Thug** | A hunched prisoner brute in a tank top, bare tattooed arms and giant taped fists: moves like a Brute (175 health, contact 25). Within about 35 px it **plants its feet and charges a punch** for 0.6 s: a narrow amber wedge fills on the floor ahead of it while its taped fist glows and draws back, then it jabs everyone in the wedge (out to 64 px) for **18**. One every 1.4 s; it can't turn while charging, so step to the side. Squad: Yard (a Thug, three Rioters and a Shiv) |
| 5 | **Shiv** | A prisoner with a taped shank, one of two kinds: Hooded (hood up, the shank held low) or Twin Shanks (shirtless, a shank in each hand); it draws the shank back as it winds up. Quick on its feet (115 speed). Circles in close, shows the lunge lane, then lunges like a Lunger (contact 18, 26 on the lunge); a hit also cuts: you bleed 4 a second for 3 s |
| 3 | **Riot Shield** | A prison officer in steel-blue riot fatigues and a visored riot helmet, behind a tall shield that stops shots and swings from the front. Up close the shield flashes (0.4 s), then it lunges and **bashes** you: 10 damage and knocked **160 px** back (you can't be bashed again for 0.8 s). It holds the shield low for a second after, when front hits land. The shield holds 4x its health and cracks as it wears; broken, the guard staggers for a second, then flails and takes 50% more damage. Go round it or break it. Squad (approach step 3): Cell block (two Riot Shields with Rioters pouring past) |
| 3 | **Gas Grenadier** | A prison officer in olive drab with a bandolier of gas canisters and a gas mask. Keeps its distance and lobs a tear-gas canister near you every 3.6 s. It plants its feet for a 1.07 s wind-up: a ring marks the spot (picked as the wind-up starts, so you can walk out of it) and fills while its arm goes back and the lenses glow, then it swings and the canister flies for 1 s. The 70 px cloud settles after 1 s and hurts (8 a second) for 4 s |
| 3 | **Security Guard** | A prison officer like the Guard, in navy: navy shirt and peaked cap, a silver badge, a white SECURITY band across the back, and a service pistol. It holds about 300 px away and strafes, keeping one side for 2-4 s, its pistol trained **straight at you** (no lead), then **locks on for 0.25 s** while still walking, slowed: the shooting line fills along the shot. Then one **tracer round** (340 px/s, 7 damage): a hot-white streak with a long tail. It only starts shooting once within about 380 px of you, then semi-auto, one every 1.5 s, and the aim doesn't follow you once locked, so a sudden turn beats it. Its pips above its head count the **seven rounds** in the magazine; empty, it drops the magazine and **reloads for 2.2 s** (a cream ring fills round it), slowing to a stop and not shooting: rush it. Squad (approach step 2): Patrol (three Security Guards with two Rioters) |
| 5 | **Taser Guard** | A prison officer like the Guard, in a slate shirt with a hi-vis yellow band across its back and an electric-blue cap badge, with a taser. It keeps its distance (320 px), plants its feet and **charges for 0.7 s**: the shooting line and the prongs crackling, brighter as it nears the shot. Then it fires one **Fork Bolt** straight at you (260 px/s, 8 damage; it doesn't lead you, so strafe across the line): a white lightning glyph that flickers its zigzag 16 times a second, with a fork, among rounder bullets. A hit **stuns you for 1 s** (a flash and six bolts web out from you); a perfect parry sends it back like any shot. One bolt every 3.2 s |
| 3 | **Turnkey** | A prison janitor in mustard coveralls and a beanie, a ring of brass keys swinging at its hip. It **never fights**: it runs from every player within 340 px (160 speed, slower than any class), sliding along walls and round you out of corners, and holds about 430 px off otherwise. It **stops to work**, a brass ring round it filling as it does (the moment to catch it): every ~3.2 s it crouches for 0.8 s and plants a **trap** where it stands, and every 10 s (the first after 4 s) it hammers for 1.2 s and builds a **Cell Door** (at most 2) into the nearest free stretch of the arena's **outer wall** (up to 900 px away, 150 px from another door, never a wall between two rooms; a gold outline of it fills in on the wall with a dotted line back to the Turnkey, and the half-built door can be shot, or goes if the Turnkey dies at the work). Traps, one of three at random: a **Floor Mine** (an olive disc with a red light ticking round it and its trigger zone dashed on the floor; arms in 0.8 s; step within 34 px and it beeps 0.35 s, then blows: 22 damage in 64 px, hurting enemies too; shoot it to set it off), a **Shackle Trap** (a bear trap with serrated jaws on a chain to a stake; arms in 0.8 s, then snaps on the first player over it: 8 damage and **rooted 0.9 s**; shoot it to break it) or a **Mop Slick** (a 40 px wet floor with a yellow sign for 14 s: **40% slower** in it). At most 3 mines and 3 shackles each (a new one retires the oldest); traps last 20 s. A **Cell Door** is a barred cell front in the wall (90 health), a prisoner in orange pacing behind the bars; a **red beacon** on top spins faster and its beams sweep the floor as its next **Rioter** is due, red chevrons on the floor mark where it'll come out, the prisoner grips the bars, and the gate slides open to let it out every 4.5 s (at most 3 of its own at once); when its Turnkey dies, its doors go dark and **jam shut** (chained and padlocked) and let no more out. Traps and doors don't hold a wave open and go when it's cleared. It **always comes on the third wave of the Warden's approach** (alone, late), and can turn up after that (70 health, 16 XP). Bots go for it and its working doors first and step round its traps |

| From | The Hive Queen's faction | Plays like | What it does |
|---|---|---|---|
| 11 | **Worker** | Warden's Rioter | A small, quick worker bee that weaves at you, in loose pairs (contact 15). **Faster in a crowd**: +10% speed for each Worker within 110 px, up to +50%. Squads: Swarm (a Soldier and six Workers), Worker pack |
| 11 | **Soldier** | Guard | An armoured bee that rushes straight at you, jaws snapping as it nears (contact 14). Inside 260 px it **charges**: 30% faster and **armoured** (its plates close, half damage). Squad: Soldier pair |
| 13 | **Comb Shield** | Riot Shield | A bee behind a plate of seven wax cells that stops shots from the front. Up close the plate flashes (0.4 s), then it lunges and **bashes** you: 10 damage, knocked **160 px** back. The cells break as it wears (two a step, the facing ones first), and **some of each shot gets through** the plate (15%, 15% more for each crack); broken, the bee staggers and falls fast. Squad: Honeycomb |
| 13 | **Honey Lobber** | Gas Grenadier | Keeps its distance, plants its feet and swells. A red ring marks the spot before it lobs a honey glob; the **honey puddle** (55 px, 4.5 s) slows whoever stands in it (45%) and doesn't hurt |
| 15 | **Stinger** | Taser Guard | Keeps its distance, plants its feet and **charges a venom lance for 0.7 s** (a lime shooting line, the lance crackling), then fires one **venom dart** (260 px/s, 8 damage, no lead; 105 health, half as much again as the Taser Guard): a lime zigzag with two white barbs. A hit **stuns** you for a second; a perfect parry sends it back |
| 13 | **Needler** | Security Guard | Hovers and **strafes fast** (70 health, half as much again), aims straight at you, **locks on for 0.25 s** (the red lane), then one white **needle** (a tracer round, 7 damage). Seven to a magazine (the pips above it), then a 2.2 s reload. Squad: Picket |
| 13 | **Bumble** | Thug | A huge, furry bumblebee: moves and hits like a Brute (175 health, contact 25). Squad: Nursery (a Bumble, three Workers and a Hornet) |
| 15 | **Hornet** | Shiv | Circles in close, shows the red **lunge lane**, then darts along it like a Lunger (contact 18, 26 on the lunge) |

They are drawn as in `Docs/Mockups/hive-faction.html`: vector bees with wings, striped abdomen, thorax and head. The Hive faction pays XP like its counterparts, and after the Queen it fades the same way the Warden's does.

The **Twin Reapers' faction**, the Dusk and Dawn cult, leads up to their fight (waves 21-30: a
ten-wave lesson cycle, one Reaper mechanic a wave; with Campaign buildup off, an approach over waves 21-28). Every member is sworn to **Dusk (pink) or Dawn (blue)** and drawn in
that colour. Squads: **Vigil** (an Acolyte and six Wisps), **Procession** (four Acolytes, two Lamplighters behind),
**Twin graves** (a Gravedigger and two Bound pairs), **Harvest** (two Scythe Wraiths, two Shades, three Wisps); the last
wave brings **Dusk and Dawn**: split to opposite edges, a Mourner on each side with a Bound pair and three Acolytes. (Mockup: `Docs/Mockups/reaper-faction.html`.)

| From | The Twin Reapers' faction | What it does |
|---|---|---|
| 1 | **Wisp** | A soul flame, in threes: drifts straight at you like a Swarmer (14 health, contact 6) |
| 1 | **Acolyte** | A hooded cultist with a sickle: stops short, raises it over a **pale cone** (96 px, 120°, 0.5 s), then one wide swing (12) |
| 3 | **Bound Wraiths** | Always a **pair** (60 health each), circling a point that drifts to just beside you, 75 px out, joined by a **tether** that hurts (10) as it sweeps over you. With a wall in the way they break the orbit and walk round it through the doors. Kill one and the other **stops and channels** it back: a ring fills round the ghost for **3 s**, then it's back at 60% health. Kill the channeller first and neither comes back |
| 3 | **Lamplighter** | Keeps about 280 px away. It plants its feet, its lantern flares and **gold lines** show the volley (0.6 s), then it fires **three lines** at you, next time a **four-way cross**, streaks of four (every 3.2 s) |
| 3 | **Gravedigger** | A cultist brute with a shovel: moves and hits like a Brute (140 health, contact 20) |
| 5 | **Mourner** | Keeps about 260 px away and stands still to weep a **twin spiral**, one arm each way, for 2.5 s, then **rests** 3 s: push in then |
| 5 | **Scythe Wraith** | A Lunger that reaps: marks a long lane from up to 280 px out (0.8 s; the lane **locks after 0.25 s**), then dashes about 270 px along it and reaps **26 px either side** of its path (22). Leave the lane sideways |
| 5 | **Shade** | A shrouded wraith: shows a ring where it will land, blinks there and fires a ring of shots |

| From | The Singularity's faction (the Infall) | Previews | What it does |
|---|---|---|---|
| 31 | **Debris** | the Pull | Tumbling rubble (22 health, contact 9) that falls at you on an **inward spiral**: it comes in from the side when far and straight at you within 90 px, so it's hard to line up. Squad: Infall (a Void Acolyte and six Debris) |
| 31 | **Void Acolyte** | bent shots | A hooded void cultist holding a tiny black hole. Keeps 280 px away and strafes; every 2.4 s it fires **three slow orbs** (150 px/s, 14 each): one straight and two that leave wide and **bend back in to meet it where you stood**. Step forward or back, not sideways |
| 33 | **Pulsar** | Lasers | A spinning neutron star. Within 300 px it plants, and its **two beams** (one each way) **flicker for 1 s** a little behind you, then switch on and **sweep a quarter turn** through you over 4 s (16 a touch). It rests 2.5 s and goes again. Walk the way the beams turn. At most 3 a wave (extra ones come as Void Acolytes). Squad: Binary (two Pulsars) |
| 33 | **Graviton** | the Pull, Gravity Wells | Keeps 320 px away. Every 6 s it plants its feet (0.6 s, a small well swirling in its hand lifts), a ring marks a spot near you, and 0.8 s later a **Small Well** lands there: for 4 s it **drags you toward it** (80 px/s at the middle, 240 px out) and **bends your shots into it** (150 px out). Get clear, or shoot the well (30 health). A new lob takes its old well away. Squad: Gravity trap (a Graviton, a Collapsar and three Debris) |
| 35 | **Orbiter** | the disk, Slingshot | Circles you 260 px out with **three orbs** turning round it. The orb facing you **flares** for 0.55 s, then it **slings** it straight at you (330 px/s, 17); the gap in its ring grows back |
| 33 | **Collapsar** | Collapse, Hawking | A slow, dense star (220 health, contact 20) that walks at you and **shrinks and darkens** as it's shot. Killed, it **collapses**: for 0.8 s it pulls everyone near in (100 px/s at the middle, 260 px out), then bursts into a **ring of 12** shots (14 each). Kill it at range |
| 33 | **Lens** | Gravity Wells | Walks at you (90 health, contact 12) holding a tall **gravity lens** out front. Your shots that come within 90 px of its face **curve round it and away**, and any that reach the lens do nothing. It turns slowly (1.3 rad a second): **get to its side** and shoot it there. Squad: Lens array (two Lenses in front of three Void Acolytes) |
| 35 | **White Dwarf** | White hole | A small, hot star (40 health, 230 px/s) that darts in zigzagging. Within 120 px it stops and **flashes white** for 0.5 s (a ring shows how far the pulse will reach), then **pulses**: everyone within 170 px is **shoved 150 px back**, and up to 10 of **your shots** within 150 px **turn round** and fly back at you (14 each). Then it backs off and comes again. Hold fire when it flashes |

They are drawn as in `Docs/Mockups/singularity-faction.html`, in the Singularity's colours. Design notes: `Docs/SingularityFaction.md`.
The **Colossus's faction**, The Quarry, leads up to its fight (waves 41-48 as an approach): the quarry crew who woke it and the stone
that broke off it (design: `Docs/Approaches.md`, "The Quarry"; mockup: `Docs/Mockups/colossus-faction.html`). Squads: **Landslip** (a Quarryman and six Rubble), **Dig crew** (two
Quarrymen with two Rock Hurlers behind) and, from wave 45, **Quarry** (a Stomper, three Rubble and a Driller),
**Wall-in** (two Slab Bearers with four Rubble) and **Fault line** (three Geomancers, so their cracks cross). They pay XP
like their counterparts (Rubble 7, Quarryman 15, Stomper 20, Slab Bearer 10, Rock Hurler 9, Driller 9, Geomancer and
Cinder Priest 11). The last wave before it always brings the **Avalanche**: four Slab Bearers marching at you as a wall, two
Stompers and two Rock Hurlers close behind it (the hurlers lob over the wall); once the wall gets within about 190 px or
half its slabs are broken, the rest break out. In the Campaign its ten waves (41-50) teach one enemy each, in the
Warden's order: Rubble, Quarrymen, Stompers, Geomancers, a recap with Drillers, Slab Bearers, Rock Hurlers, Cinder
Priests, every squad, then the exam (the Avalanche and a big Landslip).

The **Storm Herald** has one (waves 71-78 as an approach): the **Squall**, a storm front walking ahead of it, all eight of its enemies.
Squads: **Squall line** (a Squall Gunner and six Dust Devils) and, from wave 73, **Storm cell** (two Stormcallers and three Dust Devils) and
**Power line** (two Pylon pairs); from wave 75, **Grounding wall** (two Grounders with three Stormcallers behind) and **Supercell** (a
Thunderhead, a Lineman and three Dust Devils). The last wave before it always brings the **Storm front**: six Stormcallers in a line
behind two Grounders. While any of those callers lives they call no strikes of their own; instead they sweep a **wall of strikes**
(amber rings, 1.1 s warning, 20 each) through you from their side, a column every 0.42 s, 60 px further each time, with **two strikes
missing** from each column. The gap drifts a slot one way or the other as the wall comes on: walk with it. A sweep is 17 columns; 3.5 s
later the next one comes. Kill the callers to end it. In the Campaign its ten waves (71-80) teach one enemy each, in the Warden's
order: Dust Devils, Squall Gunners, Grounders, Stormcallers, Pylons, a recap with Thunderheads, Linemen, Ball Lightning, every squad,
then the exam (the Storm front and a big Squall line).

| From | The Storm Herald's faction (the Squall) | Previews | What it does |
|---|---|---|---|
| 71 | **Dust Devil** | the Gale's wind | A small whirlwind (22 health) that weaves across your line as it comes in. Its touch does 8 and **shoves you 120 px**, into walls and shots behind you; then it veers off and comes again |
| 71 | **Squall Gunner** | Crosswind | Carries its own gust (the vane on its back). Keeps 260 px away; every 2.2 s it fires **three shots aimed upwind of you** that **curve back** across where you stood. Its wind always blows the same way: dodge against the curve |
| 73 | **Stormcaller** | Strikes | A robed caller with a staff. Keeps 300 px away; every 2.8 s an **amber ring** (40 px) fills under you for 1.1 s, then **lightning** strikes it (20). Step out of the ring |
| 73 | **Grounder** | Thunderclap | Walks in behind a **copper shield** that turns slowly. Shots on its face are **grounded** (no damage) and charge it (a blue arc fills along the shield); at 12 it **claps a ring** of 14 shots back with **two gaps** either side of its front. Flank it |
| 73 | **Pylon** (pair) | Lightning rods | Two posts that walk to **either side of you** and plant. Three times: the **arc** between them flickers 0.6 s, then **fires along the line** (18); then they walk again. Kill either and the line is gone. At most two pairs a wave |
| 73 | **Thunderhead** | Downburst | A slow, heavy **storm cloud** (220 health, can't be shoved, contact 20) that walks at you. Within 380 px, every 2.6 s it flashes and drops a **row of three strikes** (30 px) ahead of it toward you, landing **nearest first** 1, 1.2 and 1.4 s later (20 each). Sidestep the row, don't run along it |
| 75 | **Ball Lightning** | Ball lightning | The Herald's own orb (70 health) in its waves: it drifts at you, two within 200 px **arc** to each other (20), and each **pops into 8 shots** after 6 s. Slows and freezes hold it. Kill them far from you and don't walk between two. At most 4 a wave |
| 75 | **Lineman** | the Conductor | Keeps 260 px away. Every 5.5 s it **marks** the nearest player: an amber ring (120 px) with a countdown. After 2.6 s the charge **jumps to every ally in the ring** (16 each) and 8 shots burst out from each of them; alone, it only bursts. The marked player walks away from the team |

They are drawn as in `Docs/Mockups/storm-herald-faction.html`, in the Herald's colours. Design notes: `Docs/StormFaction.md`.

| From | The Colossus's faction | Plays like | What it does |
|---|---|---|---|
| 1 | **Rubble** | Rioter | A jagged chunk of stone that rolls at you, weaving, in pairs (contact 15) |
| 1 | **Quarryman** | Guard | A quarry worker in a hard hat, its headlamp beam ahead of it and a pickaxe that swings when it's close: straight at you (contact 14) |
| 3 | **Stomper** | Thug | A slab-shouldered stone brute (180 health, contact 25) that walks at you like a Brute. Within 260 px it **stops and lifts a foot for 0.8 s** (it rises, its veins glow) while a **green gap** shows a short step to one side of you, then **stomps** out a small ring of push blocks like the Warden's gates (150 px/s, gone at 220 px). Step into the gap and it passes you by; caught, it passes over you once (12) and you end up behind it. Every 4 s |
| 3 | **Slab Bearer** | Riot Shield | A quarry worker behind a stone slab that stops shots and swings from the front. Up close the slab flashes (0.4 s), then it lunges and **bashes** you: 10 damage, knocked **160 px** back. It cracks as it wears; broken, the slab **drops in front of it as cover for 3 s** that stops shots from **both** sides (yours too), and the bearer staggers and takes 50% more |
| 3 | **Rock Hurler** | Gas Grenadier | A chunk of stone that keeps its distance and hauls a boulder back while an **orange ring** fills on **where you're heading** (picked as the wind-up starts: change direction), then lobs it (every 3.6 s). It lands 1 s later for **18**, bursts into **six shards** and lies as a **rubble pile** for 2 s that stops shots from both sides |
| 5 | **Driller** | Shiv | A quarry worker with goggles and a jackhammer: circles in close, shows the lunge lane, then **drills** along it (contact 18, 26 on the drill). The **crack** it leaves along its path glows and **erupts 0.4 s** after it stops (12 to whoever stands on it): dodge across the line, not along it; a hit also cuts: you bleed 4 a second for 3 s |
| 5 | **Geomancer** | Taser Guard | A hooded cultist with a crystal staff: keeps its distance, plants the staff and charges for 0.7 s (the shooting line, the crystal glowing), then sends a **crack racing** at where you stood (600 px/s, up to 460 px). It glows for 0.5 s, then **erupts** along its length (12): step off the line. Every 3.2 s |
| 5 | **Cinder Priest** | Taser Guard | A dark-robed priest cupping a molten core: charges for 0.7 s along a shooting line, then fires one **magma bolt** straight at you (240 px/s, 9, no lead). Wherever it stops, a hit, cover or the end of its 300 px, it leaves a **lava pool** for 2.5 s (20 a second). A **perfect parry** sends it back and leaves no lava. Every 3.2 s |

The **Duelist's faction**, the Masked Court, leads up to its fight (waves 51-58 as an approach): masked fencers, each showing you one of
the Duelist's moves (mockup: `Docs/Mockups/duelist-faction.html`). It replaces the basic faction the Duelist had before.
Squads: **Salon** (a Blade Dancer and six Fencers), from wave 53 **Seconds** (three Seconds and two Fencers) and from
wave 55 **Guard line** (two Parriers and four Fencers) and **Masked ball** (a Harlequin, a Mummer and three Fencers).
XP: Fencer 7, Blade Dancer 14, Parrier 10, Second 9, Marshal 9, Gallant 12, Harlequin 22, Mummer 10. The last wave
before it always brings the **Ring of Honour**: ten Fencers and two Gallants in a ring 300 px round you, facing in,
turning slowly and closing to 150 px; every 1.4 s an opposite pair of Fencers shows its lines and lunges in, and the
Gallants salute before theirs (parry them). It breaks up after 14 s or once half of it is down. In the Campaign
its ten waves (51-60) teach one enemy each, in the Warden's order: Fencers, Blade Dancers, Harlequins, Seconds, a recap
with Mummers, Parriers, Marshals, Gallants, every squad, then the exam (the Ring of Honour and a big Salon).

| From | The Duelist's faction | Plays like | What it does |
|---|---|---|---|
| 1 | **Fencer** | Shiv | A masked foil-fencer (32 health, contact 10) that runs in crowds. It closes to within 160 px, shows a **short red line for 0.4 s**, then **hops about 100 px** down it (16 on the hop) and recovers for 0.55 s: step off the line; a hit also cuts: you bleed 4 a second for 3 s |
| 1 | **Blade Dancer** | Guard | A masked fencer in a crimson cape (70 health, contact 14). Within 300 px it stands still and shows the Duelist's **warning line for 0.6 s**, then **lunges up to 230 px** and leaves **three blades** hanging on its path. 0.6 s later each fires **one shot out of each side**, across the line: never stand beside it. Every 3.5 s |
| 3 | **Parrier** | Riot Shield | A masked fencer on guard behind a gold parrying arc and a main-gauche. A shot on its **front is parried straight back** at whoever fired it (at most one every 0.12 s) and wears its guard; up close it **bashes** you back like a Riot Shield. It turns slowly: **get behind it**. Broken, it staggers and takes 50% more |
| 3 | **Second** | Security Guard | The duelist's second (45 health): keeps its distance and locks on like a Security Guard, its hand going back and five faint lanes showing, then throws an **aimed fan of five daggers** (230 px/s, 10 each, 0.16 rad apart). Every 2.2 s; it never reloads. Slip between them |
| 3 | **Marshal** | Gas Grenadier | The duel's referee (55 health): keeps its distance and raises its baton, then draws a **gold circle 140 px round where you stand**. It draws itself round over 0.6 s (step out now); then for 4 s you **can't leave it**, and leaning on the edge hurts (8, at most every 0.6 s). Anyone outside can walk in. Every 7 s |
| 5 | **Gallant** | Taser Guard | A champion in a steel cape (80 health, contact 20) that never shoots. Within 340 px it **salutes** for 0.8 s (sword straight up, a gold glint, a shrinking ring), then **lunges through you** (up to 400 px). A **perfect parry** as it arrives knocks it **off balance for 2.5 s**, taking **50% more** damage; dodge it and it **rakes** the spot with five shots. Every 4.5 s |
| 3 | **Harlequin** | Thug | A heavy two-sworded jester in crimson diamonds (240 health, contact 25) that walks at you. Within 260 px, the **Crossing**: it shows a line through you for 0.7 s and dashes it, 170 px past you, then **vaults to the other arm of the X**, shows the crossing line for 0.45 s and dashes that. Both leave blades every 70 px that fire **out sideways** after 0.8 s: stand in a gap of the X. Every 6 s |
| 5 | **Mummer** | Shiv | A fast masked player in a violet cape (40 health) that circles in and lunges like a Shiv. Within 260 px it **blinks and becomes three**: they circle you 150 px out for 5 s, each firing every 0.9 s, but only the **real one's shots are solid**; the copies' are **hollow rings** that pass through you. The copies look the same, have 1 health and give nothing; kill the real one and they vanish. Every 9 s; a hit also cuts: you bleed 4 a second for 3 s |

The **Devourer's faction**, The Larder, leads up to its fight: its brood (mouths with no eyes, the Devourer in miniature)
and the hooded butchers who keep it fed (design: `Docs/Approaches.md`, "The Larder"; mockup:
`Docs/Mockups/devourer-faction.html`). All eight are built, each teaching a turn of the fight.
Squads: **Feeding Frenzy** (a Butcher and six Gnawlings) and, from step 3 of the approach, **Larder Wall** (two Carcass
Bearers with four Gnawlings), **Meat Hooks** (two Hook Throwers behind two Butchers), **Gullet** (a Gulper, three
Gnawlings and a Gutworm) and **Bile Rain** (three Bile Spitters, so their pools overlap). XP: Gnawling 7, Butcher 15,
Gulper 25, Carcass Bearer 10, Bile Spitter 9, Gutworm 12, Hook Thrower 10, Maw Priest 12. The last wave before it always
brings **the Feast**: four Carcass Bearers marching at you as a wall, two Gulpers and two Bile Spitters close behind it
(the spitters lob over the wall); once the wall gets within about 190 px or half its meat is broken, the Gulpers push
through and breathe you in. In the Campaign its ten waves (81-90) teach one enemy each, in the Warden's order:
Gnawlings, Butchers, Gulpers, Maw Priests, a recap with Gutworms, Carcass Bearers, Bile Spitters, Hook Throwers, every
squad, then the exam (the Feast and a big Feeding Frenzy). **Morsels** (the Devourer's, 60 health) also
fall from broken Carcass Bearers: shoot one or walk over it, or a hungry Gnawling within 320 px runs for it, eats it and
grows (50% more health, drawn bigger), as the Devourer heals from them in its Hungry turn.

| From | The Devourer's faction | Plays like | What it does |
|---|---|---|---|
| 1 | **Gnawling** | Rioter | A lump of pink flesh that is mostly mouth: weaves at you in pairs, chomping (contact 15); eats morsels |
| 1 | **Butcher** | Guard | A hooded butcher with a bone mask and a cleaver: straight at you (contact 14). Within 70 px it **stops and raises its cleaver for 0.4 s** while a **red half-circle** fills in front, then **chops** it (110 px, 14), the shape of the Devourer's Snap bite, and rests 1.6 s. Step back or round it |
| 3 | **Gulper** | Thug | A swollen gut (180 health, contact 25) that walks at you like a Brute. Within 220 px it **stops and its cheeks swell for 0.6 s** while its **cone** shows, then it **breathes in** for 1 s: the 70° cone, 200 px long, pulls you toward its mouth at 120 px/s. Reach the mouth and it **bites** (20) and the breath ends. Step out of the cone sideways, as from the Devourer's Inhale. Every 4 s |
| 3 | **Carcass Bearer** | Riot Shield | A hooded butcher behind a side of meat that stops shots and bashes you back (10, 160 px). The meat tears as it wears; broken, it drops in front as a **morsel** and the bearer staggers and takes 50% more |
| 3 | **Bile Spitter** | Gas Grenadier | A lump of brood with a green bile sac: keeps its distance and, every 3.6 s, its sac swells while a **green ring** fills **where you're heading**, then it lobs a glob. It lands 1 s later as a 35 px **pool of acid** for 4 s (10 a second), the Devourer's Bile in small. Change direction out of the ring |
| 5 | **Gutworm** | Shiv | A burrowing worm of brood (70 health, contact 12) that walks at you, then **dives**: a ripple chases you underground for 1.8 s (110 px/s; it can't be hurt or touched), stops and **marks** 50 px for 0.6 s, then **erupts**: a bite (16) and a ring of 8 shots, then 3 s on the surface. Get out of the mark. The Devourer's Burrow in small |
| 5 | **Hook Thrower** | (its own) | A hooded butcher swinging a meat hook. Keeps about 260 px off, shows its **line** for 0.7 s, then throws the hook down it (600 px/s, 320 px). A hit does 8 and **reels you 90 px** toward it. Sidestep, parry it, or shoot across the chain to cut it; a miss leaves it open (+50%) |
| 5 | **Maw Priest** | Taser Guard | A priest wearing the Devourer's jaw (90 health): a **90° maw** in front. Shots into it **heal it for half**; shots in its back do **25% more**. It turns at 30° a second, keeps its distance and, after a 0.7 s charge, fires a **fan of three** from its maw. Circle round it: the Devourer's Ravenous Maw in small |

The **Eclipse's faction**, the Penumbral Court, leads up to the final fight (waves 91-98 as an approach): sun-cultists and scraps of
flare, each previewing one of the Eclipse's phases (design: `Docs/EclipseFaction.md`; mockup: `Docs/Mockups/eclipse-faction.html`).
Squads: **Dawn** (a Corona Priest and six Embers) and, from wave 93, **Mirror pair** (two Heliographs); from wave 95,
**Moon court** (two Moonwards and two Sickles), **Night watch** (a Dusk Lobber, a Gnomon and three Embers) and **Echoes**
(one member from each earlier boss's faction: a Rioter, a Worker, a Wisp, Debris, Rubble, a Dust Devil, a Fencer, a
Shambler and a Gnawling). At most three Heliographs a wave. The last wave before it always brings **Last Light**: four
Heliographs at the corners and eight Embers along the sides of a square round you that turns slowly and closes in (from
330 to 140 px from the middle to each side), like the Eclipse's burning edge; the Embers' sparks hang where they pass
and the Heliographs flare in from the corners. Kill a side to break out; it breaks up after 14 s or once half of it is
down. In the Campaign its ten waves (91-100) teach one enemy each, in the Warden's order: Embers, Corona Priests,
Heliographs, Moonwards, a recap with Gnomons, Dusk Lobbers, Sickles, Novas, every squad, then the exam (Last Light and a
big Dawn).

| From | The Eclipse's faction (the Penumbral Court) | Previews | What it does |
|---|---|---|---|
| 91 | **Ember** | Lunar wake | A scrap of solar flare (16 health, 135 px/s, contact 8) that **weaves** in at you. Every 0.2 s it drops a **spark** (6) that drifts back slowly (25 px/s) and **hangs for 1.5 s**, so crossing its trail hurts |
| 91 | **Corona Priest** | Corona rings | A sun-cultist inside a halo of spikes. Keeps 260 px away and strafes; its halo **flares for 0.7 s**, then bursts into a **ring of 13 slow shots** (130 px/s, 14 each): one straight at you and a **gap one slot beside it**. Step into the gap |
| 93 | **Heliograph** | Solar flares | Carries a sun mirror. Within 340 px it plants and aims at you: a **line flickers for 1 s**, then it becomes a **40 px lane of fire** for 0.5 s (20). It rests 2.5 s and goes again. Step off the line, not back along it |
| 93 | **Moonward** | the Penumbra's moon | A pale knight (100 health, contact 12) that walks at you with a small **moon circling it** 30 px out, once every 2.4 s. The moon **swallows any shot** it touches: fire when it swings round the back |
| 93 | **Gnomon** | Eclipse flash | A walking sundial (240 health, contact 20, can't be shoved): a stone slab with a **sun lantern** at its back. Within 360 px it plants with its slab turned a little to **one side of you**, its **shadow** shows on the floor and the lantern swells for 1.5 s; then it **flashes** everyone within 520 px who isn't in the shadow (30). Every 6 s. Step sideways into the shadow, toward it |
| 93 | **Dusk Lobber** | the Umbra | Plays like a Gas Grenadier, but lobs a **ball of night** that bursts into a **pool of dark** (90 px, 4 s): inside you see only 40 px round you, and it **drains 6 a second**. Stay in the light |
| 95 | **Sickle** | Crescents | A grey-robed cultist that keeps 240 px away and strafes. Every 2.4 s its blade glows (0.6 s), then it throws a **crescent** that leaves to one side, **loops through where you stand and comes back** round to it (300 px/s, 21). Dodge it twice |
| 95 | **Nova** | Totality | A small unstable star (40 health, 230 px/s, contact 10) that darts in zigzagging. Within 140 px it **lights a 3 s fuse** (a ring runs down round it, a faint ring shows its reach) and hangs about you, slowly; while lit it **takes 50% more damage**. Then it **goes nova**: 30 to everyone within 110 px, and it's gone. Burst it or back off |

**Bubbles (Shielded elites):** a bubble is twelve cells round the rim, each holding a twelfth of the shield. A hit wears down the cell facing whoever shot it (flashing, then breaking into shards) and spills over to its neighbours once that cell is gone, so the side you shoot from is the side that opens. Burning and bleeding keep chewing the weakest cell. A honeycomb inside only shows where its cell is still up, and a thin shield bar sits above the health bar. Blue is a Shielded elite, violet is the Warder's.

## Bosses

**Red aimed shots.** A boss's red aimed shots are barbed spearheads with a black heart and a red streak, pointing where
they fly. They hit for 30 and most blockers can't stop them (the walls and shields that say "red aimed shots too" can).

**Every boss sounds like itself.** Each mechanic has a sound for its warning and for its strike, from a palette of
synthesized boss sounds: a rising whine before lasers and aimed attacks and a buzzing beam when they fire, a crack and
deep thud for slams and landings, a rush for charges and dives, the Warden's prison siren, the Queen's swarm buzz
and honey splats, scythe swishes for the Reapers and the Duelist, an in-rush for the Singularity's pulls and the
Devourer's inhale, the Colossus's rumbles and breaking stone, cages slamming, the Devourer's jaws, the Herald's
thunder and a deep gong for rites, last stands and the Eclipse's totality. They follow the Enemies volume.
(`Tools/SoundCheck/render.sh` writes them to WAV files.)

**Players have footsteps.** A quiet digital tick over a short low pulse, faster, louder and brighter the faster you move (about 4 steps a second at 240 px/s); silent when standing still, during a dodge's dash and while downed. Your own steps are loudest; allies' play quieter and fade out by 700 px. They follow the Effects volume. (Mockup: `Docs/Mockups/footstep-sounds.html`, option D.)

**Melee rushers have sounds, and far sounds are quieter.** They all have footsteps (a crowd of one kind makes one step at a time, at the nearest one's volume). Swarmers chomp and pop, Brutes thud, grunt and bash, and Lungers zip when their pink line shows and again at the leap and the landing. Every kind dies with its own sound, but a pack of one kind dying together makes one. Enemy sounds and enemy shots get quieter with distance from you: full volume within 260 px, down to about an eighth at 1100 px or more. They follow the Enemies volume. (Mockup: `Docs/Mockups/melee-enemy-sounds.html`.)

**Bosses keep up with strong builds.** When a boss arrives, each player's upgrades are scored (damage x fire rate
x projectiles x crits, plus boss legendary cards and companions) and compared with a typical build at that wave (one
picked sensibly from the same offers). A party far above typical meets a boss with more health: (how far above)
to the 0.6 power, up to x3. Bosses after wave 40 already get +10% health every two waves for the same reason, so the bigger
of the two applies, not both. Typical and weaker builds meet the usual boss, and a strong build still kills faster.
Off in the Boss Lab. (`run.sh 1 1 bosspower` prints the numbers.)

**Health at each phase change.** When a boss changes phase (every boss's phase shifts and last stand, and the
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
calling in adds and phase changes.

The Campaign has **100 regular waves** with a different boss after every 10th one. The boss is its own fight between wave 10 and 11, wave 20 and 21 and so on: it keeps the wave number of the wave before it (the HUD says BOSS FIGHT, the wave box says BOSS CLEARED) and doesn't use up a wave, so 10 regular waves lead up to each boss. Bosses after wave 40 get extra health (+10% per two waves on
top of the normal scaling: x2 at wave 60, x4 at wave 100) to keep up with stacked upgrades. Their name, health bar and
warnings show at the top. Beat The Eclipse after wave 100 (50 in a run saved before the Campaign) to win; the host can then keep going in **endless** mode (C),
where the bosses come back around, tougher each time.

| Wave | Boss | Mechanic |
|---|---|---|
| 10 | **The Warden** | **Look:** the Chief Warden, a navy greatcoat with a crimson collar, peaked cap and gold epaulettes, who acts out each move: racks and kicks back with a riot shotgun, turns on his heel for the spiral, radios the tower and points out the light with his baton, throws an arm over his eyes when exposed, slams his baton into the floor at each cross plant, lobs the gas overhand, crouches for the charge, kneels as his armor locks on ([`Docs/Mockups/warden-rework.html`](Docs/Mockups/warden-rework.html), body A). **Entrance (Lockdown):** push-bullet walls close in from all four sides into a cell round it, a red padlock snaps shut and its silhouette lights up, then it blows the cell open (2.9 s, when it and its escort of **Security Guards** (one per 2 waves) start; it can't act or be hurt until then) and a CRT band types THE WARDEN, in place of the usual boss card ([`Docs/Mockups/warden-intro.html`](Docs/Mockups/warden-intro.html), option B). A prison riot in three phases, one mechanic at a time. Between phases is a **5 s phase change**: the screen clears, it can't be hurt and it drops health orbs, so the team can regroup, heal and let abilities cool down while he acts it out — staggered by the blow, then into **Lockdown** he radios the tower, holds up his keys and slams his baton down; into the **Riot** he roars, points his baton round the cell doors, turns the key and racks his shotgun; into the **last stand** he kneels while five plates lock on, then rises roaring (`WARDENTRANS` preview). The phase banner sits high, under his bar. **Boss bar (Cell Blocks):** his health bar is cut into a block per phase, sized to it and in its colour (Count gold, Lockdown blue, Riot orange, Last Stand red), with a barred gate and padlock at 65%, 30% and 10%; the next gate shakes and the note reads "LOCKDOWN IN 3%" within 4%, a gate flashes while he changes phase through it and its padlock stays open after, and each phase's name sits under its block. **Count** (100–65%): sways through the middle — orb rings, a spiral (holding still), aimed fans (red lanes show each volley 0.35 s before it fires), then an 18 s **searchlight** from the corner tower that follows you at an outrunnable pace while the **Taser Guards** below do the stunning (the Warden fires no stun shots himself); get stunned or linger in the light and the Warden throws a **net** over you: the screen fades and you're locked in a **Holding Cell** (one grid cell, off the arena) while **Riot Guards** pile in through a gap in each wall, faster and faster. Survive until the door opens in **30 s**; every ability you cast inside takes **1 s** off, and a teammate shooting the **padlock** where you were netted takes **0.25 s** off a hit. Only the netted player leaves: the others fight on; with nobody left in the arena (solo, or everyone netted, the second one in the cell next door, in view) the fight **pauses**, and the first one out starts it again (and can shoot the other's padlock). Downed in the cell, you're sent back to the fight downed, for a teammate to revive ([`Docs/Mockups/warden-net-cell.html`](Docs/Mockups/warden-net-cell.html)). Turn it round: **lead the light across the Warden** and it's blinded for 3 s (**exposed**: no firing, +50% damage), and a **perfect parry** sends a Taser Guard's Fork Bolt straight back to **stun it** for 1.5 s. Each searchlight turn also sends in **two Riot Shields** (if fewer are up) that work round to your side away from the light and bash you into it, and **two Taser Guards** through the other side doors (staggered, one Fork Bolt each every 3.2 s) that go when the turn ends. **Lockdown** (65–30%): the 12 s **cross** (dash, plant, fire a static "+", four times) runs alongside three **gates** — walls of push bullets 2 rows deep, with two doors near the side walls; each slides a little way in and stops for a moment (its doors flash) so you can line up a door, then sweeps across, shoving you back the way you came (one hit a gate, pinned or not). A turn's gates all come from the same edge, and each has a **gold lock** in the middle: six hits break it (shots, blasts from grenades, boulders and flak, melee swings and beams all count), opening a third door there. With them, lobbed **tear gas** that never lands across that gate's doors. Then a **tear gas barrage**: two volleys of canisters in a pattern with clear lanes between, while it fires slow aimed bursts. **Riot** (30–0%): cell doors burst open and **rioters** pour out, nine a door (they attack whoever is nearest, the Warden too); any still standing after 10 s bring blue **guards** with batons, who beat them down and then come for you while it turns a riot shield toward you and fires a shotgun. Rioters that reach it take 0.6% of its health each, but only up to 5% a riot turn (after that it bats them away). Its **riot shield** soaks 7% of its max health from the front, cracking as it wears; **break it** and it's exposed for the rest of that riot turn, taking 50% more; then a telegraphed **baton charge**. Below 10% it **puts on armor** (its phase change: five steel plates lock on as it kneels, it can't be hurt; then a blue armor bar above its health, worth about 24% of its max health, takes your hits first) and makes a **last stand** in the middle (about 15-20 s for a good duo): the lights go out (lit round you, the Warden and its searchlight, which hunts you again) under a steady red tint, with nothing flashing. It fires long **"+" bursts** of straight streams (2.2 s), turning 45° each time (+, ×, +...); in the 1 s pause between, red lanes show where the next goes and **one ring** of orbs goes out; a **Taser Guard** comes in through a bottom door, kept lit in the dark, and fires its Fork Bolts at you (perfectly parried, one stuns the Warden) |
| 20 | **Hive Queen** | A hive in three phases, one mechanic at a time (2 s between phases, when the screen clears and she can't be hurt). **Swarm** (100–65%): she sways along the top — **drones** (tough Shards; she's **shielded and waits** while they live, and any left after 10 s fly back into her and heal her 2% each), **bombers** that swoop out like wasps to a point beside you (left and right in turn), then lock onto your spot and dive in from the side (they don't fly the line between you and her, so shooting her won't catch them), aimed **fans**, then orb **rings**. **Honeycomb** (65–30%): she lays 4 **eggs** that each hatch 2 drones unless you break them in 6 s; the floor's hex **cells** glow and seal (18) in waves, always leaving a safe cell next to you; a sweeping **nectar spray** leaves honey puddles that slow you. **Frenzy** (30–0%): she drops into the arena — a **swarm cloud** of bees hunts you at an outrunnable 170 px/s, three **royal dives** land on your shadow (she can't be hurt in the air) with a ring of orbs, and a **bomber barrage** aims at where you are and where you're heading. Below 10% she makes a **last stand** in the middle. She's the **Brood Mother**: a crowned head and stubby wings dragging a see-through egg sac full of eggs, turned toward you; the sac is wrapped in wax in the Honeycomb, glows red and boils in the Frenzy, and hangs half-empty, cracked and leaking honey in the last stand. Before her eggs the sac swells and throbs, glowing, then squeezes them out; in a royal dive she spreads her wings wide, tucks her legs and sac in and turns toward where she'll land, then hits the floor squashed flat and wobbles back up, cracking the wax, splashing honey and kicking up dust. Her eggs arc out of the tip of her sac to where they land |
| 30 | **Twin Reapers** | Two Reapers (2600 HP each) in three phases by their combined health, one mechanic at a time. **Twilight** (100–65%): a **tether sweep** that spins round a point following you, then **lines** and a **cross** fired holding still; **Soul Bond** (the more-hurt one is immune until you even them out). **The Dual Reaper** (65–30%): they spiral together and fuse (a power-up of marked blasts), armored to a third of the damage. It fires **twin rotating lasers** while 4 **pillars** stand in the corners — kill them, or it pulls you in and blows up — and each pillar leaves a **hole**. Then it **chases** you swinging both scythes; lure it over a hole and it falls in: break its **shield** and it's **open** for 4 s (half again the damage), then it leaps back and starts over. **Sundered** (30–0%): it splits back into Dusk and Dawn and the tether snaps — marked **reaps**, a **twin spiral**, then **crossfire**; kill one and the other **enrages**, and the fallen one revives at 60% after 6 s unless you finish the pair. They're drawn as hooded reapers in torn cloaks, each with a pointed scythe (drawn back when a reap is marked) and glowing eyes (red when enraged), joined by a chain that pulls tight and glows for the sweep; the Dual Reaper is one cloak with a hood each way, and its pillars are gravestones with a gold flame. When they fuse, a moon-and-sun sigil closes under them as they spiral in, scythes raised, and the Dual Reaper unfurls out of a column of gold light; when it splits, it shakes, its seam burns white and the floor cracks, then the chain snaps into flying links as the two fly apart |
| 40 | **The Singularity** | **Look:** a lensed black hole: a black shadow ringed in white, with a tilted glowing disk crossing it that heats from pink to gold to red phase by phase (a streamer tears off it in Spaghettify); its Split's shards, Gravity Wells and Cage Wells are small ones (the wells' pull rings and the cage take its phase colour), and its Accretion disk's orbs are gold embers trailing light. A black hole in three phases, one mechanic at a time with breathers between (2 s between phases, when the screen clears and it can't be hurt). Four **Gravity Wells** spawn in the corners at the start of each phase: they bend your shots hard into themselves and drift you toward them; killed, they stay gone until the next phase. **Event Horizon** (100–65%): in the middle; **Pull** (4 s, drags everyone in under a three-armed spiral), **Collapse** (3 s of bullet rings), **Lasers** (8 s: a purple spiral, then three red lasers that flicker as a warning and sweep slowly round). **Accretion** (65–30%): it orbits the middle inside a **disk** of two rings of big orbs turning opposite ways, with gaps (touching one hurts); **Cage** (9 s: three small wells orbit every player in a shrinking ring; touching it hurts, shooting the wells breaks it), **Slingshot** (6 s: every 0.8 s three orbs leave the outer ring at you), **Twin lasers** (two, sweeping faster). **Spaghettify** (30–10%): back in the middle behind an **event horizon** that grows from 70 to 170 px (touching it hurts) and pulls all the time (60 px/s, slower than anyone walks); **Hawking** bursts (rings of 18 off the horizon), **White hole** (a 1 s white flash, then for 1.5 s it pushes and every enemy bullet on screen turns round), **Split** (three shards orbit and fire; the core can't be hurt, and damage to the shards goes to it). Below 10% its **last stand**: the horizon swells to 190 px, the pull grows (60 to 110 px/s), a spiral and rings of big orbs |
| 50 | **The Colossus** | A stone giant in three phases that breaks down **and shrinks** as it goes, one attack at a time with breathers between. It looks like a sandstone golem seen from above: two huge fists and four slab plates bolted on. It's **three times its size** in Awakening, **twice** in Crumbling (shedding chips and dust all the while), its own size in Molten Core and smaller still (0.7) for the last stand. **Stagger:** at 65%, 30% and 10% it drops to its knees, dizzy, its plates rattling, and the screen clears; its health holds there and your hits fill a gold **Break** ring round it instead (5% of its health). Broken, a layer of its shell **blasts off** (a white flash, a shockwave, slabs flying out and landing as rubble that fades in 2 s), it drops to the next phase's size at once, and it can't be hurt for 2 s while it stands up into the next phase. At the change to Crumbling the plates rattle loose, then fly off on high spinning arcs and land as the four cover slabs; from Molten Core its shell is charred, split by lava seams round a glowing core, and it kneels with its fists in the ground for the last stand. It raises both fists before every stomp, holds a boulder up in one fist before it throws, draws its fists back to brace for a charge, rams with them on the charge, and hangs limp while dazed. Its **stomps** send out rings of **indestructible stone orbs** (like the Singularity's disk, 125 px/s) with a few **small openings** (about 70 px across near it, wider farther out), one a short step (about 130 px) to the side of its target and one beside each other player: slip through an opening. Touch an orb and it hurts (20 times the difficulty's damage growth, once a ring); nothing shoves you. **Awakening** (100–65%): it walks at you, rock volleys between turns — Stomps (three openings), **Boulders** (it tears a huge boulder out of the ground and holds it over its head for 1.2 s while a dashed red ring follows where it'll land, then lobs it high: it flies 1.35 s, biggest at the top of its arc, and lands in a 70 px circle with a burst; three a turn), **Fissures** (a crack races toward you, waits 0.8 s, then erupts along its length: shots out of both sides and a burning trench for 1.2 s that hurts what stands in it; then it **stays as a scar** until the phase ends. Two a turn; once five are up, a Fissures turn wakes all five instead and they erupt again one after another). **Crumbling** (65–30%): its armour falls as **four plates of cover** that stop shots from both sides — **Charge** (a red lane, then it runs; **a plate in the way breaks and dazes it: +50% damage**), **Rockslide** (rows of rocks from an edge with a moving gap), **Aftershocks** (rings of shots whose gaps spiral). **Molten Core** (30–10%): everything leaves **lava** (20/s) — Lava stomps (three openings, a pool where it stood), **Meteors** (land in orb rings and pools), **Cave-in** (pillars fall, stand as cover for 3 s, then shatter). **Last stand** below 10%: it kneels in the middle — stomps with four openings, volleys and meteors |
| 60 | **The Duelist** | The Maestro: a masked fencer in a crimson cape with a swept-hilt rapier (its mask cracks and cape tears from To the Death; changing phase, it bows and lifts its mask into the Masquerade, and spins to carve the circle, then cuts down its own mask into To the Death; at its last stand it throws off its cape, half its mask breaks away to show a crimson eye, and it fights on with a crimson-edged blade; its decoys flicker in as violet silhouettes on a thread from it, then snap solid, and shatter like violet glass when they go). A duel in three phases (2 s between, the screen clears and it can't be hurt). **En Garde** (100–65%): it strafes 280 px out — Volleys, a **Lunge** (warning line, a fast dash leaving blades that fire sideways), a **Guard** (your shots on its front bounce back at you; it turns slowly, so get behind it), and a Blink that leaves a decoy. **Masquerade** (65–30%): three identical decoys circle you with it, and only its shots are solid (theirs are hollow rings); a **Crossing** (an X of dashes leaving blades) and a lunge chain. **To the Death** (30–10%): a **dueling circle** 360 px round the middle you can't leave (leaning on it hurts 15); it **Salutes** before it lunges — a **perfect parry** as it arrives knocks it off balance for 2.5 s at +50% damage, a dodge gets you raked; a **Flurry** of slashes. Below 10% the circle closes to 280 and it spins and salutes |
| 70 | **The Lich** | A hooded sorcerer (a skull in a ragged cloak, bone pauldrons, a soul staff; a rune and a soul mote more each phase) that sheds its cloak into a see-through **wraith** (ribcage round a beating soul gem, clawed arms) for the Final Rite; its graves glow and leak souls as they stir, and its phylacteries are glass soul jars that crack as they break. **Three phases** among six graves, one mechanic at a time. **The Crypt** (100–65%): one phylactery guards it (break it for a 12 s window); **soul fans** (5 aimed piercing shots), **grave rings** (a slow ring of 16 big orbs, each turned 12°), the dead rising from its graves. **Soul Harvest** (65–30%): three phylacteries joined by **soul beams** that hurt to cross; a **soul sweep** (twice: a warning line, then a draining beam 900 px long swung 120° across toward the nearest player), **bone prisons** (twice: a ring of 14 curved bones rises out of cracked earth 110 px round each player, two skull-topped bones and a gold chevron marking the gap, the gap facing away from the Lich; when up, they fire inward), **wailing spirits** (wailing ghosts trailing wisps; they circle the Lich, then dive at where a player was). **Undeath** (30–10%): a spectre that fades out and blinks to another grave every 2 s; **dead hands** (every 0.9 s they stir under each player, striking a 50 px spot 0.8 s later), a **soul storm** (a two-armed spiral that reverses halfway). **Final rite** below 10%: soul fans every second, grave rings every two, and the dead rise from all six graves every 6 s |
| 80 | **Storm Herald** | A storm in three phases, one attack at a time with breathers between (2 s between phases, when the screen clears and it can't be hurt). **Gathering Storm** (100–65%): it drifts along the top, firing its 3-shot piercing volley in the breathers; **Strikes** (6 s: a 70 px strike on each player and two random spots every 1.8 s, 1.1 s warning, 25, +3% every two waves), **Lightning rods** (7 s: four rods fall around you; every 1.4 s an arc flickers 0.6 s between a pair, then fires along the line, 18, +3% every two waves), **Thunderclap** (5 s: rings of 36 with two gaps, each turned 25°). **Gale** (65–30%): a **wind** blows across the arena all phase, turning round with each attack; it pushes players 70 px/s and drifts every bullet 60 px/s, yours too. **Crosswind** (6 s: 5-shot piercing volleys aimed upwind so the wind carries them on), **Ball lightning** (7 s: twice, three 70 HP orbs drift at you on the wind; two within 200 px arc to each other, and each pops into 8 shots after 6 s), **Downburst** (6 s: rows of six 110 px strikes at your height, landing one after another in the wind's direction). **Supercell** (30–10%): the sky goes dark. The **Conductor** marks one player at a time (every 8 s, for 3 s, a 180 px ring): when it runs out the charge jumps to every ally in the ring, hurting both ends, and 10 shots burst out from them (alone, it only grounds into the shots). **Storm front** (7 s: a wall of strikes sweeps across with a two-strike gap that drifts), **Strike rain** (6 s: smaller, quicker strikes), **Rod cage** (7 s: twice, six rods ring you 240 px out and arcs join them, all but one open side). Below 10% its **last stand** in the middle: four lightning spokes turning 20°/s, a strike on everyone every 2 s, its volley. Look: the **Tempest Herald**, a hooded caller with a torn storm cloak (streaming downwind in the Gale), two lightning-rod spires on its shoulders arcing to each other and a crackling staff, who acts out each attack: thrusts the staff at the sky for strikes (a bolt answers each call), drives it into the floor as rods fall, holds it level to conduct their arcs, slams it down for each Thunderclap ring, sweeps it upwind for the Crosswind, swells an orb at its head for Ball lightning, points it at the Conductor's mark and hunches in its cloak between phases; storm blue, dark violet from the Supercell |
| 90 | **The Devourer** | A giant lamprey: a ridged worm head with a round mouth of grinding tooth rings and a ringed tail; it swells and drips bile in Gorging, its skin splits red and its throat grows a third ring of teeth in Ravenous, and its throat burns in the last stand. A hunger in three phases, one attack at a time with breathers between (2 s between phases, when the screen clears and it can't be hurt); it grows each phase (50 → 58 → 64 px). **Appetite** (100–65%): it keeps 260 px from you. **Spiral** (6 s: its double spiral, 2 shots every 0.08 s), **Snap** (5 s: three lunges at a player, a red line for 0.7 s, then a 1000 px/s dash and a bite in a 110 px half-circle, 13, +3% every two waves), **Hungry** (5 s: its mouth opens and your shots **heal** it for half; rings of 20 big shots every 1.2 s; it spits 4 **morsels** (60 HP) and crawls over to eat them, each healing it 1%: shoot them or walk over them first). **Gorging** (65–30%): **Inhale** (5 s: twice, a 70° cone 600 px long pulls you 200 px/s towards its mouth, 80 at the far end; whoever reaches the mouth is **swallowed**: hidden inside for 3 s, 10 damage a second, while allies' hits on it do **double** and 300 of them spit you out early, with a ring of 12 shots), **Bile** (6 s: every 1.5 s a glob at each player and one at random, a 90 px pool of acid for 6 s, 12 a second), **Burrow** (7 s: twice, it dives (can't be hurt), a ripple chases a player for 1.9 s, stops, marks 100 px, then erupts with a bite and a ring of 16). **Ravenous** (30–10%): **the Maw**, a 90° cone in front of it: shots into it heal it for half, shots on its back do ×1.25; it turns to the nearest player at 60°/s (35°/s if you're alone). **Frenzy** (6 s: four lunges, 0.55 s warning, each ending in a ring of 12), **Jaws** (6 s: twice, two rows of 7 teeth 320 px either side of a player show for 0.6 s, then close at 140 px/s), **Vomit** (5 s: a spray of bile sweeping 70° either side of you and back). Below 10%, its **last meal** in the middle: it pulls everyone in at 55 px/s, ten teeth (two gaps) turn round it 170 px out, its spiral comes back at half the rate; being swallowed here does 20 a second |
| 100 | **The Eclipse** | The final boss, a **bright sun** with long streamers and red prominences; its **moon's disc crosses it a step each phase**, leaving a burning ring and a diamond flare from the Umbra on, until it covers it at Totality: **five phases** and a last stand, one attack at a time with breathers between (2.5 s between phases, when the screen clears and it can't be hurt), about twice as long as any other boss. A sun and its moon line up as it goes, and each phase keeps something from the last. **Corona** (100–80%): it orbits the middle; its 4-arm **Spiral** and 24-shot **Corona rings**, and **Solar flares** (a line at each player and one more, 1 s warning, then a 40 px lane of fire, 25, +3% every two waves); three sun shots in the breathers. **Penumbra** (80–60%): a **moon** (a black disc lit only on its sun side) orbits it 300 px out; it can't be hurt and **soaks your shots**. **Lunar wake** (the moon speeds up, trailing slow shots), **Eclipse flash** (the moon stops and the sun swells for 3 s, then everyone **outside the moon's shadow** takes 40, +3% every two waves), **Gravity** (its old pull and 4-way shots); two Embers. **Umbra** (60–40%): the arena goes **dark**; you see 140 px round you and in three drifting **sunspots**, and outside them the dark drains 6/s. **Crescents** (blades that loop out through you and come back), **Closing umbra** (rings of shadow close in with two gaps), a **Dark spiral**; two Corona Priests. **Echoes** (40–20%): turns borrowed from earlier bosses, each with a see-through ghost of that boss floating over the sun: the **Colossus**'s stomp rings, the **Lich**'s twin soul beams, the **Storm Herald**'s strike rain and the **Singularity**'s pull and disk (gold embers spiralling out, its ghost a faint lensed black hole over the sun); the moon shoots back. **Last Light** (20–5%): the arena's **edges burn** and close in 160 px (you can't pass, and touching burns); a frantic 5-arm spiral, **Shadow nova** (two flashes, the moon closer and jumping round between them) and a **Solar storm** of flares and crescents. **Totality** below 5%: the moon is gone; it takes **+50% damage** while a **12 s nova timer** runs, and the nova hits everyone for 70% of their health and heals it back half of what it lost since the last nova (at most 2%) before starting again, so every cycle leaves it lower |

## Loadout preview

The lobby's loadout screen shows your loadout in a small live arena (top left): your character uses the chosen weapon
on three target dummies, so you can see how it attacks. The key it's playing (the weapon, or the ability or ultimate you clicked last) carries a **now playing** mark in your role's colour: a small ▶ by its name and a playhead sweeping along its bottom edge (standing full with Reduced effects). A **zoom meter** along the top of the window (it fades in when the mouse is over the preview; the mouse wheel works too) zooms the view from 50% to 200%. **MAIN FIRE | ALT FIRE**, two keys floating at the bottom middle (shown with the meter on hover), pick what the weapon shows: just its main fire (the default) or its Alt alone, pressed on a loop with no main fire (an Alt that spends what the main fire builds, such as Railgun heat, Jab stacks, a Dragon Fang or Blood Brawler mark, Embolus or Stone spears, gets it set up just before the press; the Spirit Chalk still draws the loop it ignites). Click an ability or the ultimate and both go off: the preview casts only that, every few seconds, without firing; either key brings the weapon back. It's the real game running privately on your computer (nothing is sent to
the other players), so it always matches how weapons and abilities really behave.

## Saved loadouts

The **star** tab just left of the **Loadout** tab (gold once anything is saved; hover for the count) opens **Saved loadouts** over the right column (click the star again, the **Loadout** key or Esc to close it; the
preview and stats stay beside it). Up to **6** favourites, each a card with its name, class and role, weapon, ability
and ultimate; the one you're wearing says **EQUIPPED**. The pencil after a name renames it (Enter or the tick keeps it, Esc cancels). **LOAD** puts on every pick at once (and the perks it was saved
with, if they're still unlocked); **X** then **SURE?** deletes one. **SAVE CURRENT LOADOUT** asks for a name (up to 20
letters, the class and role by default; Enter saves, Esc cancels). Favourites are kept on your profile. Hidden while
continuing a saved run (its seats are fixed); **< LOADOUT** or Esc closes the drawer.

## Menu backgrounds

The **main menu** always shows the **Holo Floor**: a starry red sky over a glowing horizon and a red grid floor rolling toward
you, light running down its lanes. It **moves to the music**: the playing track's spectrum rises as bars of light on the
horizon (bass at the screen's edges), and each beat flares the horizon, brightens the grid and sends a line of light down the
floor (any track, your own too; still with effects off or the background STILL, half as strong on Reduced). The **lobby** and the space **behind runs** show your own pick from the Look window >
Background. Turn on **Override menu background** there to show your pick on the main menu too.
The main menu's Holo Floor is dark blue, like the CRT screens (there's no hue to pick).
The picks are quiet and **reactive** (they answer your pointer and clicks gently in the menus), with no lines or grids so they
never fight the arena floor's grid: **Deep Field** (the free default: three layers of drifting stars the pointer tilts and
brightens; a click makes the stars it passes flare), **Nebula Drift** (rank 29: slow clouds of blue and violet light that
part round the pointer; a click sends a swell of light) and **Aurora Veil** (44: soft curtains of light along the top and
bottom edges that lean toward the pointer; a click sends a shimmer along them). Behind a run they show faintly and only drift
on their own (the arena floor does the reacting).

## Arena props

Regular waves (not bosses) put things in the arena; boss fights only get health barrels.
What's left at the end of a wave stays for the next, and each wave tops it up; a boss wave clears it all away.

| Prop | What it does |
|---|---|
| **Crate** | The crates each wave brings land on a square of the 50 px floor grid, a partner (45%) on the next square over; a regular crate is 44 px, an iron-bound one 48 px. (With Visual effects on, crates, barrels and the chest draw as glowing holograms: glowing wireframes that drop out, flicker and turn pink as they wear; amber for a crate, orange for an iron-bound one, so breakable cover never looks like the cool-coloured walls and pillars, which can't be broken; off keeps the wood.) Cover. Enemy shots stop at it, red aimed shots included, and each one wears it down (8 hits + 1 per 3 waves); your shots fly over it. You and enemies can't walk through it, but you can **push** it (crates and barrels slide when you lean on them: Tanks 70 px/s, Dps 50, Healers 34, enemies a crawling 3, and they path round crates and barrels when there's any way past, even a long row or a pocket of them, shoving only when boxed in; an iron-bound crate gives at 60% of that; they stop at walls, the arena's edge and each other). Two often stand side by side as a little wall. 2 crates at wave 1, one more about every 6 waves, up to 10 |
| **Iron-bound crate** | From wave 23, about a third of the crates: twice as tough |
| **Barrel** | Rare (from wave 5, a 25-45% chance of one every two waves, sometimes two from wave 39). Any shot or blast sets it fizzing; 0.4 s later it blows up 120 px round: 40 + 10 per two waves damage to enemies, 16 to players caught in it (scaled like enemy damage), smashing crates and setting off other barrels. It can be pushed like a crate, fizzing or not |
| **Health barrel** | Boss fights only: a green barrel with a white cross drops in every 18-26 s (the first after 15 s; two at most), away from the boss. Shoot it or walk into it and it breaks into 2 health orbs |
| **Treasure chest** | An **Upgrade chest** (green, chevrons on the lock; every chest bursts open with a flash and rings, its emblem climbing out and shooting off; chests come in kinds, each its own colour and emblem: Upgrade, Gold, Vector, Relic, Piece chest, Buildables; the others are Expanding-arena nodes). Rare (8% of every two regular waves from wave 3; always on the first wave after a boss if a player has the Contraband perk). Touch it to open it: **each player gets their own item**, a card spinning in a beam of its rarity's colour that **only they can see** (uncommon to very rare from their own pool, or a 20% chance of a legendary from a boss met so far or the next one). Walk onto it to see its card (mid-wave or between waves), then **F** (Interact) takes it or **B** throws it away. Items never despawn, and **the wave waits** until the chest is open and every item is dealt with (anyone down gets back up to go and collect) |

## Arena layouts

Each **boss cycle** (the regular waves between two bosses) gets one of six fixed floor plans, picked at random and never
the same one twice running. Boss waves, Data Gates and the loadout preview are the plain open
arena. A teal chip above the wave plate names the layout. Mockups: [`Docs/Mockups/arena-layouts.html`](Docs/Mockups/arena-layouts.html);
design notes: [`Docs/ArenaLayouts.md`](Docs/ArenaLayouts.md).

- **Walls and pillars** (neon tubes with a white-hot core and diagonal hatching, the phosphor vector look of [`Docs/Mockups/arena-layouts-digital.html`](Docs/Mockups/arena-layouts-digital.html), lit in the colour of the boss the cycle leads to: Warden amber, Hive Queen honey, Twin Reapers dusk pink, Singularity violet, Colossus sandstone, Duelist silver, Lich grave green, Storm Herald storm blue, Devourer raw red, Eclipse corona gold; the floor and its grid take a faint wash of it, brightest in the middle; with Visual effects on, each wall and pillar casts a soft glow of that colour onto the floor next to it) are solid: you and enemies can't walk through them, and they stop **every** shot, yours
  and theirs. Crates stay as they are: they wear away under enemy fire and your shots fly over them. Each layout
  starts the cycle with its own crates; random crates and barrels still turn up, clear of the walls.
  Enemies path round walls to get at you, and so do the ones heading somewhere of their own: squads keeping
  formation, the Warden's Riot Shields (herding you or marching in a Riot Line), Guards and Rioters, and the
  wandering Spinner (its random spots stay clear of the walls). A Shade never blinks into a wall.
- **Edge gates.** Some layouts wall the edges of the arena. **Player gates** (teal chevrons) are the gaps you can wrap
  through, and each comes out of the matching gap on the opposite edge. **Enemy gates** (stretches of the edge wall that look like the rest of it: red is
  kept for breaches) are where enemies come in; squads form up in front of one. A layout with open edges lets you wrap anywhere
  and enemies come in along the edges it allows. Narrow Pass and Tiny Arena close the arena, so the edge
  gates drop away (the inner walls stay).
- Layouts are always on (the starting room only; the grid rooms have their own pillars and wall stubs).
- Enemies that walk steer round a wall that's in their way instead of pushing at it. Standing against a wall or in a corner
  doesn't fool them: with nothing in between they still come straight at you.
- **Touching a wall** (any wall, pillar or cover, in every arena) makes it crackle: jagged arcs of electricity crawl
  along its face from the touch, with sparks and a little bolt from you, in the wall's colour, plus a low neon hum.
  Everyone's touches show, and you hear the strongest one, quieter the further it is from you (gone past 700 px).
  The hum uses the Effects volume.
- With a layout up, regular waves have **30% more enemies** (waves 1-8 on Normal: 16, 13, 16 and 17 instead of 12, 10, 12
  and 13, two waves each), because the walls and gates make the crowd easier to read. Boss waves are unchanged. The number is
  `LayoutEnemyMult` in `Balance.cs`.

| Layout | Edges | What it is |
|---|---|---|
| **Pillar Ring** | Centre gates | Eight one-square pillars round an open middle (gaps about 125 px), crate pairs in the corners |
| **Twin Lanes** | Sides only | Two long walls with a 100 px door in each: a middle corridor and two outer strips. You can only wrap left and right |
| **Cross** | Twin gates | A pinwheel of four walls (each arm half a square off the middle line): four rooms round a 200 x 100 px hub, a crate pair in each |
| **Islands** | Open | Crate clusters (a heavy 2 x 2 block in the middle); the layout wears away as crates break |
| **Funnels** | Open | Two walls near the sides with two 150 px gates each, a 2 x 2 block in the middle; enemies only come in from the left and right |
| **Bunker** | Centre gates | A 400 x 300 fort with a 100 px door on each side and a hold point (ring) inside |

Every plan sits on the 50 px floor grid: walls one square thick with their edges on grid lines, the edge wall half a square,
crates one to a square on its middle, gates between lines (the Cell Editor's Hub tab snaps to it).

Edge sets: **Centre gates** (one player gate in the middle of each edge, two enemy gates beside it), **Twin gates** (two
player gates per edge, one enemy gate between them), **Sides only** (top and bottom walled, so you can only wrap left
and right; enemy gates on all four edges).

## Paths

With the Expanding arena off (Simple Arena), after each boss (and its rewards), the host picks one of **two paths** for the ten regular waves before the next
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
| **Swarm Fields** | 2 | Mostly melee rushers (Swarmers, Brutes, Lungers), twice as many, at 60% health |
| **Sniper Ridge** | 2 | Mostly Snipers, Mortars and Grunts; hardly any melee |
| **Elite Gauntlet** | 3 | Half as many enemies, every one elite |
| **Burning Grounds** | 3 | Fire patches flash a warning ring, then ignite and spread for a few seconds; they burn you (10 damage a second, plus 3% every two waves on Normal) and the enemies |
| **Sanctuary** | 1 | Healing pools (10 HP a second) appear around the arena; 20% fewer enemies |
| **Frozen Lake** | 2 | You slide with momentum (speed builds up and carries after you let go); enemies are 10% slower |
| **Blood Moon** | 3 | No health orbs and no regeneration, but every kill heals its killer 2 HP (+0.1 every two waves) |
| **Fogbound** | 2 | A mist hides everything more than about 350 px from every player (shared sight: stay close) |
| **Narrow Pass** | 2 | Glowing canyon walls close in to a 1120 x 720 pass: no wrapping round the edges, and bullets stop at the walls |
| **Bazaar** | 1 | The usual enemies, but every kill drops gold; its prize is a shop (below) instead of the heal and rerolls |

A chip above the wave panel shows the path and the waves left. The
mist rolls in and the walls close in during the countdown before a path's first wave, and lift before the boss.
Design notes and the decisions behind them are in `Docs/Paths.md`.

### The Bazaar's shop

On the Bazaar's waves every kill gives **every player the same gold** (0.3 per point of XP the kill is worth: about
325 each over waves 11-20, rising to about 1000 by wave 80); your gold shows above the wave panel. After its last
wave, everyone gets a shop before the boss:

- **Two categories** open first: **Upgrades** (your shelf) and **Services**, each panel showing a peek of what's inside and the
  cheapest price. Click one (or arrow keys and Enter) to open its **sideways carousel**. The item in the middle is big and shows what it
  changes as the green readout on the card (old ▸▸ new). Click a side card, use the arrows, the mouse wheel or the arrow
  keys to bring one to the middle, and click the middle one to buy it. **< Back** (or Esc) returns to the categories; the
  two tabs switch between them. Add stock jumps to the new cards.
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
instead; Boss Hunts and runs saved before the Campaign use the open arena). Design:
[`Docs/ExpandingArena.md`](Docs/ExpandingArena.md). It starts as the Classic box, already walled (no wrapping round the edges) with the camera following you at 1.5x
(scroll to zoom out), and grows as you play:

**Hits harder once it grows.** Once the first room is built, everything that hurts you on a regular wave hits **1.3x** as hard (boss waves are in the hub alone and unchanged).

**Crowds on a big map.** Enemies come in within 1500 px of a player (never a straggler across the map), in the emptiest room there, so rooms you built get their share. Waves 1-20 add up to 2.4x enemies on a big map (1.1 per extra hub of floor); later waves up to 1.4x.

- **Your own drops:** in co-op every player gets a **full copy** of each drop: coins, vector pickups, health orbs and Magnets (a Gold chest's coins and a breach's gold too). You only see your own and only you can pick them up (your Magnet pulls only yours); a teammate's copy stays where it fell. Every 8th kill's 1-vector pickup is the killer's alone. A player who leaves takes their copies with them. **As the next wave starts, your own coins, vector pickups and unclaimed chest cards still lying about come to you** (your coins stream in and **+n GOLD COLLECTED** pops over you), so readying early loses nothing; a vector pickup waits while your stock is full, a chest card that no longer fits your build is thrown away, and health orbs and Magnets stay where they are.
- **Gold coins:** every kill drops its gold as coins (5-gold and 1-gold, at most 6; a boss's or an elite's last coin carries the rest). They never despawn, spin and glint on the floor in a gold neon outline over a soft shadow (vector pickups and health orbs get the same in cyan and green), and drift to you within 90 px. **Hover:** put your **cursor** over a coin or a vector pickup (not gadgets, orbs or cards) and you take it on the spot, however far away you stand (bots don't get it).
- **Vector pickups:** a rare drop that adds vectors (a cyan square that draws its own outline; big ones have an amber ring). A normal kill drops one 1 time in 60 (+6), an elite 1 time in 8 (+12), a boss always (+12). Like coins they never despawn and drift to the nearest living player within 90 px, but only one whose stock isn't full: it waits for you to spend some. While you're full, pickups near you show **FULL** (a Stash's **+10** too) and the **VECTORS** chip turns amber and pulses. The vectors go to whoever collects it, never past their cap. Whenever your vectors go up, **+n VECTORS** pops over you, shards fly into the **VECTORS** chip, and the chip lights up and counts up; the show scales with the amount (a +1 is a small blip, +12 the whole thing).
- **Gadgets:** you carry one gadget in your free hand and up to **4 more in a bag**. **Tap X** to use the one in hand; **hold X and let go** to throw it to a teammate where you aim (up to 340 px, a dotted arc shows the landing; they catch it or pick it up with the time and uses it has left). **Hold C** to open the **gadget wheel** at your cursor (Settings → Keybinds → **Gadget wheel**): a slice for the gadget in hand and one for each in the bag; let go on a slice to swap it into your hand, in the middle to cancel. A chip over the keys shows what's in hand (its time or uses: "GRENADE x2") and a row of icons for the bag, one tile per slot with its gadget and its uses or seconds left, so you needn't open the wheel to check; a used-up gadget is replaced from the bag. Going down drops the one in hand at your feet. **Gadget chests** (red, a magnet on the lock) give every player one gadget (a Lodestone 3 times in 27, every other kind 2 in 27); it goes in the hand or bag, else it spills at your feet as your own copy. Every gadget but the Lodestone also **drops from kills**: 1 kill in 45, an elite 1 in 5 (never a boss), rolled for each player, a kind they don't already have.
  - **Magnet** (red horseshoe; timed, 60 s): switched on, every coin and vector pickup on the map flies to you (ahead of anyone else's reach); tap again to switch it off and save the rest.
  - **Energy Drink** (lime can with a bolt; timed, 60 s): switched on, you move **60% faster** (speed lines trail you, a lime ring shows the time left).
  - **Shield Generator** (blue disc; timed, 20 s): switched on, a blue dome **66 px** round you wipes out every enemy bullet that comes into it (not beams or unblockable shots).
  - **Cloak** (indigo; timed, 15 s): switched on, enemies can't pick you out: you fade and they shoot at where they last saw you (as the Ninja's Vanish).
  - **Overclock Chip** (gold chip; timed, 30 s): switched on, **+40% fire rate** and your ability cooldowns run 40% faster.
  - **EMP** (ice-blue coil; **2 pulses**): tap to send a pulse **280 px** round you: every enemy bullet in it is wiped out, enemies are stunned **1.6 s** (bosses and mini-bosses 0.4 s) and take 20.
  - **Lodestone** (cyan, a house in a ring; **3 uses**): tap to cast for **3 s**, then you're set down by the hub's middle and one use is spent; a hit, a swap or tapping X again breaks the cast (the 3 s start over, no use spent); it won't cast in the hub.
  - **Medkit** (white case, red cross; **3 uses**, was the Defib Kit): tap to **clear your wound** and heal **30% of your max health**. It isn't spent while you're at full health with no wound, and taps 0.6 s apart count once. It no longer revives anyone.
  - **Throwables** (**3 charges** each, one every 0.6 s; a tap lobs one to your cursor, 60 to 340 px): the **Grenade** (khaki) bursts for **90** in 95 px (half at the edge) with a small shove; the **Smoke Bomb** (teal can) opens into a 115 px smoke cloud for 7 s: enemies in it are slowed and can't attack (bosses only slowed); the **Gas Grenade** (purple can) opens into a 100 px purple cloud for 6 s that deals **22 a second** and slows 30%; the **Incendiary** (a burning bottle) leaves **85 px of burning ground** for 5 s (Flame Wall damage, it doesn't block bullets). The **Land Mine** (olive, a blinking light) lies armed where it lands for 45 s; the first enemy to step on it sets it off for **100** in 95 px (the Flak Cannon's mine burst, which also shreds enemy bullets). Their damage grows 15% a wave with your damage bonus, as enemy health does.
- **Vectors**: every player holds a building budget (chip under the wave plate, "Vectors 40 / 70"): pieces are free; vectors pay for gates, mystery doors and the Builder Shop's cards.
  You start with 40 and can hold 70; each cleared wave gives 4 (8 in Quick Play), every 8th kill drops a small 1-vector pickup where the enemy fell, and a cleared boss wave refills
  everyone to the cap and adds 20 on top. Two cards raise the cap, in the Expanding arena only: **Deep Inkwell** (uncommon, +20,
  stacks to 4) and the milestone card **Architect** (+40 and 20 vectors now, stacks to 3). The **Black Market** also sells a **Vector pack**: +20 vectors for 50 gold, as
  many as you can afford, and it can take you past the cap.
- **Grid** (**the only building mode**: the lobby's Building row is gone since Nov 18). Design: [`Docs/GridRooms.md`](Docs/GridRooms.md) and [`Docs/GridPieces.md`](Docs/GridPieces.md). A saved run from before keeps its old building mode. Rooms are built on a grid from **pieces** (below): every room hangs off a glowing **door spot** on a built room's wall. A door always sits in the middle of one cell: where rooms share several cells of wall, it goes in the middle cell (of the two middle ones, the one on the new room's side).
  - **The core**: the run starts with the start room grown to 4 x 3 cells (2400 x 1800) by four free rooms round it, the walls between them open, so there are 14 door spots round the core. Every room sits on one lattice of 6-tile cells.
  - **Core layouts** ([`Docs/Mockups/core-layouts.html`](Docs/Mockups/core-layouts.html)): the core has one of ten hand-made floor plans of walls, pillars and crates: **Colonnade** (pillar rows down a nave), **Citadel** (two broken rings of wall round the gate), **Circuit Board** (thin traces between pads, two chip blocks), **Bastions** (a walled fort in each quarter, open toward the middle), **Terraces** (stepped walls like stairs down to the gate), **Shattered Plaza** (broken wall shards and pillar rubble, turned 180° rather than mirrored), **Hedge Maze** (a maze ring round an open middle), **Reactor** (a pillar ring round the gate, spokes and four coolant towers), **Trenches** (long staggered trench walls with crate lines) and **Pinball** (bumper pillars of every size and short deflectors). One is picked at random each run; the Campaign picks a new one (never the last) at each map reset, so every faction gets its own, and its ten maps see all ten. Every plan keeps the 14 door lanes and the gate's floor in the middle clear (bosses fight there), and sits on the 50 px floor grid: walls one square thick with their edges on grid lines, crates one to a square (the Cell Editor's Core tab snaps to it too). The plan takes the place of the boss cycle's arena layout in the hub and of the core rooms' random cover; its crates go down with the cycle's first wave.
  - Built at once. Teleporters, gates and traps work on any room.
  - **Nodes and chests** sit at the middle of a room-sized cell, so building out along the grid claims them; a chest or relic stands in the middle of the cell it was built in (in a 2 x 2 room, one of its four cells, not the room's centre).
  - Bots build on door spots too.
  - **Pieces** (in progress, [`Docs/GridPieces.md`](Docs/GridPieces.md)): in a Grid run the hand holds **piece cards** (1 to 4 cells, plain or with **modifiers**, rolled whenever a card is offered, each on its own chance (30% floor and combat, 20% loot and one-way) so a piece can carry none, one or several: a **floor** (the room gets that ground, such as Burning Grounds) **combat** (enemies wait inside) and **loot** (a chest of one kind is guaranteed in the piece, instead of the usual 30% room chest) and **one-way** (every door into or out of the piece but one lets you through one way only, in or out, shown by amber chevrons, and by an amber arrow on the build map; walking into one from the wrong side gives a soft thunk-and-buzz and a small amber ring; while placing a One-way card its ghost shows the arrows each door would get and a green two-headed arrow on the door that stays both ways; the door it was built through always works both ways, and a piece with one door is both ways, as is any door into a mini-boss room or a dead-end room, so you're never stuck after a fight; players, bots and enemies all obey them, shots fly through); the Builder Shop's last card always carries one and each modifier adds 8 vectors to a card's price; more kinds slot in through `PieceMods`, [`Docs/PieceMods.md`](Docs/PieceMods.md)) and one-cell service cards. They come as the room cards do: a **starting pick** as the run starts, before the opening GET READY (the launch pauses after the beam-in until everyone has picked, then counts down 3 s; two random pieces of different shapes, 1 to 4 cells, modifiers rolled: take one, no reroll; the cards land face down and roll through the shapes before locking on), then a **wave pick** after each wave (take one of two: the round's basic piece or a dropped card; no reroll, after any boss pick), wave drops, chests, the Builder Shop and the boss pick. Playing one is free. The build screen's first tool is **Pieces**: pick one from your hand, point at a glowing door spot, **R** turns it, click to build. Drawn rooms are refused in a Grid run. Service cards and the Builder Shop can't be built on a chest, vector chest or relic node.
  - **Build map** (Grid): the core is drawn as one room; a faint lattice of cells fades out from the map; a piece you place slides in from a cell out and slams into the wall (a flash, the wall pulsing gold, sparks at the door). The tool rail shows **Teleporter** and **Trap** only while you own one; there is **no Open wall** in a Grid run.
  - **Mystery doors** (Grid): the room behind is a piece on the lattice, any of the nine shapes (small: a single; medium: a domino, corner, square, T, S or L; hallway: a line of 3 or 4); a shop, Black Market, Arms Dealer or Medic behind one is exactly one cell.
  - **Relic Gauntlet** (a hallway behind a mystery door, 40%): a line of 3 cells with a faction relic on a pedestal in the middle and a chest at the far end. The outer cells have void pits, wall turrets and moving lasers (two sweeping rails, then a spinner); a laser hurts and the middle cell is safe.
  - **Surprise and breach rooms** (Grid): a boss's surprise room is a 4-cell piece on the lattice (an L, a T or a square), a breach's Stash is the one cell behind it, and the gold breach's room and the Engineer's Breach land on the lattice too.
  - **Mini-bosses**: each boss cycle a sealed two-cell mini-boss room waits on the grid, one cell out from the map. Build a piece into the cell between to open it, then walk in to wake the coming boss's faction mini-boss (as before: the room locks, beat it for a prize pick).
- **Building rounds**: every cleared wave opens a round (the wave box's **Build** tab or build panel, or **Build** on its minimized strip; the countdown never waits
  for it, though Auto-ready waits until you've opened it while you hold pieces). Open it to place pieces from your hand on the map (above: **Grid**); everything you place is **built the moment you place it**, any number a round.
  **Close** closes the window, **Done · ready up** closes it and readies you; either way the wave box comes back as its minimized strip. Nothing is built mid-wave.
  (Room cards, Free draw, Open wall, wall push, Team vote and Team purse were removed on Nov 18.)
- **Side walls**: every built room grows walls out from its own outer walls, one design rolled per room
  (the same on every machine): **Fins** (short stubs, one per ~5 tiles of wall), **Baffles** (long walls from alternate sides, a
  zig-zag with a lane at each end), **Pincers** (a pair facing each other, leaving a 3-tile choke) or **T-fins** (stubs with a
  crossbar: a pocket of cover each side). They stay 2 tiles off doors and corners and off the room's middle, avoid nodes, and
  any obstacle in their way is left out. They block shots and movement like any wall. A narrow strip (3 tiles or less) gets stubs
  from alternate sides that always leave a 1.5-tile lane; a small room gets smaller ones, and plain fins when its design won't fit past a door.
- **The build screen's layout**: the tools down the left (**Pieces**, then **Teleporter** while you hold one, **Gate**, and **Trap** while you hold one; keys **1-4**) with a
  key to what the map's colours mean under them; the map in the middle (scroll to zoom, middle drag to pan) drawn as the real floor: each room's
  tile grid, the walls and side walls with their door gaps, and the cover, the lattice's door spots, this wave's breaches, gates and portals, each named in the map key;
  on the right your vectors, the tool's name and live feedback, your hand of pieces, and **Close** and **Done · ready up**.
- **The build screen's motion** (still with Reduced effects): it switches on like a tube (a bright line opens up and down), the tool rail and the
  panel slide in, the map prints top-down under a scan bar and your vectors count up. The piece under the mouse glides between door spots with
  marching edges and spins a quarter turn on **R**; a click where it fits nowhere shakes a red cross. The card you play flies from your hand to its
  spot and the hand closes up behind it; after the slam a ring rolls out, the new door spots pop in one by one and **+1 ROOM** rises. The tool
  frame slides between tools and the panel title types in; spending vectors rolls the number down, floats **−8** and drains the bar.
- **Buying a mystery room from the build map**: hover a mystery door's diamond for its price, click to open it from wherever you stand, or **right click** to save it (right click your
  own saved door to let it go); a saved door carries a lime **SAVED** tag on the build map (**SAVED · YOURS** for your own). Only while the build round is open; the same price, rooms and one-save rule as **F** and **B** by the door.
- **Room kits**: a one-use modifier the team holds (up to 6; the Build step counts them). Spend one in a building round
  (**Room kits** button on the build window): pick a kit, click a plain Combat room, and it becomes a **Shop** room or a **Floor**
  room (a path's ground and rules inside that room only, and its XP bonus). One change per room, permanent; floating rooms keep their job.
  A cleared **boss** wave gives a Floor kit (a floor not on the map yet when one is left), a won **challenge room** gives a Shop kit,
  and the **Black Market** sells a Floor kit for 200 gold.
- **Minimap**: top right (the Map key, M, cycles it, the full map and no map), a fixed-size window that follows you (centred on you, held inside the map; the whole map when it fits). It's a holo tactical screen: a faint lattice grid, scanlines and a sweep (enemies show only where it has just passed: each pings as the line crosses it, stays a second, then fades; bosses always show), bracket corners, a *MAP · W<wave>* tab with the hostiles left, players as arrows along their aim (yours with a pulsing ring), and the box your screen shows as corner brackets; the full map adds sector letters and numbers along its edges. Gold coins show as small gold dots on it.
  Anything beyond its edge is clipped, and teammates off the window show as dots on its edge.
- **Room fog** ([`Docs/FogOfWar.md`](Docs/FogOfWar.md)): every room the map gets after the core (pieces, mystery-door rooms, surprise
  rooms, breach Stashes, the mini-boss room) starts **unseen**: dark with a **?** on the minimap and the build map, and under a dark,
  slowly hatched veil in the world (its walls, doors and breaches stay bright). You see through a **line-of-sight cone**: 90 degrees the
  way you aim, 9 tiles long, plus a 1.7 tile circle round you; walls and fins stop it, so you can look in through a doorway. Everything
  the cone sweeps over is **explored for good** for the whole team (it stays dimly lit and lightens the room on the maps), but awake enemies
  and orbs in an unseen room show only while somebody's cone or circle is on them. Things that never move (room traps, coins, sleeping groups) **stay shown once the ground under them is explored**, even if the room isn't fully clear. Once about 90% of a room is explored the
  whole room is seen (a gold scan line lifts the rest of the veil). A sleeping group wakes when a player sees it from within 6 tiles.
  Enemies still spawn in unseen rooms. The core, and the room kept through a map reset, start seen. With nothing to fight, bots walk
  to the nearest unexplored spot (nearest by the walk through its room's door), looking that way.
- **Dormant groups**: when a piece card with the **combat** modifier is built (a plain or floor-only piece holds far fewer: each of its slots has a **20%** chance, and may hold none), each of its cells' **sleeper slots** (2-4 a cell, fixed by the cell's layout) has a **65%** chance, and **every cell is guaranteed at least one group** of holding a sleeping group of the faction
  enemies of the wave it was built on (one type per group; 3 enemies, one more every 15 waves, at most 6), clustered in the cell's middle.
  They stand still with drifting z's over them: no moving, no shooting, and they don't hold the wave open. When the room is seen, or
  one of them is hit (from the doorway), every group in that room wakes and fights. A map reset takes them away with the old rooms.
- **Who drew what**: every built room has a thin line of its drawer's colour just inside its walls, and the minimap fills it in their
  colour.
- **Health shrines and room chests** (owner Nov 20): the **health shrine** is the only node the map places now: three ring the hub in the first boss cycle (one per side in turn) and each later cycle adds one. Build a room over its cell and the cell becomes the shrine: a stone plinth with corner posts and a floating green cross. **Walk up to it once per player**: it fills your health and adds **50% of your max health past max** (gold: a **+n** after your max on the HUD, a gold strip over the health cells, a gold ring round you). That extra health never fades; damage takes it after any shield, and going down loses it. A shrine you've used turns grey and still for you. **Chests come from rooms instead:** every basic piece a player places (modifiers or not) and every mystery room that opens to enemies or empty has a **30% chance to hold a treasure chest** in the middle of one of its cells. The kind sets how rare it is: Gold 30, Vector 25, Piece 16, Buildables 10, Upgrade 8, Gadget 8, Relic 3 (out of 100). Vendor rooms (shop, Black Market, Arms Dealer, Medic, Builder Shop) never hold one. The old chest and relic nodes below are no longer placed; an old save's still work.
- **Faction relics** (Nov 20): every boss cycle places **1 to 3 relic nodes** for that cycle's boss faction (Warden 1, Hive Queen 3, Reaper of Dusk 2, Singularity 2, Colossus 1, Duelist 2, Lich 3, Storm Herald 2, Devourer 1, Eclipse 3). Build a room round one and a **pedestal** rises in the cell with the relic floating above it in the faction's colour; walk up to take it (one relic per player, your old one is left behind). They give small boosts, e.g. Warden's Seal -12% damage taken, Brood Crown +12% fire rate, Stone Heart +30 max health. Details: [`Docs/RelicNodes.md`](Docs/RelicNodes.md).
- **Chest nodes**: every boss cycle the game places a **chest node**: a point 10 to 16 tiles straight out from the map (at least 8 tiles from other nodes;
  chest and relic nodes keep 6 tiles clear of every room), never within 10 tiles of the start room. Nodes are always in plain view: a big lit ring in the
  node's colour with its mark at full size and a beacon rippling out, and a bold square on the minimap. Shop, Black Market (Black Market) and mini-boss rooms are no
  longer placed on the map: they come from **service cards** and **mystery doors** only (old saves keep their sealed rooms, which still open when a built room
  shares 2 tiles of wall with one).
  A chest node is locked until you build a **room** that encloses it (a normal combat room with enemies); a claimed chest node holds a real treasure chest of one of four **kinds**, rolled from the map (its marker and tip show which):
  **Node tiers:** chest, vector chest and relic nodes hold better prizes the farther they sit from the hub's wall (measured in square zones, so a corner counts by its farther side): **inner** (under 15 tiles: Gold or vector chests),
  **middle** (15 to 30 tiles: Gadget chests (1 in 4), Piece chests or Buildables chests; Buildables with room cards off) and **outer** (30+ tiles: Upgrade chests or relics). A node's tier shows round its
  marker as corner brackets in bronze, silver or gold and a row of three square cells, one filled per tier; its tip names the tier, and its minimap square gets an edge and halo in the same metal. The first cycle's ring round the hub is two inner, two middle and two outer nodes; each later cycle adds an outer relic and a chest whose tier takes turns (middle, outer, inner), so the better prizes are always out there to build toward.
  **Gold** (gold trim, a coin: bursts open like a relic node, a big spinning coin climbing out, and spills 14 gold coins worth 40 + 4 per wave for each player, for anyone to pick up), **Piece chest** (orange, a T piece on the lock: every player gets their own piece to **take** into their hand or **discard**, like an upgrade chest's item; the card says whether your hand is full),
  **Buildables** (pink, blueprint grid, a wrench: every player gets their own Black Market build card, a kind with room under the 5 cap, to **take** into their stash or **discard**) or **Upgrade** (green, chevrons: every
  player gets their own item, like a wave chest). It opens once and never holds the wave up. No cover stands on a node. Nothing respawns: new ones
  appear around the current map. A **vector chest** is the other inner-tier kind alongside Gold (cyan, chevrons on the base and the vector arrow on the lock). Every chest node holds a real treasure chest, vector chests included: once a room encloses one, touch it and it bursts open (flash, rings, the lid up, the vector arrow climbing out and shooting off) into 4 vector pickups (24 vectors) that fly to the nearest player. Nothing drops within 10 tiles of the start room.
  Bots build toward chest and relic nodes (and an old save's sealed rooms; they don't walk to chests' loot).
  Old saves with shop or Black Market nodes still work: a room round one becomes that shop.
- **Relic nodes**: every other boss cycle (the first, third, ...) also places a **relic node** (a violet diamond; once claimed, a violet **Relic chest** with cut corners and a gem on the lock). Build a room round it and walk up to
  it: **you** get its relic in your **relic slot** (one each, never shared; it stays for the run, saves included). If your slot was full, your **old relic is left in the node** in its place for anyone to take (walk away and back to swap again). Relics are
  separate from the bosses' weapon mods. The HUD shows yours as a **RELIC** chip above the wave panel. Each relic node rolls one of the relics below (its tip on the build map names it:
  - **Wall Bounce**: your shots bounce off walls and pillars once instead of stopping, keeping 70% of their damage (homing turns off after the bounce).
    Enemy shots, grenades and other lobbed shots, and stuck shuriken don't bounce.
  - **Backup**: the first time you would go down, you get straight back up with half your health and 2 s of grace. Then it's gone (your slot is empty).
  - **Hallway Runner**: you move 25% faster in hallways (boxes 3 tiles wide or less).
  - **Thorns**: an enemy that hurts you by touching you takes 14 damage back (times your damage stat), once per hit.
- **Map reset (Campaign)**: every boss you clear starts a **fresh map**. The rooms, openings, portals, gates, traps,
  nodes and mystery doors go, and the team stands in the bare hub again, so a 100-wave run plays on ten maps of about ten
  waves each. First the **KEEP ONE ROOM** vote opens (15 s, it holds the break): every room is a card with its outline to scale,
  click **KEEP** on the one you want. Most votes wins, a tie goes to the host's pick, no votes keep the biggest; it ends early once
  everyone has voted. The kept room is rebuilt free next to the hub, same shape (turned if it fits better), kind, theme and look.
  Your **room kits are kept**, and each player gets **half the vectors they spent on the old map back**, on top of
  the boss refill. Coins still lying in the old rooms go to the nearest player. The run's last boss doesn't reset anything.
  Leaving during the vote saves it: Continue opens it again. The old rooms fold into the hub as blue outlines, the kept room
  builds in gold and keeps a thin gold rim, and a **MAP n / 10** banner shows the vectors you got back.
  ([`Docs/MapReset.md`](Docs/MapReset.md))
- **Surprise rooms** (Quick Play; the Campaign resets its map instead): every time you clear a **boss**, the map gets a **free** room, a 4-cell piece on the lattice the game picks for you: an **L**, a **T** or a **square**. It goes where it closes the most loops
  (and touches a dead end), so it mends the map's shape; nobody pays. It builds in with a gold flash and scan line, a **SURPRISE ROOM** chip names it, and it stays
  gold-rimmed on the arena floor and minimap for the rest of that wave. The shapes come round in turn. It counts towards the 24-room cap and
  none is given within 2 rooms of it. ([`Docs/SurpriseRooms.md`](Docs/SurpriseRooms.md))
  **Breach events**: on about half of the breach waves from wave 13 on (so waves like 13, 29, 37, 45, ...; never the wave after a boss) one of the breaches flashes **gold** instead of red. 20 seconds into the
  wave, or when it ends if that comes first, the wall behind that breach opens onto a free surprise room. The wave's other breaches stay where they were.
  **Breach rooms**: as soon as a breach wave's breaches come up (a second and a half in) every breach left (not the gold one) opens onto a small free **Stash** (one lattice cell, 6 x 6 tiles) behind its wall. Each Stash
  holds a **vector cache** (a teal diamond marked **+10**): the first player to walk in, during a wave or between waves, takes **+10 vectors** (up to their cap), once; a full stock leaves it for later. Taking it bursts the diamond and streams cyan shards into you. It has a block and a pillar for cover, builds in with a teal flash and
  scan line and a **STASH** chip, and stays for good (no vote, no cost; it stops within 2 rooms of the map's size cap, and a breach only opens on a wall with space behind it for a Stash). The breach itself **never closes**:
  each breach lets its share of the wave in (the wave's enemies split evenly over its breaches), then nothing more comes through it; if every breach has had its share the rest come in at the usual spots. When zoomed out, a teal edge arrow points to a cache nobody has taken.
- **Mystery doors** (Docs/MysteryDoors.md): every cleared wave puts **1 or 2 sealed doors** on the map's outer wall (at most **3** at once): a magenta-violet hologram over a dark plate, with corner brackets and a white **?**, and a **? price** tag over it (red when you can't afford it). They show as violet marks on the minimap and the build map (*MYSTERY DOOR* in its key). A room is already rolled behind each, but you can't see what it is. Stand by one and press **F** (Interact) to pay its price from your own vectors (**4 + 5 a cell** behind it: 9 for one cell, 24 for four) and it opens at once, **mid-wave too**; the room is yours, like one you placed: the hologram splits and slides into the wall and a ring goes out from the doorway. **What's behind** (never shown; roughly): a piece on the lattice. A third of doors open a **small room** (one cell), **never empty**: usually a pack of **25 of the current faction's basic enemy** (Rioters in a Warden stretch, Swarmers where the boss has no faction yet) **asleep** in the room's sleeper slots (they wake when you see them, as in any dormant group), and sometimes a **shop**, a **Black Market**, an **Arms Dealer** or a **Medic** (each 1 in 25). A third open a **medium room** (a domino, an L of three, or a square, T, S or L of four cells): usually **sleeping groups** (they stay asleep in the fog until a player's view reaches them or the room is revealed), sometimes **empty**. The rest open a **long hallway** (a straight line of three or four cells) with a **treasure chest at the far end**. Enemies opened between waves come out as the next wave starts; around a boss the medium rooms come empty, but a small room's pack still comes. Bigger rooms cost more. Doors nobody opened **expire when the next wave is cleared**, unless a player **saves** one: press **B** by a door (it turns lime, *SAVED* on its tag). Each player saves **one** door at a time (saving another moves your save, B on your own lets it go, and you can't take over someone else's); a saved door stays until it's opened. If a piece takes a door's space, the door gets another room behind the same spot, or goes. **Bots** open doors too: between waves a bot walks to the nearest door it can pay for while keeping 10 vectors back for building.
- **Teleporters (portals)**: buy a **Teleporter pair** (150 gold) at the **Black Market** (the Black Market room), then on the draw screen switch to
  **Teleporter** and click a room's **outer wall**, then another room's outer wall (the tool only lands on walls: it snaps to the nearest outer
  wall, shown as a ghost portal, and a click on the floor does nothing): the pair is **built at once** from your stash (no
  vectors). Like the classic arena's edge gates, each end turns **2 tiles of that wall into a portal**: a dark gap in its pair's colour inside
  corner brackets, with scanlines rolling out of it across the floor into the room (the build map shows the same, in small). Walk into it and you come out of the other wall, keeping your place along it
  (**enemies never use them**; bot players do). It must save at least 6 tiles of walking. Each end needs a connected room on
  one side and nothing on the other, 2 tiles of plain wall (no corner or door in it), nothing standing just in front, 150 px from a door and
  250 px from another portal; breaches never open on a portal's wall. At most **3 pairs**, not counted as rooms. **Dismantle** a pair in a build round: with the Teleporter tool, click either built end (it turns red) and the pair comes down, the teleporter going back to your stash. Bots don't build them. (Pairs
  from older saves, standing in the middle of a room, still work as floor portals.)
- **Black Market turret and traps**: the Black Market also sells a **Turret** (120), a **Landmine** (80) and a **Slow field** (90). Switch the
  draw screen to **Trap**, pick one from your stash and click a spot in a room (90 px off walls, 150 px from doors, 120 px from portals and other
  traps; at most 12 on the map). A stash holds at most 5 of a kind.
  - **Turret**: a heavy teal sentry on four feet, ringed in its owner's colour, with a twin-barrelled head that tracks the nearest enemy. In a wave it
    shoots the nearest enemy within 560 px, about 3 shots a second, each for 12 + 1.5 per wave (your shots: your mods apply, kills are yours).
    It fights **5 waves** (a lit pip under it for each wave left) and goes when the 5th is cleared. You can have **3** up at once. Enemies don't attack it.
  - The **Landmine** and **Slow field** have **no effect yet**. (The Spike trap is gone; an old save's placed ones are dropped.)
- **Shutter gates**: on the draw screen, switch to **Gate** and click a door (the gold squares): a gate is fitted at once,
  for **8 vectors**. Once fitted, stand within 150 px of it and press **F** (the Interact key) to shut it: a red laser grid fills the doorway
  for **8 s**, a wall to everyone (players and enemies); shots still pass. Then it needs **30 s** to recharge (an arc fills round its
  posts). Anyone on the team can work it. Enemies (and bots) route round a shut gate through another door; with no other way in, they wait at it. Plain doors only (not an opened wall), one gate a door, at most **6** on the map. A red bar marks
  each on the minimap, bright while shut.
- **Mini-boss room**: once a built room opens it, walking in, during a wave or **between waves**, wakes the **faction mini-boss** of the boss cycle it was placed in (the
  next boss's faction: the Warden's is the **Riot Captain**; factions without their own yet bring him too; the map shows its name), and the room
  locks behind you until it falls. Every 10 s of the fight a **health orb** drops somewhere in the room for each player inside. Beating it gives everyone a prize pick (sometimes a legendary) and the room becomes an ordinary one. It's optional:
  if the wave ends first, it goes (with any men it called), you're let out, and walking in later (even right after the wave) starts a fresh duel.
  A duel begun between waves is a real fight (it attacks, your minions and kit work); readying up still starts the next wave, and the duel
  carries on into it. It can't be started just before a boss wave.
- **Riot Captain** (the Warden's mini-boss, 65% of the Warden's health): a tower shield blocks the front and never breaks; it turns slowly, so get
  round it. In turn: a **shield charge** along a red lane (18 damage, knocked 220 px; **into a wall it's dazed 2.2 s**: no shield, +50% damage;
  otherwise it holds the shield low 1.2 s), **buckshot** (3 fans of 7), **tear gas** (3 canisters round you) and a **whistle** (2 Taser Guards at
  the room's far corners, at most 2 up). Below half health: a quarter faster, it charges twice, 4 fans, 4 canisters. Its theme is *Riot Line* (a marching snare over
  gritty fifths).
- **Shop and Black Market rooms**: with one connected, kills drop gold and the shop opens every intermission without holding the countdown. Buy
  upgrades and add stock in the **shop room**, buildables at the **Black Market** (one Buildables category, like the Arms Dealer and the Medic; it sells nothing else); selling, the Floor kit and the Vector pack are the shop room's only. Heals and revive tokens are only sold at the **Medic** (below). Gold and your shelf carry over between waves
  (the shelf is new each boss cycle). The **shop room** has no screen: its wares stand on **pedestals** along two facing walls, your shelf's
  unsold cards on one (up to 4) and Add stock, Sell upgrade, Vector pack and Floor kit on the other, each floating over a stone plinth lit in its colour,
  with a coin price beside it. Stand at one: a panel names it, says what it does and **F  BUY** buys it (Sell opens the sell list); sold-out spots
  stand empty until you add stock. **Every vendor room works this way** (shop, Black Market, Arms Dealer, Medic): no stall, no screen, no
  cover, a runner in its colour from the door(s), its wares on pedestals along the walls without doors. On the grid, counted from the wall
  facing the room it was built from: the **shop** has **two doors** (that wall and the one opposite), the **Black Market** and **Arms Dealer**
  **one** (pedestals on the other three walls), the **Medic** **three** (pedestals on one side wall). Nothing is built off, or opens through,
  a pedestal wall. Gold and the shop's open/closed state are as before: the room shops stay open until the wave starts.
- **Black Market**: its four buildables (Teleporter pair 150, Turret 120, Landmine 80, Slow field 90) on pedestals, each greyed once your stash
  of it is full (5).
- **Arms Dealer** ([`Docs/ArmsDealer.md`](Docs/ArmsDealer.md)): a tiny green room from its service card or a mystery door, every weapon mod on a
  pedestal. While the shop is open, buy **any weapon mod** for gold: **150** for your first stack of a mod, **250** the second,
  **400** the third (3 stacks at most, as before; a maxed one greys out). It sells nothing else. In the Expanding arena this is the only place to get weapon mods (bosses give the card pick instead).
  Its card comes like the other service cards: wave drops, chests and the boss card pick.
- **Medic**: a tiny rose room from its service card (wave drops, chests, the boss pick) or a mystery door, with a
  **Full heal** (40) and a **Revive token** (120; one at a time) on its pedestals. It is the only room shop that sells them (the Bazaar's shop, which has no rooms, still does).
- **Breaches**: a rare event: on wave 5 and every 8th wave after it (13, 21, 29, ...), once the first room is built, enemies come in through **breaches** in the outer wall: a stretch of wall cracks open
  and a **red holographic barrier** stands in the gap (you can't go out through it; red chevrons on the floor point where they come
  from, and a red dash marks each on the minimap). 2 open on a breach wave, one more per 3 rooms built (at most 6), 400 px apart or
  more, picked by the wave and the map so everyone sees the same ones. Every 600 px lattice cell's outer side is a spot (at its middle), so long outer walls
  break more often, and a **dead end** (a room with one door) more still: compact builds and loops have fewer places to be hit
  from. **Breach warning:** when the wave starts, before anything spawns, everyone's camera flies to each breach in turn (0.5 s),
  holds while it flashes with red rings (0.6 s; a siren, and a red BREACH 1/3 band at the top), then flies back to you (0.8 s), with
  letterbox bars while it's away; you can't move, shoot or use abilities until it's over (about 3 s for 2 breaches, 7.7 s for 6).
  A breach wave brings **three times** the wave's enemies (bosses not tripled), with room for 1.5x as many alive at once. An enemy
  uses a breach at least 350 px from every player when there is one. On every other regular wave enemies simply appear at a random spot in the rooms, at least 350 px from every player. Never on a sealed room, a shop, Black Market or Gatekeeper room, or on boss waves (the fixed boss arena). In the Campaign, where each wave is played twice, only its first leg is a breach wave.
- **Cell layouts** (Grid runs): every 600 px cell of a built room uses one of **18 hand-made layouts**: 12 open ones (Open Plaza, Four Pillars, Vault, Side Alcoves, Corner Nooks, Crossfire, Maze Lite, Pillar Field, Bunker, Barricades, Diagonal Cross, Fortress Corners) and 6 **room-like** ones with doorways (Corner Closets, Vault Room, Barracks, Corner Vaults, Hall & Alcoves, Safe Room). A lone cell picks any; in a bigger piece each cell's layout follows where the rest of the piece lies: a **dead end** gets a Safe Room, Barracks or Vault Room turned to face the way in, a **corridor** cell a Hall & Alcoves or Vault Room along the corridor, a **corner** cell Closets or Vaults, a **junction** an open one. Seeded from the cell's place (the same on every machine): walls and pillars, the middle kept clear for the chest, the door lanes open, **sleeper slots** for sleeping groups and **trap slots**. Each trap slot of a **combat** or empty room rolls (35% stays empty) a **turret** (only on a slot by a wall: an **embrasure** set into the wall, an armoured red half dome on a hazard collar with one heavy barrel through a turning slit; it aims 75° either side of straight out from the wall, so you can slip past it along the wall, and shows a thin sight line when it locks on; with no wall beside its slot (an open side or a door) there's no turret. It shoots the nearest player in its arc and range, in fog or not, and can be shot in fog too; your shots wear it down, **40 + 4 a wave** health, a red health bar over it once it's damaged, and it shudders, flashes and throws sparks on every hit, cracks and smokes as it wears down; it is a **solid post** (players and enemies walk round it), and it is wrecked for the rest of the map, dropping a coin each), a **spike plate** (whole squares of the floor's 50 px grid, like the slow field; two plates in a cell never overlap: one that would takes a smaller size or another square nearby, or stays empty; a red-hatched grate whose slots jab steel points on every tick: hurts whoever stands on it) or a **slow field** (a violet grid in corner brackets that sags under you like a gravity well: slows you to 55%); shops, the Black Market and chest rooms get none. **Bonus loot**: each cell also has 1-2 loot slots; when a plain (combat or floor) room is built, each slot has a **60%** chance of a stash lying there: **15 + 2 a wave** gold in coins, now and then (25% each) a small vector pickup or a health orb. Everyone gets a full copy. Walls, pits and the rolled cover of rooms without a layout (pillars, bars, Ls, blocks) all sit on the 50 px floor grid, walls one square thick (the Cell Editor snaps walls to it). Doors between rooms are 200 px (four squares), their edges on grid lines, and a cell wall beside a door ends flush with its edge. Mockup: [`Docs/Mockups/cell-layouts.html`](Docs/Mockups/cell-layouts.html).
- **Cell variants and pits** (Grid runs, [`Docs/CellVariants.md`](Docs/CellVariants.md)): a layout can have **variants**, other versions of it; a cell of a plain **combat** piece whose layout has one takes it **40%** of the time (seeded from its place: the same on every machine; never the core, shops or other service rooms, surprise rooms). The first are **pit variants** of 8 layouts: **Sinkholes** (Open Plaza), **Four Wells** (Four Pillars), **Island Vault** (Vault), **The Chasm** (Side Alcoves), **Corner Drops** (Corner Nooks), **Crossfire Gap** (Crossfire), **Stepping Stones** (Pillar Field) and **Pit Halls** (Hall & Alcoves). A **pit** is a void in the floor: black, with a band of **gold-and-black hazard stripes** marching round its rim and a gold grid falling away inside; black with a gold edge on the minimap. **Shots fly over** pits. An enemy that is **shoved in** (knockback, charges, pulls) **dies** at once, the kill going to whoever hit it last, its coins landing on the rim; walking never takes an enemy in (they steer round pits), and bosses, mini-bosses and spawn portals hover over them. A **player** whose middle goes over a pit drops in (pulled past the lip, out of sight and out of control for **0.5 s**), then climbs out on the rim beside the spot, losing **15% of max health** (never below 1 HP) with **1 s** of invulnerability once out; a dash flies over a pit, but one that ends over the void falls. Bots keep off pits. Mockup: [`Docs/Mockups/cell-pits.html`](Docs/Mockups/cell-pits.html).
- **Obstacles** (rooms off the lattice): a built room comes with obstacles and bars of wall (pillars, bars, an L, blocks: one per ~220,000 px2, up to 4),
  laid out from the room's own box so every machine builds the same ones. They keep off the room's walls, its doors and its middle,
  so you can always walk round them; a room 3 tiles wide or less gets a pillar or block every ~500 px, alternating sides with a 180 px lane past each, none near a door or bend. A small or narrow room with doors at both ends still gets at least one
  (pillar or block, closer to the walls and doors), and a 400 px deep room between two doors gets a small pillar to one side.
- **How it plays**: regular waves use the whole map (walled, no wrapping, a minimap top right) and bring more enemies the
  bigger it is (up to 2.4 times on waves 1-20, 1.4 later); the map stops at 24 rooms. Boss waves are fought in the hub alone, as always.
- Runs in this arena are saved like any other: Continue brings back the map (rooms, opened walls, portals, floating
  rooms), the team's room kits and each seat's vectors and shop shelf. A room someone drew keeps their colour for
  whoever takes their seat.

## Data Gates

Once in every ten waves (each boss stretch) a **Data Gate** opens during the break: a dark disc of scrolling data in the challenge's colour, ringed by turning dashes, with a ring of pips that lights as the party steps in. It never opens under a wall, pillar or crate: after the 2nd, 4th or 6th wave of each boss's ten
(in the first stretch after wave 4 or 6), so never right before a boss. A card over it names the challenge on the
other side. Everyone alive stands in it to go through: it fills faster the more of you are in it, but only goes
when you all are, and not while anyone still has upgrade cards to pick (it drains until they have). The break's countdown waits while it's open; ignore it for 15 s (counting only while nobody's
choosing cards) and it closes, and the run carries on as usual.

Through the Data Gate is a **challenge**: the same arena with its own ground (a tint in the challenge's colour, a
rune circle, glyphs round the walls). A title card names the challenge and what it takes, and 3 s later it starts,
with an objective plate at the top of the screen:

| Challenge | Goal |
|---|---|
| **Defend the Crystal** | Keep the crystal in the middle alive for 40 s. Its enemies are mostly melee (Swarmers, Spikers, Lungers, Brutes; about 1 in 7 is a Grunt or Sniper), so it's about cutting off the bodies rushing it: two in three go for it (their hits and shots wear it down); its health shows as a ring round it. 600 health, +30% for each extra player. |
| **The Gatekeeper** (the Data Gates' mini-boss, in every arena) | Beat a mini-boss with its own boss bar: a ring of big orbs with a gap, three aimed fans, and a charge along a red line that ends in a burst. Below half health it attacks a third faster and rings twice. It has 65% of the Warden's health at your wave. |
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

The lobby's **Modifiers** tab (on the tab row after Difficulty; it shows how many are on) lists 19 switches that change the
whole run, for everyone. They sit in three carousel rows, **Easier**, **Risky** and **Harder**, headed like
the Loadout's rows (the tier, a few words and how many of it are on): three cards in view, a bare arrow at each end and the
mouse wheel to slide along. Each card has its tier's colour along the top, an icon, its name, what it does and an ON/OFF key
like Ready's; click anywhere on a card to switch it. The **Enabled modifiers** box along the bottom holds a key for each one
that's on, in its tier's colour (click one to turn it off; when they don't fit on one line they show only their icons, with
the name on hover). The host turns them on and off; the others see them, and changing one takes back everyone's Ready. Active
ones show as chips above the wave panel during the run.

**In Survival** every modifier works except two that have nothing to act on in its one walled room: **Tiny Arena**
and **Simple Arena**. Their cards are dimmed and say *NO EFFECT IN SURVIVAL*,
they don't count toward rank XP there, and no chip shows for them. **Zombie Infection** turns every Survival spawn into a
horde zombie of the clock's wave (zombies count for less of the spawner's budget, as in a horde wave); **Hunted** picks a
new quarry and **Second Wind** comes back on the clock's waves (every 30 s, and every 2.5 min); **Cluttered** fills the room
with 1.6x its crates. **Nightfall** covers wherever the camera is, in every mode and map size.

| Modifier | Effect | |
|---|---|---|
| Glass Cannon | Everyone deals 50% more damage, and takes 50% more | Risky |
| Tough Crowd | Enemies and bosses have 50% more health | Harder |
| Frenzy | Enemies and bosses shoot 30% faster (the bullet budget still applies) | Harder |
| Swift Foes | Enemies move 30% faster | Harder |
| Famine | Enemies never drop health orbs (bosses still do) | Harder |
| Lucky | Everyone starts with 3 extra upgrade rerolls | Easier |
| Head Start | Everyone starts at level 3 (its two picks waiting) with a free legendary pick from the first boss's pool | Easier |
| Practiced | Abilities gain mastery twice as fast | Easier |
| Bloodlust | Every kill heals its killer (2 HP + 0.1 every two waves, like Blood Moon), but enemies don't drop health orbs (bosses still do) and nobody regenerates (the Healer's aura stops healing themselves; it still heals allies) | Risky |
| Tiny Arena | After each boss, walls close in and take a quarter of the room (of its area), down to 40%. No wrapping round the edges while they're up, and bullets stop at them. They open for each boss's countdown and fight | Harder |
| Nightfall | Dark everywhere except 210 px round each player and a lantern beam (560 px, 48°) toward where each player aims; downed players lose their lantern | Harder |
| Hunted | As each wave starts, one player (at random, among those standing) is hunted: every enemy goes for them (a Roar still pulls enemies off), and they deal double damage. Red brackets turn round them, and the HUD chip says who. While they're down, enemies pick someone else | Risky |
| Death Throes | Every regular enemy (not bosses, nor things that give no XP) bursts into a ring of 6 slow shots 0.5 s after it dies, with a warning circle. Killing up close costs you | Harder |
| Cluttered | Every wave piles a lot more crates into the arena: 17 on wave 1, +3 every two waves, up to 48 (a normal run has 2 to 10), heavy crates from wave 15. They stay until enemy fire breaks them, so the field turns into a maze you have to wind through. Your shots still fly over them; enemy shots stop at them. Boss fights clear the arena, as usual. | Harder |
| Simple Arena | The open arena with its floor plans all run, instead of the Campaign's Expanding arena: no building rooms, and a path offer after each boss | Risky |
| Second Wind | Once per boss cycle (waves 1-10, 11-20, ...), each player who goes down gets back up by themselves 2 s later at half health; it's used before a revive token. The HUD chip shows whether yours is ready. While anyone holds one, everyone being down isn't game over | Easier |
| Zombie Infection | Every regular wave is a horde of green zombies, and the arena turns dark green. The count is the wave's usual x1.5 at wave 1, growing to x3 by wave 40, at most 60 alive at once (the rest come in as others fall), arriving faster so a bigger wave doesn't take longer; each zombie's XP is split so levels keep pace. **Seven kinds**: **Shambler** (wide triangle, slow, hits hard; from wave 1), **Runner** (narrow triangle, faster than you, packs of three; 5), **Spitter** (pentagon; hangs back and lobs acid that burns 10/s and slows 30%; 11, at most 4 a wave), **Bloater** (swelling circle; bursts into a spore cloud that slows 40%, on you if it reaches you; 15), **Crawler** (trapezoid; grabs and roots you for 1.5 s, 6/s, until you shoot it for 25; 17, at most 3), **Shrieker** (diamond; screams every 6 s, frenzying the zombies within 320 px: 30% faster for 4 s; 23, at most 2), **Mender** (cross; heals the zombies within 220 px by 15% every 3 s; 29, at most 2). **The dead rise:** a killed zombie has a 25% chance to get back up 3 s later (a green marker where it lies) at half health, worth no XP, and it won't rise twice; near a Mender it's 50%, in half the time. One that was burning or cut down by a melee weapon stays down (a Bloater never rises), and the wave isn't over until they're up and beaten. **Sounds:** a groan now and then from the horde, a wet rasp as the dead get back up, a spit, a grab, the Shrieker's scream, a Bloater's burst and a Mender's chime (volume: Enemies). Bosses, their adds and challenge rooms are unchanged. | Harder |

## Difficulty

The lobby's **Difficulty** tab (on the tab row after Loadout): the host picks one of five cards, and everyone sees it (changing it takes back
everyone's Ready). It applies to the whole run, is kept in saved runs (a continued run keeps its
difficulty), shows as a chip above the wave panel during the run (not on Normal), and is written in session
recordings. **Custom** lets the host set every number below with a slider or by typing it in the box beside it
(percent numbers are typed as percent: `12` is 12%); changing any number turns the difficulty into Custom, starting
from the preset you had. Each slider has a tick where Normal is, and a value shows red when it's harder than Normal
and green when it's easier. Hover a number for what it does.

| Number | Easy | **Normal** | Hard | Nightmare | Custom range |
|---|---|---|---|---|---|
| Enemy health (enemies and bosses) | 0.75x | **1x** | 1.25x | 1.5x | 0.25-4x |
| Shooters at once | 0.75x | **1x** | 1.25x | 1.5x | 0.25-3x |
| Boss health (on top) | 0.8x | **1x** | 1.2x | 1.4x | 0.25-4x |
| Solo enemy health | 70% | **85%** | 100% | 115% | 40-150% |
| Enemy damage (bullets, touching, hazards) | 0.75x | **1x** | 1.25x | 1.5x | 0.25-4x |
| Damage growth per two waves | +1.5% | **+3%** | +4% | +5% | 0-10% |
| Enemy count (regular waves) | 0.85x | **1x** | 1.15x | 1.3x | 0.5-2.5x |
| Enemy fire rate | 0.9x | **1x** | 1.1x | 1.2x | 0.5-2x |
| Enemy speed | 1.1x | **1.1x** | 1.21x | 1.32x | 0.5-2x |
| Elites (chance, from wave 23) | 0.5x | **1x** (up to 35%) | 1.5x | 2x | 0-3x (at most 90%) |
| Bullet budget | 0.85x | **1x** | 1.15x | 1.3x | 0.5-2x |
| Adds' fire in boss fights (share of the budget) | 40% | **60%** | 75% | 90% | 0-100% |
| Regeneration | 0 | **0** | 0 | 0 | 0-3 HP/s |
| Health orb drops (small) | 18% | **12%** | 9% | 6% | 0-50% |
| Revive time | 3 s | **4 s** | 5 s | 6 s | 1-10 s |
| Hit grace (after a hit) | 0.35 s | **0.25 s** | 0.2 s | 0.15 s | 0.05-1 s |
| Lives (respawns at the start of the wave you went down on) | 3 | **2** | 1 | 0 | 0-5 |
| Mechanics (which fights and enemies the run uses) | Easy | **Normal** | Hard | Nightmare | any of the four |
| Boss approach (the themed share of the wave before a boss) | 85% | **85%** | 85% | 85% | 0-100% |
| Approach ramp (build up over the four steps) | On | **On** | On | On | On / Off |

The two **boss approach** numbers are flavour, not difficulty: they're the same on every preset, changing them doesn't
make the difficulty Custom, and picking a preset keeps them. They're fixed at Blend (85% with the ramp): the lobby's
Blend / Fully themed picker is gone (Nov 18).

**Mechanics** is the run's tier: not a number but which fights and enemies it uses. Each preset sets its own; Custom keeps
the one it had and the host can pick another (slider or type the name). Normal plays as balanced. **Easy**: bosses have no
last stand (the Warden, Hive Queen, Singularity, Colossus, Duelist, Lich, Storm Herald and Devourer stay in their third stage
until they die, the Eclipse never reaches Totality; the Twin Reapers have none to lose), and the Warden's bar shows three
cells. Boss summons and escorts follow the tier too: each summon brings one fewer on Easy, one more on Hard and two more on
Nightmare (the Warden's rioters per door and guards, the Hive Queen's drones and eggs, the Lich's risen dead and spirits, the
Devourer's morsels, the Eclipse's court), and a boss wave's escort is 75% / 125% / 150% (Easy / Hard / Nightmare). Hard and
Nightmare will add boss stages and enemy variants (Docs/DifficultyTiers.md lists the steps).
The arena isn't picked here any more: the Campaign is the Expanding arena, the **Simple Arena** modifier keeps the open
arena with its floor plans (always on). (The Rooms arena prototype was removed Nov 18.) A run saved before the
Campaign keeps the arena it was saved with.

Easy is close to how the game played before difficulties (without the per-wave damage growth). Damage growth
multiplies with Enemy damage: Nightmare at wave 22 hits 1.5 × 1.5 = 2.25x as hard as a listed shot. Modifiers stack
on top (Tough Crowd, Frenzy, Swift Foes, Glass Cannon...).

## Quick Play

**The lobby's first mode, and where a new profile starts.** In the lobby the host picks **Quick Play**, **Campaign**, **Boss Hunt**
or **Survival** on the mode carousel; the lobby opens next time on the mode the host last picked (a new profile: Quick Play).
Picking it adds a **Quick Play** tab at the right end of the tab row: the run's shape (ten waves, the boss, endless), its rules, your Quick Play
records (played, won, best wave, bosses), and **the boss pool**: a row for each of the ten bosses, with its model and name once
you've beaten it anywhere, a **???** until then (on your first Quick Play the Warden shows, marked FIRST). It is laid out like the
Boss hunt tab: the pool on the left, a green **BOSS DRAW** scope on the right rolling through the bosses you've met (hover a row to
hold one there). The **Campaign** and **Survival** tabs share that layout: the Campaign's ten bosses (wave 10 to 100; BEATEN, REACHED
or LOCKED) with the next one to beat in the scope, its wave on a ladder and your Campaign records; Survival's factions in the order
they join (their time as the tag) with a **PRESSURE** scope plotting the threat over the clock (the hovered or furthest faction's
stretch lit, your best time a gold line).

- **Ten waves, then a random boss** as its own fight after wave 10 (like the Campaign's boss after a wave: it doesn't use up a
  wave number). The HUD counts the waves n / 10. **Your first Quick Play always ends on the Warden**; after that the boss is
  random: every boss comes up once before any repeats, and never the same one twice in a row.
- **The waves before a boss are its own enemies** (its faction, as in a Boss Hunt: tougher members joining as they go, its set
  piece on the fifth and tenth wave). Every boss has a faction now; a boss without one would get the **basic faction** the
  same way (see Enemies).
- **Bosses play exactly as anywhere else.** Only their health changes: about a **two-minute fight** for a typical build at
  whatever wave they come (a late boss after wave 10 has much less health than at its own Campaign wave; an early one in
  endless has more). The boost for strong builds still applies on top.
- **Win, then end or keep going.** Beating the boss is **QUICK PLAY CLEAR!**; the host can **Keep going [C]** into endless: ten
  more waves and a new random boss, after wave 20, 30, ... for as long as the party lasts.
- **Room building:** Quick Play is played on the **Expanding arena** (below), as the Campaign is: vectors, a build round after
  every wave from wave 1, building, and a free surprise room after each boss. Vectors come in at the full rate (+8 a wave, not
  the Campaign's half), so a party builds about one room a wave. The **Simple Arena** modifier keeps the open arena.
- **The tutorial:** a profile's first Quick Play shows a prompt at the top left that ticks off as you go, in five lessons:
  **The basics** (move, aim, shoot, dodge), **Level up** (pick a card), **Build a room** (click BUILD, place a piece:
  shown as soon as the first build round opens), **Your kit** (your ability, the parry shield: shown in the next
  wave) and **Gold and the shop** (collect gold, walk into a shop or
  Black Market room once one is connected). Steps count in any order; each shows your own key. **SKIP** ends it. It's only on your
  own screen (a friend's first run shows theirs), never holds the wave, and never comes back once done, skipped, or once your
  first Quick Play is over.
- **No paths, Data Gates, lives or saving.**
- **Records:** full rank XP and Cores, bosses count as beaten (unlocks and Boss Hunts), and Quick Play keeps its own plays,
  wins, best wave and bosses beaten (not counted as runs). Falling in endless adds the stretch after the win.
  Every Quick Play shows in **Records > Recent runs** with its graphs: the win or the fall, a Quick Play you quit (no XP),
  and an endless stretch after the win as its own card ("Quick Play · Endless", its curve from the win on), fallen or quit.
  Plan: `Docs/QuickPlay.md`.

## Training

**Training** (Docs/Training.md) is the fifth mode on the Game Mode carousel (after Survival): short **drills**, one mechanic each. Its page shows the drills as cards under their group (**Movement**: Move and Aim, Dodge, Wall Kick; **Combat**: Shoot, Parry, Read the Tells), each with its number, name, keys and medal (**NEW** until finished, **SOON** while it isn't built yet). Hover a card to show it, click to pick it; under the cards a scope draws the drill and the brief gives its goal, the medal times (**gold** and **silver** under a time, **bronze** for finishing), your best time and keys. **START DRILL** (the host) takes the top right of the Game Mode screen in place of TO THE LOBBY (the lobby has no use in Training), with the picked drill's number and name under it. START in the lobby never starts a run in Training. Best times are kept on the profile; nothing counts as a run. All six drills are playable.

**Playing a drill.** START DRILL takes the whole party to the classic arena, full screen, with no enemies; everyone plays a plain **recruit** whatever their loadout: no class or role look (grey fatigues, a helmet with a cyan visor), no abilities, ultimate or perks, and the **Training Pistol** (one plain round a shot; a weapon only Training has); everyone runs the drill at once with their own beacons, clock and medal, and nothing hurts you. The drill card at the top left gives the drill, what to do now, a pip per beacon, the clock and the medal you're on course for; the bottom-right corner counts what's left (**beacons left**, **walls left**, **steps left**, **targets left**, **shots left**, **tells left**). Your lit beacon pulses with a dotted line from you to it; reached ones turn gold. **Move and Aim**: walk onto beacons 1-3, then a target dummy stands on beacons 4 and 5: aim and bring it down (only your hits count, and only you see yours). When you finish, the result card shows your time, medal, best (NEW BEST when beaten) and the party's progress; the host has **RETRY** (R) and **BACK** (B or Enter: back to the lobby on the Training page). **Dodge**: six walls of shots sweep across the arena one at a time, from alternate sides and a little faster each time; dodge (Ctrl) just as one reaches you to pass through it. Get caught and the screen flashes red, the card counts HIT xN, and that wall comes again. Each wall is only yours. **Wall Kick**: three panels stand in the arena; dodge into the lit one (it glows, with KICK under it) to kick off it, in order, then the flag at the top right lights: get there (hold the dodge key after a dodge to sprint). **Shoot**: twelve targets, one at a time, each ringed with TARGET n / 12; the first three stand still, then each slides along a rail, faster each time (only you hit or see yours). **Parry**: ten shots, one at a time, each from a new side; a red arc round you shows where it's coming from. It homes in, so meet it with the shield up (right click) and facing it; anything else is a hit (red flash, HIT xN) and that shot comes again. **Read the Tells**: eight wind-ups, one at a time, each aimed where you stand as it shows, with its name and seconds left at its source: a **Sniper**'s thin line, a **Lunger**'s wide lane or a **slam**'s circle. Get out of it before it fires (a bright flash of its shape); still in it is a hit and it comes again. The wind-ups get shorter. Medals: Move and Aim gold under 0:15, silver under 0:25; Dodge gold under 0:20, silver under 0:30; Wall Kick gold under 0:15, silver under 0:25; Shoot gold under 0:25, silver under 0:40; Parry gold under 0:25, silver under 0:35; Read the Tells gold under 0:16, silver under 0:24.

## Boss Hunt

A new lobby opens on the **Game Mode** screen: the host picks **Quick Play**, **Campaign**, **Boss Hunt**, **Survival** or **Training** on the mode carousel at the top (the mode in play is ringed in a neon tube of its colour; arrows, the mouse wheel or a click on a side mode turn it), and the mode's own page fills the screen under it. The screen is grey in the **mode's colour** (gold Quick Play, cyan Campaign, red Boss Hunt, green Survival, cyan Training), whatever your role: a glow along the header, the keys, text, separators and corner brackets, and a soft glow behind the boss scope; the bosses keep their own colours. **TO THE LOBBY** (top right, with the party count and how many are ready), Back or Esc go to the lobby (on the screen **New run** first opens, Back and Esc go back to the main menu instead) (with Visual effects on, the screen blinks like a CRT changing channel: the mode page squashes to a bright line and the lobby opens out of it); the screen is part of the lobby, so going back to it never closes the lobby or drops anyone. In the lobby, the **mode card** at the top of the left column names the mode with its line; **CHANGE** (the host) or **VIEW** (a guest) opens the Game Mode screen again. Folder tabs stand on the big screen's top edge (the open one is lit in your role's colour and opens into the screen): the Saved loadouts star, **Loadout**, **Difficulty** and **Modifiers** at the left, and **Practice** at the right end; a second click on an open tab goes back to the Loadout. **Perks** open from the **Perks** key right of your class name on the loadout (it shows your equipped perk and slots used, e.g. 1/2); the Loadout tab takes you back. A Boss Hunt is a short run that ends
with one boss: **twenty steps** (see Hunt map below). Steps 1-19 bring only the boss's own enemies (its faction: for the Warden, Rioters and
Guards, then Riot Shields, Gas Grenadiers, Security Guards and Taser Guards, its Breakout, Patrol and Cell block squads, and its Riot line on steps 10 and
19); step 20 is the boss. Beat it and the hunt is won (**HUNT COMPLETE**); there's no endless, no
path offer, no Data Gate, and a hunt saves only at its step-10 checkpoint. Modifiers and the difficulty apply as in a run,
and so does the boss's health boost for strong builds. The HUD and the Tab screen count the hunt's steps (n / 20).

Every mode's page (Quick Play, Campaign, Boss Hunt, Survival; Training has its own, above) has the same layout: a header (a progress ring, the mode's name and a line), the mode's list under its heading on the left (bosses or factions; hover one to show it), a green target scope with its live model (it holds still; click and drag in the scope to turn it) and a dossier box (status, name, what happens) in the middle, and a sidebar on the right with **Your records** and the **Rules** (lit: what the mode adds; dim: what it takes away). On the **Boss hunt** page the list is the ten bosses (status tag: SOON, LOCKED, OPEN or WON xN); a boss you've never beaten is a **???** row with a "?" for its portrait (and in the scope). The host clicks an open hunt to pick it; hovering any boss shows it. For the shown boss it gives the map's steps, your hunts won, best time and the hunt's rules. A boss's hunt **opens once you've beaten it in a
run** (the host's records decide, in co-op); the others say Locked or Coming soon, and the run won't start on a
locked one. **Only the Warden has a hunt so far**: every boss has a faction now, but later bosses' hunts will need the party
handed a build that fits their strength (levels, legendary cards and mods). Records keep each hunt's plays, wins and best time per player
(not counted as runs). Winning a hunt unlocks that boss's perk (below). Plan: `Docs/BossHuntsAndPerks.md`.

**Hunt map.** A Boss Hunt is always a map of **twenty steps** (no switches), with the **Miniboss** at step 10 (see below); every step counts half a wave for enemy health and damage, so it is twice as long as the old ten-step hunt, not twice as hard. The hunt is a map on three lanes, starting at the top and ending with the boss at the bottom; the screen between waves shows it, with the party, the next nodes, the picked node's detail and a **Lock in vote** key. Everyone votes for the next node (keys 1-3 or a click) and locks in; the node with the most votes wins, a tie goes to the host's vote, and after 20 s (counted once nobody is picking upgrades) the map picks for the party. A node with a single way on is taken at once. The wave that follows is that node's. Nodes: **Skirmish** (the boss's own troops at the step's strength), **Elite squad** (one step further into the faction, the set piece of a finale wave, and 30% of its enemies come as tougher elite variants; each Elite cleared is a **boss clue**), **Ambush** (a short wave of 70% of the enemies, coming from every side all at once instead of on the clock; pays XP and leaves a chest), **Contraband** (a normal wave fought in the dark, like the Nightfall modifier; pays everyone a rare-or-better card), **Gamble** (unknown until you arrive, then a Skirmish 30%, an Elite 25%, an Ambush 20% or a Contraband 25%; shown on the sector chip under the wave plate), **Rest** (no fight: everyone heals to full and gets a reroll and a free upgrade pick) and **Shop** (no fight: the Bazaar opens; gold is earned all hunt long and leftovers turn into XP when it closes). **Miniboss** (the long hunt's step 10, a node alone in the middle: the Warden's Riot Shield as a huge elite with four times the health, with a squad and the faction's set piece around it; pays a boss clue and a rare-or-better card). A Rest or Shop never comes right after another, and the row before the boss is always one of them. On the long hunt the map scrolls to keep the next steps in view. Each Elite cleared is a boss clue, up to **5**; each clue is rolled (from the map's seed, so every screen agrees) as either a **health clue** (the boss starts the fight with 8% less health) or a **damage clue** (enemies hit 10% softer during the boss fight). Design: `Docs/HuntMap.md`. **Saving:** a hunt saves once, when the step-10 Miniboss has been beaten and everyone has made their picks (the hunt keeps that one save until it ends); quit later and Continue a run brings the party back at the vote for step 11 with the map, clues, builds and gold as they were. A hunt that ends, won or lost, loses its save, and quitting before step 10 loses the hunt. **Records:** the 20-step hunt keeps its own plays, wins and best time per boss (the old ten-step ones stay as they were); a win of either length unlocks the boss's perk.

## Survival

**Survival** (Docs/Survival.md) is a lobby mode with no waves and no boss: one clock runs and the pressure climbs with it, and the run ends when everyone is down. The first **two minutes are melee only** (Swarmers, then Brutes and Lungers), the budget starts at about a third of its line and eases in over the same time, and shooters then ramp in over three more minutes (squads with a shooter wait for that too). Enemies top the field up to a budget (killing fast brings more, up to 60 alive); the basic faction comes first and then all ten boss factions join, one every three minutes (the last at 30:00), each fading in over two minutes and holding its own spawn weight (`Survival.FactionWeight`); a squad arrives every 45 s; Elites grow to 40% of spawns; health and damage scale as one wave per 30 s; two and three players raise the budget 1.5x and 2x. The budget (in Grunts on screen) is 4 plus 0.08 a second to ten minutes, then 0.16 a second, after the slow start; a weak enemy counts for at least 0.6 of a Grunt, so a swarm of them is not free. Good-skill bots fall at about 6-8 minutes. Kills still level you up, and a **level-up pick freezes the world** (clock, enemies and shots) until everyone has chosen. Not saved to continue. The HUD shows the clock (m:ss) where the wave number was, a bar towards the next faction and a notice when one joins; the end screen shows the time survived and the phase reached. Its lobby tab (laid out like the Boss hunt tab) lists the factions in the order they join beside a pressure-over-the-clock scope, then your records and the rules: runs played, longest time, longest time with your current class, and the furthest faction reached. A Survival run is not a Campaign or Quick Play run: it keeps its own records, pays rank XP for the waves of clock survived, and shows in Recent runs as "Survival". It is played in **one big room**, four times the classic arena (3200 x 1800), walled all round, with a plus of walls in the middle (a gap at the centre to walk through) and a bracket in each corner, **wall portals** in the middle of each wall (left with right, top with bottom: walk into one and you come out of the far wall, as do enemies), and crates and barrels all over it (about 130) (smashed ones come back, out of sight). The camera follows you; enemies come in off-screen, with a minimap top right and a small red arrow at the edge of the view for the enemies you cannot see (teammates get arrows in their colour). In Survival, enemies off the view in about the same direction **merge into one arrow with a count** ("x7"; orange if an Elite is among them; a boss keeps its own big arrow). The Expanding arena's 1.3x enemy damage does not apply. Survival has **no lives** (a run isn't saved, so there is nothing to respawn from) and pays **full gold and chests** (the Campaign halves them for its doubled waves).

## Perks

A perk is a rule you bring into a run, one per boss, unlocked by winning that boss's **Boss Hunt**. Pick them on the lobby's
**Perks** view (the loadout's Perks key): your **slots** on the left (one to start, a second after you win a full run, a third after you win five different
hunts), and a card for each perk on the right. Click an unlocked card to equip it, click an equipped card (or the slot's
*remove*) to take it off, and hover a card for the rules. A second perk from the same boss takes the first one's slot. Everyone
brings their own perks in co-op, and each seat shows its perk icons on its party card. Perks are off in the
Boss Lab and the lobby preview. A saved run keeps each seat's perks, and when you continue it the view is read-only and shows
what the seat was saved with. Equipped perks are saved with your profile.

| Perk | Boss | Unlocked by | Effect |
|---|---|---|---|
| **Riot Gear** | The Warden | winning the Warden Hunt | A riot-shield plate blocks the next hit that would hurt you: no health lost, no damage counted, and half a second of invulnerability. Then the shield takes **30 s** to come back (the timer keeps running between waves and while you're downed). You start every run with it ready. It blocks one hit from an enemy: a shot, a swing, a blast, a boss slam. **Burning, gas, acid, lava and other damage-over-time ticks go through** and don't spend it. |
| **Contraband** | The Warden | winning the Warden Hunt on the Hard or Nightmare preset | **A treasure chest always turns up on the first wave after each boss** (waves 11, 21, 31...) when anyone in the party has it, on top of the random ones (not in Boss Hunts). **Your own item from any chest is rare or better** (never uncommon); teammates still get their own normal item from the same chest. See `Docs/Contraband.md`. |

The plate hovers at your side and swings toward the nearest incoming enemy shot (resting behind you otherwise). While the shield
recharges a ghost plate fills along its length, and it pops back when ready. A block flashes the plate white, throws sparks off
the impact and breaks the plate into three pieces that fly off (with visual effects reduced or off, only the flash). A small
**Riot Gear** chip above the ability bar's XP strip fills from the bottom and shows the seconds left, then READY. Teammates' plates
show too. A boss's perks share one slot, so equipping Contraband takes Riot Gear's place. Other bosses' perks come later: the view shows them as locked placeholders. Runs don't yet note which perks were used (a
"no perks" record variant is planned). Plan and build notes: `Docs/RiotGear.md`, `Docs/BossHuntsAndPerks.md`.

## Controls

| | |
|---|---|
| Move | WASD / arrow keys. Walking has a little weight: full speed in 0.06 s, and a stop or a turn of more than 90° in 0.04 s (dashes and dodges are unchanged; the Frozen Lake still slides). With Visual effects on, your body stretches along where you're heading (up to 14% walking, 32% in a dash) and leans ahead, squashes and wobbles back as you stop or land a dash, kicks up a puff of dust each step, a burst behind a dash and a skid puff on a hard turn; a dash leaves up to 5 fading copies of you behind it and kicks the camera 9 px ahead (with Camera lean on). Reduced effects keeps the dust and copies but leaves the body and camera still |
| Aim | Mouse |
| Dodge | Ctrl, or double tap a movement key (changeable). A tiny 95 px dash with 0.22 s of invulnerability; 1.1 s cooldown (Ninjas 0.77 s); a soft cloth swish plays. With no direction held it steps back from your aim. **Hold** the key (or the double-tapped movement key) after the dodge for 0.2 s to **sprint**: +50% move speed, fading away over 2.2 s (Ninjas: +70% over 3.1 s), with a pale-blue wake everyone sees; a tap doesn't sprint. **Wall kick**: dodge into a wall or a solid prop (crate, barrel, pillar, a walled arena's edge) and you kick off it with a gold ring: a fresh 150 px dash, faster than the dodge (a dodge that would stop up to 70 px short of the wall reaches on to it), and the dodge comes back at once for one free dodge (that one can't kick again) |
| Shoot | Hold left click (changeable) |
| 1st ability | 1 (changeable) |
| Parry | Hold right click (changeable) |
| Alternative fire (Dragon Fang blink, Twin Fang, Sunlance, Hook & Haul, Shuriken's Great Star stance, Bo Staff shove, Kusarigama whirl) | Left Alt |
| Swap weapon (a Soldier's second-slot Calling: Slug Breacher, Nano Sprayer, Rocket Pod) | E |
| Ultimate | Q |
| Interact: take your chest item (standing on it; B discards it), open a mystery door, shut a gate, open a room shop again, climb into or out of a Trojan Horse | F |
| Gadget: tap to switch on / off, hold and let go to throw it where you aim (right click while holding cancels) | X |
| Gadget wheel: hold to open, let go on a gadget to swap it into your hand | C |
| Wiki: tap to look anything up (pauses a solo run; in co-op only on your screen); hold and click anything on screen (text, an enemy, a player) to open its page | I |
| Rotate camera left / right: swing the view a quarter turn anticlockwise / clockwise (see Zoom in / out below) | [ ] |
| First person (experimental): see the run through your own eyes. The arena's floor art becomes the ground, walls and pillars rise, crates, barrels and chests are solid boxes and drums with their look on top, enemies, players and shots stand up as cards with a shadow under each. The mouse turns you (the cursor is locked while no menu is up; speed in Settings → Gameplay → First-person mouse), W walks where you look, and you aim at the crosshair in the middle of the screen (looking down aims at that spot on the floor). Your weapon is a 3D model low on the right in your role's colour, chosen by your weapon: a gun (recoils with a muzzle flash as you fire), a sword for melee weapons (swings right to left, then back) or a staff for spells, bows and summons (thrusts, its crystal flaring); it bobs as you walk. Your own body, the aim line and the weapon pips round you aren't drawn. Just your screen: the game, co-op and saves are unchanged. Remembered between launches | F8 |
| 2nd, 3rd and 4th abilities | 2, 3 and 4 (changeable; once unlocked). When a card gives you one, choose its key on the card screen; move them between keys any time in the Tab menu (cooldowns move with them) |
| Pick an upgrade | Click a card (or 1 / 2 / 3), then Confirm (or Enter) |
| Reroll upgrades | Reroll button or R (twice: the first arms it) |
| Ready for the next wave | Ready button or Y (rebindable), once your upgrade picks are done. Auto-ready switch beside it |
| Map | M (rebindable), wherever a run has a minimap (the Expanding arena, Survival): cycles **minimap → full map → no map**. The minimap grows into the full map (the whole world fitted mid-screen, the game dimmed behind it, a legend under it) while the view zooms out; the full map and the minimap switch off like an old CRT (folding to a bright line, then a dot) and come back the same way. "MAP OFF · M to bring it back" shows for a moment. Remembered between launches |
| Mute sound | No key by default: give it one in Settings → Keybinds (its × clears it again), or use the switch in Settings → Audio |
| Ping | Middle mouse (rebindable), in a run. **Tap** to mark your cursor with the ping you used last (Here to begin with). **Hold** for 0.2 s and a six-slice wheel opens at the cursor: move to a slice and let go to send it, let go in the middle to cancel. Everyone sees it in your colour (teal, amber, violet) as a ripple, a beam of light and a glyph ring with your name for a second. **Here** (5 s), **Focus** (6 s: snaps to the enemy nearest your cursor, rings it and follows it until it dies), **Danger** (4 s, pulses faster), **Help** (5 s: also rings you), **Loot** (5 s) and **Go** (5 s). Three live per player (a fourth replaces your oldest), 0.4 s between pings. A click doesn't fire while the wheel is open |
| Emotes | Hold T (rebindable): a see-through bar of every emote your rank has opens above the ability bar (in the Hangout, at the foot of the room). Scroll the wheel or click the arrows at its sides to move along it, click one in view or press its number (1-6, the slot it sits in: the numbers stay put, so what a key plays depends on how far along the bar you have scrolled) to play it. While it is open a click picks instead of firing. Let go and it closes |
| Stats, upgrades and key layout (just for you) | Hold Tab (click a key on a 2nd–4th ability to move it). Works while choosing an upgrade too: the cards come back when you let go |
| Pause menu | Esc |
| Change any of these keys | Settings → Keybinds (see below) |
| Developer tools | F1 (see below) |
| Kill every enemy on the field (dev, host) | B (when not on a chest item), once the F1 WAVE drawer's kill-all key is on |
| Camera lean on / off | Settings → Gameplay (on by default). The view eases a few percent toward where you aim and move (aim 65%, movement 55%); the arena and wrapping are unchanged |
| Rotate camera left / right | `[` / `]` (rebindable, Settings → Keybinds → Toggles). The view swings a quarter turn anticlockwise / clockwise (back and forth, any number of times) to look at what is above or below you; aiming, the move keys (W is always up the screen), the camera lean and the labels turn with it. Local only, resets when a run ends |
| Zoom in / out | Mouse scroll wheel during a run: up zooms in on you (up to 2x, the view follows you and stays inside the arena), down zooms back out to the whole arena. Eases back out when a run ends. While zoomed, arrows at the edge of the view point to everything off-screen: teammates in their colour with their name (pulsing red when downed), enemies in red (elites orange, bosses a big crimson arrow with a ring), and any unopened treasure chest in a pulsing arrow with a ring in its kind's colour (also a square on the Rooms minimap); nearer ones are bigger. Settings → Gameplay → Scroll-wheel zoom turns it off (on by default) |
| Teammate shots 25–100% | Settings → Gameplay (100% by default). How solid other players' bullets are drawn, so a crowd of them doesn't hide the fight; yours and the enemies' stay full. Local only, saved on your machine |
| First-person mouse 0.25x–4x | Settings → Gameplay (1x by default): how fast the mouse turns you in the first-person view (F8); each step of the bar is the same feel of change. Local only, saved on your machine |
| Aim line on / off | V (the line runs from you to the crosshair, but never past your weapon's range: as far as your shots fly, or your swing reaches; a small cap marks the end when the crosshair is beyond it) |
| Hide / show the HUD | H, or Settings > Gameplay; the mode is remembered between launches (H cycles three modes: Corner (the default; CRT phosphor readouts in bracket corners: health with a 20-cell bar, your level and a thin gold line for its progress bottom-left (teammates' health in slim rows above it), each ability key over its seconds left (the ultimate's charge %), its name and its mastery cells and level at the bottom, the wave and enemies left bottom-right with slim rows above it for what the run has: lives, gold, vectors (flashing as they come in), relic, path and hunt sector; the minimap keeps the top right), On the player (a leader line from you to a small CRT tag with your health, a cell bar and a row per ability: its key, its name and its seconds left, green when ready, the ultimate's charge in gold; it switches sides near the left wall and the top, and hides while you're downed; the wave and enemies left at the top), Hidden; it fades, "Press H to show the HUD" stays faintly at the bottom while it's hidden, and a slim health bar for each boss stays at the top in every mode) |
| Name labels (a name over you and every enemy) | N, or Settings → Gameplay (off by default) |
| Health bars over you, teammates and enemies (each on or off) | Settings → Gameplay (all on by default) |
| Save / load a run snapshot (host, dev) | F6 saves the run as it is now; F7 loads the newest (see *Run snapshots*, under *Testing tricks*) |
| Lag chart: off / pill / chart; freeze it; copy a summary | F5; Shift+F5; Ctrl+F5 (also Settings → Gameplay; see *Lag chart*, under *Playing with a friend*) |
| Open the folder with `Player.log` (menus, lobby, results; not in a run) | F2 |
| After game over / victory (host) | R = play again, L = back to lobby, C = keep going (endless, after victory; not in a Boss Hunt); with a life left, Y = respawn, N = give up |
| Click debug readout (temporary) | F9 |

### Keybinds

**Settings → Keybinds** (main menu, lobby and pause menu) lists every control: the four move keys, the role
ability, the Ultimate, the three 2nd–4th ability keys, **Fire** and **Parry** (left and right click by default; each can
be any key or mouse button), the weapon secondary (Left Alt), Dodge, the gadget, Interact (F), discard a chest item, reroll, Ready, Emotes, Ping, and the aim line,
map, HUD and mute toggles (mute starts with no key). For Fire and Parry, a "Left click / Right click" pair of buttons appears while you're picking
(the page can't take a click as a key press, since the click is how you chose the row). Left and right click can't go on
any other action, since they click everything else. Click a key, press the new one (a middle or side mouse button works too), and it
changes at once; **Esc** cancels. A key already used by another action swaps with it, so nothing ends up on two. Every action also has a **second key**
(the "+ ADD" slot beside it): either key does the action; right click a key to clear it. The red **×** at the end of a row takes every key off
that action, so it can't be pressed at all (e.g. no Reroll key) until you give it one again (taking another action's key is fine: that one
keeps its second key, or is left with none); hints that name its key show "—". The
arrow keys always move too. The ↺ beside a changed key puts it back; **Reset all** puts every key back. Saved on this computer. The menu keys
(Esc, Tab, Enter, F1-F7) can't be changed, and menus are always clicked with the mouse. The number keys can be bound:
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

The HUD has three modes on **H** (see Controls): **Corner** (the default: health, level, teammates, your keys with their
mastery, the wave and enemies left, and lives, gold, vectors, path and relic in the corners), **On the player** (a small
tag beside you) and **Hidden**; the old full bottom console is retired. Boss health bars go wide across the top middle
(a slim one stays when the HUD is hidden), and the "Hold TAB" hint is in the top-right corner for the first three waves (it fades out sooner once
you've opened the Tab screen twice). A player's name tag rises clear of an Aeroaegis shell round them.

**Between waves.** When the last enemy dies a **Wave n cleared** box drops in: a dark blue CRT screen like the menus',
its title gold after a boss. It shows the wave's **clear time** and **score**, a row per player with **kills, damage
dealt and damage taken** for that wave, and **awards**: **Untouched** (took no damage), **Reviver** (stood a teammate up),
and in a party **Most damage**, **Most kills** and **Most healing** (only for a single leader with something to show).
A **Squad** row adds them up. The box has **tabs** along its top, opened in any order: **Results** (the numbers above, plus a
gold **build panel** when a building round is open: the pieces in your hand by name, your vectors and **Open build map**),
**Build · n** (n is the pieces in your hand; it opens the Expanding arena's build window, and pulses gold until you open it
that break; closing the window (**Close**, or **Done · ready up**, which also readies you) folds the box into its minimized
strip) and **Next wave** (the enemy types the next wave draws from, new ones marked NEW, a boss wave naming its boss, and
the **road to the next boss**) and, while any chest on the map is unopened, **Chests · n**: a row for each (its kind, what it
gives each player, which way and how many seconds' walk) beside a map of the rooms with the chests on it, the fee and your
gold. **Collect** opens one where it stands for **15 gold + 2 per wave**, paid by whoever presses it (dim when you can't
afford it); everyone gets their share as if you'd walked there: a Gold chest's gold straight into each wallet, a vector
chest's vectors straight to each stock, each player's card at their own feet (take it or throw it away; it's swept to you as
the wave starts), a piece chest's or a Buildables chest's card at each player's feet too. Walking there stays free, and it only
works between waves. In the arena, a small tag floats over every unopened **Gold** and **Vector** chest with what you get from it
(**+56 GOLD**, **+24 VECTORS**; everyone gets the same), and **FULL** over a vector chest while your stock is full. Build and Next wave are left out when there is no building round (hunts, the last
wave, a full map, other arenas): Results shows the next wave's panel then. Along the bottom of every tab: the **Start wave n**
key (Y, bottom right), the **Auto-ready** switch, the status and the countdown bar. Skipping the build is fine (pieces keep
for a later break): while you hold pieces, **n pieces saved for later** shows over the key. The next wave only starts once **everyone is ready**, **3 seconds** after the last Ready, and there is no
timer while anyone is still waiting. **Auto-ready** is remembered between runs: you count as ready as soon as your upgrade
picks are done, and when everyone has it on the countdown is the usual 4 seconds, so solo play with it on moves at the
old pace (it is off by default). In a building round, with pieces in your hand, Auto-ready waits until you've opened the
build window (or pressed Start) that break, so it never skips building by accident. At the right of each player's row a **status chip** shows what they're doing: **Picking** (upgrade picks still to make, orange), **Building** (the build window open, violet), **Shopping** (the shop open, gold), **Looting** (the box minimized, out in the arena), or, on the box, a small box for whether they're ready (a tick, an A for auto-ready, dashed while they aren't). **Minimize** (top right of the box) folds it into a slim strip at the top so you can walk to a shop or a chest; the strip (a slim, see-through bar with solid keys) keeps the next wave, the status, **Start**, **Build** (during a building round; gold until you open it) and **Open** (Y there readies you at once). It opens again at the next break.
**The boss gate.** In the break before a boss (Quick Play, the Campaign, a Boss Hunt) there's no Ready: a **boss gate**, a
red summoning circle with a skull in the middle, opens in the middle of the hub, and the fight starts once **every
player still standing is in it** for 1.2 s (downed players don't count). Take your time to shop, build and pick first: there's no
countdown, and Auto-ready doesn't start it. A socket under the gate lights for each player inside, a thread runs to them,
and its inner ring burns round as the team's fill rises. Its red leans toward the coming boss's colour (pink for the Warden, amber
for the Hive Queen, violet for the Singularity...). A soft low pulse hums every few seconds while it waits, a chime plays on your screen as each player steps in, and a deep gong as it
takes you. When it opens and the box isn't on your screen (shopping, building, or folded away), a toast at the top says
**THE BOSS GATE IS OPEN**, with the boss's name and how many are in, for 4 seconds. It shows as a ring on the minimap and, when you're zoomed in, as an arrow at the edge of the view. The box's last tab reads **Gate**: **Go to the gate** (Y) folds the box
away, and the status shows how many are in. Bots walk in on their own.
The first countdown of a run and the dev tools' wave jump don't use ready-up. Mockup:
`Docs/Mockups/wave-cleared-ready.html` (option 1).

**Chat.** In a run, press **Enter** to open a text line at the bottom right, above your card and the teammates'
rows. Type (up to 120 characters), **Enter** sends and **Esc** cancels. While you type, movement, abilities and
the pause key are ignored. Each message is a small pill with an edge in its player's colour; lines fade away one by
one after about 6 seconds, and the box is gone when it's quiet (a line from a teammate shows it again for a moment,
without taking your keyboard). The line also closes itself after 12 seconds with no typing. The host keeps it to
about one message a second per player. Mockup: `Docs/Mockups/chat-box.html`.

## End of a run

**Lives.** If everyone goes down (solo: you) and the run has a **life** left (the difficulty's Lives: Easy 3, Normal 2,
Hard 1, Nightmare 0; a hearts chip over the wave panel shows them), the ending holds before the results and a box asks
**Respawn [Y]** or **Game over [N]** (the host chooses). Respawn replays the wave you went down on, the run exactly as it
was when that wave started (everyone's upgrades, levels, gold, the map), a life used; the score is kept. Game over ends
the run and shows the results. Closing the game keeps it at the wave's start, so **Continue** respawns it later (the
saved-runs list shows the lives left). Out of lives it's a plain game over. A run won without using a life is
**Deathless** (on the results screen and in Recent runs). Every boss beaten also leaves a **checkpoint** (CHECKPOINT
SAVED in the wave box), the fallback if the wave's save is missing. Not in Boss Hunts or Quick Play.

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

## Lobby Practice (Hangout)

The **colour wheel** key on the loadout preview window's top bar (where PLAY was: Practice replaced it) opens a **hue strip**: drag along it to light the preview's floor in any colour (kept between sessions); **ROLE** goes back to your role's colour. The floor is dark with a soft pool of light round your character.

The lobby's **Practice** key (right end of the tab row) swaps your loadout for **Practice** in the same window, with a glitch (the loadout splits into red and blue copies, tears and collapses to a bright line; Practice opens out of it), so the party column with everyone's class and Ready mark stays in view and Ready and Start work as usual. Each player swaps on their own (the **Loadout** tab swaps back); the room is shared, so a player on their loadout stands still in it. Change your loadout and just your character is swapped in place; someone joining starts the room over with them in it; Start replaces it with the run. Your keys and mouse play it while the mouse is over the window, from the first moment (no countdown). Practice is the **real match, held between waves for good**. You have your whole loadout exactly as in a run (weapon, Alt, abilities, ultimate, class perks), because it is the same game, but with no cooldowns. No wave ever starts, there is no countdown, no levelling, nothing is saved, recorded or unlocked. There's no god mode (so health costs like the Hemomancer's work as in a run), but nobody goes down: at 0 health a **pillar of light** takes you up and sets you down, full, somewhere else in the room. **Twelve enemy spawners** stand round the edges, each a pad with a ghost of its enemy: stand on one and press **F** (Interact) to turn it on or off, and **B** or the **mouse wheel** (over the window) to change its enemy (any of the regular enemies, no bosses). While it's on it brings its enemy in every 3 s, up to 5 alive from that pad, and they fight like a wave's; turning it off takes them away. Turrets, summons and other abilities that only act in a fight work here too. **PvP** is always on: your shots, blasts, swings and abilities hurt other players at 35% of their damage (never yourself; healing weapons still heal teammates). Every weapon's damage reaches players, summons, turrets, lock-ons and beams included. Their slows, freezes, stuns, knockback, bleeds, burns and poison land on players too, at half the length (a stun at most 1.2 s, a slow never below 45% speed). Other players' Blood Walls and Ossuary Walls block and hurt you too. Marks, holds (a grasp or a cage stuns you), wet, venom and the attacker's damage cards work on players as on enemies; after a stun you can't be stunned again for 1.5 s. Design: [`Docs/Hangout.md`](Docs/Hangout.md).

### The old lobby room (no longer reachable)

The first build's room (the lobby tab that used to sit here, still described below) is replaced by the mode above. The lobby's Hangout room was a tab where everyone's character walks around. Move yours with **WASD** (or the arrow keys,
or your rebound movement keys); the others in the lobby move theirs, and everyone sees everyone. Each character is
your real class and weapon model, with the **trail** you have equipped behind it, your **rank badge** and a name plate
with your **frame's** colour (and a green dot once you've readied up). Works in every lobby mode. Design: [`Docs/Hangout.md`](Docs/Hangout.md).

The room is a big map (2400 x 1600) that you see through a window: the view follows you and stops at the glowing teal border. A **minimap** at the window's top left shows where everyone is. Switching to the Hangout from the Loadout tab
plays a 0.85 s grow: the loadout's preview window stretches into the room (it just snaps with reduced effects or from any other tab).

**PvP** is a checkbox under the minimap, **off** by default and yours alone; everyone sees each player's setting. With it **on** you face the mouse and **hold the left button** to fire your loadout weapon (its fire rate, shot speed and reach; one plain shot, none of the abilities, pellets, beams or swings), and other players with PvP on can shoot you. With it **off** you can neither shoot nor be shot, shots pass through you. Everyone has 100 health with a bar overhead; it comes back 3 s after the last hit. At 0 you are **knocked out** for 2.5 s (a ghost, can't move or shoot), then return at a spawn spot with full health. Turning PvP off heals you. The host decides every hit.

**Abilities** (PvP on only; the **1st ability** key or **right click**, one per role, like the very first build): a **DPS** has **Dash** (a quick 240 px lunge the way you walk, or the way you face when standing still; 2.5 s cooldown). A **Tank** has **Barrier** (a 360 degree bubble around you that deletes every other player's shots for 4 s; 10 s cooldown; your own shots pass out). A **Healer** has **Burst heal** (heals everyone within 230 px, you included, for 60 health and **revives anyone knocked out at once** with 60; 8 s cooldown). Your ability and its cooldown show under the PvP box. Turning PvP off drops a barrier.

## Weapon variants

Each weapon has **2 variant tiers** (lobby: the button on the right of a weapon key, "Variants n/2"), bought with **Cores**, an account
currency saved in your profile. Tier I costs 10 Cores, tier II 20. The Twin Rifle's tiers (Twin Rifle Auto and Out of Ammo) and the Fireball's (Waterball and Earthball) are built, open for testing with no Cores needed; the other weapons' tier effects are not built yet.
Earning Cores: a won or lost **Campaign** pays 1 per boss defeated and 5 for the win (a full 100-wave win: 15); a won **Boss Hunt** pays 5;
Endless pays none. **Core Carriers** are the surprise: from wave 5, a regular enemy now and then spawns with a pulsing gold halo (one at a
time, at most 6 a run); killing it adds 1-3 Cores to the run's total, paid when the run ends and shown on the results. Design: [`Docs/Variants.md`](Docs/Variants.md).

## Rank

**Lobby profiles:** click your party card's portrait (or the card, if you are not the host) to open your profile; click a teammate's card to see their holo card, large: their frame, card background, name effect and trail, rank badge, XP, kills, best wave, bosses and what they have equipped. Esc, the close key or a click outside closes it.

A **cosmetic account rank**. Every finished run (and Boss Hunt) earns XP; XP raises your **Rank**; ranks unlock
cosmetics only, never power. It is separate from the **Level** on the results cards, which is the in-run level.
Design: [`Docs/Rank.md`](Docs/Rank.md); mockups: `Docs/Mockups/rank-screens.html`.

**Class expertise and weapon skins:** every run's XP also goes to the class you played, as that class's **expertise**
(level 0 to 10; level *n* to *n+1* costs 1,000 x (*n*+1) XP, 55,000 for level 10). Expertise unlocks that class's
**weapon skins**, one equipped per weapon. Pick them in Profile > Look > **Weapon skin**: pick any class and role (it starts on
your lobby pick), then a weapon, to see its skins with a live preview and your expertise level and bar for that class.
Everyone in the run sees and hears your skin. Not ability **mastery**, which is per run.

| Skin | Weapon | Unlock | What changes |
|---|---|---|---|
| **Rubber Chicken** | Twin Blades | Knight expertise 3 | Both sabres become rubber chickens held by the feet. The necks flop behind every swing and stretch with its speed (the Double Jab becomes a peck), the beaks open and squeak while they cut, the eyes go to X on the hit, and feathers come off instead of the blade trail, with a puff where each cut ends. Every Twin Blades sound becomes a squeak: an "eek" on the cuts, "bwek bwek" on the jabs, a warbling squeal on the Pirouette, a wheeze and full scream on the Crescent Shear, a rubber thwap and honk on hits, a squeal on Twin Fang. Cosmetic only: the swings, timing and hits are the Twin Blades' own. |

**XP for a run:** 20 per wave cleared, 150 per boss defeated, 40 per ally revived and 500 for a win, times
(1 + each active modifier's bonus: **Harder +25%**, **Risky +15%**, **Easier +0%**, at most x3) and the difficulty's
step (Easy x0.75, Normal x1, Hard x1.25, Nightmare x1.5, Custom x1). A hunt counts its waves and the boss, with no
win bonus. Runs where dev tools (F1) or the bot were used earn nothing, like Records.
**Ranks:** rank *r* to *r+1* costs 200 x *r* XP (rank 8 needs 1,600). Tiers start every 10 ranks up to 100, every 50 up to 500 and every 100 up to **1000** (the max): Recruit (1-9), Challenger 10, Veteran 20, Champion 30, Legend 40, Elite 50, Master 60, Grandmaster 70, Warlord 80, Overlord 90, Mythic 100, Ascendant 150, Paragon 200, Titan 250, Colossus 300, Sovereign 350, Immortal 400, Eternal 450, Celestial 500, Cosmic 600, Astral 700, Radiant 800, Transcendent 900, Apex 1000. Each tier has its own badge colour (wings from 30, a star from 50, three stars from 150), and every rank adds a **mark** under the number (up to 9; the 50 and 100 rank tiers add one per fifth or tenth of their span), starting again at each new tier.

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
| every tier (10, 20, ... 1000) | A new title and badge colour (automatic) |
| 9 | **Ember trail**: embers drift behind you as you move |
| 13 | **Steel frame**: a lit rim on your lobby party card and name chip |
| 15 | **Frost trail**: a pale chill line with ice crystals that stay where they formed, glint and melt |
| 17 | **Circuit frame**: a teal rim with bracketed corners and a pulse of light running round the edge |
| 19 | **Storm trail**: crackling static, a jagged yellow-white thread that re-rolls every few frames |
| 21 | **Ember Edge frame**: a flickering warm rim with embers lifting off the top edge |
| 23 | **Void trail**: a dark ribbon with a violet edge, motes pulled back toward you |
| 25 | **Void Pulse frame**: a violet rim with a breathing inner rim and two dark motes circling the card |
| 26 | **Gold stardust trail** |
| 31 | **Gold frame** |
| 41 | **Legend Foil frame**: gold with a bright band sweeping across the rim and twinkling corner stars |
| 36 | **Prism trail**: three ribbons cycling through the spectrum, with white glints |
| 46 | **Light Cycle trail**: a wall of light that follows where you've been but snaps to straight runs with right-angle corners; the floor tiles it crosses light up, and the wall breaks into squares that drift off as it ages (about 2.4 s). Cyan only. With Visual effects off it is one flat line |
| 29 | **Nebula Drift**: a reactive menu background (slow clouds of light that part round the pointer) |
| 44 | **Aurora Veil**: a reactive menu background (soft curtains of light along the top and bottom edges) |
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

**Cursors** change your crosshair in a run. Classic is free; Fine Cross (rank 4), Corner Sight (5), Ember Ring (12), Frost Snowflake (16), Circuit Sight (18), Storm Arc (22), Void Eye (27), Gold Diamond (32), Prism Ring (39), Light Cycle (47, it lays a corner-snapped wall of light behind the pointer), and, higher up, Radar Sweep (80), Sniper Scope (81), Chevron Snap (82), Sonar Ping (83), Pixel Plus (84), Petal Ring (85), Laser Dot (86) and Target Lock (87) are rank rewards. Each swells for a quarter second when you fire. Your equipped cursor also replaces the system pointer in the main menu and lobby (the system pointer is hidden everywhere: each look draws its own Corner pointer, a small L-bracket in the look's colours with its own flourish (Ember sparks, Storm crackle, Circuit pulse, Gold twinkle, Prism spectrum, and so on). It collapses to a dot and the crosshair swells out as a run starts, and back again when a menu, the pause screen or the end screen opens; about 0.6 s, instant with reduced effects), and the Light Cycle cursor's wall fades out in under a second. Only you see your own cursor.

**Name effects** change how your name is drawn for everyone: on the lobby party card, the Hangout name plate and the tag over your head in a run (small names keep only colour and glow, so they stay readable; with Visual effects off each is still).

**Emotes** are a short bubble over your head for 2.4 s (a gesture, a heart, GG...) that everyone sees, in the Hangout and in a run; several also move your character (a hop, a sway, a shake, two spins), the model only, never your hitbox, and not with Visual effects off. Every one your rank has is on the emote bar (hold T); nothing to equip. After one there is a 2 s pause in a run (0.75 s in the Hangout).

Everyone in the run sees your trail, and everyone in the lobby sees your rank, frame and card background; the host clamps what
players report, and only accepts a frame or trail your rank has. XP, what's equipped and the last 20 runs are kept in
`profile.txt` (`xp`, `equip`, `rlog` lines).

## Saved runs and records

**Runs save themselves.** The host's game saves the run as each wave starts (once everyone has picked their
upgrades, the path is chosen and the shop is closed), and again if the run is left or the game closed between
waves. Every arena saves (Classic and Expanding, with its map). Leaving mid-wave keeps the save from that wave's start. A run that's won or lost loses its save. The
The lobby's loadout preview is never saved.

**Records count the Campaign's waves** (1-100): best wave, best endless wave, Recent runs and the run charts. A profile
from before the Campaign has its waves doubled once when it loads (a 50-wave run's wave n is about the Campaign's wave
2n); Boss Hunt entries keep their ten waves. Rank XP is unchanged: it pays by bosses and progress, not the wave number.

**Play** on the main menu rolls the case's shutter up onto the saved runs: a screen headed **YOUR RUNS** with every save on one
scrolling list, newest first. The newest run is a tall row (LATEST RUN, its wave large, each seat's player and class) with a big
**Continue**, always lit; every other run is a one-line row (wave, when it was saved, how long it ran) with its own **Continue**.
Each row has a **DEL** key, dashed and quiet until the mouse is on
it (click twice: it lights red and turns into **REALLY?**); a deleted run is moved to a `deleted` folder next to the saves, not erased, so you can drag it back. Scroll with the mouse wheel or drag the scroll bar; a "more" strip at the foot of the list counts the
runs below. **New run** is at the screen's foot. With no saves the screen says so and offers **New run**. Play again (or Esc) rolls the shutter back down. Continuing hosts a
lobby for that run: it shows **Continuing a saved run** instead of the mode switch, and a **Seats** tab instead of
your loadout. Everyone plays the character they had: a friend who joins takes back the seat with their name (or
the first free seat, and moves aside if its owner turns up); **Take this seat** moves you to a free one. Classes,
loadouts and modifiers can't be changed. Seats nobody takes are left out. The match starts at the next wave, with
everyone's upgrades, ability keys, levels, XP, rerolls, mastery, gold, revive token, health and run stats back,
and the run's score, time, path and route. Saves are plain text in the game's data folder, per profile (`profiles/<id>/saves/*.run`),
with upgrades and abilities by name, so they keep working as the game changes.

**Profile** (the old Records and Your look pages, merged; opened by clicking your player card on the main menu or in the lobby; the pause and lobby menus have no Profile row): the same loadout box as the pause menu's (same opening and windows, with an X key at its top right instead of a Back key), in Settings' holo look (neon edges and glowing corners on the box and its window, a glowing line from the open option across to the window): a title, a profile card (your rank badge, name with a pencil key, title and rank, XP bar), four quiet tab keys (small faded letters, the open one a small lit pill) and the open tab's options as keys; each option opens in a CRT window to the right that slides out the first time and only retypes after that (its X key and Esc close it first, then the profile). **Look** is the cosmetics above. **Rank** is Progress (rank, title, XP, the next unlocks), the Reward ladder, Tiers (the five titles with their rank ranges) and XP sources. **Records** is Overview, Bosses, Classes, Challenge rooms and **Recent runs** (below). **Unlocks** has a window per kind (Frame, Trail, Background, Card background, Name effect, Aim line, Cursor, Emotes) with a check or a padlock on each cosmetic, and the run-feat unlocks. The Records tab keeps this computer's player's runs started, won and lost, best wave, best endless wave,

**Records > Recent runs** (Docs/RecentRuns.md): a strip of your last 20 runs (newest first, four at a time with arrows; each card says the mode, class, wave and won, died or quit; a run you leave before it ends counts too, with no XP), six tiles for the open run (result, time, wave, damage, damage taken, XP) and two tabs. **Overview** has three graphs: **damage per second** over the run (your line in gold with a blue team line when you played in a party, alternating bands for the waves, a red tint on boss waves, a red dot where you went down), **health** over time for everyone in the party, and **accuracy per wave**. Hover a time graph for a readout of that moment. **Trends** shows the wave reached and the damage dealt run by run (green won, red died); click a run on it to open it. Each finished run keeps a sample of everyone's totals and health every 10 seconds, in a small file in your profile's `runs` folder (the newest 20 are kept); Quick Play is tracked like a run (a quit one too); after a win, the endless stretch gets its own **Endless** card (Quick Play's or the Campaign's, fallen or quit: a fall adds its best endless wave and XP for the waves and bosses since the win, a quit no XP); runs from before this, Challenge rooms have no graphs (Boss Hunts and rooms keep their own records). Co-op: every player's profile keeps its own copy with the whole party's lines. Runs with dev tools or the bot are not kept, like the rest of Records.
best score, time played, totals (kills, damage, healing, downs, revives), each boss beaten, and runs and wins per
class. The host tells every player's game when a boss falls and when a run ends, so in co-op everyone's records
grow. Runs where dev tools (F1) or the bot were used don't count.
**Profiles:** the game asks **Who's playing?** at launch (`Docs/Mockups/whos-playing-carousel.html`). It boots up first
(3.45 s, `Docs/Mockups/whos-playing-load-in.html` C): the screen fades up from black, the monitor's glass powers on, boot lines
type on it while the drive reads (READING PROFILES ... n FOUND), a flash brings up the title, the disks rise from below (middle one
first, with a glint) and the keys and buttons rise in last; any key or click skips it (Reduced effects: a short fade). The monitor with
WHO'S PLAYING? on its glass and the floppy drive in its chin, and under it one big floppy disk per profile on an endless
side-scrolling carousel: the middle disk large, two either side smaller and fainter, fading out at the edges, wrapping round (with only 2 or 3 profiles the reel repeats, so a disk rolls smoothly off one side as its copy comes in on the other). Scroll with the **‹ ›** keys,
the arrow keys (or A and D), the mouse wheel or by dragging the disks; click a side disk to bring it to the middle; click the middle one
(or press Enter) to load it. The drive's readout says INSERT: <name> for the middle disk. Under the carousel: **PROFILE n OF m** (with a
dot per profile while there are 12 or fewer; FULL at 50), then **Manage profiles** (RENAME and DELETE under the middle disk; Delete
asks twice: SURE?) and **New profile**, which adds a blank grey disk at the end, brings it to the middle and asks for its name (it's
written on the disk's label in marker as you type; the drive says FORMAT NEW DISK). Up to **50 profiles**; New profile is grayed out
at 50. Loading a disk plays the **disk boot sequence** (`Docs/Mockups/disk-boot-sequence.html`, 4.8 s): the disk lifts and the rest of
the carousel fades, it rises to the drive shrinking to the slot's width, tips back and slides up into the slot (it flashes as it
seats), the drive reads it (DISK DETECTED, READING <name>… with the light blinking amber and the cells filling, then <name> READY in
green) while WHO'S PLAYING? backspaces away and VECTOR SECTOR: ARENA types on, then the main menu's case drops out from under the monitor
and powers on (see The main menu). The **eject key** beside the drive plays it backwards: the case powers down and lifts back up
under the monitor, the drive drains its cells, the latch clicks and your disk kicks out of the slot on a spring (TAKE DISK), comes the
rest of the way out and drops back to the middle of the carousel as the title retypes WHO'S PLAYING?. Each step has its sound; any key
fast-forwards the disk going in (the rest plays in half a second) and skips the way back with a quick TV-static channel flip. **Easter egg:** name a profile "San Andreas" (any spacing or case: SanAndreas, san_andreas) or GTASA, GTA:SA or SA and its disk turns into a plain blue floppy with a paper shutter and a round metal hub. Any name with "Doom" in it (Doom, DOOM, Doom 2, ...) gets a black shareware install disk with a cream label and red lettering ("REGISTERED DISK n", n is its slot). "Mission Impossible", MI, "Ethan Hunt" or "NOC list" gets a blood-spattered pale 230 MB optical disk. "Windows 95" or Win95 gets a 90s setup disk (white label, a four-pane logo). "Antaleon" gets a see-through hologram disk (cyan outline, scanlines, a sweeping glint, a flicker) that shows the profile's name. "Pacman", "Pac-Man", "Chomper" or "Waka" gets a Pac-Man disk, and while that profile is active the arena draws players as chompers, enemies as role-coloured ghosts and bullets as dots and its **Play opens a Pac-Man maze game** instead of the saved-runs picker (steer with WASD or the arrows; eat every dot, power pellets turn the four role ghosts blue; Esc pauses, Q leaves; one player for now: `Docs/Pacman.md`). The look stays when you rename the profile. Each profile has its own name (used in the lobby), records, rank, cosmetics and saved runs; an older single profile is copied into one named after your old player name, and the old files are left where they were. Kept in `profile.txt` inside the profile's folder under `profiles/` in the game's data folder. **Profile > Rank** shows your account rank (see **Rank**). Each save keeps the one before as `profile.txt.bak`; a cut-off or damaged file is set aside (`profile.corrupt-*.txt`, `profile.damaged-*.txt`) and the backup is used. If the disk refuses a save, a red "Profile not saved" popup shows.

**Unlocks**: each is an entry in `Unlocks.All` (`Profile.cs`) with a condition on the records; it's earned the
moment the condition holds (an **Unlocked** toast in the run, listed in Profile > Unlocks). So far: **Engineer** (win a run; open to everyone for now) and **Warden Hunt** (beat the
Warden in a run; see Boss Hunt). Records also keep each Boss Hunt's plays, wins and best time.

## Run stats (Tab)

Hold **Tab** during a run for the **Run stats** screen: hologram panels projected over the arena (in your CRT
colour, lit by a beam along the bottom of the screen), your stats down the left, and four tabs (click to switch; it
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
- **Inventory:** everything you carry: your **wallet** (gold, vectors with a bar to your cap, revive token, the gadget in
  your hand with its time left, rerolls), your **relic** and what it does, your **cards** (six slots, each drawn as its
  room; hover one for what it is), your **buildables** (the Black Market buys in your stash: teleporters and traps, each
  of 5), and the team's **room kits**. A list that holds more than fits **scrolls** with the mouse wheel (a bar on its
  right shows where you are, and "n MORE" under it); the wheel doesn't zoom the camera while this page is up.
  - **Using things from it:** while a build round is open, **click a card** (it reads **PLAY ›** under the mouse) and the Tab
    screen gives way to the build screen with that card picked; **click a buildable** you have (**PLACE ›**) and it opens on the
    Teleporter or Trap tool with it chosen. The Tab screen stays shut until you let go of Tab. Outside a build round, while downed,
    or from the pause menu's Run stats you can only look (the hover tip says why).
  - **Teammates:** keys on the right of the header (**YOU** and each teammate, underlined in their class colour) switch to a
    teammate's inventory: their wallet, relic, cards and stash (look only; rerolls show "—", they aren't sent).

The panels **switch on** from a thin line when the screen opens and when you change tab (about a third of a
second; teammates' live models appear once it settles). With **Reduced effects** or **Visual effects** off they're
simply there, without the drifting band and flicker.

**Solo, holding Tab pauses the run** (letting go resumes it). **With friends it doesn't pause** (you can still move
and shoot while it's up); to look together, pause (**Esc**) and press the small bar-chart key beside the pause menu (hover: "Current run stats"). The host sends
everyone the run's numbers about once a second, and again the moment the game pauses.

## Pause menu and settings

Press **Esc** in a run to open the pause menu. Pausing **freezes the run for everyone** (your friend
sees who paused) and either player can resume. The pause menu, **Settings**, **Profile** and **How to play**
share one look, the **lobby loadout's box** (`Docs/Mockups/pause-settings-loadout-box.html`): a navy panel with a thin blue rim and bracket corners that slides out from the left (24 px in from the left, top and bottom) and stops 36 px short of the box beside it, the same gap the lobby's loadout box has to the screen's right edge; that is the standard placement for every drawer menu, and the window it opens takes the loadout box's place. The title ("PAUSED_", "SETTINGS_", "MENU_") and its sub-line sit straight on it over a thin rule, with the soft option keys below (a faint tint, brighter under the mouse, outlined when selected). The whole screen darkens softly toward its edges while one of these menus is up.
Behind the pause menu and the lobby's Esc menu the screen darkens over 1.5 s under a faint, moving static (Reduced effects: just the darkening). Both menus and the windows they open (Settings, How to play, Profile) are drawn like the Game Mode screen: grey boxes and keys in the current mode's one colour (Quick Play gold, Boss Hunt, Survival, Training, ... their own), with a dark shade over each box's glass.
What an option opens appears as a second loadout box to the right (the pause menu's run summary sits there until you pick something): it powers on in place
the first time you pick something (the soft sweep below); picking another option while it is open only rewrites its text. A label that swaps to another (the list switching between the menu and Settings, a window's text) is overwritten in place: a cursor sweeps left to right with a few flickering scrambled glyphs at it, the new text behind and the old ahead (0.42 s); text that only appears or goes still types or backspaces. Esc
(or the window's X key) slides it back in; Esc again goes back. Window controls are digital: ON/OFF keys, segment keys,
blocky bar sliders with - / + keys, and keybind keys that read "PRESS KEY_" while listening. Opening plays once: the
box and its keys **sweep in** together in place (a wide, soft feathered edge glides down the box in 0.55 s with a faint glow just under it, showing it as it goes; no hard line, no flicker), the title types, its sub-line types a boot log (`> LINK OK > SAVE OK > WAVE n`)
and then the real line, a scan bar sweeps the keys and their texts type in; then nothing
animates. With Reduced effects or Visual effects off there is no animation at all (the box slides in instead). Closing slides the cabinet and window
out (Back or Esc), and only then does the window really close. The **pause menu** and the **lobby's Esc menu** **dock** instead (`Docs/Mockups/pause-dock.html`, option C): the box comes in from the
left (0.28 s) and the run summary (or the open window) from the right a beat later, each trailed by two fading frame outlines; as they land a soft
flash and a scan line run down them, the title types and the keys slide in one by one (0.18 s on, 0.03 s apart). The run summary's
parts (the wave, the next boss, the Data Gate, your run's stats, the players) slide in from the right in turn (0.22 s on, 0.04 s apart) and the
stats count up from 0. Closing sends them back out to
their sides in 0.22 s. With Reduced effects they slide in and out with no trails, flash or scan line.

- **Pause menu:** **Resume** (Esc), **Settings**, **How to play** (a window beside the list, its topics as keys along the top), **Settings** (the list switches to Audio, Visual
  effects, Gameplay, Keybinds and a Back key; each is a window), **Quit**, which asks first; **Run stats** (the Tab screen, so a paused party can look at it together) is a small key beside the list
  ("QUIT?", Keep playing picked to start with): **To lobby** (the host only: everyone beams out up a
  pillar of light, the screen goes dark and the lobby slides back in; the run stays saved to continue later), **To
  desktop** (closes the game), or **Keep playing** (Esc). The controls hint (Hold Tab, H) sits at the foot of the
  cabinet while paused. Choose with the mouse or ↑ ↓ and Enter. The frozen arena stays in view on the
  right with a **this run** screen (it fades while a window is out): the wave, time and score, chips for the path (and waves left), the run's modifiers
  and the difficulty, the next boss (moving), and this stretch's Data Gate (won, lost, or when the next one
  can open). The card runs down to near the bottom of the screen, with the players at its foot as compact cards
  (class, level, health; the Tab screen has the rest), stacked from the bottom up.
- **The lobby's menu** (Esc in the lobby; the main menu's neon edges and glowing corners on its box and windows): **Back to the lobby** (Esc again), **Settings** (the cabinet's list
  switches, like the pause menu's), **HOW TO PLAY** (in the drawer too) and **Quit game** (no Profile or Main menu
  rows: your lobby card opens the profile and the lobby's red back arrow leaves: the screen switches off like an old TV, squashing to a white line, and the main menu snaps back open from it; with Reduced effects or effects off the lobby folds back into the menu). The lobby has no Leave button or settings cog any more; its bottom
  bar is just **Ready** and **Start** (host), side by side in the middle, with who you're waiting on to their left.
- **Menu sounds:** the main menu and the Play screen have their own sounds: a tiny glassy tick on hovering a key or a saved run (every other menu's buttons use the same tick, quieter with Reduced effects), a power chord for Play and New run, a digital chirp for Join game and Settings (and for every other menu's button clicks; the lobby's buttons play the Settings tiles' light tick instead), a tube switching off for Quit (the game closes after it), three falling beeps for the eject key, a split-flap clack as each title letter lands, quiet bleeps while the rooms draw, a spark when the neon flickers, four access beeps and a chord as the Play screen opens (a falling two-note on Back), a warm chord for Continue, a sharp blip when DEL is armed and falling bleeps when a run is deleted, and a very low hum under the main menu and Who's playing? (on the Music volume).
- **The main menu** is a machine (`Docs/Mockups/main-menu-machines.html`, design B): at the top the **monitor**, a graphite case with
  the title on its CRT glass in a neon outline (each letter flips into place as it's typed; a blinking cursor after it) and a chin with
  ARENA CRT-14, two grilles, a power light and the **floppy drive** (READY in green with your profile's name) with its **eject key**
  beside it (it goes back to Who's playing?). Under the monitor, the **case**, in a neon outline: **Play**, **Join game** and **Wiki**
  as graphite keycaps down its left over a grille (each with a lamp: Play's is lit, Join game's lights while it is open; they
  press down when clicked), a **bay** in the middle behind a ribbed metal roller shutter, and on the right **your profile** on a small
  screen (a face, your name, rank title and number, the XP to the next rank; click it to open your **Profile**), then a recessed control
  strip under it (a small flat cog key for **Settings**, the POWER and DISK lights over their labels, a power key to **Quit**) and a grille level with
  the left one. Until you have opened your profile once (and for at most your first five launches on that profile), a small
  purple **YOUR PROFILE** tag beside the screen bounces toward it; clicking the tag opens the profile too. **Play** rolls the shutter up
  onto your saved runs (see Saved runs); **Join game** rolls it up onto the host's address and **Connect** (and how joining is going,
  with **Cancel**); **Settings** rolls it up onto **SETTINGS** on the bay's screen: four tiles (Audio, Visual effects, Gameplay, Keybinds;
  the arrow keys and Enter work too), and picking one switches the small screen off to a bright line and on again with that group's
  settings, all on the same screen (bar sliders with − and +, ON / OFF keys, rows of choices, key caps you click then press a key to
  rebind, the network port; a long page scrolls; each change flashes SAVED); **‹ BACK** or Esc returns to the tiles. Pressing the key
  again, or Esc, rolls the shutter back down. (The lobby's and pause menu's Settings keep the side drawer.) **Wiki** rolls it up onto
  **WIKI**: a search box (type straight away) over every class, role, weapon, ability, Ultimate, upgrade card, perk, enemy, boss,
  minion, gadget, relic, buildable, run modifier, path, game mode, difficulty preset, challenge room, arena layout, mechanic
  (Dodge, Parry, Wound, Elite, Mastery, Vectors...), status effect (Burning, Frozen, Bleeding... and on players Swallowed, Grabbed,
  Taunting), boss phase (Count, Gale, Penumbra... and the shared Last stand), boss attack (Searchlight, Inhale, Conductor...; each lists its
  boss and phase, and a phase lists its attacks) and prop or chest in the game, names first, then anything whose text matches (classes and roles in
  gold, cards and perks in green, enemies and bosses in red, gadgets, relics and buildables in amber, modes, paths and the like in
  cyan, minions in steel). Click a row (or Enter for the first) to open its page: what it is, its text, and its **references**: what it belongs to
  (a weapon's class and role, a card's weapons, a boss's faction...), what its text names (NAMED HERE) and what names it (MENTIONS IT),
  each one a link. **‹ BACK** or Esc steps back through the pages, then rolls the shutter down. New content joins the wiki on its own.
  The same wiki opens anywhere else as a panel over the screen: the **Wiki key** (I; Settings → Keybinds → Toggles) or the **WIKI**
  row in the pause and lobby menus. Solo, it pauses the run while open; in co-op it doesn't, and only you see it; Esc or a click
  outside closes it. **Hold the Wiki key** for the eyedropper: the text under the pointer gets a gold outline and, in a run, the enemy
  minion, player, shot (a player's opens their weapon), crate, barrel or chest under it counts too, and the tag lists an
  enemy's or player's status effects (a right click opens the first); a phase or attack name on a boss bar opens that boss's; a tag by the pointer names what a click opens (POINT AT SOMETHING when nothing), and a click opens
  its page (an enemy, a boss, a class, an ability on your HUD, a card's name...). The same eyedropper is the **PICK** key at the
  top of the wiki: the wiki steps aside (a solo run stays paused), and the next click on anything opens its page; Esc cancels. **New run** boots the lobby like a CRT out of the bay's screen: a
  link log types itself, squeezes into a bright line, and the line opens top and bottom like an old TV warming up to show the whole
  lobby. Its entrance is the end of the **disk boot sequence** (see Profiles): the case falls out from under the monitor, lands with a
  squash, a small bounce, a flare of its neon, the monitor jolting and dust puffing off its feet, then powers on (POWER, DISK, the key
  lamps one by one, the profile screen switching on like an old tube, a rattle of the shutter); any key fast-forwards it to the end (in half a
  second); with Reduced effects or Visual effects off, a short fade. Starting a run fades the machine as the bay's screen
  grows into the lobby. (How to play is in the lobby's and the pause menu's drawer.)
- **Settings** (main menu, the lobby's menu, and the pause menu; opened from the main menu or the lobby's button it has the holo look: neon edges and glowing corners on the list and its window, a glowing line with packets of light from the open group across to the window, the window open from the start, and its **X** (or Esc) closes Settings, so there's no Back key): four groups, **Audio** (level-meter volume sliders: 100 thin bars that turn amber then red near the top and ripple only while you drag, for
  **Master**, **Music** (all music; the **Music player** tab picks the track), **Your Weapons** (your and your friends' shots and spells), **Enemies** (enemy
  fire) and **Effects** (explosions, abilities, UI), and mute), **Visual effects** (with a live preview of what they
  do: shots and a hit, its number, an ability's name, a blast and its shake) and **Gameplay** (aim line, show the
  HUD, **camera lean**, **scroll-wheel zoom**, **Enemy names on hover** (off by default), the lag chart and lag log, **Teammate shots**, **First-person mouse** (the F8 view's turn speed), and the **network port** hosting and joining use, 7777 unless changed; everyone in a game needs the same one).
- **Profile:** four tabs. **Look** (the cosmetics and **Weapon skin**), **Rank** (Progress, Reward ladder, Tiers, XP sources), **Records** (Overview, Bosses with each one moving beside its count, Classes, **Recent runs**, **Challenge rooms** with each room played and
  won) and **Unlocks**. Mid-run and in the lobby it has no Look tab.
- **How to play** (in Settings: the drawer turns into its topics, and Back returns to the groups): Controls, The
  basics, Fighting, Between waves, Data Gates (with a little animated gate), Paths and modifiers, Playing
  together.

Esc goes back (from the pause menu's Settings, to its list). The same audio options are
under **Settings** on the main menu, and they're saved between sessions.

**Your own sounds:** any sound effect can be replaced by a file named after it (the names are the game's sound list, `Sfx` in
`Core/Entities.cs`, e.g. `ShotRifle`, `BossGong`, `UiClick`). Put a PCM WAV in the **sounds** folder next to the music folder
(`persistentDataPath/sounds`, read at startup), or an AudioClip in `Assets/Resources/Sfx` to ship it with the game; the folder
wins. `ShotRifle_1.wav`, `ShotRifle_2.wav`... give it several takes, played in turn. It keeps the sound's volume slider, its
shortest gap between plays and its small pitch wander. To make one, open `Tools/SoundForge/index.html` in a browser: pick
a preset, shape it with the sliders and export a WAV already named for the sound you chose (several takes at once if you like). It has an equalizer, an output
volume, and a seamless loop mode for hums and drones (the game loops `WallHum`, `ArcHum`, `HeliRotor` and `SawWhirr`).
**My presets** saves your own sounds in the browser; Export/Import moves them to a file and back.

**Visual effects** (the right-hand column of **Settings**, saved between sessions; all drawn on your own screen only,
so they never change the fight or what your friend sees):

- **Visual effects** on/off. Off draws the arena exactly as before.
- **Lighting** (on by default, needs Visual effects): shots, muzzle flashes, blasts, lightning (strikes and chains), Railgun beams, bursts (Nova, Shockwave, Frost bursts, parries, ...) and fire light the floor round them in their own colour, falling off with distance, and walls, pillars, crates, chests, barrels, turrets, players and enemies cast shadows from them; a faint haze of the same light hangs over the bodies, so light shafts show between the shadows. **Hard shadows** or **Soft shadows** (the default: the brightest 8 lights get blurred edges), and **Floor dim** Off (the default), Low or High darkens the floor so the lights stand out. Shots flying close together share one light, only the 24 brightest on screen cast shadows (the rest keep their own glow), and in a big crowd (80 to 320 enemies) the lighting steps back (fewer lights, hard edges) so it never adds lag where the frame is already heavy.
- **Screen shake:** Off, **Low** (the default) or Full. **Charge camera:** every hold-and-release weapon (Mender's Bow, Railgun, Warhammer, Golem Staff, Arc Lightning, Imp Caller, Gore Mortar) eases your view out and back against your aim as it charges, then snaps it forward past rest on release and settles, kicking by how full the charge was; the Railgun pulls back and kicks hardest, the Warhammer punches in most, the Imp Caller is lightest. A fully charged Railgun beam or Warhammer slam holds the picture for a moment on its first hit (about 2 and 3 frames). Off with Screen shake off or Reduced effects. Getting hurt, a heavy enemy's death and above all a boss's
  death kick the camera; explosions only add a light rumble (several at once don't pile up).
- **Arena floor:** **Bending** (the default) or Static. The floor grid lights up in each player's class colour near
  them; with Bending its lines also bow away from every player like a stretched sheet and trail a little behind them as
  they move. Static keeps the lines straight and only shows the colour. Kill ripples run through it either way, and a
  Dodge sends a small, quick ripple from where it started that lights the lines in your colour (your own dodges
  only). The keys under the title pick the floor's **pattern** (Bending / Static is the small lamp key beside it);
  each works in every arena and reacts the same way (bends round players unless Static, their colour near them,
  ripples from kills and your Dodge):
  - **Grid** (the default): the grid above.
  - **Flux:** a needle every 24 px combed along a slow drifting current; needles swirl round each player, swing into
    a runner's wake, and a kill's ring combs them outward and stretches them.
  - **Seismo:** horizontal phosphor lines every 16 px (every 4th brighter) with a slow refresh band rolling down;
    they lift into a bump round each player that leans behind them, and shake like a seismograph as a ring passes.
  - **Plates:** 32 px bevelled plates that sink under and round each player and flip like a split-flap board as a
    ring passes.
  - **Circuit:** traces on a 32 px grid (straight runs, 45 degree bends, pads), laid per room; they bow round players,
    and kills, dodges and footsteps send pulses racing along them.
- **Damage numbers:** damage to each enemy rises off it, merged every 0.15 s (so a fast weapon shows one total);
  big hits are larger and gold.
- **Ability names** (on by default): each ability's name over whoever casts it.
- **Reduced effects:** no screen shake, no hit-pause and no white flashes, for anyone they bother.
- **Background** (the menu background).

What it adds: the Tank Soldier's **Roar** (from [`Docs/Mockups/tank-abilities.html`](Docs/Mockups/tank-abilities.html)): a short pull-in, then three staggered rings with a filled blast and speed streaks, and each enemy flashes and pops a "!" as the wave reaches it; while the taunt runs, six armour plates turn round the soldier (flickering in its last second), a rim arc drains with the timer, dashed tethers run to the nearest ten enemies and a soft pulse goes out every second. **Bulldoze** shows its 60 px lane for half a second, then afterimages, speed streaks, dust off both heels, skid marks, a bow wave and a flicker for the invulnerable window; enemies in the lane flash and throw sparks (heavy ones more), and a double ring and a ring of dust mark where it ends. Both kick the camera (not in Reduced effects). Off, the old ring and arc draw as before. The Soldier DPS's abilities (from [`Docs/Mockups/soldier-dps-abilities.html`](Docs/Mockups/soldier-dps-abilities.html)): your own dashes (Leap Slam, the Royal Dive...) leave a tapered cyan comet streak that cools over 0.4 s, with sparks where you land (it was made for the old Dash); **Poison Dart** (from [`Docs/Mockups/poison-dart.html`](Docs/Mockups/poison-dart.html), look A, Toxic Drip) leaves your hand in a green puff and flies a steel needle with green fletching on a glowing green trail, splashing on where it lands, and stays stuck in the enemy it hit; poisoned enemies glow toxic green, grow teardrops that fall and splash, bubble, wear a lime rim, a green health bar and a drop-and-dart icon; a poisoned death flashes, bursts and flings droplets, leaves a bubbling puddle for a couple of seconds, and lobs a wobbling glob onto each enemy it spreads to, which splats there; the Twin Rifle's **grenades** (and Airstrike's bombs) burst in a white flash, an orange blast, eight streaking shards and smoke, the grenade showing a faint ring growing to its blast radius in flight; **Airstrike** (from [`Docs/Mockups/airstrike-rework.html`](Docs/Mockups/airstrike-rework.html), Carpet Wall) is a red smoke flare arcing to the cursor, a red lane with marching dashed edges and chevrons growing out to both walls, the jet's shadow sweeping ahead of the jet with two contrails, bombs falling with their shadows growing, craters, and flames along the lane while it burns; a **Claymore** is an olive curved plate on two legs: a gold ring fills round it and a dashed cone marches while it arms, then the cone glows faint orange between two red laser tripwires and its light blinks; tripped, the cone strobes red for the fuse, and the blast is a white-hot flash, a wide orange cone of fire with a gold edge and a spray of steel balls (with Visual effects off: plain cone outlines and a flat cone for the blast); **Chain Light** as a soft fan of light blooming out of your hand, with god-rays and a beam and halo on
each enemy it hits; **Arc Lightning** as its tagging cone filling with violet light from your feet (dashed edges crawling outwards, a scan line
sweeping across it), the staff glowing and sparking harder with every tag, a crackling brand and a live wire on each tagged
enemy, then a cyan bolt hopping down the chain, white-hot for a moment at each hop, and Forked Volley's bolts curving out to
alternate sides; **melee attacks** drawn as the weapon itself swinging through a glowing arc that throws sparks and leaves
embers (thrusts stab through a beam, the Lance's Breaching Thrust slides out through a glowing box, and the Warhammer is raised over your
shoulder as it charges, then swung over into the floor with a glowing circle and a pillar of light); an **additive glow** on bullets, blasts, sparks, bosses and players (enemy bullets are always drawn on top of every effect, spark and glow, so a busy fight never hides what you have to dodge); enemies **flash white and bulge**
when hit (only by a real hit: burning, bleeding and poison don't flash them; under steady fire the flash softens to a
light tint and the bulge to a gentle pulse, and a boss never goes more than 70% white, so it keeps its colour and shape); every health bar gets a **damage ghost** (a pale chunk where health was lost lingers for 0.45 s, then drains
after it); **hit-pause** (the picture holds for a few frames on a heavy enemy's death or a huge hit, at most every 0.4
s, and longer for a boss; each of **your own kills**, with any weapon, holds it one frame and kicks the screen a little, longer when three or more drop at once; a kill is yours when your shot ended on it or it died within reach of you);
**multikill calls** (your kills chained less than 0.6 s apart slam a call into the top middle of the screen, DOUBLE KILL,
TRIPLE KILL, QUAD KILL, RAMPAGE (5) and MASSACRE (8), warming from white through gold, orange and red to magenta, with
the count under it); the wave's **last kill** throws a white flash ring, a gold ring and a gold ripple across the floor
with a short hold, before the WAVE CLEARED band; a boss's death **slows time** to a quarter for about a second (the
host's slow reaches everyone; off with Reduced effects); enemies **shatter** into shards of their own shape with a white flash, sparks and a
**scorch mark** that fades to a faint stain in 8 s and stays for 45 s, so the floor shows how the fight went; **sparks** where shots land (fewer where many hits pile into one spot) and a flash at your muzzle; **trails** behind fast
shots; the **grid ripples** out from big blasts; a light tint for the path you're on or the boss you're
fighting; a faint **red tint in the corners** when you're
hurt, and below 30% health a **heartbeat** there (two beats, quicker and redder the lower you are); a gold **level-up** burst (bigger for a legendary)
with a white ring and gold ripple out from you that makes nearby enemies flinch; and a boss's **entrance card** (3.2 s,
with the HUD slid out of the way until it's done); the **ability bar's tiles react** (a tile pops with a white rim
when its cooldown finishes, bigger and gold for your ultimate; a returning second charge pops its pip; a used ability
presses its tile in; pressing one that's still recharging shakes it with a red rim and timer; Reduced effects keeps a
smaller pop and the red, without the shake or white flash); a health orb flying to you leaves a **trail** and a faint
link to you, and picking one up pulses green on you; a **puff of dust** at your feet when you set off, turn hard or
dash; and **weather on every path** (embers rising on Burning Grounds, snow on Frozen Lake, petals on Sanctuary, red
ash under the Blood Moon, mist motes in Fogbound, grit on Narrow Pass, pollen on Swarm Fields, wind-blown dust on
Sniper Ridge, gold motes on the Elite Gauntlet, lantern sparks at the Bazaar), fading in as the path starts.
**Weapons' shots have their own looks** (from [`Docs/Mockups/projectiles.html`](Docs/Mockups/projectiles.html)): the
Twin Rifle and Burst SMG fire tracers, the Pistol a light slug pushing rings of air, the
Scattergun flechettes, Ice Shards
(from [`Docs/Mockups/ice-shards.html`](Docs/Mockups/ice-shards.html)) faceted ice kites with glints that shatter on a hit; lightning flickers and forks with a white-hot core; Shuriken spin with motion blur;
Fireballs shed embers and smoke, the Golem's boulder dust, the Needle Swarm's thrown needles drips. The lobby's loadout preview shows the
new shot looks, trails, sparks, bullet endings and hit flashes too (not the glow). **The Soldier DPS's guns animate** (from [`Docs/Mockups/soldier-dps-gun-animations.html`](Docs/Mockups/soldier-dps-gun-animations.html)), keeping the glowing shots: the Twin Rifle is held out wide in both hands (**Spread arms**, from [`Docs/Mockups/twin-rifle-firing.html`](Docs/Mockups/twin-rifle-firing.html)), each muzzle where its stream starts and turned toward the crossing; each gun kicks back along its own barrel and climbs outward, flicks its charging handle, flashes at its muzzle with puffs out of the brake's side ports and a warm pool of light on the floor, joins gun and round with a bright streak on the round's first frame, and flips brass out of its port that lands, bounces, slides and stays on the floor for over a second; the recoil runs through you, each elbow folding with its gun's kick and the whole body jolting back (harder for a grenade); the barrels heat from the tip (orange, then white) the longer you hold fire, shimmer when hot and smoke after a burst; the Railgun's three coils light up one after another as you charge with a ring closing on the muzzle (gold in the Perfect window), its rails heat from steel to white-hot with the heat meter and shimmer when hot, and a shot is a white flash, arcs between the rails, a kick that shoves the rails apart (all bigger with the charge) and steam off the coils; the beam flashes white-hot with arcs crackling down its sides, then its charged path glows and sparks, and Heat Dump rolls out as a cone of fire with flames and embers; the Burst SMG's three barrel tubes turn faster and heat to white as it spins up, its flash grows, it drops brass and smokes once hot, and a faint cone shows the real spray tightening. The Twin Rifle's **grenades launch with Rocket assist** (from [`Docs/Mockups/twin-rifle-grenade-launch.html`](Docs/Mockups/twin-rifle-grenade-launch.html)): a backblast cone and a jet of smoke out of the back of the tube, a dull olive-white flash and a ring of smoke at its mouth, the firing gun alone kicking back and climbing outward, its breech dropping open to throw out a fat 40 mm casing behind you before it snaps shut; the grenade flies nose first with a flickering flame and a puffy smoke trail, leaving from the drawn tube and easing onto its real line. When your hits load a tube its breech drops open, a shell slides in and it snaps shut with a spark and a green ring. **Bullets end instead of
vanishing:** one that runs out of range shrinks and fades into a wisp of smoke; one that hits an enemy flattens into a
splash with sparks bouncing back; one that's blocked (your parry, a Barrier, a Shield Wall, a blocking minion, or a
Shielder's or Riot Shield's front) ricochets off, tumbling, with a bright spark; and one that hits a player bursts in
its own colour with a small ring (its name and the wave, the arena dimming for a moment). A boss's death bursts in
waves. Mocked up first in [`Docs/Mockups/visual-flair.html`](Docs/Mockups/visual-flair.html).

**Music:** everything is generated in code, like the sound effects. The menus and lobby play a synthwave
theme (`Assets/Scripts/Game/MenuMusic.cs`); runs play a soft track during regular waves, and **every boss
has its own track** that crossfades in while it's on the field (`Assets/Scripts/Game/GameMusic.cs`, where each
track is a short recipe: tempo, key, chords and which instruments play). Tracks render in the background, and
the next boss's track is prepared ahead of time. The **Music** slider controls all of it.

**Music player** (Settings > Audio > *Music player*): 24 tracks to pick from, which play everywhere (menus and matches)
instead of the automatic music until you pick **Automatic** again. Besides the menu theme, the lobby theme (a mix of *Lo-fi Lounge*, *Crystal Caves* and *Deep Freeze*), the wave battle track and the
ten boss themes (plus the Riot Captain's *Riot Line*) there are ten extra moods (`Game/MusicLibrary.cs`): *Lo-fi Lounge*, *Crystal Caves* and *Deep Freeze*
(Chill); *Neon Highway*, *Sunrise Run*, *Arcade Heist* and *Dust Road* (Action); *Haunted Hollow* and *Iron Rain* (Dark);
*Last Stand* (Epic). Controls: Prev / Play-Pause / Next, Shuffle, Repeat (all, or this one), a progress bar, and
"Boss themes still play" (on by default: a boss fight swaps in its own theme, then your track returns). Wheel scrolls the
list. Your pick, shuffle and repeat are saved. Tracks render in the background the first time (a few seconds).

**Your own music in Unity:** drop audio files into `Assets/Resources/Music` and they appear in the music player as
*Custom* tracks, no setup (for long songs set the clip's Load Type to Streaming). For more control make a **Music Pack**
(Assets > Create > Arena > Music Pack, saved as `Assets/Resources/MusicPack`): assign a clip to replace the **menu**
theme, the **wave battle** track or any **boss's** theme (with a volume each), give each **faction** its own battle
track (**Factions**: pick the boss; it plays during the regular waves leading up to that boss), and list **Tracks** with your own title
and mood. Anything you don't fill in stays generated. Code: `Game/MusicPack.cs`, `LoadMusicPack` in `MusicPlayerUi.cs`.

**Your own music from files (no Unity needed):** in the Music player click **Open folder** (the game's `music` folder inside its save data folder; it is
made for you), drop in `.wav`, `.mp3` or `.ogg` files, and click **Rescan** (the tab also rescans when you open it). They
join the list as green *Custom* tracks, named after the file (`my_song.mp3` shows as "my song"), and work with everything
above: shuffle, repeat, play in menus and runs. Tracks are levelled to a similar loudness and loop. A file the game
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
in the lobby, not while continuing a saved run). A bot takes the first role nobody has, a random class, weapon and 1st ability,
and readies up by itself; it revives, heals, dodges and uses its abilities like the other bots (see *Bots*, under *Testing tricks*), picks its own
upgrades and carries on between waves. On its card, the skill button cycles **Casual, Good, Expert**, **MIX** deals it a
new random loadout and **x** removes it. Click a bot's card (it gets a gold rim) to choose its role, class, weapon and
ability in the loadout panel, just like your own; click your own card to go back. With a bot's card chosen, type in the **Bot name** box above its preview to rename it. In the Hangout, bots stroll about the room and play emotes now and then (any emote, whatever their rank). Bots leave when you leave the lobby. A run with bots in the party isn't saved and isn't
counted in Records, XP or ranks, like the F1 bot. It still plays like any run: a Campaign with bots is on the Expanding arena, with building rounds and lives.

Without Tailscale, the host can forward **TCP port 7777** on their router to their PC and give the friend
their public IP. On the same Wi-Fi, use the host's local `192.168.x.x` address. Windows Firewall may ask
the host to allow the game the first time they host; click **Allow**.

### Lag chart (F5)

For finding out why a run feels laggy. Anyone can turn it on, host or guest: **F5** in a run cycles **off → pill →
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

Also: `--name=X`, `--port=N`, `--class=soldier|elementalist|summoner|knight|ninja|hemomancer|engineer`, `--weapon=1-4`, `--ability=1-4`, `--ultimate=N`, `--start-wave=N`
(jump ahead), `--bot` (your player moves and shoots by itself), and `--record`.

`Tools/SimTest` runs a host and a client against each other over localhost without Unity and checks that
they stay in sync (`Tools/SimTest/run.sh [start-wave] [seconds] [suite]`, needs Mono). The default suite is
`core`; `features` runs every feature test, `all` runs both, and `run.sh list` shows every suite. Tests live in
`Tools/SimTest/Tests/`, one file per system, and their random rolls are seeded so a run is repeatable
(`SEED=random` for fresh rolls).
### Developer tools (F1)

Press **F1** anywhere for the developer **dock** (Esc, F1 or its X closes it). It's a slim bar across the top of the screen. In a
match the game keeps running behind it (a click or scroll over the dock is the dock's alone: nothing under it is clicked and you don't shoot); in the lobby and menus it covers the
screen. On the bar: the wave, **GOD** (everyone), **FULL HEAL**, **SKIP WAVE**, **KILL ALL** and **SNAP** (run snapshots, below) pinned for one click, four keys that
open a small drawer under them (**WAVE**, **PARTY**, **VIEW**, **LABS**), and a **BOT** lamp you can click. The stat editor opens
as a drawer on the right, and the bigger tools (Spawn an enemy, Spawn an upgrade, Spawn a gadget, chest or relic, Run snapshots) open as one sheet. While you type in one of
its fields the game's keys are off. Outside a run only VIEW and LABS show.

- **WAVE drawer (host):** **Kill-all key (B)**: a switch (saved between sessions) that makes **B** kill every enemy on the field,
  bosses included, as your kills: XP, score, drops, gold and kill credit as usual, through shields and immunity. It
  doesn't end the wave: enemies still to come keep coming. Standing on a chest item, B throws it away instead. **Freeze enemies**: a switch under it that stops every enemy moving and shooting and stops new ones spawning
  (you can still kill the frozen ones; the wave won't clear while it's on; it resets when a run starts). The **PARTY drawer** has **Everyone** buttons: level up, max mastery, full heal and reset stats for every player at once (god mode and full heal are also pinned on the bar). The WAVE drawer's **Data Gates** buttons (between waves): straight into Crystal, Gatekeeper, Kill Rush, Zones or Mirror.
- **WAVE > SPAWN AN ENEMY… (host, while a wave is under way):** a sheet with every enemy (and a **BOSSES** tab) shown as its live model; filter by
  name, pick a **count** (1, 3, 5, 10) and **Elite**, then click one to put them in a row in front of you. A boss comes in the middle of the
  arena as on its wave (the Twin Reapers bring both twins). They count toward the wave like any enemy. Marks the run as a dev run.
  **Spawner** (a switch under the filter): a click places a spawner pad of that enemy in front of you instead (not bosses; any time in a
  run). While a wave is on it brings one in every **1 / 2 / 4 / 8 s**, never more than **MAX 1 / 3 / 5 / 10** of its own alive (Elite
  makes them elite). The pad shows a ghost of its enemy, a ring filling to the next one and a pip per enemy alive; its tag says what and
  how often. Up to 12 at once; **UNDO** takes the newest away, **CLEAR** all of them (their enemies stay). The wave won't clear while one
  keeps refilling.
- **PARTY > SPAWN AN UPGRADE… (host, in a run):** a sheet listing every upgrade (filter by name or text, or by rarity, including weapon mods and milestone
  cards; pick whose build it is for with several players). Click one and it drops a few steps in front of that player exactly as a treasure
  chest's item does: only they see it, walk onto it, then **F** takes it and **B** throws it away (the wave waits for it, like a chest's). Only
  upgrades their build could be offered are clickable (greyed: another class, a missing ability or weapon). Marks the run as a dev run.
- **PARTY > ITEMS & CHESTS… (host, in a run):** pick whose it is with several players. **GOLD +10 / +50 / +250 / +1000** and **VECTORS
  +1 / +5 / +20 / +100** scatter that much in front of them as their own coins or vector pickups: walk over them (a full vector wallet
  leaves vectors lying). Click a gadget (Magnet, Recall, Energy Drink) and a full
  one drops in front of that player as their own copy: walk onto it to pick it up. Click a chest (Upgrade, Gold, Vector, Relic, Piece,
  Buildables, Gadget) and one stands in front of them, opened like any chest (everyone gets their own item); it doesn't hold the wave. Click a relic (every one, by boss faction) and it goes
  straight into their relic slot, replacing the old one (hover for what it does); **REMOVE RELIC** empties the slot. Marks the run as a dev run.
- **PARTY > EDIT STATS… (host):** choose a player, then edit any of their stats by hand: type a number or use - / +.
  Type in **Filter** to narrow the list (e.g. "barrier"). Edited stats show in gold, are sent to every player's
  game, and stay on top of upgrades they pick later; **Reset stats** goes back to what their upgrades give.
  Also **God mode** (takes no damage), **Full heal**, **Level up** (an extra upgrade pick) and **Max mastery**
  (every ability they have to mastery 5). Joining players can see everyone's stats but not change them.
- **VIEW drawer:** **place traps** (host, in a run): pick Turret, Spike or Slow and a size, then click the arena to put one down (right click takes one away, **CLEAR** all of them); and a bot that plays for you. **LABS drawer:** the **Cell Editor**.
  **Account rank** (PARTY drawer) sets this computer's rank without playing for it: **+1**, **+10**,
  **30**, **MAX** (1000) and **RESET** (rank 1). Cosmetics the new rank hasn't reached come off. **MAX** also
  unlocks everything (every Boss Hunt, every perk, all perk slots, the Wildcard; saved on the profile) and **RESET** locks it
  again. Outside a run (main menu or lobby) the dock bar has a **MAX RANK + UNLOCK ALL** key instead.

### Run snapshots (F6 / F7)

Save a run at any moment and load back to that exact state, e.g. to try the same boss phase or bug again and again.
**F6** in a run saves a snapshot (a line at the top says so), **F7** loads the newest one. **F1 → SNAP** opens the **Run
snapshots** sheet: type a name and **Take snapshot**, or **LOAD**, **Rename** (Enter or Save keeps it) or **Delete** any saved one (newest first, with its mode,
wave, enemy count, run time and party). A snapshot keeps everything in the run: players and builds, enemies and their
AI, shots in the air, the map, timers and the random rolls, so after loading, the run plays on exactly as it did (the
bot does the same thing again too). Files are kept in the profile's `snapshots` folder (**Open folder**), the newest 40.
- Host only, and loads only with no guests connected; start any solo run first, then load (the mode, map and party all come from the snapshot).
- Loading marks the run as dev-touched (no records), like god mode. Your BOT switch stays as it is.
- Snapshots still load after code changes: a field that's gone is skipped and a new one keeps its current value (the
  sheet's note says how many). A renamed class can't load; the note says which.

### Bots

- **Bot team play** (lobby bots, the F1 bot and the command-line lab's bots): a hurt bot walks to healing orbs and the team's
  healing zones (Haven, Healing Rain, Soul Lantern, World Tree, Consecrated Ground, and so on). A Soldier DPS bot
  plants a **Claymore** when its target is within 420 px. Healer
  bots cast a Medkit, Healing Rain, Soul Lantern, Holy Cross or Lumen Ward at the downed or most hurt ally, and aim a
  Mender's Bow arrow or Transfusion Dart through a hurt teammate within 420 px. A Roar tank roars when a teammate is
  hurt or down with enemies on top of them (or when it is under half health); a Knight tank plants **Last Stand** over
  a downed ally and keeps it up while they're being stood back up. Tests: `Tools/SimTest/run.sh 1 30 botteam`.
- **Bot dodge and sprint** (same bots): a bot **dodges** when every step it could walk still gets it hit and a dodge
  clears it (hits only count once the dodge's invulnerability ends; a shot a ready perfect parry would take is left to the
  parry), and when it has a long way to go (over 450 px past where it wants to be) with nothing to shoot and nothing
  coming, it holds the dodge on into a **sprint**. It doesn't spam it: it waits a gap on top of the cooldown, Casual 3 s
  (and needs more danger), Good 2.2 s, Expert 1.5 s, and 7 / 5 / 3.5 s between sprints. Tests: `run.sh 1 30 botdodge`.
- **Bot parry and weapon Alts** (same bots): a bot raises the **parry** toward a shot or a sword cut it can't step out of,
  timed for a perfect parry (a Casual bot's timing drifts, a Good one blocks once its best step still looks risky, an
  Expert takes any clean perfect parry), keeps it up while more come from that side, and drops it when the side is clear
  or the shield is under a quarter. It also uses its **weapon's Alt** the way a person would: Double Barrel and
  the Scattergun's Shock Grenade point-blank, mines, gas and Twin Rifle grenades on crowds or the boss, Hook & Haul on an enemy just
  out of reach, Twin Fang and the Moonwheel at range, the Sunlance through a hurt or downed ally, Censer Cast once Embers
  are lit, Absolute Zero, the Tempest, Out of Ammo's spin, the Nunchaku guard, the Pistol Whip and the Bulwark's Brace
  under a barrage, the Kusarigama whirl and Undertow on a group, the Katana switch to the other stance's finisher, Dragon Fang Kunai
  blinks, Rocket Dash, the Twin Rifle Auto lock, Stonespear volleys, Embolus bursts, Sympathy, Martyr's Leash, the honey
  spray and the smoke drum. The Shuriken's Great Star stance isn't used (it
  needs a drawn route). Tests: `Tools/SimTest/run.sh 1 30 botweapon`.
- **Bot level-up picks and shopping** (same bots): a bot takes the card worth the most to it instead of a random one.
  It weighs what the card does to damage, fire rate, extra shots, health, damage taken, cooldowns and speed by its role
  (a tank values health and damage taken, a healer cooldowns), counts cards made for its weapon, class or ability above
  other special cards, adds for rarity and a new ability while a key is free, and values a card it already has less as
  its copies weaken. At the shop it heals first when under half health, then buys the shelf card worth the most for its
  gold (skipping cards worth little to it), a revive token if it has none, adds stock only with gold to spare, tops up
  its health, and is done. Tests: `Tools/SimTest/run.sh 1 30 botpicks`.

**The bots** play like a person. They plan a short way ahead, moving in the 8 WASD directions to avoid where
bullets, enemy bodies, lightning strikes and the Reapers' tether will be. When one of the Warden's **gates** is coming, they head for its nearest door (or the shot-out lock) in time and walk through it. They keep a distance that suits
their weapon and role, close in until a target is within their weapon's reach before they pull the trigger (using anything already in reach on the way), lead their shots, and go for the adds that protect a boss first (phylacteries,
gravity wells, the Queen's shield drones). When a Tank's **Last Stand** is planted they stand in its lee, just behind the tank and away from where the plates face (unless someone needs reviving). They revive downed allies, heal when someone is hurt, use
defensive abilities when things get tight, and use the rest on the boss.

| Skill | Reacts after | Plans ahead | Keeps track of | Changes direction |
|---|---|---|---|---|
| Casual | 0.35 s | 0.3 s | nearest 6 bullets | ~4 times a second |
| Good | 0.24 s | 0.4 s | nearest 12 bullets | ~7 times a second |
| Expert | 0.15 s | 0.5 s | nearest 25 bullets | ~12 times a second |

Bots don't reroll upgrades. (The same bots can join your own lobby: see **Bots** under Playing with a friend.)

From the command line, `Tools/SimTest/run.sh 1 1 lab <wave> <skill 1-3> <runs> <seat> [seat] [seat]` runs
boss fights with bots without drawing and prints the results. A seat is role/class/weapon, for example `T/S/0` (Tank
Soldier, first weapon), `H/M/1`, `D/U/2` (U = Summoner) or `T/K/0` (K = Knight).

### Balance probes

`Tools/SimTest/run.sh 1 1 ttk [last-wave]` is a balance probe: it plays a whole run with invulnerable bots
that level up and pick upgrades at random, and prints how long each boss survived.

## Code layout

See [`CODEMAP.md`](CODEMAP.md) (one line per source file) and section 2 of [`HANDOFF.md`](HANDOFF.md) (the folder overview).
