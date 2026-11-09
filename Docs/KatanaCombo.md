# Katana: two stances (design)

Status: **built** (Nov 2026, protocol 230). Mockup: [`Docs/Mockups/katana-stances.html`](Mockups/katana-stances.html)
(https://claude.ai/artifact/CV8cs8PEVMuhjqMcDxGX3h), changed as the owner asked afterwards (below). It replaces the swipe,
swipe, lunge chain and the strike meter (protocol 179 to 227; that version's design is in git history, and its mockups
`katana-combo.html`, `katana-chain.html` and `katana-reach.html` are still in `Docs/Mockups/`). The first round of ideas is
[`Docs/Mockups/katana-ninja-rework.html`](Mockups/katana-ninja-rework.html).

> The Katana has two stances. The weapon-action key switches between them. Each stance has its own attack and its own
> meter, and holding fire with the meter full spends it on that stance's move.

## The owner's rules
- The switch does **nothing extra**: an animation, a small cooldown, and a small burst that pushes nearby enemies back a
  little. It never spends a meter.
- The moves the mockup fired on a switch are now **that stance's own move**, triggered by **holding fire** when its meter
  is full.
- **Zanshin** builds its own meter, kept when you switch. The **Kage Chain** builds its meter from the enemies Hayate's dashes
  hit, kept too.
- **+1** (extra projectiles): one more Kage Chain hop, and every dash and swipe strikes once more (60%).
- Flow is kept.

## Hayate (stance 0): the dash
| | |
|---|---|
| Fire | dash-cut 150 px along your aim in 0.1 s, untouchable, a line 36 px wide; everything on it takes 1.4x the weapon's damage (28) |
| Cooldown | 0.55 s, or none if the dash killed something |
| Flow | a level per killing dash, up to 5: +8% length, +0.2x damage (+4) each. After 2.2 s without a kill it drops a level every 0.7 s, only while you are in Hayate |
| Meter | every enemy a dash hits adds 1 to the Kage meter (10 fills it) |
| Hold fire, meter full | **Kage Chain**: hop through the 6 nearest enemies (each from the last), 75 ms a hop, untouchable, 3.5x (70) to each |

## Senbon (stance 1): the marks
| | |
|---|---|
| Fire | swipes, 120°, 120 px, 0.8x (16), 0.26 s each, alternating sides |
| Marks | every enemy a swipe hits and doesn't kill takes a mark, up to 3 (every screen sees them) |
| Meter | every enemy a swipe hits adds 1 to the Zanshin meter (10 fills it) |
| Hold fire, meter full | **Zanshin**: the marked freeze (not bosses); you hop through each (60 ms), back to where you stood (90 ms), hold still (0.28 s) and sheathe (0.42 s). At the click each marked enemy takes 1.7x (34) per mark, the marks clear, and enemy shots within 380 px are cut. Untouchable throughout |

## The switch
- The weapon-action key (Left Alt). 1 s cooldown. No attacks for 0.22 s while the blade turns.
- Pushes enemies within 110 px back 45 px (not heavy ones), no damage.
- Meters, marks and Flow are kept.

## Holding fire
With the stance's meter full, a press still attacks once; keep holding 0.35 s and your own game starts the move (it picks
the hops from what it sees, moves you along them, and tells the host). The host checks the meter, picks its own targets by
the same rule and lands the cuts on time. With the meter not full, holding fire repeats the attack as before.

## Code
- `Core/Katana.cs`: `FireKatana`, `KatanaLanded` (meters, marks, a killing dash), `TickKatana` (Flow), `TickKatanaFire`,
  `LocalKatana` (your dash, the switch, the hold, the hops), `HostKatanaDash` / `HostKatanaSwitch`, `TickKatanaRuns`.
- Constants: top of `Core/Ninja.cs`. Replicated: `Player.KatanaStance`, `KatanaFlow`, `KatanaKage`, `KatanaZan`,
  `KatanaStep`, `KatanaDashes`; `Enemy.KatanaMarks`. `Msg.WeaponDash` kinds 5 (switch), 17 (Kage Chain), 18 (Zanshin).
  `FxKind.KatanaStanceFx`, `KatanaHop`; `Sfx.KatanaStanceSwap`.
- The dash is a `Swing` box landed from where it began (`Swing.FromSet`), so the cut follows the line you dashed.
- Look: `Game/KatanaLook.cs` (held pose per stance, meters, Flow, marks, the switch, the hops, your own Zanshin's dimming and
  letterbox), `Game/MeleeLooks.cs` (the dash's spear and afterimages, the swipes' crescents).

## Open
- Co-op: the marks belong to no one; any Katana's Zanshin cuts every marked enemy.
- Old sounds kept and unused: `KatanaWindup`, `KatanaLost`, `KatanaEmpty`, `KatanaAim`, `KatanaSwitch`; `FxKind.KatanaStrike`.
