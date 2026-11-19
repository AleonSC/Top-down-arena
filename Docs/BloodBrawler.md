# Blood Brawler (was the Bone Spear)

**Status: built (steps 1-3, protocol 399).** Code `Assets/Scripts/Core/BloodBrawler.cs`, look `Assets/Scripts/Game/BloodBrawlerLook.cs` (preview `BONESPEAR=<s> ZOOM=1.8 Tools/Preview/shot.sh x hud`), tests `Tools/SimTest/run.sh 1 30 bloodbrawler`. Mockup `Docs/Mockups/bone-spear-rework.html`. Rework of the DPS Hemomancer's Bone
Spear, renamed the Blood Brawler (`WeaponId.BloodBrawler`, `BloodBrawler.cs` and the `bloodbrawler` suite kept): a ranged string the Hemomancer lands by working the target's blood, as phantom
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
| 1 | Pulse (Opener) | 18 | 0.26 s | Phantom jab from your side, shoves 24 px away |
| 2 | Reel (Opener) | 15 | 0.35 s | Thread goes taut, pulls it 110 px toward you |
| 3 | Heart Lift (Link) | 27 | 0.45 s | Blood spikes throw it airborne 0.7 s (stunned); hits on it airborne +25% |
| 4 | Marrow Burst (Finisher) | 36 + 12 per hit in the string | 0.65 s | Swells and bursts into 8 bone shards, 24 each, 120 px |

Marrow Burst lands while the target is still airborne from Heart Lift, so it gets the +25%.

## Alt fire: Sympathy

- Tap, 8 s cooldown: threads from the marked enemy to up to 4 other enemies within 170 px.
- For 4 s every move landed on the marked enemy also lands on each bound enemy at 50% (same shove, pull, lift, burst).
- No mark, no cast.

## Upgrades

- Marrow card (built): Marrow Burst throws 2 more bone shards and Sympathy binds 1 more enemy, a stack (up to 2).
- Extra projectiles (Multishot and the like): each one lands every move on one more enemy near the mark, at 50% (built; +54% on a crowd with two).

## Held look

- The model holds no spear: an open hand, cut across the palm, blood running down the fingers and dripping off them; it pushes for the Pulse, pulls back half closed for the Reel, spreads for the Heart Lift, closes into a fist through Marrow Burst's wind-up and flares open as it bursts.

## Balance (step 3)

- Damage raised 1.5x from the mockup (the Pulse 12 to 18; the other moves keep their ratios), so a full string on one boss-sized target matches the old spear; shards and echoes are the crowd damage.

## Build steps

1. Core: `BloodBrawler.cs` (target, string step on `Player.Combo`, the four moves, Sympathy), replicated target and
   bound list (`Session.ProtocolVersion` bump), old spear shot and pickup zone removed, test suite `bloodbrawler`.
2. Look: `Game/BloodBrawlerLook.cs` (thread, mark, phantom fist, lift column, swell and shards, Sympathy threads),
   preview `BONESPEAR=<s>`.
3. README, Marrow card text, `CODEMAP.md` line, balance pass from the sim.
