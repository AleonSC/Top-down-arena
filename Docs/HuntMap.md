# Hunt map: path selection for Boss Hunts (design)

Status: **proposal, nothing built.** Numbers are first guesses. Open questions at the bottom need the owner's call.

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

1. `HuntMap.cs` + tests, with only Skirmish and Elite nodes. Playable with a debug picker.
2. Map screen + preview screenshots (before/after PNGs for you to pick the look).
3. Net: the vote, snapshot, protocol bump.
4. Rest, Shop, Ambush, Contraband and Gamble nodes, one at a time.
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
