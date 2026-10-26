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

Both players can pick the same role if you want.

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
| Ability | Space or right click |
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

Also: `--name=X`, `--port=N`, `--start-wave=N` (jump ahead), `--bot` (your player moves and shoots by itself).

`Tools/SimTest` runs a host and a client against each other over localhost without Unity and checks that
they stay in sync (`Tools/SimTest/run.sh [start-wave] [seconds]`, needs Mono).

## Code layout

- `Assets/Scripts/Core/`: plain C# with no Unity dependency
  - `Defs.cs`: role, enemy and bullet stats; tweak numbers here to rebalance
  - `Game.cs`: the match: host simulation (enemies, bullets, waves, damage) and network sync
  - `Session.cs`: hosting, joining, lobby and role selection, message types
  - `Net.cs`: small TCP transport
- `Assets/Scripts/Game/ArenaApp.cs`: the Unity side: input, drawing (GL), menus and HUD (IMGUI)
- `Assets/Editor/ArenaEditor.cs`: creates the scene and the **Arena** build menu
