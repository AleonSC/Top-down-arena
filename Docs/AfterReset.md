# To do after the usage reset

A running list of things to pick up next session. Tick off / delete as they're done.

## Bugs / polish
- [ ] **Pause menu: models show up before the rest of the menu.** When you press pause, the player and enemy
      models (e.g. the next boss's model where the pause menu shows the next boss) appear first, ahead of the
      rest of the menu. They should come in with the panel (same fade/slide timing), not pop in early.
      Check how the live models are drawn in the pause menu vs. the panel's open animation.

## Balance / design
- [ ] **Defend the Crystal should use mostly melee enemies.** Ranged enemies make the challenge about shooting the
      crystal from afar; it should be about bodies rushing it that you have to intercept. Change the room's enemy
      mix (`Assets/Scripts/Core/Challenges.cs`) to mostly melee (Swarmer, Brute, Lunger) with a few ranged at most,
      and update the `challenges` SimTest check if it depends on the mix.

## Visuals
- [ ] **Mockups for melee attack animations.** HTML mockups (like the others in `Docs/Mockups/`) of how melee
      weapons' swings, thrusts and slams could look, to pick from before changing the game.

## Bigger features (design docs ready)
- [ ] Zombie Infection modifier, basic version first: [`ZombieInfection.md`](ZombieInfection.md)
- [ ] Boss Hunts, then Perks: [`BossHuntsAndPerks.md`](BossHuntsAndPerks.md)
