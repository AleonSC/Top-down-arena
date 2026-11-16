# Blood Lash: chain combo

**Status: built** (protocol 354). Code `Assets/Scripts/Core/BloodLash.cs`, look `Assets/Scripts/Game/BloodLashLook.cs`, tests
`Tools/SimTest/run.sh 1 30 bloodlash`, mockup `Docs/Mockups/blood-lash-chains.html`.

## Owner's decisions

- Fire is a four-step string: Crack, Backhand, Snap, Reap, with numbers as in the mockup.
- Reap turns spent links into Clot and replaces the old Clot Burst; hits no longer build Clot by themselves.
- Alt fire is take B, **Martyr's Leash** (A Gore Anchor, C Crimson Flail and D Sever were the other options).

## Rules

| Move | Damage | Reach | Links | Notes |
| --- | --- | --- | --- | --- |
| Crack | 24 (48 tip) | 130 px, 110° | +1 (+2 tip) | Tip taunts 1.5 s. 0.32 s. |
| Backhand | 24 (48 tip) | 130 px, 110° | +1 (+2 tip) | Sweeps back. 0.32 s. |
| Snap | 36 | 190 px, 36° | +2 | Taunts. 0.40 s. |
| Reap | 20 + 12/link | 340 px, chained only | all spent | Yank to touching over 0.3 s, 0.6 s stun, 1 Clot per link. |

- A press within 0.8 s of a step ending moves on; later, the string starts over. Parrying starts it over too.
- Up to 4 links per enemy (Crimson Whip: +1 a card). A chain holds 3 s after its last hit, then drops a link every 0.5 s,
  and snaps past 400 px.
- Martyr's Leash (hold Left Alt): up to 3 s, 2% max health a second, 3 s cooldown after. Chained enemies are taunted, their
  chains don't fade, and they're dragged back to within 160 px. 40% of each hit you take is taken off and dealt to the chained
  enemies three times over, split by links. Heavy enemies and bosses are taunted and hit but not dragged.

## Open tuning

- `LeashReturn` (3x) and `LeashShare` (40%) are first guesses; check them against session reports.
