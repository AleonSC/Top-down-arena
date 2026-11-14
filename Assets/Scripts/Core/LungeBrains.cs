namespace Arena
{
    /// Step 2 of the enemy behaviour classes (Docs/EnemyBehaviours.md): the lungers. One LungeMove and one LungeSpecial class,
    /// each lunger with its own numbers: Phase 0 closes in (then circles), 1 winds up behind a telegraph line, 2 leaps,
    /// 3 recovers. The Lunger, Shiv, Hornet (HiveBees.cs), Driller (its leap's crack: QuarryFaction.cs) and Mummer (its
    /// masquerade runs first: CourtFaction.cs MummerSpecial) use the plain numbers; a Fencer hops (CourtFaction.cs) and a
    /// Scythe Wraith reaps along a long, locked line (ReaperFaction.cs).
    public partial class Game
    {
        /// A lunger's numbers. Track < 0: it tracks you for the whole wind-up; otherwise only for the first Track s.
        sealed class LungeNumbers
        {
            public float Start = 220f, Circle = 170f, WindUp = 0.6f, Track = -1f, Dash = 0.32f, Speed = 640f, Recover = 0.8f, Damage = 26f;
        }

        /// Closes in to Circle px, then circles while it waits; a straight leap at Speed; slowed while it recovers.
        sealed class LungeMove : MovePart
        {
            readonly LungeNumbers n;
            public LungeMove(LungeNumbers n) { this.n = n; }

            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
            {
                if (e.Phase == 0)
                    return dist > n.Circle ? dir : dir.Orthogonal() * e.Strafe * 0.6f;
                if (e.Phase == 2)
                {
                    speed = n.Speed;
                    return V2.FromAngle(e.Facing);
                }
                if (e.Phase == 3)
                    speed *= 0.3f;  // recovering after the leap
                return V2.Zero;
            }

            /// Committed to the leap's line; a mark that stops tracking stays put once it's locked.
            public override bool Turn(Game g, Enemy e, float want, float dt) =>
                e.Phase >= 2 || n.Track >= 0f && e.Phase == 1 && e.PhaseT <= n.WindUp - n.Track;

            public override float Contact(Game g, Enemy e) => e.Phase == 2 ? n.Damage : -1f;
        }

        /// The wind-up, leap and recovery clock. In a squad it waits for a Brute before winding up (LungerWaits, Variety.cs).
        sealed class LungeSpecial : SpecialPart
        {
            readonly LungeNumbers n;
            public LungeSpecial(LungeNumbers n) { this.n = n; }

            public override void Tick(Game g, Enemy e, V2 tpos, float dt)
            {
                float dist = tpos.DistanceTo(e.Pos);
                e.PhaseT -= dt;
                if (e.Phase == 0 && dist < n.Start && e.PhaseT <= 0f && !g.LungerWaits(e, tpos))
                {
                    e.Phase = 1;
                    e.PhaseT = n.WindUp;
                }
                else if (e.Phase == 1)
                {
                    if (n.Track < 0f || e.PhaseT > n.WindUp - n.Track)
                        e.Facing = (tpos - e.Pos).Angle;  // tracks you while winding up
                    if (e.PhaseT <= 0f)
                    {
                        e.Phase = 2;
                        e.PhaseT = n.Dash;
                    }
                }
                else if (e.Phase == 2 && e.PhaseT <= 0f)
                {
                    e.Phase = 3;
                    e.PhaseT = n.Recover;
                }
                else if (e.Phase == 3 && e.PhaseT <= 0f)
                {
                    e.Phase = 0;
                    e.PhaseT = g.Rand(0.8f, 1.6f);
                }
            }
        }

        static Brain Lunge(LungeNumbers n) => new Brain { Move = new LungeMove(n), Special = new LungeSpecial(n), Roles = BrainRole.Lunger };

        static void LungeBrains(Brain[] t)
        {
            var plain = new LungeNumbers();
            var fencer = new LungeNumbers { Start = FencerStart, Circle = FencerStart - 30f, WindUp = FencerWarn, Dash = FencerHop, Recover = FencerRecover, Damage = FencerHopDamage };
            var scythe = new LungeNumbers { Start = ScytheStart, WindUp = ScytheMark, Track = ScytheTrack, Dash = ScytheDash, Speed = ScytheSpeed, Recover = ScytheRecover };
            var plainBrain = Lunge(plain);
            foreach (var type in new[] { EnemyType.Lunger, EnemyType.Shiv, EnemyType.Hornet, EnemyType.Driller, EnemyType.Mummer })
                t[(int)type] = plainBrain;
            t[(int)EnemyType.Fencer] = Lunge(fencer);
            t[(int)EnemyType.ScytheWraith] = Lunge(scythe);
        }
    }
}
