# Katana combo rework (design)

Status: **built** (Nov 2026, protocol 176), with the changes listed under "Built differently" below. Mockups: `Docs/Mockups/katana-combo.html` (playable chain, meter and Alt strike) and
`Docs/Mockups/katana-look-glow.html` (the chosen look). Three other looks were mocked and not picked:
`katana-look-ink.html`, `-neon.html`, `-steel.html` (all from `katana-look.template.html`).

> The Katana stops being one slash on repeat. It is a fixed chain, **swipe, swipe, lunge**, and every hit charges a
> meter. A full meter lets you hold the weapon-action key, mark an enemy anywhere on the map and cut across to it,
> landing behind it.

## What it replaces
- The **sheathe and quick-draw** (`Ninja.cs`: `KatanaSheath`, `KatanaDrawReach`, `KatanaDrawMult`, `KatanaDrawWidth`,
  `Player.SheathT`, `Swing.DrawCut`). The lunge is the new third step and does the same job (a line through
  everything) without making you stop attacking first.
- The Katana's own description in `Defs`/`Ninja.cs` and the README row.

Everything else about the weapon keeps working: reach 95, the `+1 cut` upgrade, damage multipliers, `Melee = true`
and the swing pipeline (`NewSwing`, `pendingSwings`).

## The chain
| Step | Shape | Time | Damage | Meter per enemy hit |
|---|---|---|---|---|
| Swipe 1 | 110° arc, left to right | 0.26 s | 20 | +10 |
| Swipe 2 | 110° arc, right to left | 0.26 s | 20 | +10 |
| Lunge | step forward 150 px, 260 px line through everything in it | 0.56 s (0.20 wind-up, 0.12 thrust, 0.24 recover) | 45 | +20 |

- Always in that order. After the lunge the chain starts again at swipe 1.
- **Queueing:** pressing fire while a step plays buffers the next step, which starts the moment the current one ends.
  Tapping through two swipes lands the lunge as early as possible.
- **Reset:** if the next step is not started within **0.7 s** of the last one ending, the chain returns to swipe 1.
- Damage and timing numbers are the mockup's and need a balance pass. Fire rate today is 0.3 s per slash, so the
  full chain (swipe, swipe, lunge) at about 1.1 s per cycle, 0.36 s per attack, is slightly slower on paper; the lunge's 3-enemy line and the
  meter are what pay for it. Tune after building.
- Hits follow the existing swing pipeline (damage numbers, `NinjaHitMult` for Vanish's ambush). The lunge's 45 is a straight 2.25 x, not the old 2.5 x.

## The strike meter
- 0 to 100. Each enemy a step hits adds the amounts above (per enemy, so the lunge through three enemies is +60).
- **Drains** at 25 per second once you have not hit anything for 1.2 s. A full bar is gone after about 4 s of standing
  still. Meter is only added by Katana hits, not by other damage.
- Spent in full by the Alt strike. The drain runs whichever weapon you hold, so swapping away empties it.
- Shown on the Ninja HUD as a bar that turns rose and pulses at 100, and goes grey while it drains.

## The Alt strike
Input is the **weapon-action key** (Left Alt by default), the same key as Kunai blink, Shuriken recall and Twin Blades
lunge (`Input.WeaponAction`, `LocalWeaponDash`). With the Katana equipped it has no other job, so there is no conflict.

1. **Hold** (meter 100): the arena dims and every enemy gets a bracket. The enemy nearest your cursor is marked with a
   reticle and a route line that runs through it and ends 52 px behind it. Moving the cursor changes the target.
2. **Release**: commits the strike on the marked enemy. Releasing with less than 100 meter, or with no enemy marked,
   does nothing (the bar shakes).
3. **Wind-up 0.35 s**: you hold a sheathed stance. You are invulnerable from release until the strike ends.
4. **Cut 0.14 s**: you cross the map along the route. The line is drawn as one long thrust beam.
5. **Held frame 0.65 s**: you stand 52 px behind the target, facing away from where you came from; the target is still
   standing with a hairline across it.
6. **Fall 0.8 s**: the target takes **120** damage and splits. Everything the route crossed takes **60**. Control returns
   at the start of this step; the splitting animation plays out over it.

Rules:
- The route ignores walls, bullets and line of sight. The end point is clamped inside the arena.
- Bosses take the full 120 for the first version; revisit after testing (see open questions).
- Does not advance or reset the chain.
- Costs the whole meter; the `+1 cut` upgrade does not add to the strike.

## Look (the chosen "Current glow")
Reuses the game's melee look from `Game/MeleeLooks.cs` (style E, "Weapon + energy") and the Katana's existing colour
`new Rgba(1f, 0.56f, 0.66f)`. No new colours.
- **Swipes:** `DrawSlashLayer` as it is: a `GlowArcBand` from 72% to 100% of the reach, a 2 px white core at 92%, 22
  sparks, the Katana model on top. The only change is the sweep direction alternating, which `MeleeSign` already does
  for the Katana.
- **Lunge:** the thrust branch of `DrawSlashLayer`: the soft beam (`SoftLine` 26 px and 10 px), white core and
  `MeleeSparks` with `line = true`. A new `Effect` with a negative `Radius` (a thrust) and a longer reach.
- **Alt strike:** a thrust effect from your start to your end point, with a wider soft beam (34 px). The route line and
  reticle in targeting use the same rose. The split target uses a rose edge. New drawing in `Game/`, built from the
  same helpers (`SoftLine`, `Square`); no new shader work.
- **HUD:** the meter bar and the targeting overlay. Mockup drawing is the reference for both.
- Effects off: the strike still needs a readable cue, so it draws the plain old-style line and the split enemy.

## Networking and state
Everything gameplay-relevant is host-authoritative, like every other weapon.
- **New `Player` fields** (replicated in `GameNet`, next to `SheathT`): `KatanaStep` (byte, the next step to play),
  `KatanaMeter` (float, 0-100), `KatanaStrikeT` (float, time into the strike, 0 when none) and `KatanaStrikeTo` (V2,
  the end point). `SheathT` and its snapshot line are removed.
- **New message** `Msg.KatanaStrike` (added at the **end** of `Msg`, before `None`): client to host, carrying the target
  enemy id. The host checks the meter, the weapon and that the enemy is alive, then runs the strike. The marking while
  holding is local to the player's own screen and is not sent.
- **Bump `Session.ProtocolVersion`** from 175 to **176**.
- **Time in co-op:** the mockup slows time to 12% while targeting. That is fine in solo, but in a shared simulation
  it would slow other players. Recommendation: targeting never slows the simulation. Instead the striker's own screen
  dims and its UI runs a slow-motion style highlight, and the hold lasts at most **1.5 s** before it cancels.
  The strike's held frame (steps 3 to 5) is the only moment that touches the simulation, and it applies to the striker
  and the target only, not a global freeze. In solo play the true slow-down can be switched on as a cosmetic option.
- **Prediction:** the local player starts the wind-up on release at once and the host confirms. A rejected strike
  (meter, dead target) snaps the player back and plays the "bar shakes" cue.

## Interactions to check
- **Shadow Step, Kunai blink, Blink-style teleports:** the Alt strike is a separate, longer teleport; make sure an
  invulnerable strike cannot stack with another mid-teleport. `DashT` already blocks `LocalWeaponDash` while active.
- **Stun and Downed:** like every weapon action, you cannot start a strike while stunned or downed. A stun during
  wind-up cancels it and refunds nothing.
- **Parry and Vanish:** Vanish's ambush bonus applies to the first hit after vanishing as it does today (a swipe,
  lunge or strike, whichever lands first).
