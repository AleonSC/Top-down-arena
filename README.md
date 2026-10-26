# Top-Down Arena

A co-op top-down bullet hell for two players over the network, made in **Unity 6**.
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

Both players can pick the same role if you want. Everyone also slowly regenerates health (0.5 HP/s).

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

## Levels and upgrades

Every enemy killed gives **both** players XP. Level up during a wave and you'll see **LEVEL UP!**; when
the wave is cleared, each player picks one of **3 upgrades per level gained**: click (or press 1/2/3) to
select a card, then **Confirm** (or Enter). Don't like the cards? **Reroll** (or R) for a fresh set. You
start with 2 rerolls and everyone gets 2 more for each boss killed. The next wave waits until both players
have chosen.

| Rarity | Color | What you get |
|---|---|---|
| **Common** | white | Small stat boosts: +8% damage, +8% fire rate, +6% speed, +15 HP, regen, -6% cooldowns |
| **Uncommon** | green | Bigger boosts (+20% damage, +40 HP, -15% cooldowns...) and damage reduction |
| **Rare** | blue | **Second Charge** for your Space ability, an upgrade for the specific ability you picked (e.g. bigger Barrier, longer Roar, double-damage Bulldoze, bigger Haven, faster Soul Link, bigger grenades, harsher marks), wider aura; plus Multishot, Piercing Rounds and Overcharge (second charge on Q/E/Shift) |
| **Very rare** | purple | A brand-new ability in your **Q** slot, then **E**, then **Shift**: Shockwave, Overdrive, Blink, Nova or Turret (each one only once) |
| **Legendary** | orange | Unique weapon upgrades, **only from bosses**: after a boss dies, each player picks one of that boss's three (can't be rerolled) |

| Boss | Legendary weapon upgrades |
|---|---|
| The Warden | **Warden's Halo** (every 4th volley adds a ring of 8 shots) · **Iron Volley** (+1 projectile, +25% damage) · **Ringburst** (kills burst into 6 shots) |
| Hive Queen | **Swarm Rounds** (shots split into 3 on hit) · **Royal Jelly** (hits heal 1 HP, up to 10/s) · **Brood Mother** (20% of kills spawn a 4 s mini-turret) |
| Twin Reapers | **Executioner** (double damage below 35% HP) · **Twin Fang** (25% chance of double damage) · **Soul Harvest** (kills heal 5 HP) |
| The Singularity | **Collapse Rounds** (shots implode on impact) · **Graviton Rounds** (hits slow enemies 40%) · **Dark Matter** (+2 pierce, +20% damage) |

Rarer cards show up a bit more often as you level. All the numbers live in
`Assets/Scripts/Core/Upgrades.cs` if you want to rebalance or add upgrades.

## Rules

- Your hitbox is the small white dot in the middle of your character, not the whole circle.
- At 0 HP you're **downed**. Stand next to a downed ally for 3 seconds to revive them (or the healer can pulse them back).
- If both players are downed, it's game over. Clearing a wave revives anyone downed and heals everyone a bit.
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
| 12+ | **Elites** | Any enemy can spawn with a gold ring: 2.5x health, bigger, shoots faster, worth 3x XP, always drops an orb. More common each wave |

## Bosses

Bosses rotate every 5 waves and come back stronger each cycle. Their name and health bar show at the top.

| Wave | Boss | Mechanic |
|---|---|---|
| 5 | **The Warden** | Cycles through spirals, rings of big orbs and aimed fans |
| 10 | **Hive Queen** | Summons Shards every few seconds and is **immune while 3+ of her swarm are alive**. Clear the swarm, then hit her |
| 15 | **Twin Reapers** | Two bosses circling the arena. Kill one and the other **enrages**; finish it within **8 seconds** or the fallen twin revives at half health |
| 20 | **The Singularity** | Sits in the center. Every 12 s it opens a **gravity well** that drags everyone toward it (run against it!), then collapses into dense rings |
| 25+ | ... | The cycle repeats with tougher versions |

## Controls

| | |
|---|---|
| Move | WASD / arrow keys |
| Aim | Mouse |
| Shoot | Hold left click |
| Role ability | Space or right click |
| Bonus abilities | Q, E and Shift (once unlocked) |
| Pick an upgrade | Click a card (or 1 / 2 / 3), then Confirm (or Enter) |
| Reroll upgrades | Reroll button or R |
| Mute sound | M |
| Pause menu | Esc |
| After game over (host) | R = play again, L = back to lobby |

## Pause menu and settings

Press **Esc** in a match to open the pause menu. Pausing **freezes the match for both players** (your friend
sees who paused) and either player can resume. The menu has volume sliders for **Master**, **Weapons** (your
and your friend's shots and spells), **Enemies** (enemy fire) and **Effects** (explosions, abilities, UI),
a mute toggle, and **Leave game**. The same audio options are under **Settings** on the main menu, and
they're saved between sessions.

## Playing with a friend

**You both need the same version of the game.** The version is shown at the bottom of the main menu and
in the lobby. If they don't match, the joining player is turned away with a message saying so. After
pulling an update, both players should rebuild (or the host should send the new build).

One person **hosts**, the other **joins**. The host's game acts as the server, so you don't need anything else.

1. Both of you install [Tailscale](https://tailscale.com/download) (free) and join the same tailnet.
2. Host: open the game, click **Host game**. The lobby lists your addresses; give your friend the one starting with `100.`.
3. Friend: type that address into **Host address**, click **Join game**.
4. Both pick a role; the host clicks **Start game**.

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

## Code layout

- `Assets/Scripts/Core/`: plain C# with no Unity dependency
  - `Defs.cs`: role, enemy and bullet stats; tweak numbers here to rebalance
  - `Upgrades.cs`: the upgrade cards, rarities and the extra abilities
  - `EnemyAI.cs`: waves, enemy behaviour and attack patterns, boss mechanics
  - `Kits.cs` / `Minions.cs`: minion and zone definitions; mage and summoner weapons, abilities and minion AI
  - `Game.cs`: the match: host simulation (enemies, bullets, waves, damage) and network sync
  - `Session.cs`: hosting, joining, lobby and role selection, message types
  - `Net.cs`: small TCP transport
- `Assets/Scripts/Game/ArenaApp.cs`: the Unity side: input, drawing (GL), menus and HUD (IMGUI)
- `Assets/Scripts/Game/SoundBank.cs`: sound effects, synthesized in code (no audio files). Each weapon has
  its own gunshot recipe (muzzle crack, body, thump, room tail, reverb) you can tweak
- `Assets/Editor/ArenaEditor.cs`: creates the scene and the **Arena** build menu
