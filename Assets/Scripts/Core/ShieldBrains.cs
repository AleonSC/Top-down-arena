namespace Arena
{
    /// Step 1 of the enemy behaviour classes (Docs/EnemyBehaviours.md): the shield family. Every shield bearer walks, turns,
    /// bashes and lunges like the Warden's Riot Shield (WardenFaction.cs); its guard part is where each one differs:
    /// a Comb Shield lets part of each shot through as its cells crack (HiveBees.cs), a Slab Bearer drops its slab as cover
    /// when the shield breaks (QuarryFaction.cs), a Carcass Bearer drops its meat as a morsel (LarderFaction.cs), and a
    /// Parrier sends front hits back (CourtFaction.cs).
    public partial class Game
    {
        /// Herds into its place in the line, then bashes, lunges, staggers (RiotShieldMove); turns slowly (RiotShieldFace).
        sealed class ShieldMove : MovePart
        {
            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed) => g.RiotShieldMove(e, tpos, dir, ref speed);
            public override bool Turn(Game g, Enemy e, float want, float dt)
            {
                g.RiotShieldFace(e, want, dt);
                return true;
            }
        }

        /// The bash, the wind-up and lunge, the stagger and flail (RiotShieldSpecial).
        sealed class ShieldSpecial : SpecialPart
        {
            public override void Tick(Game g, Enemy e, V2 tpos, float dt) => g.RiotShieldSpecial(e, tpos, dt);
        }

        /// A Riot Shield's front: blocks from the front until it's held low or broken, cracks in three steps, then shatters.
        class ShieldGuard : GuardPart
        {
            public override bool Blocks(Game g, Enemy e, V2 from) => g.RiotShieldBlocks(e, from);
            public override void Setup(Game g, Enemy e) => e.ShieldHp = e.ShieldMax = e.MaxHp * RiotShieldShare;
            public override void Wear(Game g, Enemy e, float amount) => g.WearRiotShield(e, amount, this);
            /// When the shield shatters (before the shatter effect).
            public virtual void Shattered(Game g, Enemy e) { }
        }

        /// A Comb Shield's wax cells let a share of a blocked shot through, more with each crack (CombLeak).
        sealed class CombGuard : ShieldGuard
        {
            public override void Shot(Game g, Enemy e, Bullet b)
            {
                Wear(g, e, b.Dmg);
                g.HurtEnemy(e, b.Dmg * CombLeak(e), b.Owner);
            }
        }

        /// A Slab Bearer's slab falls in front of it as cover.
        sealed class SlabGuard : ShieldGuard
        {
            public override void Shattered(Game g, Enemy e) => g.DropSlab(e);
        }

        /// A Carcass Bearer's meat falls as a morsel.
        sealed class CarcassGuard : ShieldGuard
        {
            public override void Shattered(Game g, Enemy e) => g.DropCarcass(e);
        }

        /// A Parrier's main-gauche sends a front hit back at the shooter (ParrierBounce), then the guard wears.
        sealed class ParrierGuard : ShieldGuard
        {
            public override void Shot(Game g, Enemy e, Bullet b)
            {
                g.ParrierBounce(e, b);
                Wear(g, e, b.Dmg);
            }
        }

        static void ShieldBrains(Brain[] t)
        {
            var move = new ShieldMove();
            var special = new ShieldSpecial();
            t[(int)EnemyType.RiotShield] = new Brain { Move = move, Special = special, Guard = new ShieldGuard(), Roles = BrainRole.Wall };
            t[(int)EnemyType.CombShield] = new Brain { Move = move, Special = special, Guard = new CombGuard(), Roles = BrainRole.Wall };
            t[(int)EnemyType.SlabBearer] = new Brain { Move = move, Special = special, Guard = new SlabGuard(), Roles = BrainRole.Wall };
            t[(int)EnemyType.CarcassBearer] = new Brain { Move = move, Special = special, Guard = new CarcassGuard(), Roles = BrainRole.Wall };
            t[(int)EnemyType.Parrier] = new Brain { Move = move, Special = special, Guard = new ParrierGuard(), Roles = BrainRole.Wall };
        }
    }
}
