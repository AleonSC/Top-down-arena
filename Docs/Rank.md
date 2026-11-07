# Rank (design)

Status: **built** (Nov 2026, protocol 135): `Core/Rank.cs`, the profile fields, the `rank` test suite, the results strip,
the lobby badge and frame, the menu chip, Records > Rank, the Ember and Gold trails (Frost, Storm, Void and Prism added later, protocol 136; Light Cycle 139) and the Dusk Grid background.
Mockups: [`Mockups/rank-screens.html`](Mockups/rank-screens.html) (https://claude.ai/artifact/6jPzqJeT4sFbfWhm1Ewkf9).
**The badge is the "Engraved crest" (option A of `Docs/Mockups/rank-badges.html`): a bevelled shield with tier chevrons, wings from Champion and a star on Legend, drawn procedurally in `RankLook.cs`. (Earlier: style B, a pixel shield.)** All numbers below are placeholders to tune now that
it's playable. What was built differently from the mockup is listed under "As built".

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
| 14 | Trail: Frost |
| 16 | Frame: Circuit |
| 15 | Menu background: Dusk grid |
| 18 | Trail: Storm |
| 20 | Title: Champion, Frame: Ember Edge |
| 22 | Trail: Void |
| 24 | Frame: Void Pulse |
| 25 | Trail: Gold stardust |
| 30 | Frame: Gold, Title: Legend |
| 35 | Trail: Prism |
| 40 | Frame: Legend Foil |
| 45 | Trail: Light Cycle |
| 50 | Frame: Prism |
| 6 | Card background: Dusk Grid |
| 10 | Card background: Starfield |
| 13 | Card background: Blueprint |
| 19 | Card background: Ember Glow |
| 23 | Card background: Aurora |
| 27 | Card background: Circuit Board |
| 32 | Card background: Rain on Glass |
| 37 | Card background: Void Rift |
| 42 | Card background: Gilded Foil |
| 55 | Card background: Prism Shards |

- **Title**: the line under your name on the menu chip, the lobby hover card and Records.
- **Trail**: a short particle trail behind your player in the match (drawing only, like the existing weapon looks).
- **Frame**: the border of your lobby party card (and your results card).
- **Card background**: the fill of your lobby party card, behind your name (`Game/CardBackgrounds.cs`; mockup `Docs/Mockups/card-backgrounds.html`). Ten, from rank 6 to 55. Everyone in the lobby sees it (`LobbyEntry.CardBg`, protocol 140).
- **Name effect**: how your name is drawn for everyone (`Game/NameEffects.cs`, `DrawName`; mockup `Docs/Mockups/name-effects.html`). Twelve, rank 4 to 56, including Glitchy at 36. `LobbyEntry.NameFx`, protocol 142.
- **Aim line**: the line from your weapon to the crosshair (`Game/AimLineLooks.cs`, `DrawAimLook`; mockup `Docs/Mockups/aim-lines.html`). Classic is free; nine unlock: Dotted Tracer 5, Chevron Flow 11, Ruler 14, Ember Wisp 17, Frost Shard 21, Storm Arc 26, Pixel Beam 29, Helix 34, Prism 46. Only you see your own line, so it is not in the lobby and there is no protocol change. `Profile.AimLine`, saved as the sixth field of the `equip` line.
- **Cursor**: your crosshair in a match (`Game/CursorLooks.cs`, `DrawCursorLook`; mockup `Docs/Mockups/cursors.html`). Classic is free; ten unlock: Fine Cross 3, Corner Sight 4, Ember Ring 11, Frost Snowflake 15, Circuit Sight 17, Storm Arc 21, Void Eye 26, Gold Diamond 31, Prism Ring 38, Light Cycle 46. Each look also has a Corner pointer for menus and overlays that morphs into it (`DrawLookPointer`/`DrawLookMorph`). Local only, no protocol change. `Profile.Cursor`, the seventh field of the `equip` line.
- **Emote**: a bubble over your head for 2.4 s, seen by everyone in the Hangout and in a match (`Game/EmoteLooks.cs`, the bar in `Game/EmoteBar.cs`; mockups `Docs/Mockups/emotes.html`, `emote-bar.html`). Thirteen, rank 1 to 52, nothing to equip: hold the Emote key (T) for the bar; the number keys 1-6 play the bar's slots (the leftmost in view is 1), so they play whatever you have scrolled to. Some move the model (`EmoteBody` in `EmoteLooks.cs`, applied in `DrawPlayer` and the Hangout avatar). `Msg.Emote` (protocol 141); the host checks the rank and keeps the pause (2 s in a match, 0.75 s in the lobby).
- **Menu background**: an extra entry in the Settings > Visual effects picker (`MenuBackgrounds.cs`). Dusk Grid (15) and the **reactive** Ripples (25), Boot Log (28), Vector Rocks (39) and Life Grid (43) so far (mockup `Docs/Mockups/crt-backgrounds.html`). The last three keep a little state, stepped once a frame with `ReactStepOnce`, and read `ReactClick` / `ReactEnergy` (`Game/BackgroundReactSet.cs`). A reactive background is one whose id starts with `bg-react-` (`Rank.IsReactiveBackground`) and reads the pointer, its speed and click ripples from `Game/BackgroundReact.cs`. To add one: a `CosmeticDef` in `Rank.cs`, a name, accent, `case` and `RewardBackgrounds` entry in `MenuBackgrounds.cs`, a `BgXxx(t)` using `ReactPointer` / `ReactRipple` (copy `BgRipples`). Local only, no protocol change.
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

## As built

- **Resolved questions:** a rank-up shows only on the results screen (no mid-run toast); trails are drawn for every
  player (each game keeps its own short path history; the lobby entry says who has which trail); no first-run bonus,
  since rank 2 costs 200 XP and a first run is worth about that much already.
- **Hunts** earn XP for the waves cleared and the boss, with no win bonus. A boss beaten on the wave you then lose on
  counts (the wave is treated as cleared).
- **Rank report** (the results' second page, `DrawRankReport` in `RankUi.cs`): the mockup's full panel. The **Rank report**
  button by Leave (or a click on the strip) swaps the stat cards for it; **Stats** swaps back. Each run's results start
  on the stats page. Other players' XP gains aren't known on your screen, so the party box shows their ranks only.
- **Results screen:** the rank strip sits under the cards (they're 408 px high now, the tiles 56) rather than in a
  panel beside them: the screen had no room for one. It shows the bar filling, the total, the breakdown on one line
  and the modifiers behind the multiplier, or on a rank up what it unlocked. Each card has its player's badge.
- **No separate Cosmetics tab:** you equip in **Records > Rank** (the reward ladder has an Equip button on each
  cosmetic you've earned). The Dusk Grid is equipped by picking it in Settings > Visual effects (or its Equip button),
  and is skipped while locked.
- **Menu chip** (reworked Nov 2026): a generic player figure (no role or class: those are picked in the lobby), your
  name, your rank title and number, the XP bar under them and the badge on the right. It uses your equipped frame
  for its rim. Click the name to rename; click anywhere else on it to open your cosmetics window.
- **Cosmetics window** (`Game/CosmeticsUi.cs`): look C, the drawer, from [`Mockups/customization.html`](Mockups/customization.html)
  (https://claude.ai/artifact/GXoYicaHt5JUazDpKnkfPU): a drawer with your rank and a row per category (Frame, Trail,
  Background, Reward ladder), and the open one on the right with a live preview, how others see you, and the Equip
  list. "Your pick" for the background goes back to the one you had before Dusk Grid. The "example" tiles in the mockup
  aren't built.
- **Titles** come with the tier and aren't equipped separately.
- **Dev tool:** F1 > Tools > Account rank (+1, +10, Rank 30, Max rank, Reset): `ArenaApp.DevSetRank` in `RankUi.cs`.
- **Main menu bar:** 92 px tall (was 70) with a 360 x 64 chip, a 46 px badge and 150 x 50 buttons, so the chip's name, title
  and XP bar fit.
- **Network:** `LobbyEntry` gained `Rank` and `Frame` and `Trail` (in the `Lobby` message), and clients send
  `Msg.Cosmetics` (rank, frame, trail) when they join, return to the lobby, or equip something. The host clamps the
  rank and drops a frame or trail the rank doesn't have. Protocol 134 to 135.

## Possible next steps

- More cosmetics in the ladder (each is a row in `Rank.Cosmetics` plus its drawing).
- A frame on the results cards, and a trail preview in Records.
- Tune the XP numbers and the 200 x rank curve from real play.
