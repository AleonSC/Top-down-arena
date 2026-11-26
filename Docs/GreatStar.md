# Shuriken: Great Star stance (design)

Status: **built** (Nov 2026, protocol 348). The owner chose concept **B, Trace the Path**, with hits on the way back too.
Mockup: [`Docs/Mockups/shuriken-great-star.html`](Mockups/shuriken-great-star.html) (https://claude.ai/artifact/7jSAgH8LF3qpkRchxSm2nj).
Concepts A (Swipe & Bend) and C (Wind-up & Flick) are in the same mockup and were not picked.

## How it plays
- **Left Alt** (weapon-action key) switches the DPS Ninja's Shuriken between the fan (as before) and the **Great Star**. Going into
  the Great Star pulls every stuck star out (the old recall).
- In the Great Star stance, **hold Fire** to draw a route of ink from your Ninja through the cursor, up to **900 px** of ink.
- **Let go**: one huge star flies the route at **1150 px/s**, cutting every enemy on it for **60** (x the weapon's damage upgrades;
  +15% per extra projectile). A click without drawing throws it **600 px** straight at the cursor.
- At the end of the route it flies **home** at 1100 px/s, cutting everything it passes again for **half (30)**. Catch it to throw
  again (0.25 s). A shield or cover that stops it puts it straight back in your hand.

## How it's built
- `Core/GreatStar.cs`: the switch (`HostStarSwitch`, the old `WeaponAlt.Recall` kind), the route on your own game (`LocalGreatStar`),
  the throw (`Msg.StarThrow` guest -> host with the points; host -> all with bullet id, owner and points), the flight on every game
  (`TickGreatStars`; Msg.Steer keeps clients in step).
- `Game/GreatStarLook.cs`: the held star, the ink route and ink bar, the thrown star and its trail.
- Sounds: `Sfx.GreatStarThrow` (a rush, a steel shing, a fading whirr) and `Sfx.GreatStarCatch` (a steel clack and short ring), in `Game/NinjaSoundBank.cs`.
- Test: `Tools/SimTest/Tests/GreatStar.cs` (suite `fourmore`). Preview: `GREATSTAR=held|draw|fly NINJA=0 Tools/Preview/shot.sh x hud 2`.

## Not done
- Bots never use the Great Star (they stay on the fan).
