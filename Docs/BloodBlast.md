# Blood Blast: blood-slash chain

**Status: planned** (no code yet). Tank Hemomancer, 4th weapon key. Mockup `Docs/Mockups/blood-blast.html`
(published: https://claude.ai/artifact/ENVDpfrNckiH4KoUpHvEp2).

## Owner's decisions

- Fire is a six-step chain, in this order: **Crimson Slash → Backcut → Thorn Flurry → Vein Whirl → Sanguine Cleave →
  Crescent Flight**, then back to Crimson Slash.
- Every attack costs health (numbers below).
- Every attack adds **1 blood stack** to each enemy it hits (once per attack, so Thorn Flurry's three cuts add 1).
  Up to **10 stacks** an enemy. When a stacked enemy dies, the Hemomancer gets **2 health per stack**.
- The chain **never times out**: the next press always plays the next step.
- Alt fire is **Crimson Tide**, and it adds +1 stack to every enemy it hits.
- The other mockup moves (Hemo Pike, Scythe Reap, Crossfang, Clot Spikes, Arterial Lunge, Hemorrhage Nova) and alts
  (Bloodletting, Coagulate, Vein Harpoon, Sanguine Eruption) are not used.

## Rules

| Step | Move | Damage | Shape | Health | Time |
| --- | --- | --- | --- | --- | --- |
| 1 | Crimson Slash | 22 | 115 px, 120° sweep left → right | 4 | 0.42 s |
| 2 | Backcut | 22 | 108 px, 130° sweep right → left | 4 | 0.40 s |
| 3 | Thorn Flurry | 3 × 9 | 85 px, 100°, three alternating cuts | 5 | 0.50 s |
| 4 | Vein Whirl | 18 | 95 px, full 360° spin | 6 | 0.58 s |
| 5 | Sanguine Cleave | 40 | 70 px circle centred 105 px ahead, shove 130 | 9 | 0.78 s |
| 6 | Crescent Flight | 26 | an 80° crescent flying 260 px, pierces | 7 | 0.62 s |

- A full loop: 35 health (17.5% of the Tank's 200), 157 damage to one target, about 3.3 s.
- **Crimson Tide** (weapon-action key): 12% max health, 8 s cooldown. 0.65 s charge (blood gathers in front of you),
  then a 100° crescent wave rolls out 400 px, piercing, 60 damage and a 150 shove; +1 stack on each enemy hit.
- Stacks belong to the Hemomancer who put them on; whoever lands the kill, that Hemomancer is healed when the
  returning blood reaches them. Stacks never fade
  while the enemy lives. A boss holds at most 10 like anything else (20 health on its death).
- Health costs follow the Hemomancer rules (`BloodMage.cs`): they can't take you below 1 and count as `healthSpent` in
  the session report. The kill heal is ordinary healing.

## Look (from the mockup)

- Each move is shaped blood: a dark crimson body, a red core and a pale leading edge, flinging drops that stain the floor.
- The owner wants it to **look really good in game**: every move gets its own `Tools/Preview` render before it ships,
  and the look step is as big as the core step.
- Stacks: small blood drops circling the enemy with a ×N count.
- **Blood return** (a stacked enemy dies): the blood flies back into the Hemomancer, the heal lands when it arrives
  (not on the kill), and the Hemomancer flashes with an absorb ring and a green +N number. Four options in the mockup,
  **pick pending**: Homing Drops (1.0 s), Blood Ribbon (0.9 s), Vein Draw (1.1 s), Blood Vortex (1.15 s). Each scales with
  the stack count (more drops, a wider ribbon, fatter pulses, a bigger orb) and leaves a blood stain where the enemy died.
- Every press shows a small red −N health number.

## Build steps

1. **Core**: `WeaponId.BloodBlast` (end of the enum), the six-step chain in `Core/BloodBlast.cs` (step in `Player.Combo`,
   health costs, hits), `Enemy.BlastStacks` + owner (replicated), the kill heal; suite `bloodblast`; protocol bump.
2. **Alt**: Crimson Tide on the weapon-action key (next free `Dash` byte), charge and wave, stacks on hit; tests.
3. **Look**: `Game/BloodBlastLook.cs`: the six moves, the Tide, stack drops and the chosen blood return (an `FxKind`); `Tools/Preview`
   renders of each step.
4. **Polish**: per-weapon upgrade (`ExtraDesc`) and a Rare card, sounds, README rows, bot range, balance check from a
   session report.

## Open tuning

- 2 health a stack with 10 max is a first guess: a kill pays 2 to 20. Check sustain against session reports.
