# Lightbringer rework (design)

Status: **proposed, not built**. Mockup: `Docs/Mockups/lightbringer-rework.html` (looping demo of the chain, the
radiant finisher and both alt-attack candidates). Waiting on the owner's pick of alt attack.

> The Lightbringer stops being one cut on repeat. It is a three-step healer chain, **heal, protect, group heal**, the
> light gauge powers the finisher, and the weapon-action key gets an alt attack.

## The chain
| Step | Shape | Time | Damage | Healer job | Light |
|---|---|---|---|---|---|
| Dawn Cut | 100° cut + crescent (pierce 2) | 0.45 s | 17 + 12 | crescent heals allies it passes +6 | +1 / enemy |
| Mercy Cut | 100° backhand + crescent | 0.45 s | 17 + 12 | crescent wards allies it passes: 12 HP shield, 3 s | +1 / enemy |
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
