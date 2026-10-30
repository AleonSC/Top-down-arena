using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Storm Herald's stage of the fight, by its health: three stages, then a last stand.
    public enum HeraldStage : byte { Gathering, Gale, Supercell, LastStand }

    /// What it's doing now. Each stage loops its turns, one attack at a time, with breathers (Rest) between.
    public enum HeraldTurn : byte { Rest, Strikes, Rods, Thunderclap, Crosswind, BallLightning, Downburst, StormFront, StrikeRain, RodCage, Eye }

    /// A lightning rod: falls for `Fall` seconds (a marked spot), then stands until the turn ends.
    public class HeraldRod { public V2 Pos; public float Fall; }

    /// An arc between two rods: flickers for ArcWarn seconds (T counts up), then fires for ArcLive.
    public class HeraldArc { public int A, B; public float T, Warn; }

    /// Everything about the Storm Herald's fight its body doesn't carry. The host runs it; clients get it in the
    /// snapshot (SyncHerald) to draw the rods, arcs, wind, the Conductor and the spokes, and to push their own player
    /// and bend their bullets with the wind.
    public class HeraldState
    {
        public HeraldStage Stage, TransTo;
        public HeraldTurn Turn;
        public float TurnT, TransT;  // seconds into the turn; > 0: changing stage (can't be hurt), counting down
        public int Wind;             // the Gale's wind: -1 west, +1 east, 0 still
        public float Spin;           // the last stand's spokes
        public int CondId;           // the Supercell's Conductor (a player id, 0 for none) and seconds left on the mark
        public float CondT;
        public readonly List<HeraldRod> Rods = new List<HeraldRod>();
        public readonly List<HeraldArc> Arcs = new List<HeraldArc>();

        // Host only.
        public int Step = -1, Fired, FrontSide = -1, FrontStep, FrontGap, CageOpen, CondNext;
        public float TurnLen, ActT, CondWait;
    }

    /// The Storm Herald (wave 40): a fight in three stages by its health, like the Warden's, then a last stand.
    ///   Gathering Storm (100-65%): it drifts along the top; Strikes (as before), Lightning rods (arcs between rods
    ///     around you), Thunderclap (rings with two gaps). Its piercing volley in the breathers.
    ///   Gale (65-30%): the wind blows across the arena all stage, turning round each turn: it pushes players and
    ///     bends every bullet. Crosswind (volleys aimed upwind), Ball lightning (slow orbs that arc to each other),
    ///     Downburst (rows of big strikes at your height).
    ///   Supercell (30-10%): the Conductor (one player at a time carries a charge that jumps to allies near them),
    ///     Storm front (a sweeping wall of strikes with a gap), Strike rain, Rod cage (rods ringed round you, one open
    ///     side).
    ///   Last stand (below 10%): in the middle, four lightning spokes turning, strikes on everyone, its volley.
    /// Built from the mockup in Docs/Mockups/storm-herald-3-stage.html. Drawn in HeraldLook.cs.
    public partial class Game
    {
        const float HeraldGaleAt = 0.65f, HeraldSupercellAt = 0.3f, HeraldLastAt = 0.1f, HeraldTransTime = 2f;
        public const float StrikeRadius = 70f, StrikeWarn = 1.1f;
        public const float RodFall = 1f, ArcWarn = 0.6f, CageArcWarn = 0.5f, ArcLive = 0.25f, RodArcWidth = 16f;
        public const float WindPush = 70f, WindDrift = 60f, BallWind = 40f, BallSpeed = 80f, BallLife = 6f, BallArcRange = 200f, BallArcWidth = 12f;
        public const float ConductorRadius = 180f, ConductorTime = 3f;
        const float ConductorEvery = 8f, ConductorFirst = 3f;
        public const float SpokeLength = 700f, SpokeWidth = 18f, SpokeSpin = 20f * MathX.Pi / 180f;
        const float CageRadius = 240f;

        static readonly HeraldTurn[][] HeraldTurns =
        {
            new[] { HeraldTurn.Strikes, HeraldTurn.Rest, HeraldTurn.Rods, HeraldTurn.Rest, HeraldTurn.Thunderclap, HeraldTurn.Rest },
            new[] { HeraldTurn.Crosswind, HeraldTurn.Rest, HeraldTurn.BallLightning, HeraldTurn.Rest, HeraldTurn.Downburst, HeraldTurn.Rest },
            new[] { HeraldTurn.StormFront, HeraldTurn.Rest, HeraldTurn.StrikeRain, HeraldTurn.Rest, HeraldTurn.RodCage, HeraldTurn.Rest },
            new[] { HeraldTurn.Eye },
        };
        static readonly float[][] HeraldTimes =
        {
            new[] { 6f, 1f, 7f, 1f, 5f, 1.5f },
            new[] { 6f, 1f, 7f, 1f, 6f, 1.5f },
            new[] { 7f, 1f, 6f, 1f, 7f, 1.5f },
            new[] { 999f },
        };

        HeraldState HeraldOf(Enemy e) => e.Herald ?? (e.Herald = new HeraldState());

        public static bool HeraldImmune(Enemy e) => e.Type == EnemyType.StormHerald && e.Herald != null && e.Herald.TransT > 0f;

        /// Arcs, spokes and the Conductor's discharge: a little more each wave (30 at wave 40).
        float ArcDamage => 18f + Wave * 0.3f;

        /// The Gale's wind as a velocity (px/s across the arena) at full strength `strength`, or zero.
        public V2 HeraldWind(float strength)
        {
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.StormHerald && e.Herald != null && e.Herald.Wind != 0 && e.Herald.TransT <= 0f && e.Hp > 0f)
                    return new V2(e.Herald.Wind * strength, 0f);
            return V2.Zero;
        }

        /// Where the herald wants to be: drifting along the top as it always has, in the middle for the last stand.
        V2 HeraldGoal(Enemy e)
        {
            if (e.Herald != null && e.Herald.Stage == HeraldStage.LastStand)
                return new V2(Width / 2f, Height / 2f);
            return new V2(Width / 2f + (float)Math.Sin(e.T * 0.45f) * Width * 0.3f, 150f + (float)Math.Sin(e.T * 0.7f) * 50f);
        }

        void HeraldSpecial(Enemy e, V2 tpos, float dt)
        {
            var s = HeraldOf(e);
            if (s.Step < 0)
                EnterHeraldStage(e, HeraldStage.Gathering);
            float frac = e.MaxHp > 0f ? e.Hp / e.MaxHp : 1f;

            if (s.TransT > 0f)
            {
                s.TransT -= dt;
                if (s.TransT <= 0f)
                {
                    s.TransT = 0f;
                    EnterHeraldStage(e, s.TransTo);
                    StageStartBurst(e, HeraldColor(s.Stage));
                }
                return;
            }
            if (s.Stage == HeraldStage.Gathering && frac <= HeraldGaleAt) { StartHeraldTransition(e, HeraldStage.Gale); return; }
            if (s.Stage == HeraldStage.Gale && frac <= HeraldSupercellAt) { StartHeraldTransition(e, HeraldStage.Supercell); return; }
            if (s.Stage == HeraldStage.Supercell && frac <= HeraldLastAt)
                EnterHeraldStage(e, HeraldStage.LastStand);  // straight away, as the others do

            s.TurnT += dt;
            if (s.Stage == HeraldStage.LastStand)
                s.Spin += SpokeSpin * dt;
            TickRodsAndArcs(s, dt);
            if (s.Stage == HeraldStage.Supercell)
                TickConductor(e, s, dt);

            Player target;
            Players.TryGetValue(e.TargetId, out target);
            if (target == null || target.Downed)
                target = FirstAlive();
            V2 at = target != null ? target.Pos : tpos;

            s.ActT -= dt;
            if (s.ActT <= 0f)
                switch (s.Turn)
                {
                    case HeraldTurn.Strikes:
                        s.ActT = 1.8f;
                        StrikeEveryone(StrikeRadius, StrikeWarn);
                        for (int i = 0; i < 2; i++)
                            AddHazard(RandomInArena(80f), StrikeRadius, StrikeWarn);
                        Sounds.Add(Sfx.BossCharge);
                        break;
                    case HeraldTurn.Rods:
                        s.ActT = 1.4f;
                        if (s.Fired == 0)
                        {
                            s.Rods.Clear();
                            s.Arcs.Clear();
                            AddRod(s, at + new V2(-300f, Rand(-60f, 60f)));
                            AddRod(s, at + new V2(300f, Rand(-60f, 60f)));
                            AddRod(s, at + new V2(Rand(-60f, 60f), -230f));
                            AddRod(s, at + new V2(Rand(-60f, 60f), 230f));
                        }
                        else
                        {
                            int pair = s.Fired % 3;  // one pair, the other, then both
                            if (pair != 1) AddArc(s, 0, 1, ArcWarn);
                            if (pair != 0) AddArc(s, 2, 3, ArcWarn);
                        }
                        s.Fired++;
                        break;
                    case HeraldTurn.BallLightning:
                        s.ActT = 3.5f;
                        for (int k = 0; k < 3; k++)
                        {
                            var b = SpawnEnemyAt(EnemyType.BallLightning, ClampToArena(e.Pos + new V2((k - 1) * 260f, 70f), 20f), 0.3f, e.Id);
                            BroadcastFx(FxKind.Ring, b.Pos, b.Def.Color, 40f, 0.3f, Sfx.None);
                        }
                        Sounds.Add(Sfx.SpellZap);
                        break;
                    case HeraldTurn.Downburst:
                    {
                        s.ActT = 2f;
                        int from = s.Wind > 0 ? 0 : 5;
                        float y = Math.Max(60f, Math.Min(Height - 60f, at.Y));
                        for (int i = 0; i < 6; i++)
                            AddHazard(new V2(150f + i * 260f, y), 110f, 0.8f + 0.25f * Math.Abs(i - from));
                        Sounds.Add(Sfx.BossCharge);
                        break;
                    }
                    case HeraldTurn.StormFront:
                    {
                        s.ActT = 0.5f;
                        float x = s.FrontSide > 0 ? 70f + s.FrontStep * 110f : Width - 70f - s.FrontStep * 110f;
                        if (x > 30f && x < Width - 30f)
                        {
                            for (int i = 0; i < 7; i++)
                                if (i != s.FrontGap && i != s.FrontGap + 1)
                                    AddHazard(new V2(x, 70f + i * 127f), 65f, 0.7f);
                            s.FrontGap = Math.Max(0, Math.Min(5, s.FrontGap + (rng.NextDouble() < 0.5 ? -1 : 1)));
                        }
                        s.FrontStep++;
                        break;
                    }
                    case HeraldTurn.StrikeRain:
                    {
                        s.ActT = 0.6f;
                        for (int i = 0; i < 3; i++)
                            AddHazard(RandomInArena(80f), 50f, 0.9f);
                        var alive = AlivePlayersList();
                        if (alive.Count > 0)
                            AddHazard(alive[rng.Next(alive.Count)].Pos, 50f, 0.9f);
                        break;
                    }
                    case HeraldTurn.RodCage:
                        if (s.Fired == 0 || (s.Fired == 1 && s.TurnT >= 3.5f))
                        {
                            s.Fired++;
                            s.ActT = RodFall;
                            s.Rods.Clear();
                            s.Arcs.Clear();
                            s.CageOpen = rng.Next(6);
                            for (int k = 0; k < 6; k++)
                                AddRod(s, at + V2.FromAngle(MathX.Tau * k / 6f + Rand(-0.1f, 0.1f)) * CageRadius);
                        }
                        else
                        {
                            s.ActT = 1.2f;
                            for (int k = 0; k < 6; k++)
                                if (k != s.CageOpen)
                                    AddArc(s, k, (k + 1) % 6, CageArcWarn);
                        }
                        break;
                    case HeraldTurn.Eye:
                        s.ActT = 2f;
                        StrikeEveryone(StrikeRadius, StrikeWarn);
                        break;
                    default:
                        s.ActT = 0.1f;
                        break;
                }
            if (s.Stage != HeraldStage.LastStand && s.TurnT >= s.TurnLen)
                NextHeraldTurn(e, s);
        }

        static Rgba HeraldColor(HeraldStage stage) => stage == HeraldStage.Gale ? new Rgba(0.5f, 0.7f, 1f)
            : stage == HeraldStage.Supercell ? new Rgba(0.66f, 0.55f, 1f) : stage == HeraldStage.LastStand ? new Rgba(0.83f, 0.55f, 1f) : new Rgba(0.56f, 0.82f, 1f);

        Player FirstAlive()
        {
            foreach (var p in Players.Values)
                if (!p.Downed)
                    return p;
            return null;
        }

        List<Player> AlivePlayersList()
        {
            var list = new List<Player>();
            foreach (var p in Players.Values)
                if (!p.Downed)
                    list.Add(p);
            return list;
        }

        void StrikeEveryone(float radius, float warn)
        {
            foreach (var p in Players.Values)
                if (!p.Downed)
                    AddHazard(p.Pos, radius, warn);
        }

        void AddRod(HeraldState s, V2 pos) => s.Rods.Add(new HeraldRod { Pos = ClampToArena(pos, 40f), Fall = RodFall });

        static void AddArc(HeraldState s, int a, int b, float warn)
        {
            if (a < s.Rods.Count && b < s.Rods.Count)
                s.Arcs.Add(new HeraldArc { A = a, B = b, Warn = warn });
        }

        public static bool ArcLiveNow(HeraldArc a) => a.T >= a.Warn && a.T < a.Warn + ArcLive;

        /// Host: rods land, arcs flicker then fire (hurting anyone on the line once as they fire), and spent arcs go.
        void TickRodsAndArcs(HeraldState s, float dt)
        {
            foreach (var r in s.Rods)
                if (r.Fall > 0f)
                {
                    r.Fall -= dt;
                    if (r.Fall <= 0f)
                        BroadcastFx(FxKind.Ring, r.Pos, new Rgba(0.56f, 0.82f, 1f), 40f, 0.3f, Sfx.Hit);
                }
            foreach (var a in s.Arcs)
            {
                bool was = ArcLiveNow(a);
                a.T += dt;
                if (!was && ArcLiveNow(a))
                {
                    V2 pa = s.Rods[a.A].Pos, pb = s.Rods[a.B].Pos;
                    BroadcastFx(FxKind.Bolt, pa, pb, new Rgba(0.8f, 0.92f, 1f), 0f, ArcLive, Sfx.SpellZap);
                    foreach (var p in Players.Values)
                        if (!p.Downed && p.InvulnT <= 0f && SegmentDistance(p.Pos, pa, pb) < RodArcWidth / 2f + p.Def.HitR)
                            Damage(p, ArcDamage, "Storm Herald (lightning rods)");
                }
            }
            s.Arcs.RemoveAll(a => a.T >= a.Warn + ArcLive);
        }

        /// The Supercell's Conductor: one player at a time (taking turns) is marked for 3 s; when it runs out, lightning
        /// jumps from them to every ally inside the ring (hurting both ends), and 10 small shots burst out from them.
        /// Alone, or with everyone clear, it only grounds into the shots.
        void TickConductor(Enemy e, HeraldState s, float dt)
        {
            if (s.CondId != 0)
            {
                s.CondT -= dt;
                Player c;
                if (!Players.TryGetValue(s.CondId, out c) || c.Downed)
                {
                    s.CondId = 0;
                    s.CondWait = 1f;
                    return;
                }
                if (s.CondT > 0f)
                    return;
                var col = new Rgba(0.83f, 0.6f, 1f);
                bool jumped = false;
                foreach (var q in Players.Values)
                    if (q != c && !q.Downed && q.Pos.DistanceTo(c.Pos) < ConductorRadius)
                    {
                        jumped = true;
                        BroadcastFx(FxKind.Bolt, c.Pos, q.Pos, col, 0f, 0.3f, Sfx.None);
                        if (q.InvulnT <= 0f)
                            Damage(q, ArcDamage, "Storm Herald (Conductor)");
                    }
                BroadcastFx(FxKind.Bolt, new V2(c.Pos.X + Rand(-60f, 60f), -20f), c.Pos, col, 0f, 0.3f, Sfx.SpellZap);
                BroadcastFx(FxKind.Ring, c.Pos, col, ConductorRadius, 0.4f, Sfx.None);
                if (jumped && c.InvulnT <= 0f)  // the charge only hurts its carrier when it finds someone to jump to
                    Damage(c, ArcDamage, "Storm Herald (Conductor)");
                for (int i = 0; i < 10; i++)
                {
                    V2 dir = V2.FromAngle(MathX.Tau * i / 10f);
                    SpawnBullet(Defs.SmallShot, c.Pos + dir * 24f, dir * 150f, 0f, 8f, Defs.BulletDamage(Defs.SmallShot), 0, true).Source = e.Type;
                }
                s.CondId = 0;
                s.CondWait = ConductorEvery - ConductorTime;
                return;
            }
            s.CondWait -= dt;
            if (s.CondWait > 0f)
                return;
            // The next player in turn.
            var alive = AlivePlayersList();
            if (alive.Count == 0)
                return;
            alive.Sort((a, b) => a.Id.CompareTo(b.Id));
            var next = alive[s.CondNext % alive.Count];
            s.CondNext++;
            s.CondId = next.Id;
            s.CondT = ConductorTime;
            BroadcastFx(FxKind.Ring, next.Pos, new Rgba(0.83f, 0.6f, 1f), ConductorRadius, 0.5f, Sfx.BossCharge);
        }

        /// Host, every tick while the herald is up: ball lightning drifts and arcs, and the spokes hurt.
        void TickHerald(float dt)
        {
            Enemy herald = null;
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.StormHerald && !IsSpawning(e) && e.Hp > 0f)
                    herald = e;
            // Ball lightning: drifts at the nearest player on the wind, pops after 6 s.
            var balls = new List<Enemy>();
            foreach (var b in Enemies.Values)
                if (b.Type == EnemyType.BallLightning && !IsSpawning(b) && b.Hp > 0f)
                    balls.Add(b);
            V2 wind = HeraldWind(BallWind);
            foreach (var b in balls)
            {
                Player near = null;
                foreach (var p in Players.Values)
                    if (!p.Downed && (near == null || p.Pos.DistanceSq(b.Pos) < near.Pos.DistanceSq(b.Pos)))
                        near = p;
                if (near != null)
                    b.Pos += (near.Pos - b.Pos).Normalized() * BallSpeed * dt;
                b.Pos = ClampToArena(b.Pos + wind * dt, b.Radius);
                if (b.T >= BallLife + 0.3f)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        V2 dir = V2.FromAngle(MathX.Tau * i / 8f + b.Id);
                        SpawnBullet(Defs.SmallShot, b.Pos + dir * b.Radius, dir * 140f, 0f, 8f, Defs.BulletDamage(Defs.SmallShot), 0, true).Source = EnemyType.StormHerald;
                    }
                    BroadcastFx(FxKind.Ring, b.Pos, b.Def.Color, 50f, 0.3f, Sfx.SpellZap);
                    b.Hp = 0f;
                    Enemies.Remove(b.Id);  // it pops (no kill, no reward)
                }
            }
            balls.RemoveAll(b => b.Hp <= 0f);
            for (int i = 0; i < balls.Count; i++)
                for (int j = i + 1; j < balls.Count; j++)
                {
                    V2 a = balls[i].Pos, c = balls[j].Pos;
                    if (a.DistanceTo(c) >= BallArcRange)
                        continue;
                    foreach (var p in Players.Values)
                        if (!p.Downed && p.InvulnT <= 0f && SegmentDistance(p.Pos, a, c) < BallArcWidth / 2f + p.Def.HitR)
                            Damage(p, 20f, "Storm Herald (ball lightning)");
                }
            if (herald == null || herald.Herald == null)
                return;
            // The last stand's spokes.
            var s = herald.Herald;
            if (s.Stage == HeraldStage.LastStand && s.TransT <= 0f)
                foreach (var p in Players.Values)
                {
                    if (p.Downed || p.InvulnT > 0f)
                        continue;
                    for (int k = 0; k < 4; k++)
                    {
                        V2 tip = herald.Pos + V2.FromAngle(s.Spin + k * MathX.Pi / 2f) * SpokeLength;
                        if (SegmentDistance(p.Pos, herald.Pos, tip) < SpokeWidth / 2f + p.Def.HitR)
                        {
                            Damage(p, ArcDamage, "Storm Herald (eye of the storm)");
                            break;
                        }
                    }
                }
        }

        void EnterHeraldStage(Enemy e, HeraldStage stage)
        {
            var s = HeraldOf(e);
            s.Stage = stage;
            s.Step = -1;
            s.Wind = stage == HeraldStage.Gale ? -1 : 0;  // the first turn turns it round to blow east
            s.CondId = 0;
            s.CondWait = ConductorFirst;
            s.Rods.Clear();
            s.Arcs.Clear();
            if (stage == HeraldStage.LastStand)
                ClearBallLightning();
            NextHeraldTurn(e, s);
            DoLog("the Storm Herald: " + stage);
        }

        void StartHeraldTransition(Enemy e, HeraldStage to)
        {
            var s = HeraldOf(e);
            s.TransT = HeraldTransTime;
            s.TransTo = to;
            s.Wind = 0;
            s.CondId = 0;
            s.Rods.Clear();
            s.Arcs.Clear();
            ClearBallLightning();
            Hazards.Clear();
            ClearEnemyBullets();
            session.Broadcast(Proto.End(Proto.Begin(Msg.ClearEnemyBullets)));
            BroadcastFx(FxKind.Ring, e.Pos, HeraldColor(to), 900f, 0.8f, Sfx.BossChime);
            DoLog("the Storm Herald: changing to " + to);
        }

        void NextHeraldTurn(Enemy e, HeraldState s)
        {
            var turns = HeraldTurns[(int)s.Stage];
            s.Step = (s.Step + 1) % turns.Length;
            s.Turn = turns[s.Step];
            s.TurnLen = HeraldTimes[(int)s.Stage][s.Step];
            s.TurnT = 0f;
            s.ActT = 0.3f;
            s.Fired = 0;
            s.Rods.Clear();
            s.Arcs.Clear();
            if (s.Stage == HeraldStage.Gale && s.Turn != HeraldTurn.Rest)
                s.Wind = s.Wind > 0 ? -1 : 1;  // the wind turns round with each attack
            if (s.Turn == HeraldTurn.StormFront)
            {
                s.FrontSide = -s.FrontSide;
                s.FrontStep = 0;
                s.FrontGap = 1 + rng.Next(4);
            }
            e.FireT = 0.3f;
        }

        void ClearBallLightning() => RemoveAll(EnemyType.BallLightning);

        void HeraldFire(Enemy e, V2 tpos, float aim)
        {
            var s = HeraldOf(e);
            if (s.TransT > 0f)
            {
                e.FireT = 0.2f;
                return;
            }
            switch (s.Turn)
            {
                case HeraldTurn.Rest:
                case HeraldTurn.Eye:  // its old volley: 3 piercing shots
                    e.FireT = 1f;
                    for (int i = -1; i <= 1; i++)
                        EnemyShot(e, aim + i * 0.15f, 330f, Defs.PierceShot);
                    break;
                case HeraldTurn.Thunderclap:  // rings of 36 with two gaps, each turned 25 degrees
                {
                    e.FireT = 1.5f;
                    e.Ang += 25f * MathX.Pi / 180f;
                    const int n = 36;
                    const float step = MathX.Tau / n;
                    for (int i = 0; i < n; i++)
                    {
                        float a = e.Ang + step * (i + 0.5f);
                        float off = MathX.Fmod(a - e.Ang, MathX.Pi);  // how far round from the nearer of the two gaps
                        if (Math.Min(off, MathX.Pi - off) < 2f * step)
                            continue;
                        EnemyShot(e, a, 160f, Defs.SmallShot);
                    }
                    BroadcastFx(FxKind.Ring, e.Pos, new Rgba(0.56f, 0.82f, 1f), 160f, 0.4f, Sfx.BossPulse);
                    break;
                }
                case HeraldTurn.Crosswind:  // 5 piercing shots aimed upwind, so the wind carries them onto you
                {
                    e.FireT = 0.8f;
                    float flight = tpos.DistanceTo(e.Pos) / 260f;
                    V2 at = tpos - HeraldWind(WindDrift) * flight;
                    float a = (at - e.Pos).Angle;
                    for (int i = -2; i <= 2; i++)
                        EnemyShot(e, a + i * 0.12f, 260f, Defs.PierceShot);
                    break;
                }
                default:  // its strikes, rods and the rest come from HeraldSpecial
                    e.FireT = 0.2f;
                    break;
            }
        }

        void SyncHerald(NetSync s, Enemy e)
        {
            bool has = e.Herald != null;
            s.Bool(ref has);
            if (!has)
                return;
            var q = e.Herald ?? (e.Herald = new HeraldState { Step = 0 });
            int stage = (int)q.Stage, turn = (int)q.Turn, transTo = (int)q.TransTo, wind = q.Wind + 1;
            s.Byte(ref stage);
            s.Byte(ref turn);
            s.Byte(ref transTo);
            s.Byte(ref wind);
            s.Float(ref q.TurnT);
            s.Float(ref q.TransT);
            s.Float(ref q.Spin);
            s.Int(ref q.CondId);
            s.Float(ref q.CondT);
            int rods = q.Rods.Count;
            s.Byte(ref rods);
            if (s.Reading)
            {
                q.Rods.Clear();
                for (int i = 0; i < rods; i++)
                    q.Rods.Add(new HeraldRod());
            }
            foreach (var r in q.Rods)
            {
                s.Vec(ref r.Pos);
                s.Float(ref r.Fall);
            }
            int arcs = q.Arcs.Count;
            s.Byte(ref arcs);
            if (s.Reading)
            {
                q.Arcs.Clear();
                for (int i = 0; i < arcs; i++)
                    q.Arcs.Add(new HeraldArc());
            }
            foreach (var a in q.Arcs)
            {
                s.Byte(ref a.A);
                s.Byte(ref a.B);
                s.Float(ref a.T);
                s.Float(ref a.Warn);
            }
            if (s.Reading)
            {
                q.Stage = (HeraldStage)stage;
                q.Turn = (HeraldTurn)turn;
                q.TransTo = (HeraldStage)transTo;
                q.Wind = wind - 1;
            }
        }

        /// Clients, each frame: the clocks run between snapshots (the turn, rods falling, arcs, the Conductor, the spokes).
        void UpdateHeraldView(float dt)
        {
            if (IsHost)
                return;
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.StormHerald && e.Herald != null)
                {
                    var s = e.Herald;
                    s.TurnT += dt;
                    if (s.CondId != 0)
                        s.CondT = Math.Max(0f, s.CondT - dt);
                    if (s.Stage == HeraldStage.LastStand && s.TransT <= 0f)
                        s.Spin += SpokeSpin * dt;
                    foreach (var r in s.Rods)
                        r.Fall = Math.Max(0f, r.Fall - dt);
                    foreach (var a in s.Arcs)
                        a.T += dt;
                }
        }
    }
}
