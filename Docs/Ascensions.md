# Weapon Ascensions: 4 tiers

**Status: design agreed, nothing built.** The lobby's ascension button is still a placeholder ("0/3", `LobbyUi.AscensionKey`).

## Decisions (owner)

- **4 tiers per weapon** (was 3 in the placeholder), matching the 100-wave Campaign.
- **Campaign reward:** reaching wave 25 / 50 / 75 / 100 gives +1 ascension tier to the weapon the player is holding at that moment (tier 1 / 2 / 3 / 4).
- **Ascension Tokens:** dropped by bosses in **Boss Hunt only** (not Campaign). One shared currency, spent in the lobby to buy the next tier of any weapon you own.
- **Persistence:** tokens and tiers are saved in the profile (like Rank and unlocks); they survive across runs.

## Proposed numbers (not agreed, tune in play)

- Token cost of the next tier: 10 / 20 / 40 / 80 (tier 4 costs the most).
- Boss Hunt payout: a few tokens per boss kill, more for a no-death hunt.

## Open

- What each tier does per weapon (the "twist" in `Docs/Rank.md`); needs one design pass per weapon.
- Co-op: each player's profile gets its own tier; the host's wave reward applies to every player's held weapon.
- Profile save format and protocol bump when built.
