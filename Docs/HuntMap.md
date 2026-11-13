# Hunt map: path selection for Boss Hunts (design)

Status: **steps 1-4 built (every node type)**; step 5 (what the clues do) and 6 (tuning) are left (protocol 333; the map is a switch on the lobby's Boss Hunt tab, off by default, so the ten-wave hunt stays as it was) (the screen is `Game/HuntMapUi.cs`, drawn with the lobby's widgets; preview `huntmap`; not wired into the match yet).
Earlier: step 1 (`Core/HuntMap.cs`, suite `huntmap`: the map, its links, Skirmish and Elite nodes, progress, restore; not in the game yet). Numbers are first guesses. Open questions at the bottom need the owner's call.

## The idea

A Boss Hunt is currently ten fixed waves ([`BossHuntsAndPerks.md`](BossHuntsAndPerks.md) §1). Make it a **hunt map**, in
the style of Slay the Spire: you track the boss through a short branching map, and each step you pick which node to
take next. The last node is the boss. The arena stays **one single room**; a node only changes what spawns, the rules
for that fight, and what you win. (Expanding rooms stay a Campaign feature: [`ExpandingArena.md`](ExpandingArena.md).)

Not to be confused with [`Paths.md`](Paths.md): those are two-card picks after each boss in a full run and change a
stretch of five waves. The hunt map picks one fight at a time.

## Map shape

- **9 steps, then the boss** (step 10), so a hunt stays ~8-10 minutes and `Hunts.Length` stays 10.
- Each step shows **2-3 nodes**; you may only move to a node connected to the one you just cleared (lines between columns).
- The map is **generated per hunt from a seed** (host rolls it, sent in the start message), so a co-op party sees the same map.
- Step 1 is always a plain faction fight. Step 9 is always a rest or shop (so you can prepare for the boss). Step 5
  always offers an elite.
- Node details are visible before choosing: type icon, faction, danger pips, and the reward. No hidden info except
  gamble outcomes.

## Screen layout (chosen)

Approximate HTML mockup: [`Mockups/hunt-map.html`](Mockups/hunt-map.html) (layout only; the real screen is drawn with
the `DevKit.cs` widgets and checked with `Tools/Preview`, so colours, fonts and spacing will differ).

- **Frame:** the lobby's. Back key and a spaced-caps title ("HUNT MAP") on top, a vote timer on the right, the blue
  phosphor palette on navy, bracket-cornered boxes, spaced-caps section headings with a rule under them.
- **Two columns:** the map on the left in a `LoadoutBox`, and **all the information in one column beside it** (it stays
  in view as the map scrolls; on a narrow window it stacks under the map). The column, top to bottom:
  1. **Hunt:** target, step, gold, boss clues bar.
  2. **Party:** one seat per player with their role, their vote and a LOCKED tag.
  3. **Next sector:** a key per choice (glyph, name, danger pips, reward, the 1-3 hotkey).
  4. **Detail:** the highlighted choice: name, what it is, danger and reward tiles, who voted for it.
  5. **Lock in vote:** a green key like READY; tap again to change your vote.
  6. A legend of the node glyphs.
- **Map direction:** vertical, **start at the top, the boss at the bottom**.
- **Nodes:** all squares. The type is the small vector glyph inside it (triangle skirmish, diamond elite, chevron
  ambush, hexagon contraband, plus rest, `$` shop, `?` gamble) in the type's colour; the boss is a larger square.
  Cleared nodes are filled and ticked, with their name beside them. The next choices have a dashed gold frame and a
  `[1]`-`[3]` tag, and the vote letters (P1-P3) beside them.
- **Three lanes** (left, middle, right). A node is on one lane.
- **Lines are right angles only, no diagonals.** A link leaves a node's bottom, runs down, turns sideways in the gap
  and comes down into the next node. Cleared route: bright. The next choices: gold. Everything else: dim, thin.
