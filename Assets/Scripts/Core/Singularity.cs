using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// A ring of three small gravity wells orbiting a spot a player was standing on. The ring shrinks;
    /// touching it hurts, and it's gone once its three wells are shot down.
    public class Cage
    {
        public int Id, PlayerId;
        public V2 Center;
        public float Radius, Spin;
        public readonly List<int> Wells = new List<int>();  // host: the Cage Well enemies holding it up
    }

    /// The Singularity's stage of the fight, by its health: three stages, then a last stand.
    public enum SingStage : byte { EventHorizon, Accretion, Spaghettify, LastStand }

    /// What it's doing now. Each stage loops its turns, one mechanic at a time, with breathers (Rest) between.
    public enum SingTurn : byte { Rest, Pull, Collapse, Lasers, Cage, Slingshot, TwinLasers, Hawking, WhiteHole, Split, Last }

    /// Everything about the Singularity's fight its body doesn't carry. The host runs it; clients get it in the snapshot
    /// (SyncSing) to draw the accretion disk, the event horizon and the stage change.
    public class SingState
    {
        public SingStage Stage, TransTo;
        public SingTurn Turn;
        public float TurnT, TransT;      // seconds into the turn; > 0: changing stage (can't be hurt), counting down
        public float Horizon;            // the event horizon's radius (Spaghettify and the last stand), else 0
        public float DiskT;              // the accretion disk's clock: where its orbs are
        public int DiskGone;             // outer-ring slots flung off by the Slingshot (bit per slot)

        // Host only.
        public int Step = -1;
        public float TurnLen, StageT, Orbit, RingT, RefillT;
        public bool Reversed;
    }

    /// The Singularity (wave 20): a fight in three stages by its health, like the Warden's, then a last stand.
    ///   Event Horizon (100-65%): in the middle; Pull (drags everyone in under a spiral), Collapse (rings), Lasers.
    ///   Accretion (65-30%): it orbits the middle inside a disk of debris (two rings of orbs with gaps); Cage,
    ///     Slingshot (orbs flung off the disk at you), Twin lasers.
    ///   Spaghettify (30-10%): back in the middle behind a growing event horizon that pulls all the time; Hawking
    ///     bursts, White hole (it pushes, and every enemy bullet turns round), Split (three shards fire; the core
    ///     can't be hurt, the shards pass their damage to it).
    ///   Last stand (below 10%): the horizon swells, the pull doubles, a spiral and orb rings.
    /// Four Gravity Wells spawn in the corners at the start of each of the three stages: they bend your shots into
    /// themselves and drift you toward them. Killed, they stay gone until the next stage.
    public partial class Game
    {
        // Phase (replicated) tells clients what to draw.
        public const byte SingNormal = 0, SingPull = 1, SingCollapse = 2, SingCage = 3, SingLaserWarn = 4, SingLasers = 5,
            SingRest = 6, SingSlingshot = 7, SingHawking = 8, SingWhiteHole = 9, SingSplit = 10, SingLast = 11, SingShift = 12;
        const float SingLockdownAt = 0.65f, SingSpaghettiAt = 0.3f, SingLastAt = 0.1f, SingTransTime = 2f;
        const float LaserWarnAt = 1.5f, LaserOnAt = 2.3f;  // seconds into the Lasers turn
        public const int LaserCount = 3;
        public const float LaserWidth = 10f, LaserSpin = 0.32f;  // radians per second, always counter-clockwise
        public const float TwinLaserSpin = 0.45f, TwinLaserWarn = 0.8f;
        public const float CageStartRadius = 180f, CageShrink = 22f, CageWallWidth = 10f, CageSpin = 1.4f;
        const int CageWellsEach = 3;
        // The accretion disk: two rings of big orbs, turning opposite ways, two orbs missing per gap.
        public const int DiskInnerN = 20, DiskOuterN = 30, DiskGapEvery = 10;
        public const float DiskInnerR = 150f, DiskOuterR = 250f, DiskInnerSpin = 0.6f, DiskOuterSpin = -0.4f, DiskOrbR = 10f;
        const float SingOrbitR = 200f, SingOrbitSquash = 0.7f, SingOrbitSpin = 0.15f, SlingEvery = 0.8f, SlingSpeed = 300f;
        // The event horizon: grows 70 -> 170 px over Spaghettify's first loop; 190 in the last stand. Its pull.
        const float HorizonStart = 70f, HorizonEnd = 170f, HorizonGrow = 20.5f, HorizonLast = 190f;
        const float SpaghettiPull = 60f, LastPull = 110f, WhitePush = 260f;
        public const float ShardOrbit = 220f, ShardSpin = 0.7f;

        // Corner wells
        public const float WellPullRange = 420f;      // players' shots this close bend into a corner well
        public const float WellDriftRange = 480f, WellDrift = 45f;  // and players this close drift toward it (px/s at the well)
        const float WellInset = 110f;

        static readonly SingTurn[][] SingTurns =
        {
            new[] { SingTurn.Pull, SingTurn.Rest, SingTurn.Collapse, SingTurn.Rest, SingTurn.Lasers, SingTurn.Rest },
            new[] { SingTurn.Cage, SingTurn.Rest, SingTurn.Slingshot, SingTurn.Rest, SingTurn.TwinLasers, SingTurn.Rest },
            new[] { SingTurn.Hawking, SingTurn.Rest, SingTurn.WhiteHole, SingTurn.Rest, SingTurn.Split, SingTurn.Rest },
            new[] { SingTurn.Last },
        };
        static readonly float[][] SingTimes =
        {
            new[] { 4f, 1f, 3f, 1f, 8f, 1.5f },
            new[] { 9f, 1f, 6f, 1f, 7f, 1.5f },
            new[] { 5f, 1f, 4f, 1f, 8f, 1.5f },
            new[] { 999f },
        };

        public readonly Dictionary<int, Cage> Cages = new Dictionary<int, Cage>();
        int nextCageId = 1;

        SingState SingOf(Enemy e) => e.Sing ?? (e.Sing = new SingState());

        public static bool SingImmune(Enemy e) => e.Type == EnemyType.Singularity && e.Sing != null && (e.Sing.TransT > 0f || e.Sing.Turn == SingTurn.Split);

        /// How many lasers sweep, and how fast: three slow ones, or two faster ones in Accretion.
        public static int SingLaserCount(Enemy e) => e.Sing != null && e.Sing.Stage == SingStage.Accretion ? 2 : LaserCount;
        public static float SingLaserSpin(Enemy e) => e.Sing != null && e.Sing.Stage == SingStage.Accretion ? TwinLaserSpin : LaserSpin;

        /// Its body for hits and touch: the core, or the event horizon once it's open.
        public static float SingRadius(Enemy e) => Math.Max(e.Def.Radius, e.Sing != null && e.Sing.TransT <= 0f ? e.Sing.Horizon : 0f);

        /// Where one of the disk's orbs is (ring 0 inner, 1 outer); false for a gap or a flung-off slot.
        public static bool DiskOrb(SingState s, V2 center, int ring, int i, out V2 pos)
        {
            int n = ring == 0 ? DiskInnerN : DiskOuterN;
            float r = ring == 0 ? DiskInnerR : DiskOuterR, spin = ring == 0 ? DiskInnerSpin : DiskOuterSpin;
            pos = center + V2.FromAngle(MathX.Tau * i / n + spin * s.DiskT) * r;
            if (i % DiskGapEvery < 2)
                return false;
            return ring == 0 || (s.DiskGone & (1 << i)) == 0;
        }

        public static bool DiskUp(Enemy e) => e.Sing != null && e.Sing.Stage == SingStage.Accretion && e.Sing.TransT <= 0f;

        /// The Singularity's phase for drawing and cues, from its turn.
        static byte SingPhaseOf(SingState s)
        {
            if (s.TransT > 0f) return SingShift;
            switch (s.Turn)
            {
                case SingTurn.Pull: return SingPull;
                case SingTurn.Collapse: return SingCollapse;
                case SingTurn.Lasers: return s.TurnT < LaserWarnAt ? SingNormal : s.TurnT < LaserOnAt ? SingLaserWarn : SingLasers;
                case SingTurn.TwinLasers: return s.TurnT < TwinLaserWarn ? SingLaserWarn : SingLasers;
                case SingTurn.Cage: return SingCage;
                case SingTurn.Slingshot: return SingSlingshot;
                case SingTurn.Hawking: return SingHawking;
                case SingTurn.WhiteHole: return SingWhiteHole;
                case SingTurn.Split: return SingSplit;
                case SingTurn.Last: return SingLast;
                default: return SingRest;
            }
        }

        void SingularitySpecial(Enemy e, float dt)
        {
            var s = SingOf(e);
            if (s.Step < 0)
                EnterSingStage(e, SingStage.EventHorizon);
            byte was = e.Phase;
            float frac = e.MaxHp > 0f ? e.Hp / e.MaxHp : 1f;
            V2 middle = new V2(Width / 2f, Height / 2f);

            if (s.TransT > 0f)
            {
                s.TransT -= dt;
                e.Pos += (middle - e.Pos).LimitLength(420f * dt);
                if (s.TransT <= 0f)
                {
                    s.TransT = 0f;
                    e.Pos = middle;
                    EnterSingStage(e, s.TransTo);
                    StageStartBurst(e, s.Stage == SingStage.Accretion ? new Rgba(0.75f, 0.45f, 1f) : new Rgba(1f, 0.45f, 0.85f));
                }
                e.Phase = SingPhaseOf(s);
                e.Radius = SingRadius(e);
                return;
            }
            if (s.Stage == SingStage.EventHorizon && frac <= SingLockdownAt) { StartSingTransition(e, SingStage.Accretion); return; }
            if (s.Stage == SingStage.Accretion && frac <= SingSpaghettiAt) { StartSingTransition(e, SingStage.Spaghettify); return; }
            if (s.Stage == SingStage.Spaghettify && frac <= SingLastAt && s.Turn != SingTurn.Split)
                EnterSingStage(e, SingStage.LastStand);  // straight away, as the others do

            s.TurnT += dt;
            s.StageT += dt;
            s.DiskT += dt;
            e.Phase = SingPhaseOf(s);

            // Where it is: the middle, or orbiting it in Accretion.
            if (s.Stage == SingStage.Accretion)
            {
                s.Orbit += SingOrbitSpin * dt;
                V2 goal = middle + new V2((float)Math.Cos(s.Orbit), (float)Math.Sin(s.Orbit) * SingOrbitSquash) * SingOrbitR;
                e.Pos += (goal - e.Pos).LimitLength(220f * dt);
            }
            else
                e.Pos += (middle - e.Pos).LimitLength(220f * dt);

            // The event horizon and its pull.
            if (s.Stage == SingStage.Spaghettify)
                s.Horizon = HorizonStart + (HorizonEnd - HorizonStart) * Math.Min(1f, s.StageT / HorizonGrow);
            else if (s.Stage == SingStage.LastStand)
                s.Horizon += (HorizonLast - s.Horizon) * Math.Min(1f, dt * 2f);
            else
                s.Horizon = 0f;
            e.Radius = SingRadius(e);
            float pull = s.Turn == SingTurn.Pull ? SingularityPull : s.Stage == SingStage.Spaghettify ? SpaghettiPull : s.Stage == SingStage.LastStand ? LastPull : 0f;
            if (s.Turn == SingTurn.WhiteHole)
            {
                if (s.TurnT >= 1f && s.TurnT < 2.5f)
                {
                    pull = -WhitePush;
                    if (!s.Reversed)
                    {
                        s.Reversed = true;
                        ReverseEnemyBullets();
                        session.Broadcast(Proto.End(Proto.Begin(Msg.ReverseEnemyBullets)));
                        BroadcastFx(FxKind.Ring, e.Pos, Rgba.White, 700f, 0.6f, Sfx.BossPulse);
                    }
                }
            }
            if (pull != 0f)
            {
                PullCenter = e.Pos;
                PullStrength = pull;
            }

            switch (s.Turn)
            {
                case SingTurn.Lasers:
                case SingTurn.TwinLasers:
                    if (e.Phase == SingLaserWarn && was != SingLaserWarn)
                        e.Facing = Rand(0f, MathX.Tau);  // a new starting angle for each sweep
                    if (e.Phase == SingLasers)
                        e.Facing += SingLaserSpin(e) * dt;
                    break;
                case SingTurn.Slingshot:
                    s.RingT -= dt;
                    if (s.RingT <= 0f)
                    {
                        s.RingT = SlingEvery;
                        Slingshot(e, s);
                    }
                    break;
                case SingTurn.Split:
                    for (int k = 0; k < 3; k++)
                        foreach (var o in Enemies.Values)
                            if (o.Type == EnemyType.SingShard && o.ParentId == e.Id && (int)o.Ang == k)
                                o.Pos = e.Pos + V2.FromAngle(s.TurnT * ShardSpin + MathX.Tau * k / 3f) * ShardOrbit;
                    break;
            }
            // Flung-off orbs come back to the disk, one every 0.5 s, outside the Slingshot.
            if (s.DiskGone != 0 && s.Turn != SingTurn.Slingshot)
            {
                s.RefillT -= dt;
                if (s.RefillT <= 0f)
                {
                    s.RefillT = 0.5f;
                    s.DiskGone &= s.DiskGone - 1;
                }
            }
            if (s.Stage != SingStage.LastStand && s.TurnT >= s.TurnLen)
                NextSingTurn(e, s);
        }

        void EnterSingStage(Enemy e, SingStage stage)
        {
            var s = SingOf(e);
            s.Stage = stage;
            s.Step = -1;
            s.StageT = 0f;
            s.DiskGone = 0;
            s.Orbit = 0f;
            if (stage != SingStage.LastStand)
                SpawnCornerWells(e);  // four fresh ones each stage; killed, they stay gone until the next
            if (stage == SingStage.LastStand)
                ClearShards();
            NextSingTurn(e, s);
            DoLog("the Singularity: " + stage);
        }

        void StartSingTransition(Enemy e, SingStage to)
        {
            var s = SingOf(e);
            s.TransT = SingTransTime;
            s.TransTo = to;
            ClearCages();
            ClearShards();
            ClearCornerWells();
            ClearEnemyBullets();
            session.Broadcast(Proto.End(Proto.Begin(Msg.ClearEnemyBullets)));
            BroadcastFx(FxKind.Ring, e.Pos, to == SingStage.Accretion ? new Rgba(0.75f, 0.45f, 1f) : new Rgba(1f, 0.45f, 0.85f), 900f, 0.8f, Sfx.BossChime);
            DoLog("the Singularity: changing to " + to);
        }

        void NextSingTurn(Enemy e, SingState s)
        {
            var turns = SingTurns[(int)s.Stage];
            if (s.Turn == SingTurn.Cage)
                ClearCages();
            if (s.Turn == SingTurn.Split)
                ClearShards();
            s.Step = (s.Step + 1) % turns.Length;
            s.Turn = turns[s.Step];
            s.TurnLen = SingTimes[(int)s.Stage][s.Step];
            s.TurnT = 0f;
            s.RingT = 0f;
            s.Reversed = false;
            e.FireT = 0.3f;
            if (s.Turn == SingTurn.Cage)
                SpawnCages(e);
            if (s.Turn == SingTurn.Split)
                for (int k = 0; k < 3; k++)
                {
                    var sh = SpawnEnemyAt(EnemyType.SingShard, e.Pos + V2.FromAngle(MathX.Tau * k / 3f) * ShardOrbit, 0.3f, e.Id);
                    sh.Ang = k;
                    BroadcastFx(FxKind.Ring, sh.Pos, sh.Def.Color, 50f, 0.4f, Sfx.None);
                }
            e.Phase = SingPhaseOf(s);
        }

        /// The four corner wells, fresh: any left from the last stage go first.
        void SpawnCornerWells(Enemy e)
        {
            ClearCornerWells();
            var corners = new[] { new V2(WellInset, WellInset), new V2(Width - WellInset, WellInset), new V2(WellInset, Height - WellInset), new V2(Width - WellInset, Height - WellInset) };
            foreach (var c in corners)
            {
                var w = SpawnEnemyAt(EnemyType.GravityWell, c, 0.8f, e.Id);
                w.Waypoint = c;
                BroadcastFx(FxKind.Ring, c, Defs.Get(EnemyType.GravityWell).Color, 120f, 0.6f, Sfx.None);
            }
            Sounds.Add(Sfx.Summon);
        }

        void ClearCornerWells() => RemoveAll(EnemyType.GravityWell);
        void ClearShards() => RemoveAll(EnemyType.SingShard);

        void RemoveAll(EnemyType type)
        {
            scratchIds.Clear();
            foreach (var o in Enemies.Values)
                if (o.Type == type)
                    scratchIds.Add(o.Id);
            foreach (int id in scratchIds)
                Enemies.Remove(id);  // they wink out (no kill, no reward)
        }

        /// Three orbs leave the outer ring at the players, from the slots nearest them.
        void Slingshot(Enemy e, SingState s)
        {
            int shots = 0;
            var targets = new List<Player>();
            foreach (var p in Players.Values)
                if (!p.Downed)
                    targets.Add(p);
            if (targets.Count == 0)
                return;
            for (int k = 0; k < 3; k++)
            {
                var p = targets[k % targets.Count];
                int best = -1;
                float bestD = float.MaxValue;
                V2 bestPos = V2.Zero;
                for (int i = 0; i < DiskOuterN; i++)
                {
                    V2 at;
                    if (!DiskOrb(s, e.Pos, 1, i, out at))
                        continue;
                    float d = at.DistanceSq(p.Pos);
                    if (d < bestD) { bestD = d; best = i; bestPos = at; }
                }
                if (best < 0)
                    break;
                s.DiskGone |= 1 << best;
                V2 dir = (p.Pos - bestPos).Normalized();
                SpawnBullet(Defs.BigShot, bestPos, dir * SlingSpeed, 0f, 6f, Defs.BulletDamage(Defs.BigShot), 0, true).Source = e.Type;
                shots++;
            }
            if (shots > 0)
                BroadcastFx(FxKind.Ring, e.Pos, new Rgba(0.85f, 0.35f, 1f), DiskOuterR + 20f, 0.3f, Sfx.Throw);
        }

        /// White hole: every enemy bullet on screen turns round (each game does it; the host says when).
        void ReverseEnemyBullets()
        {
            foreach (var b in Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind))
                {
                    b.Vel = -b.Vel;
                    b.AngVel = -b.AngVel;
                }
        }

        /// A shard hit: the damage goes to the core (which can't be hurt directly while they're out).
        bool ShardPassesHit(Enemy shard, float dmg, int attacker)
        {
            if (shard.Type != EnemyType.SingShard)
                return false;
            Enemy core;
            if (Enemies.TryGetValue(shard.ParentId, out core) && core.Hp > 0f && core.Sing != null && core.Sing.TransT <= 0f)
            {
                shardPass = true;
                HurtEnemy(core, dmg, attacker);
                shardPass = false;
            }
            return true;
        }
        bool shardPass;

        void SingularityFire(Enemy e, float aim)
        {
            var s = SingOf(e);
            if (s.TransT > 0f)
            {
                e.FireT = 0.2f;
                return;
            }
            switch (s.Turn)
            {
                case SingTurn.Pull:  // gravity pull: slow three-armed spiral
                    e.FireT = 0.13f;
                    e.Ang += 0.21f;
                    for (int k = 0; k < 3; k++)
                        EnemyShot(e, e.Ang + MathX.Tau * k / 3f, 110f, Defs.SmallShot);
                    break;
                case SingTurn.Collapse:  // rings, each turned half a gap
                    e.FireT = 0.9f;
                    e.Ang += MathX.Pi / 26f;
                    for (int i = 0; i < 26; i++)
                        EnemyShot(e, e.Ang + MathX.Tau * i / 26f, 170f, Defs.SmallShot);
                    break;
                case SingTurn.Lasers:  // the purple spiral (the lasers are separate)
                    e.FireT = 0.65f;
                    e.Ang += 0.3f;
                    for (int d = 0; d < 4; d++)
                        EnemyShot(e, e.Ang + MathX.Tau * d / 4f, 150f, Defs.BigShot);
                    break;
                case SingTurn.Hawking:  // radiation off the horizon's edge
                    e.FireT = 0.6f;
                    e.Ang += 0.17f;
                    for (int i = 0; i < 18; i++)
                        EnemyShot(e, e.Ang + MathX.Tau * i / 18f, 160f, Defs.SmallShot);
                    break;
                case SingTurn.Split:  // each shard a two-armed spiral
                    e.FireT = 0.22f;
                    e.Ang += 0.35f;
                    foreach (var o in Enemies.Values)
                        if (o.Type == EnemyType.SingShard && o.ParentId == e.Id && !IsSpawning(o))
                            for (int k = 0; k < 2; k++)
                            {
                                V2 dir = V2.FromAngle(e.Ang + o.Ang * 2.1f + MathX.Pi * k);
                                SpawnBullet(Defs.SmallShot, o.Pos + dir * o.Radius, dir * 130f, 0f, 8f, Defs.BulletDamage(Defs.SmallShot), 0, true).Source = e.Type;
                            }
                    break;
                case SingTurn.Last:  // a three-armed spiral and a ring of big orbs every 1.6 s
                    e.FireT = 0.1f;
                    e.Ang += 0.19f;
                    for (int k = 0; k < 3; k++)
                        EnemyShot(e, e.Ang + MathX.Tau * k / 3f, 150f, Defs.SmallShot);
                    s.RingT -= 0.1f;
                    if (s.RingT <= 0f)
                    {
                        s.RingT = 1.6f;
                        for (int i = 0; i < 18; i++)
                            EnemyShot(e, e.Ang * 0.5f + MathX.Tau * i / 18f, 110f, Defs.BigShot);
                    }
                    break;
                default:  // it holds fire (the cage, the slingshot, the white hole, breathers)
                    e.FireT = 0.2f;
                    break;
            }
        }

        void SpawnCages(Enemy boss)
        {
            ClearCages();
            foreach (var p in Players.Values)
            {
                if (p.Downed)
                    continue;
                var cage = new Cage { Id = nextCageId++, PlayerId = p.Id, Center = p.Pos, Radius = CageStartRadius, Spin = Rand(0f, MathX.Tau) };
                for (int i = 0; i < CageWellsEach; i++)
                {
                    var w = SpawnEnemyAt(EnemyType.CageWell, CagePoint(cage, i), 0.5f, boss.Id);
                    w.Ang = MathX.Tau * i / CageWellsEach;
                    cage.Wells.Add(w.Id);
                }
                Cages[cage.Id] = cage;
                BroadcastFx(FxKind.Ring, cage.Center, Defs.Get(EnemyType.CageWell).Color, CageStartRadius, 0.5f, Sfx.None);
            }
            Sounds.Add(Sfx.Summon);
        }

        static V2 CagePoint(Cage c, int i) => c.Center + V2.FromAngle(c.Spin + MathX.Tau * i / CageWellsEach) * c.Radius;

        void ClearCages()
        {
            foreach (var c in Cages.Values)
                foreach (int id in c.Wells)
                {
                    Enemy w;
                    if (Enemies.TryGetValue(id, out w))
                        Enemies.Remove(id);  // they wink out (no kill, no reward)
                }
            Cages.Clear();
        }

        /// Host: lasers and cage walls hurt, cages shrink and spin, and a cage with no wells left breaks.
        void TickSingularityHazards(float dt)
        {
            Enemy sing = null;
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Singularity && !IsSpawning(e))
                    sing = e;
            if (sing == null)
            {
                if (Cages.Count > 0)
                    ClearCages();
                return;
            }
            float dmg = 14f + Wave * 0.4f;
            int lasers = SingLaserCount(sing);
            if (sing.Phase == SingLasers)
                foreach (var p in Players.Values)
                {
                    if (p.Downed || p.InvulnT > 0f)
                        continue;
                    for (int k = 0; k < lasers; k++)
                        if (OnLaser(sing.Pos, sing.Facing + MathX.Tau * k / lasers, p.Pos, LaserWidth / 2f + p.Def.HitR))
                        {
                            Damage(p, dmg, "The Singularity (laser)");
                            break;
                        }
                }
            if (DiskUp(sing))  // the accretion disk's orbs hurt like big orbs
                foreach (var p in Players.Values)
                {
                    if (p.Downed || p.InvulnT > 0f)
                        continue;
                    float reach = DiskOrbR + p.Def.HitR, d = p.Pos.DistanceTo(sing.Pos);
                    if (Math.Abs(d - DiskInnerR) > reach && Math.Abs(d - DiskOuterR) > reach)
                        continue;
                    bool hit = false;
                    for (int ring = 0; ring < 2 && !hit; ring++)
                        for (int i = 0; i < (ring == 0 ? DiskInnerN : DiskOuterN) && !hit; i++)
                        {
                            V2 at;
                            hit = DiskOrb(sing.Sing, sing.Pos, ring, i, out at) && at.DistanceTo(p.Pos) < reach;
                        }
                    if (hit)
                        Damage(p, Defs.BulletDamage(Defs.BigShot), "The Singularity (accretion disk)");
                }

            var broken = new List<int>();
            foreach (var c in Cages.Values)
            {
                c.Wells.RemoveAll(id => !Enemies.ContainsKey(id) || Enemies[id].Hp <= 0f);
                if (c.Wells.Count == 0)
                {
                    broken.Add(c.Id);
                    BroadcastFx(FxKind.Ring, c.Center, Defs.Get(EnemyType.CageWell).Color, c.Radius + 20f, 0.4f, Sfx.Hit);
                    continue;
                }
                c.Radius = Math.Max(0f, c.Radius - CageShrink * dt);
                c.Spin += CageSpin * dt;
                for (int i = 0; i < c.Wells.Count; i++)
                {
                    var w = Enemies[c.Wells[i]];
                    w.Pos = c.Center + V2.FromAngle(c.Spin + w.Ang) * c.Radius;
                }
                foreach (var p in Players.Values)
                    if (!p.Downed && p.InvulnT <= 0f && Math.Abs(p.Pos.DistanceTo(c.Center) - c.Radius) < CageWallWidth / 2f + p.Def.HitR)
                        Damage(p, dmg, "The Singularity (cage)");
            }
            foreach (int id in broken)
                Cages.Remove(id);
        }

        /// Is pos within `reach` of a laser fired from `from` at angle `ang` (it runs to the arena's edge)?
        public static bool OnLaser(V2 from, float ang, V2 pos, float reach)
        {
            V2 dir = V2.FromAngle(ang);
            V2 to = pos - from;
            float along = to.X * dir.X + to.Y * dir.Y;
            if (along < 0f)
                return false;
            return Math.Abs(to.X * dir.Y - to.Y * dir.X) < reach;
        }

        /// The corner wells drift players slowly toward them (applied on each player's own game).
        V2 WellDriftAt(V2 pos)
        {
            V2 drift = V2.Zero;
            foreach (var e in Enemies.Values)
            {
                if (e.Type != EnemyType.GravityWell || IsSpawning(e))
                    continue;
                float d = e.Pos.DistanceTo(pos);
                if (d < WellDriftRange && d > 1f)
                    drift += (e.Pos - pos) / d * WellDrift * (1f - d / WellDriftRange);
            }
            return drift;
        }

        void SyncSing(NetSync s, Enemy e)
        {
            bool has = e.Sing != null;
            s.Bool(ref has);
            if (!has)
                return;
            var q = e.Sing ?? (e.Sing = new SingState { Step = 0 });
            int stage = (int)q.Stage, turn = (int)q.Turn, transTo = (int)q.TransTo;
            s.Byte(ref stage);
            s.Byte(ref turn);
            s.Byte(ref transTo);
            s.Float(ref q.TurnT);
            s.Float(ref q.TransT);
            s.Float(ref q.Horizon);
            s.Float(ref q.DiskT);
            s.Int(ref q.DiskGone);
            if (s.Reading)
            {
                q.Stage = (SingStage)stage;
                q.Turn = (SingTurn)turn;
                q.TransTo = (SingStage)transTo;
            }
        }

        /// Clients, each frame: the disk keeps turning and the turn clock runs between snapshots.
        void UpdateSingView(float dt)
        {
            if (IsHost)
                return;
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Singularity && e.Sing != null)
                {
                    e.Sing.DiskT += dt;
                    e.Sing.TurnT += dt;
                }
        }

        void SyncCages(NetSync s)
        {
            int n = Cages.Count;
            s.Byte(ref n);
            if (s.Reading)
            {
                Cages.Clear();
                for (int i = 0; i < n; i++)
                {
                    var c = new Cage();
                    SyncCage(s, c);
                    Cages[c.Id] = c;
                }
            }
            else
                foreach (var c in Cages.Values)
                    SyncCage(s, c);
        }

        static void SyncCage(NetSync s, Cage c)
        {
            s.Int(ref c.Id);
            s.Int(ref c.PlayerId);
            s.Vec(ref c.Center);
            s.Float(ref c.Radius);
        }
    }
}
