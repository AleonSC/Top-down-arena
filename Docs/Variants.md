# Weapon Variants (was "Ascensions")

**Status: step 1 built** (Cores and bought tiers saved in the profile, the lobby's Variants window with a Buy button). Not built: earning Cores, and the tier effects themselves (the window says "Effect coming soon").

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
2. Earn Cores: Campaign and Boss Hunt payouts, the glowing drop enemy.
3. The two effects per weapon.
