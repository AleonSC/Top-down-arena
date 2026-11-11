# Weapon Variants (was "Ascensions")

**Status: steps 1 and 2 built** (Cores and bought tiers saved in the profile, the lobby's Variants window with a Buy button). Not built: the tier effects themselves (the window says "Effect coming soon").

**Weapon variants (first one):** the Twin Rifle's tier I is the **Twin Rifle Auto**, a whole other weapon (`WeaponDef.Variant`, `Variants.VariantOf`): the window shows it with an Equip button (unlocked for now, `Variants.WeaponVariantsUnlocked`; no Cores needed until the unlock rules are set). It rides on the class's fourth weapon pick (`Defs.WeaponLimit`) but has no lobby key.

## Decisions (owner)

- Renamed **Ascensions -> Variants**.
- **2 tiers per weapon for now** (to test whether it's too much), **each tier a different effect**.
- Unlocked by spending **Cores**, saved in the profile like Rank and unlocks.
- Cores are earned from: beating a Campaign, beating Boss Hunts, and **surprise drops**: a rare glowing enemy appears occasionally; killing it drops 1-3 Cores.

## Proposed (not agreed)

- Campaign milestone waves (25/50/75/100) pay a lump of Cores, a full clear pays more.
- Tier cost 10 / 20; boss kill ~3 (+2 no-death).

## Open

- The two effects per weapon (one design pass per weapon).
- Co-op: whose profile receives Cores.
- Profile save format and protocol bump when built.

## Steps

1. Done: `Variants.cs` (tiers, costs), `Profile.Cores` / `VariantTiers` / `BuyVariant`, the lobby window (`LobbyUi.DrawAscensionWindow`). Preview: `VARIANTS=1 Tools/Preview/shot.sh variants lobby`.
2. Done: Campaign pays 1 a boss + 5 for a win, a Hunt win 5, Endless none (`Variants.RunCores`/`HuntCores`); the glowing Core Carrier (`CoreDrops.cs`, 0.8% of regular spawns from wave 3, one alive, 6 a run, 1-3 Cores) rides the End record (protocol 284). Cores show on the results' XP line.
3. The two effects per weapon.
