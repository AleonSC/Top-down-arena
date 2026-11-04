# Contraband

Status: **built** (protocol unchanged at 154; `Core/Contraband.cs`, `perks` test suite). The second Warden perk, after Riot Gear
([`RiotGear.md`](RiotGear.md)); part of [`BossHuntsAndPerks.md`](BossHuntsAndPerks.md). Picks: a guaranteed cycle chest, and the
unlock is a Hard or Nightmare win.

## The perk

- **Unlocked by** winning the Warden Hunt on the **Hard or Nightmare** preset (Custom doesn't count). `Profile.HuntsWonHard`,
  written as a fifth field on the profile's `hunt` line (older files load with none).
- **A cycle chest:** on the first wave after each boss (waves 6, 11, 16...), a treasure chest always turns up when **anyone in the
  party** has Contraband, on top of the random 8% chance. It is an ordinary chest (touch to open, everyone gets their own item, the wave
  waits for it). Boss Hunts have no boss cycles, so no extra chest there.
- **Rare or better:** the holder's own item from **any** chest is never uncommon (an uncommon roll becomes rare; the 20% legendary chance
  is unchanged). Teammates without the perk roll their usual item from the same chest.
- One perk per boss, so it takes Riot Gear's slot if you equip it (the Warden has only these two so far).

## What the code does

- `Game.IsCycleStart(wave)` / `ContrabandChest()` in `Core/Contraband.cs`; `WaveProps` (`Props.cs`) adds the chest when the random roll missed;
  `RollChestLoot` (`Props.cs`) lifts Uncommon to Rare for the holder.
- Needs nothing on the wire: the chest and the items are host-made props and loot, already synced.

## Left out

- No visual marker for the cycle chest beyond the chest itself. Say if you want a gold ring or a call-out on the wave banner.
- Bots don't need Contraband logic; they open the chest like any other.
