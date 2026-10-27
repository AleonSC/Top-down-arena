# Top-Down Arena

A co-op top-down bullet hell for **1 to 3 players** over the network, made in **Unity 6**.

**Party size scaling:** solo, enemies have 70% health; with two players, 100%; with three, regular enemies
have 120% health and waves have 25% more of them, and bosses have 150% health.
Pick a role, then survive endless waves of enemies with a boss every 5th wave.

There are no art assets: everything is drawn in code, and the game sets itself up when you
press Play, so there are no scenes or prefabs to wire together.

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

In the lobby each player picks a **role** (Tank, Healer, DPS), a **class** (Soldier, Mage, Summoner), then
one of that combination's three weapons and three Space abilities. Weapons and abilities are unique to
their role and class.

- **Soldier**: guns and gear, full health.
- **Mage**: spells (fire, frost, lightning, arcane), 85% health.
- **Summoner**: minions that come with your weapon (they stay between waves), 90% health.

| | Soldier | Mage | Summoner |
|---|---|---|---|
| **Tank** (240 HP, enemies prefer you) | Weapons: Scattergun, Flak Cannon, Riot Repeater<br>Abilities: Barrier, Roar, Bulldoze | Weapons: Flame Burst, Frost Lance (slows), Storm Hammer (chain lightning)<br>Abilities: **Arcane Aegis** (reflects bullets), **Frost Nova** (freezes nearby enemies), **Flame Wall** (burns enemies, blocks bullets) | Weapons: Golem Staff (+ bullet-blocking Golem), Bone Guard (+ 2 orbiting skeleton shields), Bear Totem (+ Bear)<br>Abilities: **Golem Wall** (3 stone walls), **Earthquake** (you and every minion slam), **Guardian Spirit** (eats nearby bullets for 8 s) |
| **Healer** (120 HP, healing aura) | Weapons: Light Bolt, Twin Wisps, Mender's Bow<br>Abilities: Mend Pulse, Haven, Soul Link | Weapons: Radiant Orb (heals allies it passes), Chain Light, Prism Beam (grazing heals)<br>Abilities: **Time Rewind** (health back to 4 s ago, even undoing a downing), **Arcane Ward** (60 HP shields), **Sanctify** (erase bullets, heal) | Weapons: Sprite Staff (+ 2 healing sprites), Treant Rod (+ Treant guard, healing bolts), Totem Staff (plants healing totems)<br>Abilities: **Spirit Swarm** (5 healing sprites), **Phoenix** (fights, and instantly revives a downed ally), **Soul Lantern** (allies near it can't go down) |
| **DPS** (95 HP) | Weapons: Twin Rifle, Railgun, Burst SMG<br>Abilities: Dash, Cluster Grenade, Mark for Death | Weapons: Fireball (explodes), Arc Lightning (chains to 4), Arcane Missiles (curving)<br>Abilities: **Meteor** (warning circle, then 150 damage), **Blizzard** (slowing storm), **Arcane Surge** (+60% damage, +2 pierce) | Weapons: Wolf Whistle (+ 2 wolves), Wisp Lantern (+ 3 shooting wisps), Imp Caller (exploding imps every 3 s)<br>Abilities: **Bat Swarm** (8 bats), **Sacrifice** (all your minions explode, then return), **Alpha Call** (minions frenzy) |

Generic upgrades (damage, fire rate, cooldowns, Second Charge, legendaries) work for every class; minions
use your damage and attack-speed bonuses too.

Players can pick the same role if you want. Everyone also slowly regenerates health (0.5 HP/s).

Enemies sometimes drop **health orbs**: small green ones heal 15 HP (18% chance), and rare large ones
with a gold ring heal 60 HP (3% chance). Bosses always drop a large orb plus four small ones. Orbs drift
toward a hurt player who gets close, are left alone by players at full health (so your ally can grab
them), and blink before vanishing after 12 seconds.

### Weapons

Each role picks one of three starting weapons in the lobby. Damage, fire rate, Multishot and Piercing
upgrades work with all of them.

| Role | Weapon | |
|---|---|---|
| Tank | **Scattergun** | Short-range spread of 5 pellets |
| | **Flak Cannon** | Slow, heavy shells that explode on impact |
| | **Riot Repeater** | Steady single shots with a little spread |
| Healer | **Light Bolt** | Accurate long-range bolts |
| | **Twin Wisps** | Two curving shots that cross in front of you |
| | **Mender's Bow** | Bolts heal your ally when they pass through them |
| DPS | **Twin Rifle** | Fast parallel twin stream |
| | **Railgun** | Slow, devastating shots that pierce 3 enemies |
| | **Burst SMG** | Sprays very fast, inaccurate shots |

