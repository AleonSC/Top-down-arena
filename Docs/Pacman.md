# Pac-Man profile: a Pac-Man game for Pacman/Chomper/Waka profiles

Owner (Nov 2026): "do one for 'pacman' variants that loads in a pacman-type game that uses the chomper as the player and the
roles, dps tank healer and wildcard as the ghosts. bullet are the dots to eat." Then: "oh no i wanted it to play like pacman".

**Status (Nov 2026, protocol 243):** steps 1 (the disk and the arena skin), 2 (the maze simulation, `Core/Pacman.cs`, suite `pacman`) and 3 (the one-player screen, `Game/PacmanUi.cs`) built. Next: step 4, multiplayer.

## Decisions (from the owner)

- **How it starts:** with a profile whose name has Pacman, Pac-Man, Chomper or Waka in it (disk look `"pac"`, kept when renamed),
  **Play opens the Pac-Man maze game** instead of the arena. Other profiles never see it.
- **How it plays:** the classic maze. Eat every dot to clear the level, power pellets let you eat the ghosts, 3 lives, each level a bit
  faster. The four ghosts are the roles: **DPS red** (chases you head-on), **Tank cyan** (cuts you off, aiming past you through a tile ahead), **Healer pink**
  (ambushes four tiles ahead), **Wildcard orange** (chases, then wanders off when close). Chase / scatter waves like the original.
- **Multiplayer:** the game stays 1-3 players. **Joining another player who has a Pac profile adds them to the maze** as another
  chomper (a Pac profile joining a Pac game; how a non-Pac profile joins is an open question below).
- **Kept from step 1:** the chomper, ghosts and dots drawing (`Game/PacmanLook.cs`) is reused by the maze game.

## Step 1 (built): the look

- Disk: `DrawPacDisk` (black floppy, blue maze-edged label, chomper eating dots, a ghost). Names: `ProfileStore.DiskLookFor`.
- Arena skin while a Pac profile is active: players draw as chompers, regular enemies as ghosts coloured by role (support = pink,
  Pressure/Hunter = red, Anchor and plated types = cyan, the rest orange), every bullet as a dot. Front end only; preview
  `PAC=1 shot.sh x who`, `PACSKIN=1 shot.sh x hud 6 20 2`.

## Step 2 (built): the maze simulation (`Core/Pacman.cs`, no Unity)

- A 28 x 31 maze from a text layout (walls, dots, 4 power pellets, ghost house, side tunnel).
- Fixed tick like the rest of the sim. Chompers move on the grid with a queued turn (like the original: press a direction early, it turns
  at the next junction). Ghosts pick the tile that gets closest to their target at each junction, never reversing, except on a mode change.
- Modes: scatter / chase waves, frightened (blue, slow, edible, short), eaten (eyes run home). Score: dot 10, pellet 50, ghosts 200/400/800/1600
  in a row, bonus fruit twice a level.
- Lives, level clear, speed ups, extra life at 10,000.
- Tests in a new `pacman` suite: dots counted, a turn queues, tunnels wrap, each ghost's target, frightened and eaten, level clear.

## Step 3 (built): the front end, one player

- `Game/PacmanGame.cs`: draws the maze (blue walls), dots, chomper, ghosts (`DrawPacGhost` / `DrawPacChomper`), the score, lives and "READY!".
- Play from a Pac profile opens it; Esc pauses. WASD / arrows to steer.

## Step 4: multiplayer

- Host-authoritative: the host runs the sim, the others get a snapshot (new `Msg` values at the end, **protocol bump**, line in `Docs/ProtocolHistory.md`).
- A player who joins with a Pac profile appears as another chomper (own colour tint). Ghosts chase the nearest chomper; a caught chomper loses a
  shared life. Co-op score is summed.

## Step 5: polish

Waka-waka and the start jingle (`SoundBank.cs`), death animation, fruit, the intermission cartoons, a high-score table in the profile.

## Open questions

- Can a non-Pac profile join a Pac game (as a plain chomper) or only Pac profiles?
- Co-op lives: step 2 uses one shared pool; one each is a small change if you prefer it.
- Should the arena skin stay on for Pac profiles when they play the normal arena some other way (a mode in the lobby)?

## What step 2 built, and where it differs from the original

- `PacmanGame(players, seed)`: `SetInput(chomper, dx, dy)`, `Step(dt)`, read `Chompers`, `Ghosts`, `Cell`, `Score`, `Lives`, `Level`, `State`
  (Ready, Playing, LevelClear, Over), `FrightT`, and drain `Events` for sounds. Deterministic for a seed.
- The maze is the classic look but has 238 dots (the original has 240) and the ghost door is two tiles wide.
- Shared lives: one pool for everyone; a chomper that dies loses one and comes back at its start, safe for 2 s. Out of lives: it stays out; the game ends when every chomper is out.
- Ghost release: pink at once, cyan after 30 dots, orange after 60; if nobody eats for 4 s, the next one comes out anyway.
- Not built yet: the bonus fruit, the "no turning up" junctions, the dot-eating pause, Cruise Elroy (red speeding up), the 244-dot count.

## What step 3 built

- `Game/PacmanUi.cs`: Play on the main menu opens it for a Pac profile (`Menus.cs`). Black screen, blue maze outline, dots, blinking power pellets,
  the chomper (mouth chews while it moves, opens until it vanishes when it dies), the four role ghosts (blue and flashing when scared,
  eyes only when eaten), 1UP / HIGH SCORE / LEVEL, lives in reserve, READY! and GAME OVER.
- Keys: WASD, your move binds, or the arrows; Esc pauses (Esc resumes, Q leaves); Enter plays again after GAME OVER; Esc leaves.
- The high score lasts until the game is closed (it is saved with the profile in step 5). No sounds yet (step 5).
- Preview: `Tools/Preview/shot.sh x pacman` (`PACKEYS=l1.7,u0.6 PACFRIGHT=5 PACDIE=0.7 PACOVER=1 PACPAUSE=1`).
