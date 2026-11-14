# Enemy behaviour classes

**Status: steps 0-7 built, step 8 in progress** (8a: the basic faction). Owner's decisions: every enemy moves to brains (not just the kin ones), and drawing stays separate. Code today: `Core/HiveKin.cs` (the kin table), `Core/PrisonKin.cs` (`IsBrute`, `IsLunger`),
`Core/EnemyAI.cs` (`MoveEnemy`, `Special`, `EnemyFire`), each faction's `*Faction.cs`. New: `Core/Brain.cs`, `Core/ShieldBrains.cs`, `Core/LungeBrains.cs`, `Core/ChaseBrains.cs`, `Core/RiotBrains.cs`, `Core/LobBrains.cs`, `Core/ShootBrains.cs`,
`Tools/SimTest/Tests/AiTrace.cs`, `Tools/SimTest/Golden/aitrace.txt`.

## Problem

- Enemy behaviour lives in `switch`/`if` chains keyed on `EnemyType`: about 370 `case EnemyType.` lines, with the main
  ones in `EnemyAI.cs` `MoveEnemy`, `Special` and `EnemyFire`.
- Faction enemies reuse a Warden enemy's AI through the kin table (`HiveKin.Of`, `Enemy.Kin`: 39 uses in 15 files) and
  helpers like `IsBrute`, `IsLunger`, `IsWallKin`, `IsSwordsman` and `IsHook`.
- **One base only:** an enemy is either a Riot Shield or a Lunger, never a Lunger's leap with a Riot Shield's block.
- **Tuning by type check:** the base's code checks for its kin, e.g. the Lunger case asks `e.Type == EnemyType.Fencer`
  for its start distance and `ScytheWraith` for its speed. Each new kin adds more of these checks.
- **Order matters:** `EnemyFire` is a chain of about 15 `if (XFire(...)) return;` calls. Where a new enemy goes in that
  chain decides what it does.

**Why some factions use kin:** the Hive (protocol 316) was designed with one enemy for each Warden role, so its
ten-wave cycle could teach in the Warden chart's order (`Docs/HiveFaction.md`). Copying each role's AI through a
lookup table was the shortest way to build that. The Quarry, Larder and Court followed the same "mirror a Warden
enemy" design (`Docs/Approaches.md`). The Eclipse, Singularity, Storm and Reaper factions were designed around new
mechanics instead, so most of their enemies got their own code. The owner never asked for kin; brains replace it.

## Goal

- Each enemy type points to a **brain**: a small set of behaviour parts, each with its own numbers.
- A new enemy is a registry entry that combines existing parts and adds new ones only for what's actually new.
- No change to how the game plays, and no network change: each step must produce the same simulation.

