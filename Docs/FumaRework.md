# Fuma Shuriken: Throw and Orbit stances (design)

Status: **built** (Nov 2026, protocol 264). The owner chose concept **C, Orbit**. Mockup: [`Docs/Mockups/fuma-shuriken-rework.html`](Mockups/fuma-shuriken-rework.html)
(https://claude.ai/artifact/H5RR9Yhr9XyDDhQpC2iGC4), version 1. Concepts A (Recall & Buzzsaw) and B (Twin Halves) are in the
same mockup and were not picked.

> The weapon-action key swaps between the Throw stance (today's oval loop) and a Blade stance where the star circles you.
> The parry adds a combo in each stance. Numbers are first guesses to tune in the sim.

## Throw stance
- Fire: today's loop, 380 px out and back, 30 damage, cuts both ways (`FumaTurn`, `Ninja.cs`).
- Parry while the star flies: it is caught straight into orbit (Blade stance).

## Blade stance (Left Alt)
- The star leaves your hand and circles you at 80 px, one lap in 0.75 s, 8 damage a pass (0.3 s per-enemy cooldown).
- Fire: **Lash**, the star whips out 190 px across your aim and back (24), alternating sides.
- Third Fire: **Cyclone**, one wide lap out to 160 px (20). Then back to Lash.

## Parry in Blade stance
- Parry: **Reversal**, the orbit flips direction, every enemy in its ring (55 to 130 px) takes 30, and it spins 1.5x faster for 2 s.
- Perfect parry: **Stolen Stars**, each blocked shot becomes a small star circling you at 46 px; after 1.3 s they fly back at
  whoever fired them (25 each).

## How it's built
- `Core/FumaOrbit.cs`: the star isn't a bullet. Its place is `Game.FumaPose`, a function of the stance's clock and an anchor
  (angle, clock, turn rate) that the host replicates; Reversal and the end of its speed-up re-anchor it where it is.
- `Game/FumaLook.cs`: each screen runs its own copy of the clock every frame, eased toward the host's, so the star and its trail
  (the same circle sampled a moment back) move smoothly whatever the snapshot rate.
- Extra projectiles: more stars evenly round the circle (up to 4); the Lash moves the first, the Cyclone all of them.
- Tests: `Tools/SimTest/Tests/FumaOrbit.cs` (in the `ninja` suite). Preview: `NINJA=2 FUMA=orbit|lash|cyclone|steal Tools/Preview/shot.sh`.
- Not done: the star cutting enemy bullets; bots stay in the Throw stance.
