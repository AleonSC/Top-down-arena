# Riot Gear and the perk framework

Status: **steps 1 to 5 built** (`Core/Perks.cs`, the profile's `perks` line, `Core/RiotGear.cs`, the `Damage` hook; `perks` test suite); step 6 (README, final docs) not yet. Step 5 (protocol 154): the Plate look, the HUD chip and `FxKind.RiotBlock` (Pos2 = where the hit came from), needs a try in Unity: `RIOT=ready|wait,17|block,0.1 Tools/Preview/shot.sh riot hud` shows it. (Step 4, the Perks tab and seat icons, needs a try in Unity: `Tools/Preview/shot.sh perks lobby` with `PERKS=1 LOBBYTAB=5` shows it). The lobby sync is in (protocol 152): a client's perks go in `Msg.Cosmetics` (host sanitises them with `Perks.Sanitize`), `Msg.Lobby` carries every seat's, the match-start message carries each player's (`Game.PerksFor` on the host: none where runs aren't recorded, the saved seat's perks on a continued run), the snapshot carries `RiotT`, and saved runs keep a `perks` line per seat. Replay format is 2, so older replays no longer open. The block uses the existing Ring and Spark effects (`FxKind.RiotBlock` comes with the look in step 5, with the protocol bump). Part of [`BossHuntsAndPerks.md`](BossHuntsAndPerks.md) (build steps 2 and 3, Warden only). Picks from
the mockups: the perk menu is **Look A** ([`Mockups/riot-gear-perk-menu.html`](Mockups/riot-gear-perk-menu.html), https://claude.ai/artifact/WrbA5TsYMgcn6tfe7Rd8ad)
and the shield effect is **Plate** ([`Mockups/riot-gear-effects.html`](Mockups/riot-gear-effects.html), https://claude.ai/artifact/241kgcxDtwMbJVEkRijuuG).

## The perk

**Riot Gear** (Warden, perk 1, unlocked by winning the Warden Hunt).

- You start every run with the shield ready.
- It blocks the next hit that would hurt you. The block gives 0.5 s of invulnerability (`RiotGraceT`).
- After a block it takes **30 s** (`RiotRecharge`) to come back. The timer starts at the block, keeps running between waves, and doesn't
  pause while you're downed. Reviving doesn't reset it.
- It blocks one hit from an enemy: a bullet, a melee swing, a blast. Burning, poison and hazards go through.
- Off in the Practice Range and the Boss Lab (like all perks).
- This replaces the doc's old "first hit of each wave" wording.

## What the code has now

- `Game/PerksUi.cs`: the lobby tab (`PerkTab = 5`), 32 lines, every slot a locked placeholder.
- Nothing else: no perk table, no unlock storage, no equip, no sync, no record variant. `Profile.Unlocked` (a `HashSet<string>`)
  already stores unlock ids, and `Hunts.cs` already records hunt wins (`Profile.HuntEnded`, `Profile.HuntsWon`).
- `Game.Damage(Player, amount, source)` in `Core/Combat.cs` is the one place damage to a player is applied, so the block goes there.
- Replicated player fields go through `Core/GameNet.cs` (e.g. `BarrierT`, line ~406). Lobby seat fields go through `LobbyEntry`
  (`Core/Session.cs`), written and read in the Hello/lobby messages.

## Design

### 1. Data: `Core/Perks.cs` (new, plain C#)

```
enum PerkId : byte { RiotGear = 1, ... None = 0 }   // new ids go at the end
class PerkDef { PerkId Id; EnemyType Boss; string Name, Desc, Tooltip; string UnlockId; }
static class Perks { All[]; Get(id); BossOf(id); Slots(Profile); Unlocked(Profile) }
```

- Only Riot Gear and Contraband go in the table now. Contraband has no effect yet and is shown locked, as in the mockup.
- **Unlocks** are derived from existing profile data, no new storage: perk 1 = `HuntsWon[boss] > 0`, perk 2 = the hunt won on Hard or
  higher, perk 3 = won with no perks. Perk 2 and 3 need two more profile counters (`HuntsWonHard`, `HuntsWonClean`) the first time they matter.
  For this job only perk 1 needs to work.
- **Slots**: 1 to start, 2 after the first full-run win (`RunsWon > 0`), 3 after wins in 5 different hunts (`HuntsWon.Count >= 5`).
- **Equipped perks** are saved in the profile file: one `perks` line (ids, comma separated), `Profile.FormatVersion` 3 to 4.
  Older files load with none equipped. A perk that's no longer unlocked or whose slot is gone is dropped on load.
- One perk per boss: equipping a second perk from the same boss swaps it.

### 2. Effect: `Core/RiotGear.cs` (new, partial `Game`)

- `Player` gets `PerkIds` (host + replicated, up to 3 bytes), `RiotT` (seconds left on the recharge; 0 = ready, replicated as a float) and
  `RiotFlash` (client-side, not replicated).
- In `Damage`, right after the early-outs (`DevGod`, swallowed, dome) and before any scaling or shields:
  ```
  if (HasPerk(p, PerkId.RiotGear) && p.RiotT <= 0f && source is an enemy hit) { RiotBlock(p, source); return; }
  ```
  `RiotBlock` sets `RiotT = RiotRecharge`, `InvulnT = max(InvulnT, RiotGraceT)`, and broadcasts a new `FxKind.RiotBlock` (impact direction in `Pos2`).
- Placing it first means a hit the Parry, Barrier or Fortress already stopped never reaches `Damage`, so it doesn't spend the shield.
  A hit blocked by the shield doesn't hit a Spirit Ward, a shield or the player's Hp.
- Damage-over-time sources (`burn`, `poison`, hazards) call `Damage` with their own source names. The source test is a small
  list of "doesn't count" names, kept next to the other source names. To confirm while building: list every `Damage(` call and its source string.
- The per-frame tick (`Game.cs` ~1151, next to the `InvulnT` countdown) counts `RiotT` down, and when it reaches 0 broadcasts a ping effect.
- Perks off: `PerkIds` is cleared at match start when `Practice` or Boss Lab is on.

### 3. Lobby and sync (protocol bump)

- `LobbyEntry.Perks` (`byte[3]` or three bytes) is chosen in the Perks tab, sent with the loadout and shown on the lobby seat
  (small gold icons, as on the mockup's party card). The host validates the ids against the player's reported rank and slots
  as it does for cosmetics (`Session.cs` ~597), and rejects ids that aren't real perks. Unlock state is client-reported, like Rank, so it is
  cosmetic trust only: perks never grant more than one slot's worth of power, and this is a co-op game.
- The match's `Player` snapshot carries `PerkIds` and `RiotT`.
- **`Session.ProtocolVersion` 151 to 152**, and the new `Msg`/`FxKind` values go at the end, before `None`.
- Saved runs keep each seat's perks by id name (`RunSave`, `RunSaves.cs`), like upgrades. They can't be changed when continuing.
  Old saves load with no perks.

### 4. UI

- **Perks tab (`PerksUi.cs`), Look A**: left column "Equipped (n of m slots)" with the three slots (filled, empty, locked with the unlock rule);
  right column the perk cards (boss name, name, one line, state: Equipped / Unlocked / "Win the X Hunt"). Clicking an unlocked card equips it
  in the first free slot; clicking an equipped card or the slot's "remove" takes it off. Hovering shows the tooltip card. Locked cards show only the unlock rule.
  The tab keeps its gold colour and still hides in the Practice Range.
- **HUD chip** (`Game/HudTiles.cs` or beside it): the Plate chip from the mockup, a 30 px tile that fills from the bottom up while recharging, with
  the seconds left and "Ready" when up. Drawn only when you have the perk.
- **In-arena look** (`Game/RiotGearLook.cs`, new): the plate arc at the player's radius, swung toward the nearest incoming bullet (client-side,
  from the bullets it already has); ghost plate while recharging; on `FxKind.RiotBlock` a white flash, sparks off the impact point and three plate
  pieces flying off; a pop when it returns. Reduced effects: the plate and the HUD chip only. Teammates see your plate (it comes from the replicated `RiotT`).

### 5. Records, tests, docs

- Run records note the perks used, and "best wave" / "wins" get a no-perks variant (from the doc). That is a second, separate change after the
  perk works; `Profile.RunEnded` already receives the seats' loadout, so it can take the perk list then. Not part of the first build.
- `Tools/SimTest`: a `perks` suite: block spends the shield and starts the 30 s; a second hit during the 0.5 s grace does nothing; a third hit after it
  lands; burn goes through; Parry-stopped hits don't spend it; recharge ends at 30 s of sim time; practice has no perks; a saved run keeps it;
  the profile line round-trips and drops a perk that is no longer unlocked; a snapshot round-trip keeps `RiotT`.
- README: a Perks section (Riot Gear's numbers). `CODEMAP.md` and `HANDOFF.md` §2: the new files. `Docs/BossHuntsAndPerks.md`: Riot Gear's wording
  changed, framework built. Bestiary and Armory are not touched.

## Build order

1. `Core/Perks.cs`, profile line, slots and unlock rules, plus tests for those.
2. `Core/RiotGear.cs`, the `Damage` hook, the tick, `FxKind.RiotBlock`, plus the `perks` suite.
3. Lobby sync (`LobbyEntry`, snapshot), protocol 152, saved runs.
4. `PerksUi.cs` (Look A) and the lobby seat icons.
5. `RiotGearLook.cs` and the HUD chip.
6. README, CODEMAP, HANDOFF, `AfterReset.md`. Run `CompileCheck`, `SimTest core` and `features` and the new `perks` suite.

Steps 1 to 3 are the framework and are testable without Unity. Steps 4 and 5 need a try in Unity.

## Open questions

- **Source test** (decided in step 2): every `Damage` call counts as a hit except the tick sources, which pass `overTime: true`: Burning
  Grounds, Spitter acid, the Crawler's grab, tear gas, the Colossus's lava, the Warden's gas, the Devourer's bile and stomach, Scorcher embers, and
  the Eclipse's darkness and burning edge. Boss slams, lasers, barrels and contact hits are blocked.
- **Downed and revived**: should reviving refill the shield? Leaning no, since the timer runs on.
- **Co-op unlocks**: each player brings their own perks (from the doc). A teammate without the Warden unlock can't equip it.
- **Contraband** and perk 2 and 3 unlocks wait for the first playtest of Riot Gear.