- **Which links exist** (the generator's rules, so the map stays readable):
  - every node links straight down its own lane when the next row has a node there;
  - plus one **shift** per row gap, all the same way in that gap (right on even gaps, left on odd ones), so two
    links never cross;
  - any node left without a link in or out gets one to the nearest lane;
  - the node you stand on links to every neighbouring lane: those are your choices;
  - rows can have a lane empty (an empty middle lane before the boss).
  When a node has two or three links in or out, they leave and enter at separate points along its edge (-12, 0, +12 px
  for three) and each link turns at its own height, so no two lines run over each other.
- **Hotkeys:** 1-3 choose a sector, Enter locks in.

## Node types

| Node | What | Reward |
|---|---|---|
| **Skirmish** | A normal faction wave (today's build-up wave at that step's strength) | Gold + XP |
| **Elite squad** | A tougher squad or set piece (the Warden's riot line); danger 2 pips | Legendary or weapon mod pick |
| **Ambush** | Enemies come from all sides fast, shorter wave | Extra XP, a chest |
| **Contraband** | A fight with a modifier (dark, hazard floor, spawn portals, a mini-boss); pick it for the prize | A rare-or-better card |
| **Rest** | No fight: heal to full, or +1 reroll, or revive token if downed | Recovery |
| **Shop** | The Bazaar's shop screen, spend gold | Upgrades |
| **Gamble** | Unknown: a fight, a gift or a curse; shown as `?` | Varies |

Step strength still follows `Hunts.Step`, so a node's enemies get tougher as you advance regardless of the route.
Elites and Contraband are the "harder, pays more" choices; Rest and Shop are the "play it safe" ones. The choice should
be real: you can't take everything, because each step offers only 2-3 of the types.

## Boss link (the "hunt" part)

The map isn't only a menu: what you pick shapes the boss fight.

- Each **elite** cleared gives a **boss clue**: the boss starts the fight with one weakness revealed (e.g. a telegraph
  shown longer, or a starting 8% health chunk missing). Capped at 3, so skipping elites is possible but harder.
- Clues are shown on the map as pips next to the boss node ("Warden: 2/3 tracked").
- The Warden's stages and numbers stay as they are, so the Boss Lab data holds; a clue only gives a bonus to the party.

## Flow

1. Lobby: choose Boss Hunt as today; the map is rolled when the match starts.
2. **Map screen** between fights: shows the map, where you are, clues, gold. Built with `DevKit.cs` widgets
   (new `HuntMapUi.cs`, beside `HuntUi.cs`).
3. Everyone **votes** for a node (a marker per player on it); most votes wins, the host breaks ties. The next fight starts when all have voted or after 20 s.
4. The fight runs in the one arena. On clear: rewards, then back to the map.
5. Boss node: the boss at wave-10 strength as today. Win ends the run (unlock as today).

## Code plan

- `Core/HuntMap.cs`: node types, map generation from a seed, `Node` list with links, picks, clues. Pure, no Unity,
  with a `huntmap` SimTest suite (every map is connected, ends in the boss, step 9 is rest/shop, no unreachable nodes,
  same seed gives the same map).
- `Hunts.cs`: the wave number becomes the **step**; each node type maps to a wave spec (faction, step, squads, modifiers).
- `Game/HuntMapUi.cs`: the map screen. Preview with a `Tools/Preview` flag, as with `HUNT=<n>`.
- Snapshot: current node, cleared nodes, clue count, the seed. **Protocol bump** (Docs/ProtocolHistory.md).
- Save: hunts aren't saved today; leave it that way.
- README: Boss Hunt section; CODEMAP lines for the new files.

## Build order

1. ~~`HuntMap.cs` + tests, with only Skirmish and Elite nodes.~~ Done. Still to do from this step: playing it (a node picks the wave's strength: an Elite is a finale-style wave one approach step tougher; the host auto-picks until the vote exists). That touches `HuntBosses` (`ApproachStep`, `FinaleWave`) and the hunt's start, so it goes with step 3.
   - Measured over 3000 seeds: 51% of nodes have two ways on, 14% of rows give no choice at all (a 3-by-3 row gap can't give every node two links without crossing). Tune in step 6, or allow a few crossings.
2. ~~Map screen + preview screenshots.~~ Done: `HuntMapUi.cs`, `HuntMapLayout.cs` (the lines), preview mode `huntmap`.
3. ~~Net: the vote, snapshot, protocol bump.~~ Done (protocol 333): `Session.HuntMapMode` and the lobby switch, `HuntMapGame.cs` (map, vote, timer, node waves), `Msg.HuntMapState` / `Msg.HuntMapVote`; an Elite is one approach step tougher with the finale set piece; Skirmish is the hunt's own strength; Rest, Shop, Ambush, Contraband and Gamble are step 4.
4. Rest, Shop, Ambush, Contraband and Gamble nodes, one at a time. **Rest and Shop done** (no-fight nodes: the wave number moves on without a fight, a Rest heals everyone and gives a reroll, a Shop opens the Bazaar and the node clears when it closes; gold is earned all hunt long in a map hunt; never two in a row, always one before the boss). **Ambush, Contraband and Gamble done** (protocol 337): an Ambush keeps 70% of the wave and spawns it at 0.35x the usual gaps with no portals, then pays XP (30 + 6 per wave) and drops a chest; a Contraband is dark (`HuntDark` feeds Nightfall's `NightF`) and pays each player a prize pick (rare or better); a Gamble is rolled by the host when the party arrives (Skirmish 30, Elite 25, Ambush 20, Contraband 25, so it is always a fight), the outcome goes out with the map state and shows on the HUD chip, and a Gamble that turns out an Elite still gives its clue. Elites pay the clue only for now (the doc's legendary pick is not built).
5. Boss clues.
6. Tune with the Boss Lab and hunt SimTest reports (win rate by route: safe route vs elite route).

## Risks

- **Too samey:** if most nodes are plain fights, the choice is cosmetic. Elite, Contraband and Gamble need distinct feel.
- **A safe route wins:** if rest and shop routes always beat the boss, add a cost (fewer legendaries that way).
- **Co-op:** a vote needs a timeout so one idle player can't stall the hunt (20 s).
- **Later bosses:** each hunt needs its faction's waves first (only the Warden has one), so the map applies to the Warden Hunt first.

## Decisions (owner, Nov 2026)

| Question | Decision |
|---|---|
| Length | **10 steps** (9 nodes, then the boss) |
| Boss clues from elites | **Yes**, capped at 3 |
| Co-op pick | **Vote**: everyone picks a node, the most votes wins, the host breaks ties |

## Open

- Should the map also replace [`Paths.md`](Paths.md) in full runs (a map between bosses instead of two cards)? Not decided; hunts only for now.