- **Weapon variants** (`Docs/WeaponVariants.md`, Katana): all three assume the quick-draw. Nodachi and Moonlit Blade
  must be redesigned for the chain. Twin Katana (alternating hits, 60 px dash) overlaps with the chain's alternating
  swipes. These stay undesigned until this ships.
- **Death Lotus and other Ninja abilities** are unchanged.

## Implementation steps
1. **Core:** the chain state machine in `Ninja.cs` (replace the `KatanaSheath` branch), the queue and reset timers, the
   meter and drain, new `Player` fields and snapshot lines, `Msg.KatanaStrike` and its host handler, protocol bump.
2. **Tests** (`Tools/SimTest`, new `katana` suite): chain order and reset, queue, meter gain, meter drain after 1.2 s,
   strike needs 100 meter, strike damage and route damage, ends 52 px behind, invulnerable during the strike,
   snapshot round-trip and a rejected strike. Run `Tools/SimTest/run.sh 1 30 features` and core before committing.
3. **Game front end:** the lunge and Alt strike effects, the HUD bar, the targeting overlay (cursor-nearest mark,
   route line, reticle), input handling for hold and release on the weapon-action key.
4. **Docs:** README (Katana row, a new line for the meter and Alt strike), `CODEMAP.md` if new files appear, and a
   `Docs/History.md` entry. The Bestiary and Armory pages become out of date and are **not** updated unless asked.
5. **Playtest and balance:** the chain's damage, the drain, and how bosses take the strike.

## Open questions
- Does the chain's slower paper fire rate need a damage bump on the swipes?
- Bosses: full 120, or a capped share (for example 10% of max health, whichever is lower)?
- Should the drain pause during the held frame and the wind-up? (Meter is zero then anyway, so likely moot.)
- Does taking damage drain the meter faster (for example +10 per second of drain for 2 s after being hit)?
- Co-op: is the recommendation above (no global slow) acceptable, or should the strike briefly slow everything?
- Should Alt strike be usable while moving (aim and release while walking), or lock movement during the hold?

## Built differently from the plan above
- **No new message.** The strike reuses `Msg.WeaponDash` with a new kind, `DashKatana`, sending the target enemy's
  position (the host takes the nearest enemy within 90 px of it). `Msg.KatanaStrike` was not added. The protocol bump to
  176 is for the new `Player.KatanaStep` and `KatanaMeter` snapshot fields and `FxKind.KatanaStrike`.
- **The lunge does not step you forward 150 px** (a player's own game owns their position, so the host cannot move them).
  It is a 260 px line from where you stand, landing 0.15 s after the press. A client-side step could follow later.
- **No time slow while aiming**, the recommendation in the co-op section. The arena dims and brackets appear on your
  screen only; the hold is capped at 1.5 s and a hold that runs out cancels.
- **The meter shows as a rose arc round the swordsman** (a gauge like the Lightbringer's) instead of a HUD bar. Full, the
  blade is drawn sheathed with a glint.
- The meter's drain pause and bosses (full 120 for now) are as in the open questions.
