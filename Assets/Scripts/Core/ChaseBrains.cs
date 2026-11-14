namespace Arena
{
    /// Step 3 of the enemy behaviour classes (Docs/EnemyBehaviours.md): the brutes and chasers. They all walk straight at
    /// you (ChaseMove). Brutes (Brute, Thug, Bumble, Gravedigger, and the Thug kin below) enrage as they're shot
    /// (BruteEnrage.cs, through IsBrute); the Swarmer and Wisp are plain chasers. What's different is a special:
    /// the Stomper's stomp ring (QuarryFaction.cs), the Gulper's breath (LarderFaction.cs), the Harlequin's crossing
    /// (CourtFaction.cs).
    public partial class Game
    {
        /// No tricks: straight at you.
        sealed class ChaseMove : MovePart
        {
            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed) => dir;
        }

        sealed class StompSpecial : SpecialPart
        {
            public override void Tick(Game g, Enemy e, V2 tpos, float dt) => g.StomperSpecial(e, tpos, dt);
        }

        sealed class GulpSpecial : SpecialPart
        {
            public override void Tick(Game g, Enemy e, V2 tpos, float dt) => g.LarderSpecial(e, tpos, dt);
        }

        sealed class CrossingSpecial : SpecialPart
        {
            public override void Tick(Game g, Enemy e, V2 tpos, float dt) => g.HarlequinSpecial(e, tpos, dt);
        }

        static void ChaseBrains(Brain[] t)
        {
            var chase = new ChaseMove();
            var brute = new Brain { Move = chase, Roles = BrainRole.Brute };
            foreach (var type in new[] { EnemyType.Brute, EnemyType.Thug, EnemyType.Bumble, EnemyType.Gravedigger })
                t[(int)type] = brute;
            var plain = new Brain { Move = chase };
            t[(int)EnemyType.Swarmer] = plain;
            t[(int)EnemyType.Wisp] = plain;
            t[(int)EnemyType.Stomper] = new Brain { Move = chase, Special = new StompSpecial(), Roles = BrainRole.Brute };
            t[(int)EnemyType.Gulper] = new Brain { Move = chase, Special = new GulpSpecial(), Roles = BrainRole.Brute };
            t[(int)EnemyType.Harlequin] = new Brain { Move = chase, Special = new CrossingSpecial(), Roles = BrainRole.Brute };
        }
    }
}
