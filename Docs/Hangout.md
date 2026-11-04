# Lobby Hangout

A walkable room in the lobby. Every player has their character in it, wearing what their rank has earned, and moves
it with WASD (or the arrow keys, or whatever the movement keys are rebound to). Nothing to shoot, no abilities, no
collisions: it is somewhere to stand while the party sorts out loadouts and readies up. Cosmetic first, like Rank, with an optional
PvP fight (below).

## What you see

- A **Hangout** button on the lobby's Party heading (`LobbyUi.cs`); it shows the room in the right-hand panel and flips to "< Loadout" to return. The room is 2400 x 1600 units, one unit per
  UI pixel, seen through a ~790 x 580 window that follows you and stops at the room's border (a glowing teal rim). A 150 x 100 minimap sits at the window's top left (white box = your view).
- Each character is drawn with the real model for their role, class and weapon (the same drawing as the party cards).
  It faces the way it walks, and keeps its facing when it stops.
- **Trail**: their equipped rank trail flows behind them as they walk (`TrailPiece`, shared with the match and the
  cosmetics window, so all six trails look the same everywhere).
- **Frame and rank**: a name plate under the character with their rank badge, name, their full animated frame
  (a blue rim for you if you have none) and a green dot once they've clicked Ready.
- The room works in every lobby mode (Gauntlet, Boss Hunt, Practice Range, continuing a saved run).

## PvP and shooting (protocol 148)

Mockups: [`Mockups/hangout-pvp.html`](Mockups/hangout-pvp.html) (layout B picked, no slider) and [`Mockups/hangout-transition.html`](Mockups/hangout-transition.html) (look A).

- **PvP** is a checkbox under the minimap, off by default, per player. Off: you can't fire and shots pass through you. On: you face the mouse and hold the left button to fire.
- A shot comes from your loadout weapon's `FireInterval`, `Speed` and `Life` (clamped: 0.08-1.2 s, 350-1100 px/s, 0.25-1 s; a melee weapon fires a short lash). Damage is about 20 a second of fire for every weapon, so loadouts fight evenly. No abilities, pellets, beams or swings: that is the first thing to extend if you want it (`Session.ShotStats`, `LobbyCombat.cs`).
- Everyone has 100 health (a bar over anyone with PvP on). It regrows 15 a second, 3 s after the last hit. At 0 a character is knocked out for 2.5 s (a faded ghost, "KNOCKED OUT", can't move or fire), then the host puts it at a spawn spot at full health. Turning PvP off heals.
- The host applies every hit; each screen flies its own copy of every shot for drawing. A client's own shot shows at once, and the host's echo of it is skipped.
- **Grow transition** (Loadout to Hangout and back, 0.85 s, `DrawHangoutTransition`): the loadout fades out in the first 40% while the preview window (`ShowcaseRect`) stretches into the room and the ship scales and slides to its spot; the others, name plates, minimap and switch arrive over the last 40%. Any other tab switch, reduced effects and a resumed run snap. `HANGOUTANIM=0.5 HANGOUT=trail-void Tools/Preview/shot.sh hangmid lobby` freezes it part-way.

## Network (protocol 137, and 148 for PvP)

- `Msg.LobbyMove` (client to host): where this player's character is, which way it faces and its PvP switch, up to 15 a second, only
  while something changed.
- `Msg.LobbyPos` (host to all): everyone's position, facing, PvP switch, health and a respawn counter (so an owner knows the host moved it), up to 15 a second, only while something changed.
- `Msg.LobbyFire` (client to host): the angle a shot went. `Msg.LobbyShot` (host to all): owner, position and angle of a shot, for everyone to draw.
- Your own character is yours: it shows at once, and the host's echo of it is ignored. Everyone else's is smoothed
  towards the last position heard.
- The host keeps every position inside the room (and drops NaN and infinity), spawns each new arrival in a spot along the
  bottom of the room, and removes a character when its player leaves (`Session.SyncAvatars`, called whenever the lobby
  is broadcast). Moves are ignored once a match is running.
- Not saved anywhere: characters start at their spawn spots every time the party forms.

## Code

| File | What |
|---|---|
| `Core/LobbyCombat.cs` | PvP, shots, health, knock-out and respawn (`FireMyShot`, `SetMyPvp`, `StepLobbyCombat`, `ShotStats`) |
| `Core/LobbyAvatars.cs` | `Avatar`, the room's size, `MoveMyAvatar`, `StepAvatars`, sending and receiving the two messages |
| `Game/LobbyRoom.cs` | the Hangout tab: movement keys, the floor, drawing each character, its trail and name plate |
| `Tools/SimTest/Tests/Hangout.cs` | `run.sh 1 30 hangout`: spawn spots, moves each way, the walls, NaN, leaving |
| `Tools/Preview` | `HANGOUT=trail-void,trail-prism,trail-storm Tools/Preview/shot.sh hang lobby` |

## Ideas for later

Abilities in the Hangout, real weapon behaviour (pellets, beams, swings), walls and props to bump into, walking onto a pad to ready up, a chat bubble.
