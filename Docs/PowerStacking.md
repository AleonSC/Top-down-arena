# Power stacking: diminishing returns and wounds

**Status: built (Nov 2026, protocol 237). Check in Unity.** Phase 4 of [`Rooms.md`](Rooms.md).

## Why

The Nov 9 run (two players, Normal, 50 waves) ended in a cleanup: waves 41-49 took 17-24 s, the Devourer died in 59 s,
the Eclipse took 1.8 million damage in 149 s, and nothing threatened you. Three things stacked without limit:

- **Damage and fire rate.** The Knight took Hair Trigger 14 times (+20% fire rate each: x3.8) and Hollow Points 8
  times; the plain stat cards had no stack limit and added up, then multiplied with Multishot, Barrage, Explosive
  Rounds and the legendaries.
- **Cooldowns.** Clarity (-15%) and Focus (-6%) multiply with no floor: the Hemomancer's 6 Clarity + 3 Focus cut
  cooldowns to about 30%, so the defensive abilities were almost always up (Overdrive 76 casts, Blood Freeze 70).
- **Healing.** Health and regeneration cards had no limit either, and the Hemomancer healed back almost exactly what
  you both took, nearly every wave.

## Decisions (owner, Nov 2026)

1. **Diminishing returns**, not hard per-card limits.
2. **Both arenas** (Classic and Rooms). The Rooms arena's 1.3x enemy damage stays Rooms-only.
3. **Wounds**: 10% of each hit can't be healed back until the wave is cleared.
4. **Parry stays as it is** (a timing skill; its cards are already capped).

## How it works

- **Diminishing returns** (`Upgrades.Compute`, `CopyStrength`, `DiminishStep` 0.25): the cards without a stack limit
  (the twelve plain stat cards) give copy n of the same card 1 / (1 + 0.25 (n - 1)) of the first copy's effect: every
  number the card changes moves only that share of the way. Different cards count apart (Hollow Points and Sharpened
  Rounds each start at full strength). Capped cards are unchanged. Examples: Hair Trigger x14 fire rate x2.28 (was
  x3.8), Hollow Points x8 damage x1.95 (was x2.6), Clarity x6 cooldowns 53% (was 38%). The pick card's note says what the
  next copy is worth (`HudPicker.cs` `UpgradeNote`); the stat preview already shows the real change.
- **Wounds** (`Player.Wound`, `HealCap`, `Game.WoundShare` 0.1): each hit adds 10% of the health it took to the wound;
  every heal (`Heal`, regeneration, orbs, lifesteal, heal on kill, revives) stops at max health less the wound; the
  wound clears when the wave is cleared (`WaveCleared`, before its 30% heal). A Healer's overheal shield only counts
  healing past full health. Synced in the snapshot (protocol 237). Drawn as a dark red end on the HUD card's health bar
  and the bar over each player (`HealthBars.cs` `DrawWound`, `WorldPlayers.cs`).
- **Tests**: `run.sh 1 30 stacking`. The test parties that used to stack Fortitude / Regeneration / Hollow Points
  (`StartBuffed`) set those stats directly now; the boss-power test's real build measures x1.98 typical (was x2.6).
- **Measured** (bots, `run.sh 1 1 ttk 50`): late bosses with a Good bot duo took 46-126 s (Duelist 67, Lich 126,
  Herald 71, Devourer 46, Eclipse 91), inside the 60-120 s the boss health was tuned for. Single runs vary a lot.

## Left for later

- `TypicalPower` (BuildPower.cs, the "typical build" the boss health boost compares against) was measured before
  diminishing returns; builds are weaker now, so bosses get their boost a little less often. Re-measure with
  `run.sh 1 1 levels` if bosses feel soft for strong parties.
- If the late game is still a cleanup: part of the boss build-power boost for regular enemies past wave 20 (proposed,
  not decided).
