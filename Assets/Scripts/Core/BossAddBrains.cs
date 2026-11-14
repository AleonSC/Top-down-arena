namespace Arena
{
    /// Step 8f of the enemy behaviour classes (Docs/EnemyBehaviours.md), the last of it: the Hive Queen's Bombers, the
    /// Gutworm, the challenge rooms' enemies (Gatekeeper, Shadow: Challenges.cs), the Riot Captain (RiotCaptain.cs), and
    /// the bosses' adds that their boss moves itself (StillMove).
    public partial class Game
    {
        /// A straight line to the spot it was launched at; Swarm's flankers swoop round, diving in Phase 2 (Queen.cs). Its
        /// facing stays on its flight line.
        sealed class BomberMove : MovePart
        {
            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
            {
                if (e.Phase >= 1)
                {
                    var v = g.FlankerMove(e, dt);
                    if (e.Phase == 2)
                        speed *= FlankDive;
                    return v;
                }
                return e.Waypoint - e.Pos;
            }

            public override bool Turn(Game g, Enemy e, float want, V2 tpos, float dt) => true;
        }

        /// A flanker at its flank point turns in toward its target; it blows up when it reaches the spot it was launched
        /// at, or on touching anyone on the way.
        void BomberSpecial(Enemy e)
        {
            if (e.Phase == 1 && (e.Pos.DistanceTo(e.Waypoint) < FlankArrive || e.T > FlankMaxOut))
                FlankerTurnIn(e);
            bool hit = e.Phase != 1 && e.Pos.DistanceTo(e.Waypoint) < 12f;
            foreach (var p in Players.Values)
                hit |= !p.Downed && p.Pos.DistanceTo(e.Pos) < e.Radius + p.Def.HitR;
            if (hit)
                BomberExplode(e);
        }

        /// The Riot Captain walks and turns its own way (CaptainMove, CaptainFace).
        sealed class CaptainMovePart : MovePart
        {
            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed) => g.CaptainMove(e, dir, dist, ref speed);
            public override bool Turn(Game g, Enemy e, float want, V2 tpos, float dt)
            {
                g.CaptainFace(e, want, dt);
                return true;
            }
        }

        /// Its tower shield: blocks from the front, never breaks (a blocked hit just clangs).
        sealed class CaptainGuard : GuardPart
        {
            public override bool Blocks(Game g, Enemy e, V2 from) => g.CaptainShieldBlocks(e, from);
            public override void Wear(Game g, Enemy e, float amount) => g.CaptainShieldStruck(e);
        }

        /// A challenge room's enemy fires its own way (RoomFire); when it doesn't, a melee one stops trying.
        sealed class RoomFirePart : FirePart
        {
            public override void Fire(Game g, Enemy e, V2 tpos, float aim)
            {
                if (!g.RoomFire(e, tpos, aim) && e.Def.Melee)
                    e.FireT = 99f;
            }
        }

        /// A masked Mummer circles you with its copies and fires (MummerSpecial); unmasked, it lunges like the others.
        sealed class MaskSpecial : SpecialPart
        {
            readonly SpecialPart lunge;
            public MaskSpecial(SpecialPart lunge) { this.lunge = lunge; }
            public override void Tick(Game g, Enemy e, V2 tpos, float dt)
            {
                if (!g.MummerSpecial(e, tpos, dt))
                    lunge.Tick(g, e, tpos, dt);
            }
        }

        static void BossAddBrains(Brain[] t)
        {
            t[(int)EnemyType.HiveBomber] = new Brain { Move = new BomberMove(), Special = new CodeSpecial((g, e, p, dt) => g.BomberSpecial(e)) };
            t[(int)EnemyType.Gutworm] = new Brain { Move = new ChaseMove(), Special = new CodeSpecial((g, e, p, dt) => g.GutwormSpecial(e, p, dt)) };  // straight at you, on the surface or under it
            t[(int)EnemyType.RiotCaptain] = new Brain { Move = new CaptainMovePart(), Guard = new CaptainGuard(), Special = new CodeSpecial((g, e, p, dt) => g.CaptainSpecial(e, p, dt)),
                Fire = new HoldFire() };  // its attacks are its Special
            var room = new Brain { Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.RoomMove(e, p, d, di, ref sp)),
                Special = new CodeSpecial((g, e, p, dt) => g.RoomSpecial(e, p, dt)), Fire = new RoomFirePart() };
            t[(int)EnemyType.Gatekeeper] = room;
            t[(int)EnemyType.Shadow] = room;
            var carried = new Brain { Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => V2.Zero) };  // its boss moves it
            foreach (var type in new[] { EnemyType.CageWell, EnemyType.HiveEgg, EnemyType.ReaperPillar, EnemyType.SingShard, EnemyType.BallLightning, EnemyType.MummerCopy, EnemyType.Portal })
                t[(int)type] = carried;
            var mummer = t[(int)EnemyType.Mummer];  // (a lunger: LungeBrains.cs)
            t[(int)EnemyType.Mummer] = new Brain { Move = mummer.Move, Special = new MaskSpecial(mummer.Special), Roles = mummer.Roles };
        }
    }
}
