# Enemy behaviour classes

**Status: planned, not started.** Owner's decisions: every enemy moves to brains (not just the kin ones), and drawing stays separate. Code today: `Core/HiveKin.cs` (the kin table), `Core/PrisonKin.cs` (`IsBrute`, `IsLunger`),
`Core/EnemyAI.cs` (`MoveEnemy`, `Special`, `EnemyFire`), each faction's `*Faction.cs`.

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
// Core/Brains/Brain.cs
public sealed class Brain
{
    public IMove Move;        // where it goes each tick (replaces its MoveEnemy case)
    public IFire Fire;        // what it shoots when FireT runs out (replaces its EnemyFire branch)
    public ISpecial Special;  // wind-ups, dashes, drops (replaces its Special branch)
    public IGuard Guard;      // blocks or bends a hit (shield front, Lens, Parrier bounce); null = none
    public Role Roles;        // flags that answer "is it a Brute / a wall / a Lunger" for squads, bots and sounds
}

public interface IMove    { V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed); }
public interface IFire    { void Fire(Game g, Enemy e, V2 tpos, float aim); }
public interface ISpecial { void Tick(Game g, Enemy e, V2 tpos, float dt); }
public interface IGuard   { bool Blocks(Game g, Enemy e, Bullet b); }

[Flags] public enum Role : ushort { None = 0, Brute = 1, Lunger = 2, Wall = 4, Swordsman = 8, Hook = 16, Lobber = 32, ... }
```

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
// Core/Brains/Brains.cs: one line per migrated enemy, indexed like the kin table (byte -> Brain)
public static class Brains
{
    static readonly Brain[] table = new Brain[256];
    public static Brain For(EnemyType t) => table[(byte)t];   // null: not migrated yet, use the old code

    static Brains()
    {
        Set(EnemyType.RiotShield,    new Brain { Move = Shield.Walk, Guard = Shield.Front,  Roles = Role.Wall });
        Set(EnemyType.CombShield,    new Brain { Move = Shield.Walk, Guard = Shield.Comb,   Roles = Role.Wall });
        Set(EnemyType.SlabBearer,    new Brain { Move = Shield.Walk, Guard = Shield.Front,  Special = Quarry.DropSlab, Roles = Role.Wall });
        // ...
    }
}
```

### Hooking it in (old and new side by side)

- **Each dispatcher asks the brain first:** `MoveEnemy`, `Special`, `EnemyFire` and the hit check (`BlocksShot` /
  `Collide`) check the brain before running their old code:

```csharp
var brain = Brains.For(e.Type);
if (brain?.Move != null) v = brain.Move.Move(this, e, tpos, dir, dist, dt, ref speed);
else switch (e.Kin) { ... }   // the old code, shrinking each step
```

- **Brains go where the old branch was:** in `EnemyFire` and `Special`, the brain check sits where the enemy's old
  branch was, so nothing changes order until the old chain is gone.
- **Helpers ask the brain first:** `IsBrute`, `IsLunger`, `IsWallKin`, `IsSwordsman` and `IsHook` keep their names.
  They check `brain.Roles` when there is a brain, and the old type list otherwise, so squads, bots, sounds and looks
  don't change.
- **The kin table stays** until every enemy that uses it has a brain (last step).

### Checking each step: `aitrace`

- **New report `aitrace`:** `Tools/SimTest/run.sh 1 1 aitrace [git-ref]`, the simulation's version of RenderCheck.
- **What it runs:** with a fixed seed, it plays every non-boss enemy type alone against a bot for 20 s, plus one mixed
  wave per faction.
- **What it records:** every enemy's position, Phase, hp, and every bullet it spawns, each frame, then hashes it per
  type.
- **Pass:** compared against the given commit, it must print `IDENTICAL` for every type a step moves.
- **Suites too:** the existing suites (`core`, `features` and the faction suites) still run before each commit.

## Steps

Each step moves one family. Each step has to leave `aitrace` IDENTICAL and needs no protocol bump.

| Step | What | Enemies |
|---|---|---|
| 0 | `Brain`, the four interfaces, `Role`, `Brains`, the dispatcher hooks, `aitrace` | none (empty table) |
| 1 | Shields | Riot Shield, Comb Shield, Slab Bearer, Parrier, Carcass Bearer |
| 2 | Lungers | Lunger, Shiv, Hornet, Driller, Fencer, Scythe Wraith (the Mummer keeps its masquerade as a `Special`) |
| 3 | Brutes and chasers | Brute, Thug, Bumble, Gravedigger, Swarmer, Wisp, Stomper, Gulper, Harlequin |
| 4 | Rioters and Guards | Rioter, Worker, Rubble, Gnawling, Guard, Soldier, Quarryman, Butcher, Blade Dancer |
| 5 | Lobbers | Gas Grenadier, Honey Lobber, Rock Hurler, Bile Spitter, Dusk Lobber, Marshal |
| 6 | Shooters | Taser Guard, Stinger, Geomancer, Cinder Priest, Maw Priest, Gallant; Security Guard, Needler, Second |
| 7 | Remove the kin table | `HiveKin`, `Enemy.Kin`, the old cases; `IsBrute` etc. read only `Roles` |
| 8 | The rest (in scope: owner's decision) | basic faction, Penumbral, Infall, Squall, Reaper cult, zombies, late-game enemies |

- **After step 7:** a faction enemy is one `Brains` line plus its look.
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