**Not in scope:** the looks (`Game/*Look.cs` keep switching on `Type`; owner's decision), bosses, and the snapshot format.

## Design

### Parts

```csharp
// Core/Brain.cs
public sealed class Brain
{
    public MovePart Move;        // where it goes each tick and how it turns (replaces its MoveEnemy and Face cases)
    public FirePart Fire;        // what it shoots when FireT runs out (replaces its EnemyFire branch)
    public SpecialPart Special;  // wind-ups, dashes, drops (replaces its Special branch)
    public GuardPart Guard;      // a shield: what it blocks, how it wears, what a blocked shot does; null = none
    public BrainRole Roles;      // flags that answer "is it a Brute / a wall / a Lunger" for squads, bots and sounds
}

// Base classes, not interfaces, so a part only overrides what it changes (C# 7.2 has no default interface methods).
public abstract class MovePart    { public abstract V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed);
                                    public virtual bool Turn(Game g, Enemy e, float want, V2 tpos, float dt) => false;    // false: face the target at once
                                    public virtual float Contact(Game g, Enemy e) => -1f; }                       // touch damage now; < 0: the usual
public abstract class FirePart    { public abstract void Fire(Game g, Enemy e, V2 tpos, float aim);
                                    public virtual void Wind(Game g, Enemy e, V2 tpos, float rateNow) { } }       // each tick of its fire timer
public abstract class SpecialPart { public abstract void Tick(Game g, Enemy e, V2 tpos, float dt); }
public abstract class GuardPart   { public abstract bool Blocks(Game g, Enemy e, V2 from);      // BlocksShot
                                    public virtual void Setup(Game g, Enemy e) { }                // at spawn: its shield
                                    public virtual void Wear(Game g, Enemy e, float amount) { }   // WearShield (shots, swings, blasts)
                                    public virtual void Shot(Game g, Enemy e, Bullet b) => Wear(g, e, b.Dmg); }  // a player's bullet it blocked

[Flags] public enum BrainRole : ushort { None = 0, Brute = 1, Lunger = 2, Wall = 4, Swordsman = 8, Hook = 16, Security = 32, Lobber = 64, Charge = 128 }  // (`Role` is the player roles)
```

- **Parts live inside `Game`:** each part is a class nested in `partial class Game`, so it can call Game's private
  helpers (`RiotShieldBlocks`, `EnemyShot`, ...).
- **Stateless, shared parts:** a part holds only its numbers and never per-enemy state, so one `LungeMove` instance
  serves every Shiv.
- **State stays on `Enemy`:** per-enemy state stays in the existing fields (`Phase`, `PhaseT`, `Facing`, `Strafe`, ...).
  Snapshots are unchanged, so there's no protocol bump.
- **Numbers on the part:** tuning moves into the part, so the Lunger family becomes one class with different numbers:

```csharp
// LungeBrains.cs (step 2)
var plain  = new LungeNumbers();   // Start 220, Circle 170, WindUp 0.6, Dash 0.32, Speed 640, Recover 0.8, Damage 26
var fencer = new LungeNumbers { Start = FencerStart, Circle = FencerStart - 30f, WindUp = FencerWarn, Dash = FencerHop, ... };
var scythe = new LungeNumbers { Start = ScytheStart, WindUp = ScytheMark, Track = ScytheTrack, Speed = ScytheSpeed, ... };
```

### Registry

```csharp
// Core/Brain.cs, in partial class Game: one line per migrated enemy, indexed like the kin table (byte -> Brain)
public static Brain BrainOf(EnemyType t) => brains[(byte)t];   // null: not migrated yet, use the old code

static Brain[] BuildBrains()
{
    var t = new Brain[256];
    t[(int)EnemyType.RiotShield] = new Brain { Move = ShieldWalk, Guard = ShieldFront, Roles = BrainRole.Wall };
    t[(int)EnemyType.CombShield] = new Brain { Move = ShieldWalk, Guard = CombFront,   Roles = BrainRole.Wall };
    t[(int)EnemyType.SlabBearer] = new Brain { Move = ShieldWalk, Guard = ShieldFront, Special = DropSlab, Roles = BrainRole.Wall };
    // ...
    return t;
}
```

### Hooking it in (old and new side by side)

- **Each dispatcher asks the brain first:** `MoveEnemy`, `Special`, `EnemyFire` and the hit check (`BlocksShot` /
  `Collide`) check the brain before running their old code:

```csharp
var brain = BrainOf(e.Type);
if (brain?.Move != null) v = brain.Move.Move(this, e, tpos, dir, dist, dt, ref speed);
else switch (e.Kin) { ... }   // the old code, shrinking each step
```

- **Where each hook sits (built in step 0):**
  - `MoveEnemy`: in place of the `switch (e.Kin)`.
  - `Special`: in place of its final `switch (e.Kin)`. The faction calls before it (Stomper, Larder, Court) stay
    until their family moves.
  - `EnemyFire`: after the Challenges, Riot Captain, late-game, zombie and melee early-outs, before `VarietyFire`.
    Every kin enemy's old branch comes after that point, so order is kept.
  - `BlocksShot`: after the bosses, Lens and Grounder (where the Riot Shield kin check was).
  - Added in step 1: `Face` (`Move.Turn`, where the Riot Shield kin check was), `WearShield` (`Guard.Wear`, first),
    `SpawnEnemyAt` (`Guard.Setup`), and Collide's blocked bullet in Combat.cs (`Guard.Shot`, in place of the
    Parrier bounce, the wear and the Comb leak).
  - `MoveEnemy`'s brain path skips the type switch **and** the per-faction overrides after it (WardenFactionMove,
    ZombieMove, ...); the Rooms detour and `SquadMove` still apply to everyone.
