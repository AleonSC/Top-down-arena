using System;

namespace Arena
{
    /// Enemy behaviour classes (Docs/EnemyBehaviours.md): an enemy type points to a Brain made of parts, each holding its own
    /// numbers. A type with no brain still runs its own case in EnemyAI.cs's type switches (until step 8 gives it one).
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
        public virtual bool Turn(Game g, Enemy e, float want, V2 tpos, float dt) => false;
        /// Damage when it touches you right now; below 0: the usual contact damage.
        public virtual float Contact(Game g, Enemy e) => -1f;
    }

    public abstract class FirePart
    {
        /// How long it glows and shows its aim before it fires (WindUpOf, Readability.cs); below 0: the usual.
        public float WindUp = -1f;
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
        Security = 32,  // a Security Guard's kind: its own magazine, aim and strafe timer, no shooting turns (SecurityGuard.cs)
        Lobber = 64,    // a Gas Grenadier's kind (the Marshal too): keeps its range, winds up an overhand lob
        Charge = 128,   // a Taser Guard's kind: plants its feet and charges one shot along an aim line
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

        public static bool IsSecurity(EnemyType t) => BrainHas(t, BrainRole.Security) ?? false;

        /// This type's fire part's wind-up, or below 0 for the usual (Readability.cs).
        static float BrainWindUp(EnemyType t)
        {
            var b = Brains[(byte)t];
            return b != null && b.Fire != null ? b.Fire.WindUp : -1f;
        }

        /// For the sounds, looks and thrall attacks one enemy borrows from another by role: a brute sounds and draws like the
        /// Brute, a lunger like the Lunger (a Scythe Wraith keeps its own), a shield bearer like the Riot Shield, and the
        /// Taser Guard's, Security Guard's and Gas Grenadier's kinds like those. Anything else is itself.
        public static EnemyType SharesWith(EnemyType t)
        {
            var b = Brains[(byte)t];
            if (b == null || t == EnemyType.ScytheWraith)
                return t;
            var r = b.Roles;
            return (r & BrainRole.Brute) != 0 ? EnemyType.Brute : (r & BrainRole.Lunger) != 0 ? EnemyType.Lunger : (r & BrainRole.Wall) != 0 ? EnemyType.RiotShield
                : (r & BrainRole.Charge) != 0 ? EnemyType.TaserGuard : (r & BrainRole.Security) != 0 ? EnemyType.SecurityGuard : (r & BrainRole.Lobber) != 0 ? EnemyType.GasGrenadier : t;
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
            ShootBrains(t);   // step 6 (ShootBrains.cs)
            BasicBrains(t);   // step 8a (BasicBrains.cs)
            VarietyBrains(t); // step 8b (VarietyBrains.cs)
            NonShooterBrains(t);  // step 8c (NonShooters.cs)
            ReaperCultBrains(t);  // step 8d (ReaperFaction.cs)
            return t;
        }
    }
}
