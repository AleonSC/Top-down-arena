using System;

namespace Arena
{
    /// Enemy behaviour classes (Docs/EnemyBehaviours.md): an enemy type points to a Brain made of parts, each holding its own
    /// numbers. A type with no brain runs the old code in EnemyAI.cs (the kin table and the type switches). Step 0 adds the
    /// parts, the registry and the hooks; the table is empty until step 1 moves the first family.
    public sealed class Brain
    {
        public IMove Move;        // where it goes each tick (in place of its MoveEnemy case)
        public IFire Fire;        // what it shoots when FireT runs out (in place of its EnemyFire branch)
        public ISpecial Special;  // wind-ups, dashes, drops (in place of its case in Special's type switch)
        public IGuard Guard;      // blocks a hit from a point (a shield's front); null: nothing blocks
        public BrainRole Roles;   // answers "is it a Brute / a wall / a Lunger ..." for squads, bots, sounds and looks
    }

    /// A part is shared by every enemy that uses it, so it holds numbers only: per-enemy state stays on Enemy (Phase, PhaseT,
    /// Facing, Strafe, ...), which keeps snapshots unchanged. Parts are nested in Game (partial) so they reach its helpers.
    public interface IMove { V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed); }
    public interface IFire { void Fire(Game g, Enemy e, V2 tpos, float aim); }
    public interface ISpecial { void Tick(Game g, Enemy e, V2 tpos, float dt); }
    public interface IGuard { bool Blocks(Game g, Enemy e, V2 from); }

    /// What an enemy counts as wherever the code asks (IsBrute, IsLunger, IsWallKin, IsSwordsman, IsHook). A brain sets every
    /// role its enemy has: with a brain, those checks read only Roles.
    [Flags]
    public enum BrainRole : ushort
    {
        None = 0,
        Brute = 1,
        Lunger = 2,
        Wall = 4,       // a Riot Shield or its kin (the Riot line, squads' front rank)
        Swordsman = 8,
        Hook = 16,
    }

    public partial class Game
    {
        static readonly Brain[] brains = BuildBrains();

        /// The brain of this enemy type; null while it still runs the old code.
        public static Brain BrainOf(EnemyType t) => brains[(byte)t];

        /// A role check for the Is* helpers: true or false when the type has a brain, null when it still uses the old lists.
        internal static bool? BrainHas(EnemyType t, BrainRole r)
        {
            var b = brains[(byte)t];
            return b == null ? (bool?)null : (b.Roles & r) != 0;
        }

        static Brain[] BuildBrains()
        {
            var t = new Brain[256];
            // One line per migrated enemy (Docs/EnemyBehaviours.md "Steps"), e.g.
            // t[(int)EnemyType.RiotShield] = new Brain { Move = ..., Guard = ..., Roles = BrainRole.Wall };
            // Empty until step 1.
            return t;
        }
    }
}
