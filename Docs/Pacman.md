# Pac-Man profile: a Pac-Man game for Pacman/Chomper/Waka profiles

Owner (Nov 2026): "do one for 'pacman' variants that loads in a pacman-type game that uses the chomper as the player and the
roles, dps tank healer and wildcard as the ghosts. bullet are the dots to eat." Then: "oh no i wanted it to play like pacman".

**Status (Nov 2026, protocol 243):** step 1 built (the disk and the arena skin). The game itself is planned, not started.

## Decisions (from the owner)

- **How it starts:** with a profile whose name has Pacman, Pac-Man, Chomper or Waka in it (disk look `"pac"`, kept when renamed),
  **Play opens the Pac-Man maze game** instead of the arena. Other profiles never see it.
- **How it plays:** the classic maze. Eat every dot to clear the level, power pellets let you eat the ghosts, 3 lives, each level a bit
  faster. The four ghosts are the roles: **DPS red** (chases you head-on), **Tank cyan** (slow, cuts you off ahead), **Healer pink**
  (ambushes four tiles ahead), **Wildcard orange** (chases, then wanders off when close). Chase / scatter waves like the original.
- **Multiplayer:** the game stays 1-3 players. **Joining another player who has a Pac profile adds them to the maze** as another
  chomper (a Pac profile joining a Pac game; how a non-Pac profile joins is an open question below).
- **Kept from step 1:** the chomper, ghosts and dots drawing (`Game/PacmanLook.cs`) is reused by the maze game.

## Step 1 (built): the look

- Disk: `DrawPacDisk` (black floppy, blue maze-edged label, chomper eating dots, a ghost). Names: `ProfileStore.DiskLookFor`.
- Arena skin while a Pac profile is active: players draw as chompers, regular enemies as ghosts coloured by role (support = pink,
  Pressure/Hunter = red, Anchor and plated types = cyan, the rest orange), every bullet as a dot. Front end only; preview
  `PAC=1 shot.sh x who`, `PACSKIN=1 shot.sh x hud 6 20 2`.

## Step 2: the maze simulation (`Core/Pacman.cs`, no Unity)

- A 28 x 31 maze from a text layout (walls, dots, 4 power pellets, ghost house, side tunnel).
- Fixed tick like the rest of the sim. Chompers move on the grid with a queued turn (like the original: press a direction early, it turns
  at the next junction). Ghosts pick the tile that gets closest to their target at each junction, never reversing, except on a mode change.
- Modes: scatter / chase waves, frightened (blue, slow, edible, short), eaten (eyes run home). Score: dot 10, pellet 50, ghosts 200/400/800/1600
  in a row, bonus fruit twice a level.
- Lives, level clear, speed ups, extra life at 10,000.
- Tests in a new `pacman` suite: dots counted, a turn queues, tunnels wrap, each ghost's target, frightened and eaten, level clear.

## Step 3: the front end, one player

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
- Co-op lives: one shared pool or one each?
- Should the arena skin stay on for Pac profiles when they play the normal arena some other way (a mode in the lobby)?
