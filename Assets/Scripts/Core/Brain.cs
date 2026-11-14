using System;

namespace Arena
{
    /// Enemy behaviour classes (Docs/EnemyBehaviours.md): an enemy type points to a Brain made of parts, each holding its own
    /// numbers. A type with no brain runs the old code in EnemyAI.cs (the kin table and the type switches). Step 0 adds the
    /// parts, the registry and the hooks; the table is empty until step 1 moves the first family.
    public sealed class Brain
    {
        public MovePart Move;        // where it goes each tick and how it turns (in place of its MoveEnemy and Face cases)
        public FirePart Fire;        // what it shoots when FireT runs out (in place of its EnemyFire branch)
        public SpecialPart Special;  // wind-ups, dashes, drops (in place of its case in Special's type switch)
        public GuardPart Guard;      // a shield: what it blocks, how it wears, what a blocked shot does; null: nothing blocks
        public BrainRole Roles;      // answers "is it a Brute / a wall / a Lunger ..." for squads, bots, sounds and looks
    }

    /// A part is shared by every enemy that uses it, so it holds numbers only: per-enemy state stays on Enemy (Phase, PhaseT,
    /// Facing, Strafe, ...), which keeps snapshots unchanged. Parts are nested in Game (partial) so they reach its helpers.
    public abstract class MovePart
    {
        public abstract V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed);
        /// Its own turning toward `want` (radians); false: turn like everyone else (face the target at once).
        public virtual bool Turn(Game g, Enemy e, float want, float dt) => false;
        /// Damage when it touches you right now; below 0: the usual contact damage.
        public virtual float Contact(Game g, Enemy e) => -1f;
    }

    public abstract class FirePart
    {
        public abstract void Fire(Game g, Enemy e, V2 tpos, float aim);
        /// Once a tick while its fire timer runs (after Winding is set), e.g. a lobber locking its landing spot.
        public virtual void Wind(Game g, Enemy e, V2 tpos, float rateNow) { }
    }

    public abstract class SpecialPart
    {
        public abstract void Tick(Game g, Enemy e, V2 tpos, float dt);
    }

    public abstract class GuardPart
    {
        /// Does it stop a hit coming from `from` (BlocksShot)?
        public abstract bool Blocks(Game g, Enemy e, V2 from);
        /// At spawn: give it its shield.
        public virtual void Setup(Game g, Enemy e) { }
        /// A blocked hit (a shot, a swing, a blast) wears the shield (WearShield).
        public virtual void Wear(Game g, Enemy e, float amount) { }
        /// A player's bullet it blocked: by default the shield takes its damage.
        public virtual void Shot(Game g, Enemy e, Bullet b) => Wear(g, e, b.Dmg);
    }

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
        static Brain[] brains;  // built on first use, after every static field (the parts read colours and numbers from them)
        static Brain[] Brains => brains ?? (brains = BuildBrains());

        /// The brain of this enemy type; null while it still runs the old code.
        public static Brain BrainOf(EnemyType t) => Brains[(byte)t];

        /// A role check for the Is* helpers: true or false when the type has a brain, null when it still uses the old lists.
        internal static bool? BrainHas(EnemyType t, BrainRole r)
        {
            var b = Brains[(byte)t];
            return b == null ? (bool?)null : (b.Roles & r) != 0;
        }

        static Brain[] BuildBrains()
        {
            var t = new Brain[256];
            // Each family adds its enemies (Docs/EnemyBehaviours.md "Steps").
            ShieldBrains(t);  // step 1 (ShieldBrains.cs)
            LungeBrains(t);   // step 2 (LungeBrains.cs)
            ChaseBrains(t);   // step 3 (ChaseBrains.cs)
            RiotBrains(t);    // step 4 (RiotBrains.cs)
            LobBrains(t);     // step 5 (LobBrains.cs)
            return t;
        }
    }
}
