# Enemy behaviour classes

**Status: step 0 built** (scaffolding, hooks, `aitrace`; no enemy moved yet). Owner's decisions: every enemy moves to brains (not just the kin ones), and drawing stays separate. Code today: `Core/HiveKin.cs` (the kin table), `Core/PrisonKin.cs` (`IsBrute`, `IsLunger`),
`Core/EnemyAI.cs` (`MoveEnemy`, `Special`, `EnemyFire`), each faction's `*Faction.cs`. New: `Core/Brain.cs`,
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
    public IMove Move;        // where it goes each tick (replaces its MoveEnemy case)
    public IFire Fire;        // what it shoots when FireT runs out (replaces its EnemyFire branch)
    public ISpecial Special;  // wind-ups, dashes, drops (replaces its Special branch)
    public IGuard Guard;      // blocks or bends a hit (shield front, Lens, Parrier bounce); null = none
    public BrainRole Roles;   // flags that answer "is it a Brute / a wall / a Lunger" for squads, bots and sounds
}

public interface IMove    { V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed); }
public interface IFire    { void Fire(Game g, Enemy e, V2 tpos, float aim); }
public interface ISpecial { void Tick(Game g, Enemy e, V2 tpos, float dt); }
public interface IGuard   { bool Blocks(Game g, Enemy e, V2 from); }   // BlocksShot's point the hit came from

[Flags] public enum BrainRole : ushort { None = 0, Brute = 1, Lunger = 2, Wall = 4, Swordsman = 8, Hook = 16 }  // (`Role` is the player roles)
```

- **Parts live inside `Game`:** each part is a class nested in `partial class Game`, so it can call Game's private
  helpers (`RiotShieldBlocks`, `EnemyShot`, ...).
- **Stateless, shared parts:** a part holds only its numbers and never per-enemy state, so one `LungeMove` instance
  serves every Shiv.
- **State stays on `Enemy`:** per-enemy state stays in the existing fields (`Phase`, `PhaseT`, `Facing`, `Strafe`, ...).
  Snapshots are unchanged, so there's no protocol bump.
- **Numbers on the part:** tuning moves into the part, so the Lunger family becomes one class with different numbers:

```csharp
static readonly LungeMove Lunge   = new LungeMove { Start = 170f, Speed = 640f };
static readonly LungeMove Fencer  = new LungeMove { Start = Game.FencerStart - 30f, Speed = 640f };
static readonly LungeMove Scythe  = new LungeMove { Start = 170f, Speed = Game.ScytheSpeed };
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
  - `BlocksShot`: just before the Riot Shield kin check, after the bosses, Lens and Grounder.
- **Helpers ask the brain first:** `IsBrute`, `IsLunger`, `IsWallKin`, `IsSwordsman` and `IsHook` keep their names.
  They check `brain.Roles` when there is a brain (`BrainHas`), and the old type list otherwise, so a brain must set
  every role its enemy has, so squads, bots, sounds and looks
  don't change.
- **The kin table stays** until every enemy that uses it has a brain (last step).

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
| 0 ✓ | `Brain`, the four interfaces, `BrainRole`, `BrainOf`, the dispatcher hooks, `aitrace` | none (empty table) |
| 1 | Shields | Riot Shield, Comb Shield, Slab Bearer, Parrier, Carcass Bearer |
| 2 | Lungers | Lunger, Shiv, Hornet, Driller, Fencer, Scythe Wraith (the Mummer keeps its masquerade as a `Special`) |
| 3 | Brutes and chasers | Brute, Thug, Bumble, Gravedigger, Swarmer, Wisp, Stomper, Gulper, Harlequin |
| 4 | Rioters and Guards | Rioter, Worker, Rubble, Gnawling, Guard, Soldier, Quarryman, Butcher, Blade Dancer |
| 5 | Lobbers | Gas Grenadier, Honey Lobber, Rock Hurler, Bile Spitter, Dusk Lobber, Marshal |
| 6 | Shooters | Taser Guard, Stinger, Geomancer, Cinder Priest, Maw Priest, Gallant; Security Guard, Needler, Second |
| 7 | Remove the kin table | `HiveKin`, `Enemy.Kin`, the old cases; `IsBrute` etc. read only `Roles` |
| 8 | The rest (in scope: owner's decision) | basic faction, Penumbral, Infall, Squall, Reaper cult, zombies, late-game enemies |

- **After step 7:** a faction enemy is one `BuildBrains` line plus its look.
- **Mixing parts:** an enemy can combine any parts, e.g. `Move = Lunge, Guard = Shield.Front` for a shield that
  leaps.

## Risks

- **Order in the old chains:** `EnemyFire` checks Court before the Gas Grenadier kin, so the Marshal gets its circle,
  not gas. A brain must fire where the old branch did. `aitrace` catches it if not.
- **Hidden type checks:** some base code asks for a kin's own type (`e.Type == EnemyType.Fencer`). Each step greps for
  its enemies' names and moves those checks into the part's numbers.
- **Speed:** one interface call per enemy per tick, a few hundred enemies at most. No allocations, since parts are
  static.
- **Parallel sessions:** faction work touches the same dispatchers. Keep each step small and merge it quickly.

## Decisions

- **Scope:** every non-boss enemy gets a brain (steps 0-8), so any enemy's parts can be reused in a new one.
- **Drawing:** stays separate; looks keep switching on `Type`.
