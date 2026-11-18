# Round flow: a smoother break, per-player loot and remote chests

Status: **planned, nothing built** (protocol 407 when written). Decided with the owner on Nov 18, 2026. Build one step at a
time ("start on step N").

## Why

The break between waves (`Game/WaveClearedUi.cs`, `Core/WaveReady.cs`) is slow:

- Results, Build and Ready are steps you go through in order, and the Ready key has to step you through each one.
- Ready is silently ignored while you still have picks to make (cards, mods, milestones, prizes).
- Auto-ready only counts once you reach the Ready step.
- Getting a chest means minimizing the box, walking out, walking over the loot it drops, and walking back.
- In co-op you can't see who the wave is waiting on, or why.

## Decisions

- **Tabs, not steps.** Results / Build / Chests / Ready are tabs. The box opens on Results, and Ready works from any tab.
- **The Ready key says what blocks it**, e.g. "2 PICKS LEFT". Pressing it opens the first pending pick.
- **Auto-ready counts from the start of the break**, once your picks are done.
- **Status chips.** Each player row shows Picking / Building / Looting / Ready.
- **Per-player pickups.** Gold coins, vector pickups, health orbs and Magnets drop as **a full copy for each player**
  (not split). You see and pick up only your own. Shared as before: the Healer's orbs, Medkit orbs, chest loot cards
  (already per-player).
- **Floor loot is auto-collected when the next wave starts**: your own coins, vector pickups and chest loot cards. Health
  orbs and Magnets aren't collected (healing only counts when you're hurt, and a Magnet is a choice).
- **Uncollected chests tab.** It lists every unopened chest on the map: its kind, which room it's in, and a dot on the
  mini-map. **Anyone** can press Collect. The chest opens where it stands and everyone gets their share, the same as
  opening it in person. Loot cards go straight to each player and gold straight to each wallet. The node is spent.
- **Fee:** 15 + 2 per wave, paid by whoever presses Collect (the key is greyed out when they can't afford it). Walking
  there stays free. Chests can only be collected from the tab during the break, because that's where the tab lives.

## Risks

- **A full copy each multiplies gold by the player count** (3 players: about 3x the gold, vectors and orb healing).
  Prices, vector caps and orb healing are tuned for shared drops. Step 3 runs the balance suite at 1-3 players and
  reports the numbers; whether to rebalance is decided then.
- **Snapshot size:** a copy per player triples the coins in the snapshot. Each pickup carries an `Owner`. The host still
  sends every pickup, so a later step could cut it down to each player's own.

## Steps

1. **Break flow (no protocol change).** Tabs with Ready from any tab, the Ready key showing "n PICKS LEFT" and opening
   the first one, auto-ready counting from the start of the break. Previews: `uishots.sh` before and after.
2. **Status chips (protocol bump).** The host works out each player's break status (picks pending, the build window
   open, outside the hub, ready) and sends it with `Msg.WaveReady`. The client sends "build window open". Chips go on
   the player rows.
3. **Per-player pickups (protocol bump).** `Owner` added to `Coin`, `Orb` and the gadget (0 = shared). `DropGold`,
   `DropVectors`, `SpawnOrb` and `DropGadget` make one copy per player. Coins and orbs can only be picked up, or pulled
   by a Magnet, by their owner. Other players' pickups aren't drawn. Off-screen markers count only your own. Test suite
   `pickups`, plus the balance numbers.
4. **Auto-collect at wave start (no protocol change).** When the break ends, the host gives each player their own coins,
   vector pickups and unclaimed chest loot cards, with a pickup sound and a "+n gold" toast.
5. **Uncollected chests tab (protocol bump).** `Msg.CollectChest` (a chest id); the host checks the fee, takes it and
   opens the chest remotely (`OpenChest` with a `remote` flag: no ground drops, gold straight to wallets, loot cards
   straight to their owner). The tab is a list of `DkPanel` rows (chest look, kind, room, fee, Collect key) next to a
   mini-map. Preview of the tab, test suite `chests`, README section.
