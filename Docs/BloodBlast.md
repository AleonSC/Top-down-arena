# Blood Blast: blood-slash chain

**Status: built** (steps 1-4, protocol 405): the chain, health costs, stacks, kill heal and Crimson Tide in `Assets/Scripts/Core/BloodBlast.cs`, the look in `Assets/Scripts/Game/BloodBlastLook.cs`, tests `Tools/SimTest/run.sh 1 30 bloodblast`, preview `BLOODBLAST=<s> ZOOM=1.8 Tools/Preview/shot.sh x hud`. Sounds in `Assets/Scripts/Game/BloodBlastSoundBank.cs` (`Tools/SoundCheck/render.sh Blast`). Tank Hemomancer, 4th weapon key. Mockup `Docs/Mockups/blood-blast.html`
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
| 1 | Crimson Slash | 22 | 115 px, 120° sweep left → right | 2.7 | 0.42 s |
| 2 | Backcut | 22 | 108 px, 130° sweep right → left | 2.7 | 0.40 s |
| 3 | Thorn Flurry | 3 × 9 | 85 px, 100°, three alternating cuts | 3.3 | 0.50 s |
| 4 | Vein Whirl | 18 | 95 px, full 360° spin | 4 | 0.58 s |
| 5 | Sanguine Cleave | 40 | 70 px circle centred 105 px ahead, shove 130 | 6 | 0.78 s |
| 6 | Crescent Flight | 26 | an 80° crescent flying 260 px, pierces | 4.7 | 0.62 s |

- A full loop: about 23 health (11.7% of the Tank's 200), 157 damage to one target, about 3.3 s. (Health on a 200 Tank;
  the owner cut the mockup's costs by a third after the balance check.)
- **Crimson Tide** (weapon-action key): 12% max health, 8 s cooldown. 0.65 s charge (blood gathers in front of you),
  then a 100° crescent wave rolls out 400 px, piercing, 60 damage and a 150 shove; +1 stack on each enemy hit.
- Stacks belong to the last Blood Blast user to hit the enemy (two Blood Blasts on one enemy share its stacks); whoever
  lands the kill, that Hemomancer is healed when the returning blood reaches them (`BlastReturnTime`, 0.97 s). Stacks never fade
  while the enemy lives. A boss holds at most 10 like anything else (20 health on its death).
- Health costs follow the Hemomancer rules (`Hemomancer.cs`): they can't take you below 1 and count as `healthSpent` in
  the session report. The kill heal is ordinary healing.

## Look (from the mockup)

- Each move is shaped blood: a dark crimson body, a red core and a pale leading edge, flinging drops that stain the floor.
- The owner wants it to **look really good in game**: every move gets its own `Tools/Preview` render before it ships,
  and the look step is as big as the core step.
- Stacks: small blood drops circling the enemy with a ×N count.
- **Blood return** (a stacked enemy dies): the blood flies back into the Hemomancer, the heal lands when it arrives
  (not on the kill), and the Hemomancer flashes with an absorb ring and a green +N number. Owner's pick: **Blood Vortex**
  (1.15 s): the blood spins up out of the body into a swirling orb (0.45 s), which then flies to the Hemomancer trailing
  a helix of drops (0.5 s); the heal lands as it arrives. More stacks: a bigger orb and more drops (stacks + 4). The enemy
  leaves a blood stain where it died. (Homing Drops, Blood Ribbon and Vein Draw were the other options.)
- Every press shows a small red −N health number.

## Build steps

1. **Core** (done, protocol 405): `WeaponId.BloodBlast`, the six-step chain in `Core/BloodBlast.cs` (each step a `Swing`;
   Crescent Flight is a 260 px, 80° cut until step 3 draws it flying), health costs, `Enemy.BlastStacks` + owner
   (replicated), the kill heal on arrival; suite `bloodblast`. Placeholder look: the red slash and a drain line home.
2. **Alt** (done): Crimson Tide on the weapon-action key (`WeaponAlt.Tide` 46; `LocalTide`, `HostTide`, `TickTide`): pay, gather
   0.65 s (no cuts meanwhile), then the wave grows out (`TideRadius`) hitting each enemy once; local cooldown ring under
   your feet (`TankAltLook.cs`). Placeholder look: a ring while it gathers, a wide red slash as it goes.
3. **Look** (done): `Game/BloodBlastLook.cs` from `FxKind.BlastCut` (each press), `BlastTide` (gather, wave) and `BlastReturn`
   (the Blood Vortex), plus the stack drops on enemies and a glow pass; `Tools/Preview`
   renders of each step.
4. **Polish** (done): Rare card **Blood Harvest** (two stacks: +1 health a stack, +5 stacks an enemy; `Stats.BlastHealBonus`,
   `BlastCapBonus`), sounds (wet whooshes with a slosh under them and a spatter of drops, a splat once a press that lands,
   the Cleave's slam, the Tide's gather and wave, the Vortex's swirl and heartbeat as it lands), measured DPS 53
   (`WeaponDps.Generated.cs`).

## Open tuning

- Measured: 53 damage a second on one target (tank melee runs 45-65), for about 7 health a second while swinging
  nonstop (a full loop costs 23 in 3.3 s; it was 10.5 a second before the owner cut costs by a third), so a 200-health tank
  empties in about 28 s without kills. The Gore Mortar pays
  16 a second for 133 damage a second.

- 2 health a stack with 10 max is a first guess: a kill pays 2 to 20. Check sustain against session reports.
