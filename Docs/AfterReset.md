# To do after the usage reset

A running list of things to pick up next session. When an entry is done and checked in Unity, move it to the top of
`History.md` (under "Done to-do entries") instead of leaving it here ticked.

## Open

- [ ] **Corner text HUD: wave to bottom-right, teammates above your health (built, Nov 2026, no protocol change; check in Unity).** In HUD mode 1 the wave / enemies-left block moved from top-right to bottom-right so the Rooms minimap keeps the top right, and each teammate gets a slim name + health + cell-bar row stacked above your health at the bottom-left (`HudModes.cs`). Try 2-3 players with a downed teammate.
- [ ] **Practice Range redesign (built, Nov 2026, protocol 238; check in Unity).** Look A of [`Mockups/crt-practice-redesign.html`](Mockups/crt-practice-redesign.html): the lobby's Practice tab is rebuilt in the CRT look (wave rack, enemy grid, setup keys) with a **Training** tab beside it (drill cartridges by group; groundwork only, one EXAMPLE drill; drills are `.training` files in the `training` folder, `Docs/Training.md`), and Practice has **no game over**: going down opens a DOWNED window with **Respawn here (R)**, **Restart wave** (host, Enter) and **Back to lobby** (`Msg.Respawn`). Rank XP was already 0 in Practice; in-run level-ups still give upgrade picks (say if you want those off too). Try: a solo and a 2-player practice run, die with and without a teammate up, restart a boss wave, load the example drill. The enemy grid is two blocks (Enemies, Bosses), not the mockup's four, because four don't leave room for tiles big enough to show the enemy.
- [ ] **Late-game pressure: pick the options (design, nothing built).** Owner's feedback: past wave ~20 it's bullets everywhere and face-tanking. [`LateGamePressure.md`](LateGamePressure.md) has the numbers: fire rate hits 2x by wave 21 (bosses included, and they ignore the bullet budget), the budget is full from wave 15, and there's no melee after wave 13. Options A-F; my recommendation is A (fire rate capped at 1.5x, not on bosses) + B (pressure cycles) + C (a melee floor late). Say which to build. **Update (Nov 2026): the owner is building a room system for this**; the options stay here for reference.
- [ ] **Balance pass: Legion, Drawbridge, Fireball, Imp Caller, Kunai (built, Nov 2026, no protocol change; check in Unity).** From the `weapondps` / `ultdps` reports. Legion now 4 wolves + 4 bats (mastered 7; was 6 / 10), about 2,700 extra damage in the report instead of 4,700. Drawbridge: the crush is 260 (was 140) and the rails grind enemies pressed against them for 80 a second while it stands. Fireball 22 to 32 damage (51 to 71 DPS), Imp blast 45 to 70 (18 to 77), Kunai 30 to 42 (63 to 88). Play-test each; `ultdps` is noisy for melee classes, so judge the Drawbridge in a real fight. The Bestiary and Armory pages are now out of date for these numbers (not updated, as agreed).
- [ ] **Play picker and path choice keys as soft keys, look B (built, Nov 2026, protocols 234-235; check in Unity).** From [`Mockups/crt-play-picker-keys.html`](Mockups/crt-play-picker-keys.html) (https://claude.ai/artifact/4kpDTpKGzhHxXNKPEMfNgR): the saved runs' Continue and DEL are now soft keys like the lobby's (`CrtUi.SoftKey`): the latest run's Continue is always lit, the DEL keys are dashed and quiet until the mouse is on them, and an armed delete (REALLY?) lights red. **Protocol 235:** the path choice's keys (TAKE THIS PATH, NO PATH) are soft keys now too, and the inverse-video keys (`CrtUi.Button`/`Small` and their `CrtStyle` numbers) are gone. (The rest of the UI refactor, steps 1-4 and the dead code, was done earlier; the older-runs pager no longer exists.)

- [ ] **Check in Unity (Nov 2026): How to play refreshed; Field guide hidden.** How to play now covers dodge, chests (G/X), pings, emotes, ready up, Boss Hunts and the Hangout, and the revive time. The Field guide (Bestiary + Armory) is out of the lobby and pause menus (`FieldGuideRow`, MenuDrawer.cs); to bring it back, remove that check. No protocol change.

## Ideas left for later (from finished work; details in `History.md`)

- Perks for the other bosses (only the Warden has them: Riot Gear, Contraband), second/third perk unlocks per boss, a
  no-perks record variant and a "perks used" line on run records. Open question: should reviving refill Riot Gear?
- Hunts, approaches and paths using designed waves from the wave editor (the Warden Hunt's nine first).
- Hangout: emotes, a ready pad, a weapon preview spot.
- Rank: tune the XP numbers from real play.
- Pings: muting a player's pings, ping sounds, a Loot mark on the item itself.
- Exsanguinate: pull enemies all the way in, or stop at Blood Lash range?

## Unlocks & ascensions plan
Two layers that don't compete: **unlocks give new items, ascensions deepen the ones you have.** Both use the unused
unlock system in `Profile.cs` (`Unlocks.All`) and the stats Records already keep.

**Unlock rules (weapons and abilities)**
- Earned by playing that class, ideally hinting at what the item is good at. Takes 2-5 runs, not a grind.
- Can't need the item itself. Per player in co-op (like Records). Today's weapons/abilities stay unlocked.
- Built from a few **standard condition pieces**, themed per item: play class/role · reach wave N · beat boss X ·
  win hunt X (later) · with item Y equipped · simple totals. Build the pieces once; each new unlock is just data.
- Truly unique conditions only for a few showcase/secret unlocks.
- Locked items show their condition and progress in the lobby and Records.
- First one: **Missile Pod**, a 4th DPS Soldier weapon. Hold fire and sweep to lock up to 4 targets (~0.3 s each),
  release to fire homing missiles with small blasts; a tap with no locks fires one unguided rocket. Upgrade: +1 lock
  or bomblets. Weak up close. Unlock: **beat the Hive Queen as a DPS Soldier**. Mockup first.

**Ascensions (per weapon, 3 tiers; the lobby has a placeholder: an ascension button on each weapon key opening `LobbyUi.DrawAscensionWindow`)**
- Earned only by using that weapon: I = reach wave 10 with it · II = beat 3 different bosses with it · III = win a
  full run or 3 different hunts with it.
- Rewards are **twists, not raw power**; each tier is a variant you can switch on, the base stays available.
  Example (Twin Rifle): I = new tracer/muzzle look · II = streams cross closer, crossfire hits harder ·
  III = crossfire hits mark enemies for Mark for Death.
- Links between systems, kept few: Boss Hunts count toward Ascension III; at most **one unlock per class** comes from
  an ascension (e.g. Twin Rifle II unlocks the Missile Pod); perks stay separate (they come from bosses).
- Open: each weapon's actual ascension variants need designing (a design doc or mockups before building).

**Modifier unlocks** (same standard condition pieces, no new system)
- **Open from the start:** the Easier ones (Lucky, Head Start, Practiced, Second Wind) plus a couple of simple
  Harder ones (Tough Crowd, Swift Foes). Easier ones help struggling players, who have unlocked the least.
- **Unlocked by playing:** the rest of the Harder/Risky ones, each tied to something that fits its theme. Examples:
  - Nightfall: reach wave 20.
  - Death Throes: beat the Hive Queen.
  - Hunted: win a co-op run.
  - Zombie Infection: beat the Lich.
- In co-op the **host's** unlocks decide what's available.
- Locked modifiers show their condition in the Modifiers tab.
- **Badges:** a Records badge for each modifier you've won a run with, so the open ones also get tried.

- [x] **Sort modifiers by tier.** Done (Nov 2026): Easier / Risky / Harder headings in one grid.

## Difficulty with twists
Today the presets only change numbers (README "Difficulty"), so Nightmare plays like Normal, just harder. Keep the
numbers, but each step up also **adds named rules**, shown on the difficulty card.

- **Hard** adds:
  - **Smarter enemies:** snipers lead their shots, chargers fake out, melee enemies flank instead of beelining.
  - **Elite affixes:** each elite gets one trait (shielded, splits on death, leaves a hazard trail...).
- **Nightmare** adds Hard's twists, plus:
  - **Boss upgrades:** every boss gets one extra move per stage (e.g. the Warden's searchlight splits in the last
    stand, the Hive Queen's eggs hatch faster). Most work (10 bosses), most memorable.
  - Longer last stands; health orbs fade after a few seconds.
- **Ladder past Nightmare** (name it **Threat** or **Heat**, not "ascension", which is for weapons): beat a Nightmare
  run to unlock 10 rungs, each adding one stacking rule (enemies drop a hazard on death · slower revives and the
  reviver can't shoot · mimic chests · a forced Harder modifier · bosses get an extra stage · ...). Each rung won is
  a record.
- **Ties in:**
  - "Win on Hard" / "win at Threat N" become standard unlock pieces.
  - A hunt's "Hard" (perk #2) uses these twists.
  - The twists become switches in the Custom panel.
- **Build first:** the Hard twists (smarter AI + elite affixes). They're in every wave, so they change the feel
  fastest, and need no per-boss work.

## Online play without Tailscale: relay with room codes (not urgent)
So friends can play without Tailscale or port forwarding.
- A small **relay** program on a cheap server (~$4-6/month, e.g. Hetzner or DigitalOcean) that just passes messages
  between the host and the joiners. The host's game still runs the match.
- In the menu: **Host online** gives a short room code (e.g. `KJ4P`); **Join online** takes the code. Nobody
  installs anything extra.
- Fits the current setup: networking is plain TCP (`Assets/Scripts/Core/Net.cs`, `Session.cs`) and the core game
  doesn't depend on Unity, so the relay can be plain C#. Test it like the rest of multiplayer (SimTest drives real
  sessions over localhost); add a relay check.
- Deliverables: the relay program, the two menu buttons and code entry, a version check through the relay, and
  step-by-step server setup instructions (rent, install, run as a service) in the README.
- Until then, a no-code stopgap: the host runs a free tunnel app like playit.gg and friends join its address + port.
## Power stacking (built, Nov 2026, protocol 237; check in Unity)

- [ ] **Diminishing returns and wounds** ([`PowerStacking.md`](PowerStacking.md)). Repeat copies of the twelve unlimited
      stat cards give less (2nd 80%, 3rd 67%, 5th 50%; Hair Trigger x14 = x2.3 fire rate); the pick card says what the next
      copy is worth. 10% of every hit is a wound no heal brings back until the wave is cleared (dark red end on the
      health bars). Both arenas; parry unchanged. Check in Unity: the card note, the wound on the health bars, and
      whether a late run still turns into a cleanup.

## Rooms arena (built, Nov 2026; check in Unity)

- [ ] **Rooms arena, phases 0-3** ([`Rooms.md`](Rooms.md); mockup [`Mockups/rooms.html`](Mockups/rooms.html),
      https://claude.ai/artifact/4GPVBfq2ytFkB3oKkbSevj). Lobby → Difficulty → **Classic arena / Rooms arena** (Classic
      by default). Rooms: on regular waves the Wings map (two side rooms and two hallways round the hub, doors, walls,
      cover; boss waves in the hub), the following camera, nests that keep their rooms stocked (both down = the rest of
      the wave called off), Runner spills through a random door (amber warning), doors that lock behind you while a nest
      lives, room enemies that guard 10 s (down to 5) then
      overflow out of any door to hunt you while the nest refills the room, cleared rooms (orbs + XP), the minimap, the vault one wave in ten from wave 6 with the Gatekeeper (his Data
      Gate gone in the Rooms arena), and 1.3x enemy damage on rooms waves. Measured with `run.sh 1 1 arenabal`.
      **Play it and say**: is 1.3x right (`Rooms.EnemyDamageMult`), do waves drag, is the lock fair, the camera slide,
      the minimap's size and spot. Push the `classic-arena` tag from your machine
      (`git tag classic-arena abcee50 && git push origin classic-arena`). Next: phase 4 (the power-stacking nerf pass).

