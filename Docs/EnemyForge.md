# Enemy Forge

Status: **built** (Tools/EnemyForge). A desktop tool for the owner to design a new enemy from scratch, try it in a live
preview, and hand the design to Claude to implement.

## Using it

- Download `EnemyForge.exe` from the repo's **enemy-forge** release (rebuilt from `main` on every change) and
  double-click it. On Windows 10/11 it's a normal app window (WebView2, the Edge engine built into Windows; its
  DLLs ride inside the exe and unpack to `%LOCALAPPDATA%\EnemyForge`) and closing it quits. Nothing to install
  (SmartScreen may ask: More info, Run anyway). With no WebView2 runtime, or on Mac/Linux (`mono EnemyForge.exe`),
  it opens a browser window instead and quits a few seconds after that closes (the page holds `/api/alive` open).
- Tabs on the left: **Basics** (name, one line, role, faction, "plays like" a real enemy, first wave, numbers with
  the nearest real enemy shown next to each), **Look** (shape, colours, outline, face, spin, parts), **Movement**
  (Chase, Keep distance, Orbit, Kite, Lunge, Charge, Blink, Zigzag, Wander, Turret), **Attacks** (any number of:
  Aimed spread, Ring, Spiral, Sniper, Shotgun, Homing, Lob, Beam, Melee swing, Shockwave, Mines), **Traits** (split,
  death blast, death pool, front shield, reflect, heal, haste, enrage, summon, trail, phase, regen, slow aura, shared
  damage) and **Notes**.
- Middle: the preview arena (1/3/6 at once, a Tank/Healer/DPS dummy that stands still, circles, dodges or is played
  with WASD + mouse; it fires back), live numbers (damage per second on the player, time to down them, time to
  kill one) and a size line-up against the player and the nearest real enemies.
- Right: the **brief** and warnings. **Copy for Claude** copies it; paste it in a chat. **Save** writes
  `<name>.enemy.json` to an `Enemy Forge` folder next to the exe (Ctrl+S); **Designs** reopens them; **Import**
  takes a pasted design or brief. The current design is also kept between runs.

## The brief (what Claude gets)

Markdown: role, faction, "plays like" (`EnemyType`), numbers with the nearest real enemy, look, movement, one line
per attack and trait, notes, the numbers measured in the preview, an `EnemyDef` draft line for `Defs.Enemies`, and
the full spec as JSON (`forge: 1`; only switched-on traits). Implementing one: new `EnemyType` at the end (before
`None`), its `EnemyDef`, a brain (HANDOFF §2 "Adding an enemy"; start from the "plays like" enemy's), a look in
`Game/*Look.cs`, README entry, protocol bump only if a message or snapshot changes.

The preview is a simplified JS sim with wave-1 numbers, not the game's code: treat its timings and damage as
the intent, then balance in the real sim.

## Building

- `Tools/EnemyForge/package.sh [out]` builds the single-file `EnemyForge.exe` (Server.cs + Window.cs + Core, page and WebView2 DLLs embedded;
  fetches the WebView2 NuGet package into `out/` once).
- `Tools/EnemyForge/run.sh [port]` runs from source at `http://localhost:8190`, serving `web/index.html` live.
- `.github/workflows/enemyforge-release.yml` publishes the exe as the `enemy-forge` release on pushes to `main`.
