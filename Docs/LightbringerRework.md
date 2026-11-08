# Lightbringer rework (design)

Status: **built** (Nov 2026, protocol 208) with Sunlance as the alt; Dawnguard was not built. Mockup: `Docs/Mockups/lightbringer-rework.html` (built on the Twin Blades mockup engine: posed sword arm, trails, hit sparks, a scrubbable replay, a playable Try it, and a loop of each move, covering the chain, the
radiant finisher and both alt-attack candidates).

## Built differently
- Dawn and Mercy take 0.4 s each (a press counts 85% through, so 0.34 s apart), not 0.45 s; Noon 0.62 s. About 1.3 s a chain.
- A crescent still stops at the first ally it heals or wards (as the old crescent did), so it doesn't heal a whole line.
- The Sunlance has no self-target; with fewer than 4 pips or no ally in reach the key only flashes a small red ring.
- The sword is drawn by `Game/LightbringerLook.cs` in every state, held at the ready when idle (the model leaves its own sword and arms out).

> The Lightbringer stops being one cut on repeat. It is a three-step healer chain, **heal, protect, group heal**, the
> light gauge powers the finisher, and the weapon-action key gets an alt attack.

## The chain
| Step | Shape | Time | Damage | Healer job | Light |
|---|---|---|---|---|---|
| Dawn Cut | 100° cut + crescent (pierce 2) | 0.4 s | 17 + 12 | crescent heals allies it passes +6 | +1 / enemy |
| Mercy Cut | 100° backhand + crescent | 0.4 s | 17 + 12 | crescent wards allies it passes: 12 HP shield, 3 s | +1 / enemy |
| Noon Sunburst | blade planted, 140 px ring, 60 px knockback | 0.65 s | 22 | allies inside +10, you +5 | +2 / enemy |

- Fixed order; fire during a step queues the next; 0.8 s without swinging resets to Dawn.
- Mercy's ward reuses the Energy Ward shield (`Player.ShieldHp/ShieldT`); takes the larger, doesn't stack.
- `+1` upgrade: +1 crescent fanned out on Dawn and Mercy, +30 px on the ring.

## Radiant Sunburst
The 12-pip gauge powers the **next Sunburst** instead of the next swing: 220 px ring, 3× heal (30), a Mercy ward on
every ally inside, gauge spent.

## Alt attack (weapon-action key, Left Alt): pick one
- **A. Sunlance (recommended).** Tap: a lance of light to the ally nearest the cursor within 450 px. Needs 4+ light,
  spends all of it: heals 4 per pip (max 48), 10 + 2 per pip to enemies on the line, +25% revive progress on a downed
  ally. 1.5 s cooldown. A ranged single-target heal no other healer weapon has.
- **B. Dawnguard.** Hold: 120° light arc blocks bullets in front (covers allies behind), +1 light per bullet, 50% move,
  no swings. Release after 3+ blocks fires a Sunburst. Overlaps the Greatsword guard and Quarterstaff batting.

## Code touched when built
`Knight.cs` (fire case, `LightMax` use, ResolveSwings light gain), `Ninja.cs` `LocalWeaponDash` (alt), `Entities.cs`
(chain step + timer, replicated), `GameNet.cs` (snapshot; bump `Session.ProtocolVersion`), `MeleeLooks.cs` /
`WorldRenderer.cs` (cuts, ring, lance), `Defs` description, README rows, `WeaponsByKit.cs` test.

## Open questions
1. Sunlance, Dawnguard, or both (tap / hold)?
2. Sunlance on yourself when no ally is in range? (Proposed: no.)
3. Numbers are first guesses; balance pass after building.