### Ultimates

Right click (or **F**) fires your **ultimate**. Each role and class combination has its own, fixed one: it
comes with your role and class and only changes if you pick a different one. Ultimates have long cooldowns
that upgrades don't shorten, and start each match half charged. The HUD shows the charge in the gold slot.

| | Soldier | Mage | Summoner |
|---|---|---|---|
| **Tank** | **Fortress** (70 s): for 8 s every enemy targets you and you take 75% less damage; erases nearby bullets and shoves enemies back | **Glacial Tomb** (75 s): erases every enemy bullet and freezes every enemy solid for 4 s; bosses take 250 damage instead | **Titan** (80 s): a stone titan smashes enemies for 15 s and blocks bullets (soaks 1000 boss damage) |
| **Healer** | **Lifeline** (90 s): instantly revive and fully heal the whole party, plus 3 s of invulnerability | **Sanctum** (80 s): a 7 s holy dome; allies inside heal fast and can't go down, and bullets can't get in | **World Tree** (80 s): a tree for 10 s that heals allies, entangles nearby enemies and sends out 4 healing sprites |
| **DPS** | **Bullet Storm** (60 s): 6 s of triple fire rate with two extra volleys | **Meteor Storm** (70 s): 12 meteors rain on enemies over 3 s, 150 damage each | **Legion** (70 s): 6 wolves and 6 bats for 10 s, and all your minions frenzy |

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
| **Rare** | blue | **Second Charge** for your Space ability, an upgrade for the specific ability you picked (e.g. bigger Barrier, longer Roar, double-damage Bulldoze, bigger Haven, faster Soul Link, bigger grenades, harsher marks), wider aura; plus Multishot, Piercing Rounds and Overcharge (second charge on Q/E/Shift) |
| **Very rare** | purple | A brand-new ability in your **Q** slot, then **E**, then **Shift**: Shockwave, Overdrive, Blink, Nova or Turret (each one only once) |
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
| **Homing Rounds** | Shots curve toward the nearest enemy ahead of them; more stacks turn harder |
| **Ricochet** | After a hit, the shot bounces to another enemy within 300 px for 75% damage; +1 bounce per stack |
| **Incendiary Rounds** | Hits set enemies on fire for 3 s, burning 25% of the hit's damage per second |
| **Cryo Rounds** | Hits slow enemies 20% (up to 60%) with a 6% chance to freeze non-bosses for 1 s |
| **Shock Rounds** | 15% chance per hit for lightning to arc to 2 more enemies at 60% damage |
| **Explosive Rounds** | Shots explode on impact for 30% damage around them (bigger blast per stack) |
| **Velocity Rounds** | Shots fly 30% faster and farther, +15% damage |

Mods apply to your weapon's shots and your minions' shots, but not to grenades.

Rarer cards show up a bit more often as you level. All the numbers live in
`Assets/Scripts/Core/Upgrades.cs` if you want to rebalance or add upgrades.

## Rules

