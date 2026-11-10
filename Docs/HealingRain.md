# Healing Rain: design note

Status: **built** (protocol 132). Decisions below were taken as recommended; the old Time Rewind code listed under "Dead code" is still in place. It replaces **Time Rewind**, the Healer Elementalist's first ability
(Healer · Choice 0). Look chosen by the owner: **Verdant Bloom** (`Docs/Mockups/healing-rain.html`, option B).
Numbers below are first guesses to tune in play-testing.

## Why replace Time Rewind

The owner doesn't like it. It was also the odd one out in the kit: Arcane Ward already shields and Sanctify already
erases bullets and heals, and Time Rewind was a third instant "fix everyone now" button. Healing Rain is the
Healer's only ability that rewards where you stand, in the same family as Flame Wall and Blizzard.

## What it does

Cast at your cursor (like Blizzard and Flame Wall; not instant). A rain zone of radius **130 px** lasts **6 s**.

| | Base | Capstone |
|---|---|---|
| Allies inside | healed **8 HP/s** (48 over the full 6 s) | same |
| Enemies inside | slowed **30%**, no damage | same |
| Duration | 6 s | **10 s** |
| Downed allies inside | no change | revive **twice as fast** (Haven-style) |
| Cooldown | **16 s** | |

- Healing is scaled by the caster's cast power like the other zones (`z.HealMult = castPower`).
- Bosses are slowed by it the same as other enemies; nothing here is boss-immune.
- Time Rewind's old capstone (erase enemy bullets) is gone. Bullet erasing stays with Sanctify.
- Bot hint: cast on the nearest hurt ally (`b.AllyHurt`), the way the Healer bot already aims its other casts.

The capstone is the open choice (see below). I picked "10 s plus faster revives" because it gives back the one thing
Time Rewind could do that nothing else in the kit can, rescue a downed ally, without keeping a button that
undoes damage.

## Look (Verdant Bloom)

- A green disc (`0.4, 0.85, 0.5`) with a bright rim, so it never reads as Blizzard's pale blue or Sanctify's gold.
- Grass blades and small white/yellow flowers sprout across the disc over the first 4 s, so it gets lusher as it
  works. Green motes drift up from the plants. Light diagonal drops fall and splash into small ripples.
- Cast: the usual expanding `FxKind.Ring` (0.4 s) with `Sfx.SpellHoly`.
- Each heal tick (every 0.5 s) gives each healed ally a brief glow and a green `+` floater.
- Enemies inside get a teal ring (the slow cue).
- Last 1 s: the disc and plants fade, drops thin out. No timer ring (like Blizzard).
- Drawing is deterministic from the zone's `Id`, `T` and `Duration`, so it costs no extra network data.

Icon: a new pixel-art entry in `PixelArt.cs` (a cloud with drops, or a sprout under a drop). The Time Rewind
`hourglass2` mapping goes.

## What the code needs

The generic zone tick already heals (`ZoneDef.HealRate`) and slows (`ZoneDef.Slow`), as the World Tree and Honey Pool
do, so this is mostly data.

| File | Change |
|---|---|
| `Core/Kits.cs` | Append `ZoneKind.HealingRain` **after `HoneyPool`**, and a `ZoneDef { Name = "Healing Rain", Color = ..., HealRate = 8f, Slow = 0.3f }` at the matching index (the `Zones` array is indexed by `ZoneKind`). |
| `Core/Upgrades.cs` | In `AbilityId`, **rename `TimeRewind` to `HealingRain` in place.** See "Enum and saves" below. |
| `Core/Minions.cs` | Replace the `TimeRewind` `AbilityDef` (name, desc, capstone, cooldown 16, not `Instant`, `Bot`) and its case in the cast switch: `AddZone(ZoneKind.HealingRain, p, target, 130f, cap ? 10f : 6f)`, plus a `BroadcastFx(FxKind.Ring ...)`. For the capstone, give the zone a per-instance revive boost (the way Flame Wall sets `ExtraSlow`). |
| `Game/WorldEnemies.cs` | New `case ZoneKind.HealingRain` next to Blizzard: disc, rim, plants, motes, drops. |
| `Game/PixelArt.cs` | New icon, replace the `"Time Rewind"` mapping. |
| `README.md` | The Healer Elementalist ability table row and the capstone table row. |
| `HANDOFF.md`, `CODEMAP.md` | Where they mention Time Rewind. |
| `Tools/SimTest` | A feature test: an ally inside gains about 8 HP/s over a few seconds, an ally outside gains none, an enemy inside is slowed, the zone ends at its duration, the capstone lasts 10 s. |

### Enum and saves

A new `ZoneKind` value goes at the end, as the project rules say. For `AbilityId`, appending a new value would leave
`TimeRewind` as a dead entry in the middle of the Mage block, so the plan is to **reuse the slot under a new name.**
No other value shifts. The meaning of that slot changes, so:

- **Bump `Session.ProtocolVersion` (131 to 132).** `ZoneKind` and the ability's effect are both on the wire.
- A saved run (`RunSaves.cs`) that had Time Rewind equipped will load with Healing Rain instead. That's the intended
  result, but it's a change of behaviour on old saves. If you'd rather not, say so and I'll append a new
  `AbilityId` at the end instead and retire the old one.

## Dead code left by the swap

Nothing here is removed until you say so:

- `Player.HpHistory`, `DownHistory`, `HistoryIdx` (`Core/Entities.cs`) and their reset loops in `Core/Game.cs` and
  `Core/RunSaves.cs`
- `RecordHistory` and `historyT` (`Core/Minions.cs`)
- `FxKind.Rewind` and `DrawRewindFx` (`Game/WorldEffects.cs`, `Game/AbilityLooks.cs`)

Removing `FxKind.Rewind` would shift later `FxKind` values, so it should be kept as a placeholder or removed in the
same protocol bump. That's a decision for when we delete.

## Open decisions

1. **Capstone.** Recommended: 10 s and faster revives. Alternatives: 10 s only; heal rate +50%; allies inside also
   get +20% move speed.
2. **Numbers.** 8 HP/s, 30% slow, 6 s, 16 s cooldown. For scale: Arcane Ward soaks 60, Sanctify heals 25 instantly,
   Healing Totem heals 8 HP/s, Haven 15 HP/s.
3. **Enemy damage.** I left the rain with none, so it stays a support ability. A little (say 10 DPS) would make it
   usable in solo play, at the cost of overlapping Blizzard.
4. **Enum slot.** Reuse the `TimeRewind` slot (recommended) or append a new `AbilityId`.
