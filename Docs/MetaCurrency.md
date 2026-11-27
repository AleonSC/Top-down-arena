# Meta currency (design)

Status: **proposal, not decided** (Nov 2026). No code yet. Question from the owner: add a meta currency to buy
cosmetics instead of (or beside) the Rank system?

## Where we are

- **Rank** (`Docs/Rank.md`, `Core/Rank.cs`): XP per run, ~100 cosmetics unlocked at fixed ranks (frames, trails,
  backgrounds, card fills, name effects, aim lines, cursors, emotes). Rank 30 is ~140 runs.
- **Expertise** (`Expertise.cs`): per-class XP, unlocks weapon skins (the Rubber Chicken).
- **Gold** is already the in-run currency (Black Market, Contraband), so the meta currency needs another name.
  Working name: **Tokens** (arcade tokens; fits the CRT cabinet).

## The trade-off

| | Rank unlocks (now) | Currency shop |
|---|---|---|
| Player choice | none: fixed order | buy what you want first |
| Shows playtime | yes (badge) | no (spent tokens are invisible) |
| Always a next goal | yes, the next rank | yes, the next purchase |
| A "dud" reward at a rank | possible | never: you skip what you don't like |
| Balance work | one curve | prices for ~100 items |
| Grind feel | steady | can feel like a store |
| Code cost | built | new wallet, shop UI, prices; Rank's unlock table changes |

## Options

**A. Hybrid (recommended).** Keep Rank and its tier badges/titles (bragging rights, co-op "how much have they
played"). Every run also pays Tokens (= run XP / 10, so ~40-120 a run). Rank unlocks stay for a small set of
**signature** items (tier frames, Light Cycle, the top-tier looks); the rest move to a **Token shop**, priced by
the rank they unlock at now (`price = 10 x old rank`, so the Ember trail at 9 costs 90, about one run). Optional:
reaching a rank refunds its old item for free if you haven't bought it, so nobody loses progress.

**B. Replace.** Rank goes; everything is bought with Tokens. Simplest economy, but loses the badge, tiers and the
co-op signal the Rank doc was built for. Existing players get Tokens for their XP (XP / 10) and keep their unlocks.

**C. Rank only.** No currency; add choice instead: at each tier, pick 1 of 3 cosmetics. Smallest change.

## Things to decide

- Option A, B or C.
- Name (Tokens, Credits, Chips, Shards).
- Rate and prices (above are placeholders).
- Rotating shop (daily picks) or a fixed catalogue. Rotation adds pull but is FOMO-ish for a co-op indie game;
  fixed is recommended.
- Do weapon skins stay on Expertise or join the shop (recommended: stay, they show class mastery).
- Existing saves: everything already unlocked stays unlocked.

## Build steps (if A)

1. `Profile.Tokens` + award at run end beside `Rank.Award` (same no-dev-tools/no-bot rule); results strip shows `+n TOKENS`.
2. `CosmeticDef.Price` (0 = rank-only); unlock = rank reached or bought; save bought ids on `Profile`.
3. Shop in Profile > Look: the locked key shows a price and a BUY key (DevKit widgets), with the existing TRY IT ON preview.
4. Tests in suite `rank`; README Rank section; no protocol bump (cosmetics on the wire are unchanged).
