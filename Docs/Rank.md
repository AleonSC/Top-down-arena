# Rank (design)

Status: **designed, not built.** Mockups: [`Mockups/rank-screens.html`](Mockups/rank-screens.html)
(https://claude.ai/artifact/6jPzqJeT4sFbfWhm1Ewkf9). **Badge style B (the shield with tier chevrons) is chosen.**
All numbers below are placeholders to tune once it's playable.

## What it is

A **cosmetic account rank**. Every finished run earns XP; XP raises your Rank; ranks unlock **cosmetics only**
(titles, trails, frames, menu backgrounds). It never gives power or items.

It sits beside the other two progression layers and doesn't compete with them:

| Layer | Gives | Earned by |
|---|---|---|
| Unlocks (`Profile.cs`, `Unlocks.All`) | new weapons, abilities, modifiers, hunts | specific conditions ("beat the Hive Queen as a DPS Soldier") |
| Ascensions | twists on a weapon you already have | using that weapon |
| **Rank** | cosmetics and bragging rights | playing, with bonus XP for risk |

Why it exists: every run, good or bad, pays something; harder modifiers finally have a reward; and everyone in a
co-op lobby can see how much the others have played.

**Not Level.** The results cards already show `LEVEL n`, the in-run level earned during a match. Rank is a separate
number, always labelled RANK and drawn as a shield badge, never as a "Level".

## Decisions

- Cosmetic only. Gameplay items stay on conditions, not on rank.
- Per player, like Records. In co-op each player's own computer works out their XP and keeps their rank.
- Badge style B: a shield with the rank number and tier chevrons (the chevrons drop out below ~18 px radius).
- Tiers: 1-4 **Recruit** (steel), 5-9 **Challenger** (blue), 10-19 **Veteran** (teal), 20-29 **Champion** (violet),
  30+ **Legend** (gold). Chevrons: 1, 2, 3, 4, 5 by tier.

## XP

Earned when a run ends (win or lose). Placeholder values:

| Source | XP |
|---|---|
| Each wave cleared | 20 |
| Each boss defeated | 150 |
| Each ally revived | 40 |
| Win a run | +500 once |
| **Subtotal** | sum of the above |
| **Modifier multiplier** | x (1 + the sum of each active modifier's bonus), see below |

The results screen shows these as a breakdown, then fills the bar (see the mockup's screen 1).

### Modifier multiplier

The point of the modifiers brainstorm: **harder modifiers pay more**.

| Modifier tier | Bonus each |
|---|---|
| Harder | +25% |
| Risky | +15% |
| Easier | +0% (Gold Rush, Lucky, ... don't add; they don't cost XP either) |

- Capped at x3 so a stack of every Harder switch isn't an XP exploit.
- Difficulty adds its own step on top: Easy x0.75, Normal x1, Hard x1.25, Nightmare x1.5 (Custom x1).
- **No XP** from the Practice Range, runs that used dev tools (F1), or runs with the bot. The same rule Records use.
- Boss Hunts earn XP like a run (10 waves, so less), and the hunt's boss counts as a boss defeated.

### Curve

Getting from rank *r* to *r+1* costs **200 x r** XP. Rank 8 needs 1,600; rank 30 needs 6,000. A run is worth roughly
400 to 1,200 XP, so early ranks come in about one run each and rank 30 is on the order of 140 runs in total.
Tune in `Rank.NeedFor(rank)`; there's no cap on rank (the badge handles three digits).

## Rewards (cosmetics)

Placeholder ladder, from the mockup:

| Rank | Reward |
|---|---|
| 5 | Title: Challenger |
| 8 | Trail: Ember |
| 10 | Title: Veteran |
| 12 | Frame: Steel |
| 15 | Menu background: Dusk grid |
| 20 | Title: Champion |
| 25 | Trail: Gold stardust |
| 30 | Frame: Gold, Title: Legend |

- **Title**: the line under your name on the menu chip, the lobby hover card and Records.
- **Trail**: a short particle trail behind your player in the match (drawing only, like the existing weapon looks).
- **Frame**: the border of your lobby party card (and your results card).
- **Menu background**: an extra entry in the Settings > Visual effects picker (`MenuBackgrounds.cs`).
- Equipped on a Cosmetics tab in the lobby (or Records). One of each kind at a time. Everyone sees your frame and
  title; the trail is seen by the players in the match.

## Where it shows

From the mockup, in priority order:

1. **End of run:** the Rank panel beside the stat cards: the XP breakdown ticks in, the bar fills, a rank-up pops
   the badge and unlocks the reward. Also shows your party mate's gain. This is the screen that makes it feel good.
2. **Lobby party cards:** the badge on the corner of each portrait, a hover card with title, XP and equipped items,
   and the frame on the card.
3. **Main menu name chip:** title line, badge, thin XP bar; XP numbers on hover. Clicking still edits the name.
4. **Records:** profile column, reward ladder, recent runs with the modifiers used and the multiplier they gave. The
   per-modifier win badges from the unlock plan fit under the runs.
5. In the match: at most the badge beside a player's name in the HUD. No XP bar in play.

## Build notes

- **Profile:** add `Xp` (long) to `Profile` in `Profile.cs`; rank is derived from it (no stored rank to drift). Add
  equipped cosmetic ids (title, trail, frame, background). Old profiles load with 0 XP.
- **Rank code:** a small `Core/Rank.cs` (plain C#, no Unity): `NeedFor`, `RankOf(xp)`, `TierOf`, `Title`, the XP
  breakdown for a finished run (so the results screen and the profile use the same numbers), and the cosmetics
  table. Cosmetic ids are stored, so **never change an id** once players own it (same rule as `UnlockDef.Id`).
- **Others' ranks:** each player's rank, title and frame travel with their lobby info so everyone can draw them. That
  is a network message change, so **bump `Session.ProtocolVersion`** (new `Msg` values go at the end, before `None`).
  Cosmetic and client-reported, so there is nothing to validate beyond clamping the rank and ids.
- **Results screen, lobby, menu chip, Records:** draw from the shared `Rank` helpers. The badge is one drawing
  function used at every size.
- **Tests:** a `rank` SimTest suite: the curve and tier edges, the XP breakdown and multiplier cap, no XP from the
  Practice Range or dev-tool runs, profile load/save with and without XP, and the lobby message round trip.
- Reference pages (Bestiary, Armory) are not touched by this.

## Open questions

- Does Rank-up ever show a toast mid-run? (Proposed: no; it only happens on the results screen.)
- Are trails visible to the other players in co-op, or only to you? (Proposed: everyone; drawing only.)
- Should the first few ranks come from a one-off "first time" bonus (first boss beaten, first win), so a new
  player sees a rank-up in their first run? (Proposed: yes, a small bonus; a first run is ~400 XP and rank 2 needs 200.)

## Build order

1. `Rank.cs`, the `Profile` fields, the `rank` test suite. No visuals yet.
2. The end-of-run panel (it's the reason for the whole feature).
3. The lobby badge and the lobby message (the protocol bump).
4. Menu chip and Records.
5. Cosmetics: titles first (free), then frames, trails, backgrounds, and the Cosmetics tab.
