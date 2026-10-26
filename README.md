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

## Roles

| Role | HP | Weapon | Ability (Space / Right click) |
|---|---|---|---|
| **Tank** | 240 | Short-range 5-pellet shotgun | **Barrier** (12 s cooldown): a 4 s bubble that deletes enemy bullets. Enemies prefer to target the tank. |
| **Healer** | 120 | Single bolt | **Mend Pulse** (10 s): heals everyone nearby for 60 and instantly revives downed allies in range. Passive aura slowly heals an ally standing close. |
| **DPS** | 95 | Fast twin-stream rifle | **Dash** (3.5 s): quick dash with brief invulnerability. |

Both players can pick the same role if you want. Everyone also slowly regenerates health (0.5 HP/s).

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
| **Rare** | blue | Ability upgrades: **two charges** for your Barrier / Mend Pulse / Dash, bigger and longer Barrier, stronger Mend Pulse, wider aura, longer Dash invulnerability; plus Multishot, Piercing Rounds and Overcharge (second charge on Q/E/Shift) |
| **Very rare** | purple | A brand-new ability in your **Q** slot, then **E**, then **Shift**: Shockwave, Overdrive, Blink, Nova or Turret (each one only once) |

Rarer cards show up a bit more often as you level. All the numbers live in
`Assets/Scripts/Core/Upgrades.cs` if you want to rebalance or add upgrades.

## Rules

- Your hitbox is the small white dot in the middle of your character, not the whole circle.
- At 0 HP you're **downed**. Stand next to a downed ally for 3 seconds to revive them (or the healer can pulse them back).
- If both players are downed, it's game over. Clearing a wave revives anyone downed and heals everyone a bit.
- New enemy types unlock as waves go up: grunts (aimed spreads), bursters (rings), snipers (fast shots), spinners (spirals), and a boss every 5 waves.

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
| Leave | Esc |
| After game over (host) | R = play again, L = back to lobby |

## Playing with a friend

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

Also: `--name=X`, `--port=N`, `--weapon=1|2|3`, `--start-wave=N` (jump ahead), `--bot` (your player moves and shoots by itself).

`Tools/SimTest` runs a host and a client against each other over localhost without Unity and checks that
they stay in sync (`Tools/SimTest/run.sh [start-wave] [seconds]`, needs Mono).

## Code layout

- `Assets/Scripts/Core/`: plain C# with no Unity dependency
  - `Defs.cs`: role, enemy and bullet stats; tweak numbers here to rebalance
  - `Upgrades.cs`: the upgrade cards, rarities and the extra abilities
  - `Game.cs`: the match: host simulation (enemies, bullets, waves, damage) and network sync
  - `Session.cs`: hosting, joining, lobby and role selection, message types
  - `Net.cs`: small TCP transport
- `Assets/Scripts/Game/ArenaApp.cs`: the Unity side: input, drawing (GL), menus and HUD (IMGUI)
- `Assets/Scripts/Game/SoundBank.cs`: sound effects, synthesized in code (no audio files). Each weapon has
  its own gunshot recipe (muzzle crack, body, thump, room tail, reverb) you can tweak
- `Assets/Editor/ArenaEditor.cs`: creates the scene and the **Arena** build menu
