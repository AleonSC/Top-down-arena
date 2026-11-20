# Nodachi (Tank Ninja, 4th weapon)

Status: built (protocol 436). Mockup: https://claude.ai/artifact/7jmWYi6U9oafkebSdAuSnc (the owner picked the two chains below from it).

## The owner's decisions

- Alt is a **stance change**: Arashi (Storm) is long sweeping AoE, Mamori (Guard) is jabbing defensive moves.
- Each stance has its own combo, and **switching keeps both places**.
- The combo **never goes down** on its own (no idle timer); **raising the parry resets** both.
- Finishers (◆) don't end the combo; they're just a step in it.
- The moves must flow together smoothly in game.

## The chains

| Stance | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|
| Arashi | Nukitsuke | Kesagiri | Engetsu | Tenchi ◆ | Nagi |
| Mamori | Kasumi-zuki | Nidan-zuki | Senbon-zuki ◆ | Tsuka-ate | Hiki-zuki |

Numbers are in README.md (Ninja weapons table) and `Core/NodachiMotion.cs`.

## How it flows

Each step's first wind-up eases from wherever the blade was, its strikes play from key poses, and the recovery eases into the
stance's ready pose (Arashi low and trailing behind, Mamori's point at their eyes). The Storm chain is ordered so each step starts
near where the last ended: Nukitsuke ends on the right where Kesagiri winds up, Kesagiri ends left where Engetsu winds back,
Tenchi's spin carries Engetsu's turn on round and its slam comes round to the front, Nagi pulls back from the slam.

## Sounds

`Game/NodachiSoundBank.cs`: one sound per step, sent at the press and timed from the motion; a cut landing, a block, and a
shing for each stance switch (low and heavy into Arashi, crisp into Mamori).

## Ideas not built

- Bots don't switch stance (they play Arashi).