- **Helpers ask the brain first:** `IsBrute`, `IsLunger`, `IsWallKin`, `IsSwordsman` and `IsHook` keep their names.
  They check `brain.Roles` when there is a brain (`BrainHas`), and the old type list otherwise, so a brain must set
  every role its enemy has, so squads, bots, sounds and looks
  don't change.
- **The kin table stays** until every enemy that uses it has a brain (step 7). A family's old kin branches are removed
  in its own step once nothing reaches them.
- **Looks keep reading `Kin`:** `Game/WorldEnemies.cs` and `LungeLaneLook.cs` switch on `e.Kin` to pick a drawing, so
  step 7 turns those into `Type` lists (drawing stays separate: owner's decision).

### Checking each step: `aitrace` (built)

- **Test suite `aitrace`:** `Tools/SimTest/run.sh 1 1 aitrace` (about 6 s; also in CI's extras).
- **What it runs:** every non-boss enemy type (128 today), each alone on its own seed for 12 s, against a god-mode host
  who stands still and shoots at it.
- **What it records:** each frame, every enemy it brought (type, position, Phase, Facing, health) and every bullet
  (kind, position), hashed into one line per type with its end position, damage taken and shots fired.
- **Pass:** every line matches `Tools/SimTest/Golden/aitrace.txt`. A failure names the types that changed.
  `AITRACE=update` rewrites the file, only when a change is meant to alter behaviour.
- **Checked in step 0:** the code before the hooks gives the same file, and a dummy brain on the Shiv fails only the
  Shiv's line.
- **Suites too:** the existing suites (`core`, `features` and the faction suites) still run before each commit.

## Steps

Each step moves one family. Each step has to leave `aitrace` IDENTICAL and needs no protocol bump.

| Step | What | Enemies |
|---|---|---|
| 0 ✓ | `Brain`, the four part classes, `BrainRole`, `BrainOf`, the dispatcher hooks, `aitrace` | none (empty table) |
| 1 ✓ | Shields | Riot Shield, Comb Shield, Slab Bearer, Parrier, Carcass Bearer |
| 2 ✓ | Lungers | Lunger, Shiv, Hornet, Driller, Fencer, Scythe Wraith, Mummer (its masquerade still runs first) |
| 3 ✓ | Brutes and chasers | Brute, Thug, Bumble, Gravedigger, Swarmer, Wisp, Stomper, Gulper, Harlequin |
| 4 ✓ | Rioters and Guards | Rioter, Worker, Rubble, Gnawling, Guard, Soldier, Quarryman, Butcher, Blade Dancer |
| 5 ✓ | Lobbers | Gas Grenadier, Honey Lobber, Rock Hurler, Bile Spitter, Dusk Lobber, Marshal |
| 6 ✓ | Shooters | Taser Guard, Stinger, Geomancer, Cinder Priest, Maw Priest, Gallant; Security Guard, Needler, Second |
| 7 ✓ | Remove the kin table | `HiveKin`, `Enemy.Kin`, the old cases; `IsBrute` etc. read only `Roles` |
| 8 | The rest (in scope: owner's decision) | basic faction, Penumbral, Infall, Squall, Reaper cult, zombies, late-game enemies |

- **After step 7:** a faction enemy is one `BuildBrains` line plus its look.
- **Mixing parts:** an enemy can combine any parts, e.g. `Move = Lunge, Guard = new ShieldGuard()` for a shield that
  leaps.

## Step 1: shields (built)

- **File:** `Core/ShieldBrains.cs`. All five share `ShieldMove` (herd, bash, lunge, stagger; slow turn) and
  `ShieldSpecial` (`RiotShieldSpecial`), with `BrainRole.Wall`.
- **Guards:** `ShieldGuard` (the Riot Shield's front, cracks, shatter), and subclasses for what's different:
  - `CombGuard`: wears, then lets `CombLeak` of the shot through (more per crack). The Comb Shield's slow crumble is
    kept exactly.
  - `SlabGuard` / `CarcassGuard`: `Shattered` drops the slab (cover) or the meat (a morsel).
  - `ParrierGuard`: sends the shot back (`ParrierBounce`), then wears.
- **Removed (unreachable now):** the Riot Shield kin branches in `MoveEnemy`/`WardenFactionMove`, `Face`,
  `WearShield`, `BlocksShot`, the spawn's shield setup and `Special`'s switch, the Slab/Carcass type checks in
  `WearRiotShield`, the Comb check in Combat.cs, and the Parrier type check in `ParrierBounce`.
- **Kin checks turned into role checks:** `RiotShieldBroken`, the bot's lunge dodge (BotBrain.cs) and Juice.cs now
  ask `IsWallKin` (public now).
- **Checked:** `aitrace` identical for all 128 types. In the trace all five shatter, the Comb Shield takes more
  damage than the Riot Shield (the leak), and the Parrier returns 13 shots.

## Step 2: lungers (built)

- **File:** `Core/LungeBrains.cs`. One `LungeMove` and one `LungeSpecial` class, each lunger with a `LungeNumbers`:
  start distance, circling distance, wind-up, tracking time, dash time, leap speed, recovery and leap damage.
  - Plain numbers: Lunger, Shiv, Hornet, Driller, Mummer.
  - Fencer: starts closer, shorter wind-up, a short hop, its own hop damage.
  - Scythe Wraith: starts further out, a longer mark that stops tracking after `ScytheTrack`, a faster, longer dash.
- **New hook:** `MovePart.Contact` (touch damage during the leap), read in the contact check where the lunger rule
  was.
- **Removed (unreachable now):** the Lunger/Shiv/Scythe Wraith cases in `MoveEnemy` and `Special`, the Fencer and
  Scythe Wraith type checks inside them, the lunger and Scythe Wraith face locks in `Face` (now `LungeMove.Turn`), and
  the lunger rule in the contact damage.
- **Left for step 7:** the sound and thrall switches on kin (MeleeSounds.cs, ForcePact.cs) still list Lunger, Shiv
  and Scythe Wraith; they keep working through the kin table until then.
- **Checked:** `aitrace` identical for all 128 types.

## Step 3: brutes and chasers (built)

- **File:** `Core/ChaseBrains.cs`. All nine share `ChaseMove` (straight at you).
  - `BrainRole.Brute` (so they enrage as they're shot): Brute, Thug, Bumble, Gravedigger, Stomper, Gulper, Harlequin.
  - No role: Swarmer, Wisp.
  - Specials: Stomper (`StompSpecial`: its stomp ring), Gulper (`GulpSpecial`: its breath), Harlequin
    (`CrossingSpecial`: its crossing).
- **Removed (unreachable now):** the chaser case in `MoveEnemy`, and the Harlequin and Stomper calls before
  `Special`'s switch. The Larder call there now covers only the Butcher (step 4).
- **Left for step 7:** the sound and thrall switches (MeleeSounds.cs, ForcePact.cs) still list brutes and chasers by
  kin.
- **Checked:** `aitrace` identical for all 128 types.

## Step 4: Rioters and Guards (built)

- **File:** `Core/RiotBrains.cs`.
  - `RioterMovePart` (`RioterMove`): Rioter, Worker, Rubble, Gnawling (+ `EatSpecial`: eats a morsel).
  - `GuardMovePart` (`GuardMove`): Guard, Quarryman, Soldier (its charge: the part's `charges` flag), Butcher
    (+ `ChopSpecial`), Blade Dancer (+ `DanceSpecial`: its lunge and blades).
- **Removed (unreachable now):** the Rioter and Guard cases in `MoveEnemy` (with the Soldier type check), and the
  Blade Dancer, Butcher and Gnawling calls before `Special`'s switch.
- **Kept where it was:** the Worker's crowd speed-up (`WorkerPackSpeed`) runs after `SquadMove` for everyone, so
  moving it would reorder a float multiply. It stays a type check there.
- **aitrace fix:** a run could fail to open its port (still held by an earlier run), leaving that enemy's line as
  `no-fight`. It now takes the next port.
- **Checked:** `aitrace` identical for all 128 types, twice.

## Step 5: lobbers (built)

- **File:** `Core/LobBrains.cs`. All six share `LobMove` (keep `GrenadierRange` away, plant while winding up).
- **`LobFire`** holds what each one throws: ring size, colour, whether it leads you, and the patch it leaves (kind,
  life, radius). Gas Grenadier (gas), Honey Lobber (honey), Rock Hurler (leads; rubble pile), Bile Spitter (leads;
  acid), Dusk Lobber (dark). `GrenadierWind`/`GrenadierFire` take the part instead of checking the type.
- **`CircleFire`**: the Marshal's dueling circle (`MarshalCircle`, split out of `CourtFire`).
- **New hook:** `FirePart.Wind`, each tick of the fire timer (where the Gas Grenadier kin's `GrenadierWind` call was).
- **Registry built lazily:** `BrainOf` builds the table on first use, since parts now copy static colours
  (`GasLobColor`, `HurlerColor`, ...) that a field initializer in another file might not have set yet.
- **Removed (unreachable now):** `LobRadius`, `LobColor`, the type checks in `LobSpot` and the landing, the Gas
  Grenadier kin branches in `WardenFactionMove`, `EnemyFire` and the wind-up, and the Marshal case in `CourtFire`.
- **Left for step 7:** `WindUpOf` (Readability.cs) still reads the kin for the Gas Grenadier's glow timing, and so
  does `VarietyLook.cs` (drawing).
- **Checked:** `aitrace` identical for all 128 types.

## Step 6: shooters (built)

- **File:** `Core/ShootBrains.cs`.
  - `PlantMove(range)`: keep `range` away, strafe, plant while winding up. The lobbers use it at `GrenadierRange`
    (LobMove is gone); the Taser Guard's kind at `TaserRange`. The Maw Priest's copy also turns slowly (`PriestFace`).
  - `SecurityMovePart(strafe, aimStrafe)`: `SecurityMove` plus `SecurityFace` in `Turn`. The Needler's harder strafe is
    now its numbers (the type check in `SecurityMove` is gone).
  - Fire parts: `BoltFire` (Taser Guard: `TaserBolt`; Stinger: `StingBolt`), `CrackFire` (Geomancer), `MagmaFire`
    (Cinder Priest), `MawFire` (Maw Priest), `PistolFire` (Security Guard, Needler), `DaggerFire` (Second),
    `HoldFire` (Gallant, with `SaluteSpecial`).
- **`Turn` gained `tpos`** (the Security Guard turns toward the target, not an angle).
- **Removed (unreachable now):** `WardenFactionMove`, `QuarryFire`, `LarderFire` and `CourtFire` (split into
  `GeomancerCrack`, `CinderBolt`, `MawFan`, `SecondFanFire`), the Taser and Security kin branches in `EnemyFire`, the
  Maw Priest and Security faces in `Face`, the Gallant call before `Special`'s switch, and the Stinger check in
  `TaserFire`.
- **Left for step 7 (queries on the Security kin):** the magazine at spawn, the glow and countdown in the fire timer,
  the strafe-flip skip, `TakesTurns` and the look timing in Game.cs; and `WindUpOf` for the Taser and Security kin.
- **Checked:** `aitrace` identical for all 128 types.

## Step 7: the kin table is gone (built)

- **Removed:** `HiveKin.Of`, its table and `Enemy.Kin`. `HiveKin.Is` stays (it only asks "is it a Hive bee?", like
  `QuarryKin.Is`, `LarderKin.Is`, `CourtKin.Is`).
- **AI checks now read the brain:**
  - New roles `Security`, `Lobber` and `Charge` (the Taser Guard's kind).
  - `IsSecurity` replaces the Security kin checks: the magazine at spawn, the fire countdown and glow, the
    strafe-flip skip, `TakesTurns`, the look timing in Game.cs.
  - `FirePart.WindUp` (set on every lobber, Taser and Security fire part) replaces the kin checks in `WindUpOf`.
  - `IsBrute`, `IsLunger` and `IsWallKin` read only the roles. `MoveEnemy`'s and `Special`'s switches are on
    `e.Type`.
- **Borrowed sounds, looks and thrall attacks:** `Game.SharesWith(type)` answers by role:
  - brute → Brute, lunger → Lunger (the Scythe Wraith keeps its own), shield bearer → Riot Shield;
  - the Taser, Security and lobber kinds → Taser Guard, Security Guard, Gas Grenadier; anything else → itself.
  - Used by MeleeSounds.cs, ForcePact.cs, and the looks (WorldEnemies.cs, LungeLaneLook.cs, PacmanLook.cs,
    ShooterAimLook.cs, VarietyLook.cs). Drawing stays separate (owner's decision); it just asks this instead.
- **Tests:** the Hive, Quarry, Larder, Court and Penumbral suites checked the table. They now check each faction
  enemy moves with the same kind of part as its Warden counterpart (`SameMove`, AiTrace.cs).
- **Checked:** `aitrace` identical for all 128 types; `RenderCheck` IDENTICAL to the step 6 commit.

## Step 8: every other enemy (in progress)

Done one family at a time (8a, 8b, ...). Each enemy's code moves out of `EnemyAI.cs`'s type switches unchanged into
its own method; its parts call that method. Generic parts: `CodeFire` and `CodeSpecial` (BasicBrains.cs) run a given
method, so a new enemy can reuse any of them.

- **8a, the basic faction (`Core/BasicBrains.cs`):**
  - `KeepMove(range, strafe)`: Grunt, Sniper, Burster, Mortar, Shielder (turns slowly), Medic, Splitter.
  - Own moves: `ShardMove` (the Hive Queen's drones), `ChargerMove` (dash, locked turn, dash damage),
    `SpinnerMove`, `SpikerMovePart`, `SawMove` (Buzzsaw), `SpikeBlockMove` (locked lunge, lunge damage).
  - `ShielderGuard`: the Shielder's shield (block, wear, setup) out of `BlocksShot`, `WearShield` and the spawn.
  - Fire and specials: `GruntFire`, `BursterFire`, ... `MedicHeal`, `ChargerDash`, `ShielderDecay`, moved verbatim.
  - `aitrace` identical.

## Risks

- **Order in the old chains:** `EnemyFire` checks Court before the Gas Grenadier kin, so the Marshal gets its circle,
  not gas. A brain must fire where the old branch did. `aitrace` catches it if not.
- **Hidden type checks:** some base code asks for a kin's own type (`e.Type == EnemyType.Fencer`). Each step greps for
  its enemies' names and moves those checks into the part's numbers.
- **Speed:** one virtual call per enemy per tick, a few hundred enemies at most. No allocations, since parts are
  static.
- **Parallel sessions:** faction work touches the same dispatchers. Keep each step small and merge it quickly.

## Decisions

- **Scope:** every non-boss enemy gets a brain (steps 0-8), so any enemy's parts can be reused in a new one.
- **Drawing:** stays separate; looks keep switching on `Type`.