- The arena is 1600x900 and **wraps around for players**: walk off any edge and you come in on the opposite side. Enemies and bullets don't wrap, so the edges make a good escape route.
- Your hitbox is the small white dot in the middle of your character, not the whole circle.
- **Status effects** show as a tint on the enemy plus an icon above it: **snowflake** = frozen (encased in ice, can't move or shoot), **blue double arrow** = slowed, **flame** = burning (flames rise off it), **red crosshair** = marked (takes extra damage).
- At 0 HP you're **downed**. Stand next to a downed ally for 3 seconds to revive them (or the healer can pulse them back).
- If everyone is downed, it's game over. Clearing a wave revives anyone downed and heals everyone a bit.
- New enemy types unlock as the waves go up (see below), with a boss every 5 waves.

## Enemies

| Wave | Enemy | What it does |
|---|---|---|
| 1 | Grunt | Aimed 3-shot spreads |
| 2 | Burster | Rings of bullets |
| 3 | Sniper | Fast, accurate shots from far away |
| 4 | Spinner | Wanders and sprays spirals |
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

The game has **50 waves** with a different boss every 5 waves. Bosses after wave 20 get extra health (+10% per wave on top of the normal scaling: x2 at wave 30, x4 at wave 50) to keep up with stacked upgrades. Their name, health bar and warnings show at
the top. Beat The Eclipse at wave 50 to win; the host can then keep going in **endless** mode (C), where the
bosses come back around, tougher each time.

| Wave | Boss | Mechanic |
|---|---|---|
| 5 | **The Warden** | Cycles through spirals, rings of big orbs and aimed fans |
| 10 | **Hive Queen** | Summons tough Shards (3x normal health) and is **immune while 3+ of her swarm are alive** |
| 15 | **Twin Reapers** | **Soul Bond**: whichever Reaper is more than 10% further down on health is **immune** until you even them out, so split your damage. They circle the arena fast, and a **tether** between them sweeps it and hurts on touch (dash through it). Kill one and the other **enrages**; finish it within **6 s** or the fallen twin revives at 60% |
| 20 | **The Singularity** | Its **gravity phase** drags everyone toward it, then it collapses into dense rings. Four **Gravity Wells** orbit it and bend your shots into themselves, so kill them first; they come back 15 s after the last one dies |
| 25 | **The Colossus** | Stomps out huge rings with **one gap** to slip through, and throws rocks |
| 30 | **The Duelist** | Teleports every few seconds and leaves **decoys** that look just like it; decoys burst into rings unless killed |
| 35 | **The Lich** | **Immune while its phylactery stands** somewhere in the arena; break it, then strike before a new one appears. Raises skeleton grunts |
| 40 | **Storm Herald** | Calls **lightning strikes** where you stand (warning circle first). Keep moving |
| 45 | **The Devourer** | Gets **hungry** for 3 s of every 10: your shots **heal** it, so stop shooting |
| 50 | **The Eclipse** | Final boss in 3 phases: spirals and rings → gravity wells and phantoms → lightning storms and a frantic spiral |

## Controls

| | |
|---|---|
| Move | WASD / arrow keys |
| Aim | Mouse |
| Shoot | Hold left click |
| Role ability | Space |
| Ultimate | Right click or F |
| Bonus abilities | Q, E and Shift (once unlocked) |
| Pick an upgrade | Click a card (or 1 / 2 / 3), then Confirm (or Enter) |
| Reroll upgrades | Reroll button or R |
| Mute sound | M |
| Stats & upgrades (just for you) | Hold Tab |
| Pause menu | Esc |
| After game over / victory (host) | R = play again, L = back to lobby, C = keep going (endless, after victory) |

## End of a run

When a run ends (game over or victory) a results screen shows the wave reached, time, score and total
kills, plus a column per player: level, damage dealt, kills (elite and boss), biggest hit, accuracy,
healing done, allies revived, damage taken, times downed, abilities used, and the upgrades picked by
rarity. The best number in each row is highlighted, with awards like **Most damage**, **Best healer** and
**Sharpshooter**. The host can play again, go back to the lobby, or keep going after a victory.

## Loadout screen (Tab)

Hold **Tab** during a match to see your stats, weapon, abilities (with charges and cooldowns) and every
upgrade you've picked, rarest first. It only shows on your screen: the game **doesn't pause** and your
friend isn't affected, so you can check it mid-fight (you can still move and shoot while it's up).

## Pause menu and settings

Press **Esc** in a match to open the pause menu. Pausing **freezes the match for everyone** (your friend
sees who paused) and either player can resume. The menu has volume sliders for **Master**, **Weapons** (your
and your friend's shots and spells), **Enemies** (enemy fire) and **Effects** (explosions, abilities, UI),
a mute toggle, and **Leave game**. The same audio options are under **Settings** on the main menu, and
they're saved between sessions.

## Playing with a friend

**You both need the same version of the game.** The version is shown at the bottom of the main menu and
in the lobby. If they don't match, the joining player is turned away with a message saying so. After
pulling an update, everyone should rebuild (or the host should send the new build).

One person **hosts**, the other **joins**. The host's game acts as the server, so you don't need anything else.

1. Both of you install [Tailscale](https://tailscale.com/download) (free) and join the same tailnet.
2. Host: open the game, click **Host game**. The lobby lists your addresses; give your friend the one starting with `100.`.
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
  - `Game.cs`: the match: host simulation (enemies, bullets, waves, damage) and network sync
  - `Session.cs`: hosting, joining, lobby and role selection, message types
  - `Net.cs`: small TCP transport
- `Assets/Scripts/Game/ArenaApp.cs`: the Unity side: input, drawing (GL), menus and HUD (IMGUI)
- `Assets/Scripts/Game/SoundBank.cs`: sound effects, synthesized in code (no audio files). Each weapon has
  its own gunshot recipe (muzzle crack, body, thump, room tail, reverb) you can tweak
- `Assets/Editor/ArenaEditor.cs`: creates the scene and the **Arena** build menu
