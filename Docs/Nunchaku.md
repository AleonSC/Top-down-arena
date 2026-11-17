# Nunchaku: the Ninja DPS's fourth weapon (design)

Status: **built** (Nov 2026, protocol 368). Mockup: `Docs/Mockups/ninja-nunchaku.html`
(https://claude.ai/artifact/Fm5Agzse8eyNyfXk8cGVDz), variant **Twin Storm**. The page's other variants (Iron Storm, Twin
Dragons, Tidebreaker) were not picked.

> A black lacquered nunchaku with crimson bands in each hand. Fire is a heavy four-step combo; the weapon-action key switches to
> a defensive stance, the Twin Windmill, where Fire is a counter that grows with every shot the discs swat back.

## Decisions (owner)
- **Variant:** Twin Storm: Iron Storm's flow and smooth whip animation, but with a nunchaku in each hand.
- **Slot:** Ninja DPS, **Choice 3** (the 4th weapon key), after the Arc Knuckles in the Wildcard's list.
- **Alt:** switches to the defensive stance and back (a toggle, like the Rapier's Riposte), not a held key.

## Fire: the combo (the weapon's Damage is 20)
| Step | Move | Damage | Shape | Lands | Next press counts |
|---|---|---|---|---|---|
| 1 | Snap (right chuck) | 20 | a 95 px line, 22 px either side | 0.17 s | 0.23 s |
| 2 | Backhand (left chuck) | 22 | 140°, 90 px | 0.14 s | 0.24 s |
| 3 | Double Figure-8 (both) | 4 × 12 | 110°, 85 px, two crossings | 0.13 s, 0.25 s | 0.43 s |
| 4 | Twin Crush (both) | 68 | a 100 px ring 40 px ahead, stuns 0.7 s, shoves 38 px | 0.38 s | 0.75 s, then back to 1 |

- About 1.65 s for the chain, about 95 damage a second on one target. A second without a step and it starts over.
- Raising the parry resets it to the Snap (a step still winding up never lands).
- **+1 (extra projectiles):** an aftershock ring after the Twin Crush, 40 px farther out and 0.2 s later, at 60%.

## Alt: the Twin Windmill guard
- Press to raise it, again to drop it (0.3 s between). Either way the combo starts over.
- Both chucks spin a disc (46 px) at each side in front of you; you move at 75%.
- Every small enemy shot (not unblockable ones) touching a disc is **swatted** back at the nearest enemy within 420 px as your own
  shot (6 damage) and counted, up to 8 (ticks over your head).
- **Fire in the guard: Twin Counter.** Both chucks lash 175 px straight ahead (22 px either side) for 20 + 6 a swat (68 at 8),
  0.55 s between counters. It uses the swats up. The discs drop while it plays (nothing is swatted), then the chucks return to the windmill, which
  spins back up over 0.2 s: the windmill is the stance's resting state.

## Card
- **Iron Links** (Rare, 2 stacks): the discs are 25% wider, and each swat adds 2 more to the Twin Counter.

## Code
`Core/Nunchaku.cs` (host), `Core/NunchakuMotion.cs` (the poses both the host's timing and the look use), `Game/NunchakuLook.cs`.
Preview `NUNCHAKU=<s>`; test suite `nunchaku`.
