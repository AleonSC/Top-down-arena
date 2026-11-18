# Bone Spear: Marionette

**Status: step 1 built (core, protocol 398); steps 2-3 to do.** Code `Assets/Scripts/Core/BoneSpear.cs`, tests `Tools/SimTest/run.sh 1 30 bonespear`. Mockup `Docs/Mockups/bone-spear-rework.html`. Rework of the DPS Hemomancer's Bone
Spear (`WeaponId.BoneSpear` kept): a ranged string the Hemomancer lands by working the target's blood, as phantom
melee blows from inside its body.

## Owner's decisions

- Fire is a four-step string: **Pulse, Reel, Heart Lift, Marrow Burst** (the other six moves in the mockup were
  options only).
- Alt fire is take C, **Sympathy** (A Bone Javelin, B Puppet Hold and D Rupture were the other options).

## Rules

- Fire needs the cursor on an enemy within 520 px (Implosion's targeting); that enemy is marked and takes step 1.
  Each press within 0.9 s of the last move lands the next step on it; clicking another enemy, or waiting, starts over.
- 1 health a move, 3 for Marrow Burst; hits heal the usual 3%.
- A blood thread runs from the hand to the marked enemy (fades 1.4 s after the last move); the mark is three bone
  ticks circling it.

| Step | Name | Damage | Time | Effect |
|---|---|---|---|---|
| 1 | Pulse (Opener) | 12 | 0.26 s | Phantom jab from your side, shoves 24 px away |
| 2 | Reel (Opener) | 10 | 0.35 s | Thread goes taut, pulls it 110 px toward you |
| 3 | Heart Lift (Link) | 18 | 0.45 s | Blood spikes throw it airborne 0.7 s (stunned); hits on it airborne +25% |
| 4 | Marrow Burst (Finisher) | 24 + 8 per hit in the string | 0.65 s | Swells and bursts into 8 bone shards, 16 each, 120 px |

Marrow Burst lands while the target is still airborne from Heart Lift, so it gets the +25%.

## Alt fire: Sympathy

- Tap, 8 s cooldown: threads from the marked enemy to up to 4 other enemies within 170 px.
- For 4 s every move landed on the marked enemy also lands on each bound enemy at 50% (same shove, pull, lift, burst).
- No mark, no cast.

## Upgrades

- Marrow card (built): Marrow Burst throws 2 more bone shards and Sympathy binds 1 more enemy, a stack (up to 2).
- Extra projectiles (Multishot and the like): the same, +2 shards and +1 bound enemy each.

## Build steps

1. Core: `BoneSpear.cs` (target, string step on `Player.Combo`, the four moves, Sympathy), replicated target and
   bound list (`Session.ProtocolVersion` bump), old spear shot and pickup zone removed, test suite `bonespear`.
2. Look: `Game/BoneSpearLook.cs` (thread, mark, phantom fist, lift column, swell and shards, Sympathy threads),
   preview `BONESPEAR=<s>`.
3. README, Marrow card text, `CODEMAP.md` line, balance pass from the sim.
