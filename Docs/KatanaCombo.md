# Katana: two stance combos (design)

Status: **built** (Nov 2026, protocol 240). Mockup: [`Docs/Mockups/katana-stance-cancels.html`](Mockups/katana-stance-cancels.html)
(https://claude.ai/artifact/X14SX3LsqfEfUNt8RA3BNQ), version 3, built as drawn. It replaces the first stances (protocol 228-239:
a dash and swipes with meters spent by holding fire, the Kage Chain and Zanshin; in git history). Earlier ideas:
`katana-ninja-rework.html`, `katana-stances.html`.

> Two basic-attack combos, one per stance. The weapon-action key switches stance at once, and the stance you leave keeps its
> place in its combo for as long as you're away. The skill is storing a combo one step from its finisher and calling it when
> it counts. The two finishers feed each other.

## Switching
- The weapon-action key (Left Alt), 0.3 s cooldown. It stops the step you're in (what hasn't landed of it never does). No
  bonus, no penalty.
- The stance you leave keeps its place forever. The stance you're in starts over after 1.5 s without a step.
- Raising the parry clears the wait for the next step and keeps both combos' places.

## Hayate (stance 0): the dash combo. Damage is x the weapon's 20.
| Step | What | Damage | Next step after |
|---|---|---|---|
| Kiri | dash-cut 120 px along your aim in 0.09 s, untouchable, a line 32 px wide | 1.1x (22) | 0.22 s |
| Kaeshi | the same, angled 20° to alternate sides | 1.1x (22) | 0.22 s |
| Tsumuji | a 70 px dash into a full spin, 85 px round you | 1.3x (26) | 0.36 s |
| Momentum Burst | the blade stabbed into the ground (planted 0.2 s): a 150 px shockwave shoves enemies 120 px, cuts the small bullets in it and marks what it hits for 4 s | 0.4x (8) | 0.32 s |

A dash that kills brings the next step at once.

## Senbon (stance 1): the fancy combo
| Step | What | Damage | Next step after |
|---|---|---|---|
| Agari | a 130° sweep, 120 px | 0.8x (16) | 0.25 s |
| Kaeshi | the sweep back the other way | 0.8x (16) | 0.25 s |
| Engetsu | a 30 px step into a full-moon spin, 105 px | 1x (20) | 0.34 s |
| Senbon | six crossing slashes (95 px lines) in a cone 60-125 px ahead, 0.05 s apart | 0.35x (7) each | 0.36 s |
| Caltrops | 16 caltrops thrown out to 60-150 px round you; they lie 8 s, and one stepped on hurts, slows to half speed for 1.5 s and breaks | 0.2x (4) | 0.48 s |

## The finishers together
- **Caltrops, then the Burst:** the shockwave flings every caltrop you have lying on the ground at the nearest enemy, 0.4x (8) each.
- **The Burst, then Caltrops:** the caltrops you throw seek the enemies the Burst marked, shared out between them, 0.4x (8) each.

## +1 (extra projectiles)
Each dash strikes once more at 60%, two more caltrops, and a 25% wider Momentum Burst.

## Code
- `Core/Katana.cs`: `FireKatana` (each step), `KatanaLine`/`KatanaArc`/`KatanaSlash` (the cuts, as swings that draw nothing
  themselves), `KatanaLanded` (each cut's look, a killing dash), `MomentumBurst`, `ThrowCaltrops`, `TickKatanaLater` (timed
  cuts, marks fading, caltrops landing and flying: the host steers them), `HostKatanaSwitch`, `TickKatana` (the combos' memory),
  `LocalKatana` (your own game's dashes and steps, the Burst's plant, the switch).
- Constants: top of `Core/Ninja.cs`. Replicated: `Player.KatanaStance`, `KatanaHay`, `KatanaSen`, `KatanaStep` (the last step,
  stance * 8 + step), `KatanaDashes`; `Enemy.KatanaShocked`. `Defs.Caltrop` bullets (`Bullet.Tossed` while in the air: they fly
  over enemies). `FxKind.KatanaDash`, `KatanaSweep`, `KatanaSlash`, `KatanaBurst`, `KatanaCut`, `KatanaStanceFx`;
  `Sfx.KatanaBurstSfx`, `KatanaCaltrops`, `KatanaFling`, `KatanaStanceSwap`.
- Look: `Game/KatanaLook.cs`, the mockup's shapes and numbers: the katana model in both hands (curved blade, white edge,
  temper line, gold habaki and tsuba, wrapped grip), the dash's spear and afterimages, the sweeps' crescents, Senbon's lenses,
  every cut's line and sparks and a dead enemy's halves in its own colour and shape, the Burst's rings, cracks and flash,
  marked enemies' turning dashed ring, caltrops, and both stances' combo pips. Hit-stop: `Juice.cs` (25 ms a cutting step,
  70 ms and a kick on the Burst).
