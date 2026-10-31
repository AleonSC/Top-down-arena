using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Twin Reapers' stage of the fight, by their combined health: Twilight (two Reapers and their tether), the
    /// Dual Reaper (fused into one), then Sundered (apart again, the tether gone).
    public enum ReaperStage : byte { Twilight, Dual, Sundered }

    /// What they're doing right now; each stage loops through its turns, one mechanic at a time.
    public enum ReaperTurn : byte { Rest, Sweep, Lines, Cross, Lasers, Chase, Reap, Spiral, Crossfire }

    /// One Reaper's reap in Sundered: marked (a line and the scythe's arc), dashing, then swinging.
    public class ReaperReap
    {
        public byte Phase;        // 0 waiting, 1 marked, 2 dashing, 3 swinging (replicated)
        public float A, Len, T;   // its direction and length, seconds in this phase (replicated)
        public float Wait, Left;  // host: until the first reap; how far the dash still goes
        public int Done;          // host: reaps done this turn
    }

    /// Everything about the Reapers' fight; the host runs it, clients get it in the snapshot (SyncReapers).
    public class ReaperFight
    {
        public ReaperStage Stage;
        public ReaperTurn Turn;
        public float TurnT;
        public byte Trans;               // 0, or a stage change: 1 the fusion, 2 the split
        public float TransT;             // seconds into the stage change
        public V2 SweepC;                // the tether sweep: the point they spin round (it follows you)
        public float SweepA;
        public float Laser;              // the Dual Reaper's lasers' angle
        public float Suck;               // > 0: pulling everyone in before it blows (seconds in)
        public float SwingT;             // the chase: until its next double swing
        public float Heading;
        public bool Trapped;             // fallen into a hole: shield up, then open
        public float ShieldFrac, Open;   // its shield left (0-1), seconds it's still open
        public float Leap;               // > 0: leaping back to the middle (seconds left)
        public readonly List<V2> Holes = new List<V2>();
        public readonly ReaperReap ReapA = new ReaperReap(), ReapB = new ReaperReap();

        // Host only.
        public int Step = -1;
        public float TurnLen, MaxA, MaxB, Shield, ShieldMax;
        public bool Fused, Blasted, Boomed;
        public V2 LeapFrom;
        public readonly List<V2> Blasts = new List<V2>();
    }

    public partial class Game
    {
        public ReaperFight Reapers;  // the Twin Reapers' fight, while it's on (replicated)

        const float FuseAt = 0.65f, SplitAt = 0.3f;
        public const float FuseTime = 4.5f, SplitTime = 2.5f;
        const float FuseMerge = 1.6f, BlastsAt = 2f, BlastWarn = 1.6f, BlastRadius = 120f, BlastDamage = 40f;
        // Twilight's tether sweep: the pair spins round a point that follows its target.
        const float SweepFollow = 110f, SweepSpin = 0.7f, SweepRadius = 280f, SweepWarm = 0.6f;
        // The Dual Reaper.
        public const float DualLaserWarm = 1f;
        const float DualLaserSpin = 0.45f, DualLaserWidth = 7f, DualLaserDamage = 15f, DualLaserTime = 12f;
        const float SuckTime = 2f, SuckPull = 300f, SuckRadius = 260f, SuckDamage = 40f;
        const float ChaseSpeed = 150f, ChaseTime = 15f, SwingEvery = 1.1f, SwingReach = 140f, SwingHalfArc = 1.05f, SwingDamage = 26f;
        public const float SwingWarn = 0.35f, HoleRadius = 60f;
        const float HoleCatch = 50f, ShieldShareDual = 0.05f, OpenTime = 4f, DualLeapTime = 1f;
        const float DualArmor = 0.35f, DualOpenMult = 1.5f;
        public static readonly V2[] PillarSpots = { new V2(110f, 110f), new V2(Width - 110f, 110f), new V2(110f, Height - 110f), new V2(Width - 110f, Height - 110f) };
        // Sundered.
        const int ReapsEach = 3;
        public const float ReapMark = 0.6f;
        const float ReapDash = 650f, ReapMaxLen = 420f, ReapSwing = 0.5f, ReapReach = 120f, ReapHalfArc = 1.4f, ReapDamage = 22f;

        static readonly ReaperTurn[][] ReaperTurns =
        {
            new[] { ReaperTurn.Sweep, ReaperTurn.Rest, ReaperTurn.Lines, ReaperTurn.Rest, ReaperTurn.Cross, ReaperTurn.Rest },
            new[] { ReaperTurn.Lasers, ReaperTurn.Rest, ReaperTurn.Chase },
            new[] { ReaperTurn.Reap, ReaperTurn.Rest, ReaperTurn.Spiral, ReaperTurn.Rest, ReaperTurn.Crossfire, ReaperTurn.Rest },
        };
        static readonly float[][] ReaperTimes =
        {
            new[] { 6f, 1f, 5f, 1f, 5f, 1f },
            new[] { DualLaserTime, 1f, ChaseTime },
            new[] { 99f, 1f, 6f, 1f, 5f, 1f },  // the reaps end once each Reaper has done its three
        };

        public static bool IsReaper(EnemyType t) => IsTwin(t) || t == EnemyType.DualReaper;

        /// Changing stage: neither can be hurt.
        bool ReaperImmune(Enemy e) => IsReaper(e.Type) && Reapers != null && Reapers.Trans != 0;

        /// The Dual Reaper is armored: it takes a third of the damage, except trapped in a hole with its shield broken,
        /// when it takes half again. While its shield is up, the shield takes the hits. Returns false if nothing gets through.
        bool DualReaperHit(Enemy e, ref float dmg)
        {
            if (e.Type != EnemyType.DualReaper || Reapers == null)
                return true;
            var R = Reapers;
            if (R.Trapped && R.Shield > 0f)
            {
                R.Shield -= dmg;
                R.ShieldFrac = Math.Max(0f, R.Shield / Math.Max(1f, R.ShieldMax));
                if (R.Shield <= 0f)
                {
                    R.Open = OpenTime;
                    BroadcastFx(FxKind.Shatter, e.Pos, e.Pos + new V2(0f, -(e.Radius + 18f)), new Rgba(0.8f, 0.88f, 1f), MathX.Pi, 0.6f, Sfx.Clang);
                }
                return false;
            }
            dmg *= R.Trapped && R.Open > 0f ? DualOpenMult : DualArmor;
            return true;
        }

        /// A living enemy of this type (one killed this tick doesn't count).
        Enemy FindEnemy(EnemyType t)
        {
            foreach (var e in Enemies.Values)
                if (e.Type == t && e.Hp > 0f)
                    return e;
            return null;
        }

        /// Fusing or fused: no Reaper falls or revives on its own then.
        bool ReapersJoined => Reapers != null && (Reapers.Trans == 1 || Reapers.Stage == ReaperStage.Dual);

        // --- The fight (host, each tick, after the enemies have moved) -----------------------------

        void TickReapers(float dt)
        {
            Enemy a = FindEnemy(EnemyType.TwinA), b = FindEnemy(EnemyType.TwinB), d = FindEnemy(EnemyType.DualReaper);
            if (a == null && b == null && d == null)
            {
                if (!twinPending)
                    Reapers = null;
                return;
            }
            var R = Reapers ?? (Reapers = new ReaperFight());
            if (a != null) R.MaxA = a.MaxHp;
            if (b != null) R.MaxB = b.MaxHp;
            if (R.Step < 0)
            {
                if ((a != null && IsSpawning(a)) || (b != null && IsSpawning(b)))
                    return;
                NextReaperTurn(R);
            }
            float frac = d != null ? d.Hp / d.MaxHp : ((a != null ? a.Hp : 0f) + (b != null ? b.Hp : 0f)) / Math.Max(1f, R.MaxA + R.MaxB);

            if (R.Trans != 0)
            {
                TickReaperChange(R, a, b, d, dt);
                return;
            }
            if (R.Stage == ReaperStage.Twilight && frac <= FuseAt && a != null && b != null)
            {
                StartReaperChange(R, 1);
                return;
            }
            if (R.Stage == ReaperStage.Dual && d != null && frac <= SplitAt && R.Leap <= 0f)
            {
                StartReaperChange(R, 2);
                return;
            }

            R.TurnT += dt;
            bool over = R.TurnT >= R.TurnLen;
            switch (R.Turn)
            {
                case ReaperTurn.Sweep:
                    TickSweep(R, a, b, dt);
                    break;
                case ReaperTurn.Lasers:
                case ReaperTurn.Chase:
                    if (d != null)
                        over = TickDual(R, d, dt);
                    break;
                case ReaperTurn.Rest:
                    if (R.Stage == ReaperStage.Dual && d != null)
                        over = TickDual(R, d, dt);
                    break;
                case ReaperTurn.Reap:
                    if (a != null) TickReap(a, R.ReapA, dt);
                    if (b != null) TickReap(b, R.ReapB, dt);
                    over = (a == null || R.ReapA.Done >= ReapsEach) && (b == null || R.ReapB.Done >= ReapsEach);
                    break;
            }
            // Soul Bond belongs to Twilight; the enrage and revive belong to Sundered (and Twilight).
            if (R.Stage != ReaperStage.Twilight)
                foreach (var e in new[] { a, b })
                    if (e != null && e.Phase == 2)
                        e.Phase = 0;
            if (R.Stage == ReaperStage.Twilight && R.Turn == ReaperTurn.Sweep && R.TurnT > SweepWarm)
                TetherHurts(a, b);
            if (over)
                NextReaperTurn(R);
        }

        void NextReaperTurn(ReaperFight R)
        {
            var turns = ReaperTurns[(int)R.Stage];
            R.Step = (R.Step + 1) % turns.Length;
            R.Turn = turns[R.Step];
            R.TurnLen = ReaperTimes[(int)R.Stage][R.Step];
            R.TurnT = 0f;
            Enemy a = FindEnemy(EnemyType.TwinA), b = FindEnemy(EnemyType.TwinB), d = FindEnemy(EnemyType.DualReaper);
            foreach (var e in new[] { a, b, d })
                if (e != null)
                    e.FireT = 0.6f;
            if (b != null && R.Turn == ReaperTurn.Crossfire)
                b.FireT = 1.2f;  // half a beat after Dusk
            switch (R.Turn)
            {
                case ReaperTurn.Sweep:
                    if (a != null && b != null)
                    {
                        R.SweepC = V2.Lerp(a.Pos, b.Pos, 0.5f);
                        R.SweepA = (b.Pos - a.Pos).Angle;
                    }
                    break;
                case ReaperTurn.Lasers:
                    R.Laser = Rand(0f, MathX.Tau);
                    R.Suck = 0f;
                    if (d != null)
                        RaisePillars(d, R);
                    break;
                case ReaperTurn.Chase:
                    R.SwingT = SwingEvery;
                    break;
                case ReaperTurn.Reap:
                    R.ReapA.Done = R.ReapB.Done = 0;
                    R.ReapA.Phase = R.ReapB.Phase = 0;
                    R.ReapA.Wait = 0f;
                    R.ReapB.Wait = 0.5f;
                    break;
            }
        }

        // --- Stage changes ----------------------------------------------------------------------------

        void StartReaperChange(ReaperFight R, byte kind)
        {
            R.Trans = kind;
            R.TransT = 0f;
            R.Fused = kind == 2;  // the split starts fused
            R.Blasted = R.Boomed = false;
            R.Suck = 0f;
            R.Trapped = false;
            twinPending = false;
            TwinReviveT = 0f;
            ClearEnemyBullets();
            session.Broadcast(Proto.End(Proto.Begin(Msg.ClearEnemyBullets)));
            BroadcastFx(FxKind.Ring, new V2(Width / 2f, Height / 2f), kind == 1 ? new Rgba(1f, 0.8f, 0.35f) : new Rgba(1f, 0.35f, 0.35f), 900f, 0.8f, Sfx.BossChime);
            DoLog(kind == 1 ? "the Twin Reapers fuse" : "the Dual Reaper splits");
        }

        void TickReaperChange(ReaperFight R, Enemy a, Enemy b, Enemy d, float dt)
        {
            R.TransT += dt;
            float t = R.TransT;
            V2 mid = new V2(Width / 2f, Height / 2f);
            if (R.Trans == 1)
            {
                // They spiral in toward each other, tighter and faster, and merge in a flash.
                if (t < FuseMerge && a != null && b != null)
                {
                    float f = Ease3(t / FuseMerge), r = 360f * (1f - f), ang = t * (2f + 6f * f);
                    a.Pos = mid + new V2((float)Math.Cos(ang + MathX.Pi) * r, (float)Math.Sin(ang + MathX.Pi) * r * 0.7f);
                    b.Pos = mid + new V2((float)Math.Cos(ang) * r, (float)Math.Sin(ang) * r * 0.7f);
                }
                if (t >= FuseMerge && !R.Fused)
                {
                    R.Fused = true;
                    float hp = (a != null ? a.Hp : 0f) + (b != null ? b.Hp : 0f);
                    if (a != null) { Recorder?.BossMerged(a); Enemies.Remove(a.Id); }  // fused, not killed (SessionRecorder)
                    if (b != null) { Recorder?.BossMerged(b); Enemies.Remove(b.Id); }
                    var dual = SpawnEnemyAt(EnemyType.DualReaper, mid, 0f, 0);
                    dual.MaxHp = R.MaxA + R.MaxB;
                    dual.Hp = Math.Max(1f, hp);
                    BroadcastFx(FxKind.Boom, mid, Rgba.White, 160f, 0.5f, Sfx.BigExplode);
                    BroadcastFx(FxKind.Ring, mid, new Rgba(1f, 0.8f, 0.35f), 900f, 1f, Sfx.None);
                }
                // It powers up: marked circles all round the arena (one on each of you), then they blow.
                if (t >= BlastsAt && !R.Blasted)
                {
                    R.Blasted = true;
                    R.Blasts.Clear();
                    for (int i = 0; i < 10; i++)
                    {
                        V2 at = RandomInArena(120f);
                        for (int k = 0; k < 10 && at.DistanceTo(mid) < 160f; k++)
                            at = RandomInArena(120f);
                        R.Blasts.Add(at);
                    }
                    foreach (var p in Players.Values)
                        if (!p.Downed)
                            R.Blasts.Add(p.Pos);
                    foreach (var at in R.Blasts)
                        BroadcastFx(FxKind.Target, at, new Rgba(1f, 0.67f, 0.24f), BlastRadius, BlastWarn, Sfx.None);
                    Sounds.Add(Sfx.BossCharge);
                }
                if (t >= BlastsAt + BlastWarn && !R.Boomed)
                {
                    R.Boomed = true;
                    foreach (var at in R.Blasts)
                    {
                        foreach (var p in Players.Values)
                            if (!p.Downed && p.InvulnT <= 0f && p.Pos.DistanceTo(at) < BlastRadius + p.Def.HitR)
                                Damage(p, BlastDamage, "The Dual Reaper (power-up)");
                        BroadcastFx(FxKind.Boom, at, new Rgba(1f, 0.7f, 0.3f), BlastRadius, 0.45f, Sfx.None);
                    }
                    Sounds.Add(Sfx.BigExplode);
                }
                if (t >= FuseTime)
                {
                    R.Trans = 0;
                    R.Stage = ReaperStage.Dual;
                    R.Holes.Clear();
                    R.Step = -1;
                    NextReaperTurn(R);
                }
                return;
            }
            // The split: it shakes and cracks, then bursts back into Dusk and Dawn, who fly apart.
            if (t >= 1.2f && R.Fused && d != null)
            {
                R.Fused = false;
                float hp = d.Hp, share = R.MaxA / Math.Max(1f, R.MaxA + R.MaxB);
                V2 at = d.Pos;
                Recorder?.BossMerged(d);  // split, not killed (SessionRecorder)
                Enemies.Remove(d.Id);
                ClearPillars();
                R.Holes.Clear();
                var na = SpawnEnemyAt(EnemyType.TwinA, at + new V2(-20f, 0f), 0f, 0);
                var nb = SpawnEnemyAt(EnemyType.TwinB, at + new V2(20f, 0f), 0f, 0);
                na.MaxHp = R.MaxA; na.Hp = Math.Max(1f, hp * share);
                nb.MaxHp = R.MaxB; nb.Hp = Math.Max(1f, hp * (1f - share));
                BroadcastFx(FxKind.Boom, at, Rgba.White, 140f, 0.5f, Sfx.BigExplode);
                BroadcastFx(FxKind.Ring, at, new Rgba(1f, 0.35f, 0.35f), 700f, 0.9f, Sfx.None);
            }
            if (t >= SplitTime)
            {
                R.Trans = 0;
                R.Stage = ReaperStage.Sundered;
                R.Step = -1;
                NextReaperTurn(R);
            }
        }

        static float Ease3(float x) => 1f - (1f - x) * (1f - x) * (1f - x);

        // --- Twilight -------------------------------------------------------------------------------

        void TickSweep(ReaperFight R, Enemy a, Enemy b, float dt)
        {
            if (a == null || b == null)
                return;
            var target = PlayerOrNull(a.TargetId) ?? PlayerOrNull(b.TargetId);
            if (target != null && !target.Downed)
                R.SweepC += (target.Pos - R.SweepC).LimitLength(SweepFollow * dt);
            R.SweepC = new V2(Math.Max(SweepRadius + 40f, Math.Min(Width - SweepRadius - 40f, R.SweepC.X)), Math.Max(320f, Math.Min(Height - 320f, R.SweepC.Y)));
            if (R.TurnT > SweepWarm)
                R.SweepA += SweepSpin * dt;
            foreach (var e in new[] { a, b })
            {
                V2 goal = ClampToArena(R.SweepC + V2.FromAngle(R.SweepA + (e == a ? MathX.Pi : 0f)) * SweepRadius, e.Radius);
                e.Pos += (goal - e.Pos).LimitLength((R.TurnT < SweepWarm ? 600f : 1200f) * dt);
            }
        }

        void TetherHurts(Enemy a, Enemy b)
        {
            if (a == null || b == null)
                return;
            V2 dvec = b.Pos - a.Pos;
            float len = Math.Max(1f, dvec.Length);
            V2 dir = dvec / len;
            foreach (var p in Players.Values)
            {
                if (p.Downed || p.InvulnT > 0f)
                    continue;
                float along = (p.Pos.X - a.Pos.X) * dir.X + (p.Pos.Y - a.Pos.Y) * dir.Y;
                if (along < a.Radius || along > len - b.Radius)
                    continue;
                if (p.Pos.DistanceTo(a.Pos + dir * along) < TetherWidth / 2f + p.Def.HitR)
                    Damage(p, 18f + Wave * 0.4f, "Twin Reapers (tether)");
            }
        }

        /// Where a Twin Reaper goes this turn (the sweep and the reap move them themselves).
        V2 ReaperMove(Enemy e, V2 tpos, ref float speed)
        {
            var R = Reapers;
            float side = e.Type == EnemyType.TwinA ? -1f : 1f;
            V2 mid = new V2(Width / 2f, Height / 2f);
            if (R == null || R.Step < 0)
                return (mid + new V2(side * Width * 0.26f, 0f) - e.Pos) / 60f;
            if (R.Trans == 2)
            {
                speed = 700f;
                V2 to = mid + new V2(side * 300f, 0f);
                return e.Pos.DistanceTo(to) > 4f ? (to - e.Pos).Normalized() : V2.Zero;
            }
            if (R.Trans == 1)
                return V2.Zero;
            switch (R.Turn)
            {
                case ReaperTurn.Sweep:
                case ReaperTurn.Lines:
                case ReaperTurn.Cross:
                case ReaperTurn.Reap:
                    return V2.Zero;
                case ReaperTurn.Spiral:
                {
                    speed = 400f;
                    V2 to = mid + new V2(side * 300f, 0f);
                    return e.Pos.DistanceTo(to) > 4f ? (to - e.Pos).Normalized() : V2.Zero;
                }
                case ReaperTurn.Crossfire:
                {
                    speed = 120f;
                    V2 to = new V2(side < 0f ? 140f : Width - 140f, Math.Max(100f, Math.Min(Height - 100f, tpos.Y)));
                    return e.Pos.DistanceTo(to) > 4f ? (to - e.Pos).Normalized() : V2.Zero;
                }
            }
            if (R.Stage == ReaperStage.Twilight)
            {
                // Between turns: drift round their circle, as they always have.
                float ang = e.T * 0.47f + (e.Type == EnemyType.TwinA ? 0f : MathX.Pi);
                return (mid + new V2((float)Math.Cos(ang) * Width * 0.26f, (float)Math.Sin(ang) * Height * 0.28f) - e.Pos) / 60f;
            }
            V2 goal = new V2(tpos.X + side * 340f, Math.Max(120f, Math.Min(Height - 120f, tpos.Y)));
            return (goal - e.Pos) / 60f;
        }

        /// Their guns, per turn (EnemyFire calls this when the timer runs out).
        void ReaperFire(Enemy e, V2 tpos)
        {
            var R = Reapers;
            float aim = (tpos - e.Pos).Angle;
            if (R == null || R.Trans != 0)
            {
                e.FireT = 0.2f;
                return;
            }
            switch (R.Turn)
            {
                case ReaperTurn.Lines:  // three lines of rising-speed shots, the middle one red
                    e.FireT = 1.4f;
                    for (int l = -1; l <= 1; l++)
                        for (int k = 0; k < 6; k++)
                            EnemyShot(e, aim + l * 0.5f, 140f + k * 35f, l == 0 ? Defs.PierceShot : Defs.SmallShot);
                    break;
                case ReaperTurn.Cross:  // a rotating cross
                    e.FireT = 0.9f;
                    e.Ang += 0.35f;
                    for (int dd = 0; dd < 4; dd++)
                        for (int k = 0; k < 3; k++)
                            EnemyShot(e, e.Ang + MathX.Tau * dd / 4f, 120f + k * 50f, Defs.SmallShot);
                    break;
                case ReaperTurn.Spiral:  // from its spot: Dusk clockwise, Dawn the other way
                {
                    e.FireT = 0.1f;
                    float side = e.Type == EnemyType.TwinA ? -1f : 1f;
                    if (e.Pos.DistanceTo(new V2(Width / 2f + side * 300f, Height / 2f)) > 8f)
                        break;
                    e.Ang += e.Type == EnemyType.TwinA ? 0.2f : -0.2f;
                    for (int k = 0; k < 3; k++)
                        EnemyShot(e, e.Ang + MathX.Tau * k / 3f, 140f, Defs.SmallShot);
                    break;
                }
                case ReaperTurn.Crossfire:  // a 7-shot fan from the wall
                    e.FireT = 1.2f;
                    if (R.TurnT > 0.8f)
                        for (int i = 0; i < 7; i++)
                            EnemyShot(e, aim + (i - 3) * 0.245f, 200f, Defs.SmallShot);
                    break;
                default:
                    e.FireT = 0.1f;
                    break;
            }
        }

        // --- The Dual Reaper ---------------------------------------------------------------------------

        void RaisePillars(Enemy d, ReaperFight R)
        {
            foreach (var spot in PillarSpots)
            {
                bool taken = false;
                foreach (var h in R.Holes)
                    taken |= h.DistanceTo(spot) < 10f;
                foreach (var o in Enemies.Values)
                    taken |= o.Type == EnemyType.ReaperPillar && o.Pos.DistanceTo(spot) < 10f;
                if (!taken)
                    SpawnEnemyAt(EnemyType.ReaperPillar, spot, 0.5f, d.Id);
            }
        }

        int CountPillars()
        {
            int n = 0;
            foreach (var o in Enemies.Values)
                if (o.Type == EnemyType.ReaperPillar)
                    n++;
            return n;
        }

        void ClearPillars()
        {
            var ids = new List<int>();
            foreach (var o in Enemies.Values)
                if (o.Type == EnemyType.ReaperPillar)
                    ids.Add(o.Id);
            foreach (int id in ids)
                Enemies.Remove(id);
        }

        /// A pillar that falls leaves a hole in the floor (the Dual Reaper can be trapped in it).
        void PillarFell(Enemy pillar)
        {
            if (Reapers != null && Reapers.Stage == ReaperStage.Dual && Reapers.Trans == 0)
            {
                Reapers.Holes.Add(pillar.Pos);
                BroadcastFx(FxKind.Boom, pillar.Pos, new Rgba(0.55f, 0.5f, 0.75f), 70f, 0.5f, Sfx.Explode);
            }
        }

        /// The Dual Reaper's turns; returns true when the turn is over.
        bool TickDual(ReaperFight R, Enemy d, float dt)
        {
            V2 mid = new V2(Width / 2f, Height / 2f);
            if (R.Trapped)
            {
                if (R.Shield <= 0f)
                {
                    R.Open -= dt;
                    if (R.Open <= 0f)
                    {
                        R.Trapped = false;  // out it pops, back to the middle
                        R.Leap = DualLeapTime;
                        R.LeapFrom = d.Pos;
                        BroadcastFx(FxKind.Ring, d.Pos, new Rgba(1f, 0.8f, 0.35f), 160f, 0.4f, Sfx.Shockwave);
                    }
                }
                return false;
            }
            if (R.Leap > 0f)
            {
                R.Leap -= dt;
                d.Pos = V2.Lerp(R.LeapFrom, mid, Ease3(1f - Math.Max(0f, R.Leap) / DualLeapTime));
                if (R.Leap <= 0f)
                {
                    R.Leap = 0f;
                    d.Pos = mid;
                    R.Holes.Clear();
                    BroadcastFx(FxKind.Ring, mid, new Rgba(1f, 0.8f, 0.35f), 500f, 0.6f, Sfx.Explode);
                    R.Step = -1;  // and round again, with new pillars
                    return true;
                }
                return false;
            }
            switch (R.Turn)
            {
                case ReaperTurn.Lasers:
                {
                    if (R.Suck > 0f)
                    {
                        // The pillars stood: it pulls everyone in, then blows.
                        R.Suck += dt;
                        PullCenter = d.Pos;
                        PullStrength = SuckPull;
                        if (R.Suck >= SuckTime)
                        {
                            R.Suck = 0f;
                            PullStrength = 0f;
                            foreach (var p in Players.Values)
                                if (!p.Downed && p.InvulnT <= 0f && p.Pos.DistanceTo(d.Pos) < SuckRadius + p.Def.HitR)
                                    Damage(p, SuckDamage, "The Dual Reaper (blast)");
                            BroadcastFx(FxKind.Boom, d.Pos, new Rgba(1f, 0.6f, 0.3f), SuckRadius, 0.6f, Sfx.BigExplode);
                            R.TurnT = 0f;  // and again, with the pillars that are left
                            R.Laser = Rand(0f, MathX.Tau);
                        }
                        return false;
                    }
                    if (R.TurnT > DualLaserWarm)
                    {
                        R.Laser += DualLaserSpin * dt;
                        foreach (var p in Players.Values)
                        {
                            if (p.Downed || p.InvulnT > 0f)
                                continue;
                            for (int k = 0; k < 2; k++)
                            {
                                V2 dir = V2.FromAngle(R.Laser + k * MathX.Pi), rel = p.Pos - d.Pos;
                                float along = rel.X * dir.X + rel.Y * dir.Y, across = Math.Abs(rel.X * -dir.Y + rel.Y * dir.X);
                                if (along > d.Radius && across < DualLaserWidth + p.Def.HitR)
                                {
                                    Damage(p, DualLaserDamage, "The Dual Reaper (laser)");
                                    break;
                                }
                            }
                        }
                    }
                    if (R.TurnT > DualLaserWarm && CountPillars() == 0)
                        return true;  // every pillar down: a breather, then the chase
                    if (R.TurnT >= DualLaserTime)
                    {
                        R.Suck = 0.001f;
                        BroadcastFx(FxKind.Ring, d.Pos, new Rgba(1f, 0.4f, 0.4f), SuckRadius, 0.5f, Sfx.BossCharge);
                    }
                    return false;
                }
                case ReaperTurn.Chase:
                {
                    var target = PlayerOrNull(d.TargetId);
                    if (target != null)
                        R.Heading = (target.Pos - d.Pos).Angle;
                    R.SwingT -= dt;
                    if (R.SwingT <= 0f)
                    {
                        R.SwingT = SwingEvery;
                        for (int side = -1; side <= 1; side += 2)
                        {
                            float sa = R.Heading + side * MathX.Pi / 2f;
                            foreach (var p in Players.Values)
                            {
                                if (p.Downed || p.InvulnT > 0f || p.Pos.DistanceTo(d.Pos) > SwingReach + p.Def.HitR)
                                    continue;
                                float diff = Math.Abs(MathX.Fmod((p.Pos - d.Pos).Angle - sa + MathX.Pi, MathX.Tau) - MathX.Pi);
                                if (diff < SwingHalfArc)
                                    Damage(p, SwingDamage, "The Dual Reaper (scythe)");
                            }
                            BroadcastFx(FxKind.Slash, d.Pos, d.Pos + V2.FromAngle(sa) * SwingReach, side < 0 ? Defs.Get(EnemyType.TwinA).Color : Defs.Get(EnemyType.TwinB).Color,
                                SwingHalfArc, 0.2f, Sfx.SwingHeavy);
                        }
                    }
                    foreach (var h in R.Holes)
                        if (d.Pos.DistanceTo(h) < HoleCatch)
                        {
                            // Into the hole: it's stuck, and puts up a shield.
                            R.Trapped = true;
                            d.Pos = h;
                            R.Shield = R.ShieldMax = d.MaxHp * ShieldShareDual;
                            R.ShieldFrac = 1f;
                            R.Open = 0f;
                            BroadcastFx(FxKind.Ring, h, new Rgba(1f, 0.8f, 0.35f), 140f, 0.5f, Sfx.Shockwave);
                            DoLog("the Dual Reaper fell into a hole");
                            return false;
                        }
                    if (R.TurnT >= ChaseTime)
                    {
                        R.Leap = DualLeapTime;  // gave up: back to the middle
                        R.LeapFrom = d.Pos;
                    }
                    return false;
                }
                default:  // the breather after the lasers
                    return R.TurnT >= R.TurnLen;
            }
        }

        V2 DualMove(Enemy e, V2 tpos, ref float speed)
        {
            var R = Reapers;
            if (R == null || R.Trans != 0 || R.Trapped || R.Leap > 0f || R.Suck > 0f)
                return V2.Zero;
            V2 mid = new V2(Width / 2f, Height / 2f);
            if (R.Turn == ReaperTurn.Chase)
            {
                speed = ChaseSpeed;
                return (tpos - e.Pos).Normalized();
            }
            speed = 400f;
            return e.Pos.DistanceTo(mid) > 4f ? (mid - e.Pos).Normalized() : V2.Zero;
        }

        // --- Sundered: the reap -------------------------------------------------------------------

        void TickReap(Enemy e, ReaperReap rp, float dt)
        {
            if (rp.Done >= ReapsEach)
                return;
            if (rp.Wait > 0f)
            {
                rp.Wait -= dt;
                return;
            }
            var target = PlayerOrNull(e.TargetId);
            V2 tp = target != null ? target.Pos : new V2(Width / 2f, Height / 2f);
            rp.T += dt;
            switch (rp.Phase)
            {
                case 0:
                    rp.Phase = 1;
                    rp.T = 0f;
                    goto case 1;
                case 1:  // marked: it tracks its target for half the time, then locks
                    if (rp.T < ReapMark * 0.5f)
                    {
                        rp.A = (tp - e.Pos).Angle;
                        rp.Len = Math.Min(ReapMaxLen, Math.Max(80f, tp.DistanceTo(e.Pos) - 20f));
                    }
                    if (rp.T >= ReapMark)
                    {
                        rp.Phase = 2;
                        rp.T = 0f;
                        rp.Left = rp.Len;
                    }
                    break;
                case 2:
                {
                    float step = Math.Min(rp.Left, ReapDash * dt);
                    e.Pos = ClampToArena(e.Pos + V2.FromAngle(rp.A) * step, e.Radius);
                    rp.Left -= step;
                    if (rp.Left <= 0f)
                    {
                        rp.Phase = 3;
                        rp.T = 0f;
                        foreach (var p in Players.Values)
                        {
                            if (p.Downed || p.InvulnT > 0f || p.Pos.DistanceTo(e.Pos) > ReapReach + p.Def.HitR)
                                continue;
                            float diff = Math.Abs(MathX.Fmod((p.Pos - e.Pos).Angle - rp.A + MathX.Pi, MathX.Tau) - MathX.Pi);
                            if (diff < ReapHalfArc)
                                Damage(p, ReapDamage, e.Def.Name + " (reap)");
                        }
                        BroadcastFx(FxKind.Slash, e.Pos, e.Pos + V2.FromAngle(rp.A) * ReapReach, e.Def.Color, ReapHalfArc, 0.22f, Sfx.SwingHeavy);
                    }
                    break;
                }
                default:
                    if (rp.T >= ReapSwing)
                    {
                        rp.Done++;
                        rp.Phase = 0;
                        rp.Wait = 0.1f;
                    }
                    break;
            }
        }

        // --- Replication ----------------------------------------------------------------------------

        void SyncReapers(NetSync s)
        {
            bool has = Reapers != null;
            s.Bool(ref has);
            if (!has)
            {
                if (s.Reading)
                    Reapers = null;
                return;
            }
            var R = Reapers ?? (Reapers = new ReaperFight());
            int stage = (int)R.Stage, turn = (int)R.Turn, trans = R.Trans, holes = R.Holes.Count;
            s.Byte(ref stage);
            s.Byte(ref turn);
            s.Float(ref R.TurnT);
            s.Byte(ref trans);
            s.Float(ref R.TransT);
            s.Vec(ref R.SweepC);
            s.Float(ref R.SweepA);
            s.Float(ref R.Laser);
            s.Float(ref R.Suck);
            s.Float(ref R.SwingT);
            s.Float(ref R.Heading);
            s.Bool(ref R.Trapped);
            s.Float(ref R.ShieldFrac);
            s.Float(ref R.Open);
            s.Float(ref R.Leap);
            s.Byte(ref holes);
            if (s.Reading)
            {
                R.Stage = (ReaperStage)stage;
                R.Turn = (ReaperTurn)turn;
                R.Trans = (byte)trans;
                while (R.Holes.Count < holes) R.Holes.Add(V2.Zero);
                while (R.Holes.Count > holes) R.Holes.RemoveAt(R.Holes.Count - 1);
            }
            for (int i = 0; i < R.Holes.Count; i++)
            {
                V2 h = R.Holes[i];
                s.Vec(ref h);
                R.Holes[i] = h;
            }
            foreach (var rp in new[] { R.ReapA, R.ReapB })
            {
                s.Byte(ref rp.Phase);
                s.Float(ref rp.A);
                s.Float(ref rp.Len);
                s.Float(ref rp.T);
            }
        }

        /// Clients: run the clocks between snapshots.
        void UpdateReaperView(float dt)
        {
            if (IsHost || Reapers == null)
                return;
            var R = Reapers;
            R.TurnT += dt;
            if (R.Trans != 0)
                R.TransT += dt;
            R.SwingT -= dt;
            R.ReapA.T += dt;
            R.ReapB.T += dt;
        }
    }
}
