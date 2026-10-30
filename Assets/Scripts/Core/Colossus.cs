using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Colossus's stage of the fight, by its health: three stages, then a last stand.
    public enum ColStage : byte { Awakening, Crumbling, MoltenCore, LastStand }

    /// What it's doing now. Each stage loops its turns, one attack at a time, with breathers (Rest) between.
    public enum ColTurn : byte { Rest, Stomps, Boulders, Fissures, Charge, Rockslide, Aftershocks, LavaStomps, Meteors, CaveIn, Last }

    /// A stomp: a ring of push blocks (two rows, like the Warden's gates) spreading out from where it stomped, with
    /// one or two gaps. You can't get through it: it shoves you back in if you walk into it, carries you out if it
    /// catches you, and rolls over you if it pins you against the edge (see ColossusPush).
    public class StompRing
    {
        public V2 Center;
        public float Radius, Warn, Gap1, Gap2;  // replicated; Gap2 only with Gaps == 2
        public int Gaps = 1;
        public readonly HashSet<int> Hit = new HashSet<int>();     // host: players it has hurt (once each)...
        public readonly HashSet<int> Rolled = new HashSet<int>();  // ...and rolled over against the edge (once more)
    }

    /// Something thrown or falling: a boulder, a meteor or a cave-in pillar. Its shadow grows until it lands.
    public class ColDrop
    {
        public const byte Boulder = 0, Meteor = 1, Pillar = 2, LastMeteor = 3;
        public V2 Pos, From;
        public float T, Dur, Radius;
        public byte Kind;
        public bool Hot => Kind == Meteor || Kind == LastMeteor;
    }

    /// A fissure racing out from the Colossus's feet; once it stops it waits, then erupts along its length.
    public class ColCrack
    {
        public V2 From;
        public float Ang, Len, Max, Wait;
        public int Seed;
    }

    /// A fallen armour plate (Crumbling): cover that stops bullets both ways and crumbles after enough enemy hits.
    public class ColSlab
    {
        public V2 Pos;
        public float W, H;
        public int Hp;
    }

    /// A cave-in pillar: cover for a few seconds, then it shatters into shots.
    public class ColPillar
    {
        public V2 Pos;
        public float T;
    }

    /// Everything about the Colossus's fight its body doesn't carry. The host runs it; clients get it in the snapshot
    /// (SyncColossus) to draw the rings, landings, fissures, plates, pillars, the charge line and the stage change.
    public class ColossusState
    {
        public ColStage Stage, TransTo;
        public ColTurn Turn;
        public float TurnT, TransT, DazeT, ChargeWarn, ChargeLeft, ChargeDir;
        public readonly List<StompRing> Rings = new List<StompRing>();
        public readonly List<ColDrop> Drops = new List<ColDrop>();
        public readonly List<ColCrack> Cracks = new List<ColCrack>();
        public readonly List<ColSlab> Slabs = new List<ColSlab>();
        public readonly List<ColPillar> Pillars = new List<ColPillar>();

        // Host only.
        public int Step = -1, Fired, SlideEdge, SlideGap, NextTarget;
        public float TurnLen, ActT, GapA, ShockA, MeteorT, LavaTickT;
        public readonly Dictionary<int, V2> LastPos = new Dictionary<int, V2>(), Vel = new Dictionary<int, V2>();
    }

    /// The Colossus (wave 25): a fight in three stages by its health, like the Warden's, then a last stand. It breaks
    /// down as it goes: armour, then shell, then a molten core.
    ///   Awakening (100-65%): it walks at you 200 px off, its rock volley between turns; Stomps (rings of push
    ///     blocks with a gap), Boulders (thrown where you're heading), Fissures (a crack that erupts).
    ///   Crumbling (65-30%): four armour plates fall as cover that blocks shots both ways, and it walks faster;
    ///     Charge (a plate in the way breaks and dazes it: +50% damage), Rockslide (rows of rocks from an edge),
    ///     Aftershocks (rings of shots whose gaps spiral).
    ///   Molten Core (30-10%): everything leaves lava; Lava stomps, Meteors, Cave-in (pillars that shatter).
    ///   Last stand (below 10%): it kneels in the middle; stomps with two gaps, its volley, a meteor every 3 s.
    public partial class Game
    {
        const float ColCrumbleAt = 0.65f, ColMoltenAt = 0.3f, ColLastAt = 0.1f, ColTransTime = 2f;
        // Stomp rings: push blocks like the Warden's gates (GateRows, GateRowGap, GateBulletR), spreading out.
        public const float RingSpeed = 150f, RingWarn = 0.45f, RingGapHalf = 0.33f;  // the gap is 0.66 rad (about 38 degrees) wide
        public const float ColKeep = 200f, ColDazeTime = 1.5f, ColDazeMult = 1.5f;
        public const float ColChargeWarn = 1f, ColChargeSpeed = 700f, ColChargeLength = 1100f;
        public const float SlabW = 110f, SlabH = 34f;
        const int SlabHp = 40;
        public const float PillarR = 28f, PillarLife = 3f;
        public const float CrackSpeed = 600f, CrackMax = 900f, CrackWait = 0.6f, CrackStep = 45f;
        const float LavaDps = 20f, LavaLife = 6f, LavaWarn = 0.5f;
        static readonly V2[] SlabSpots = { new V2(520f, 290f), new V2(1080f, 290f), new V2(430f, 600f), new V2(1170f, 600f) };

        static readonly ColTurn[][] ColTurns =
        {
            new[] { ColTurn.Stomps, ColTurn.Rest, ColTurn.Boulders, ColTurn.Rest, ColTurn.Fissures, ColTurn.Rest },
            new[] { ColTurn.Charge, ColTurn.Rest, ColTurn.Rockslide, ColTurn.Rest, ColTurn.Aftershocks, ColTurn.Rest },
            new[] { ColTurn.LavaStomps, ColTurn.Rest, ColTurn.Meteors, ColTurn.Rest, ColTurn.CaveIn, ColTurn.Rest },
            new[] { ColTurn.Last },
        };
        static readonly float[][] ColTimes =
        {
            new[] { 6f, 1f, 5f, 1f, 5f, 1.5f },
            new[] { 6f, 1f, 7f, 1f, 6f, 1.5f },
            new[] { 6f, 1f, 6f, 1f, 6f, 1.5f },
            new[] { 999f },
        };

        ColossusState ColOf(Enemy e) => e.Colossus ?? (e.Colossus = new ColossusState());

        public static bool ColossusImmune(Enemy e) => e.Type == EnemyType.Colossus && e.Colossus != null && e.Colossus.TransT > 0f;
        public static bool ColossusDazed(Enemy e) => e.Type == EnemyType.Colossus && e.Colossus != null && e.Colossus.DazeT > 0f;

        /// The Colossus on the field, if any (for the pushes on each player's own game).
        Enemy TheColossus()
        {
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Colossus && e.Colossus != null && !IsSpawning(e))
                    return e;
            return null;
        }

        // Phase (replicated, for cues and drawing): the turn, or ColShift while changing stage.
        public const byte ColShift = 50;

        // --- Moving ---------------------------------------------------------------------------------

        /// Walks at its target, 200 px off, faster once its armour is gone. Holds still while it aims a charge or
        /// is dazed, runs along the charge, and kneels in the middle for the last stand.
        V2 ColossusMove(Enemy e, V2 dir, float dist, ref float speed)
        {
            var c = ColOf(e);
            V2 middle = new V2(Width / 2f, Height / 2f);
            if (c.TransT > 0f || c.DazeT > 0f || c.ChargeWarn > 0f)
                return V2.Zero;
            if (c.ChargeLeft > 0f)
            {
                speed = ColChargeSpeed;
                return V2.FromAngle(c.ChargeDir);
            }
            if (c.Stage == ColStage.LastStand)
            {
                speed = 420f;
                return e.Pos.DistanceTo(middle) > 6f ? (middle - e.Pos).Normalized() : V2.Zero;
            }
            speed = c.Stage == ColStage.Crumbling ? 55f : c.Stage == ColStage.MoltenCore ? 40f : e.Def.Speed;
            return KeepDistance(dir, dist, ColKeep);
        }

        // --- The fight (host) -----------------------------------------------------------------------

        void ColossusSpecial(Enemy e, V2 tpos, float dt)
        {
            var c = ColOf(e);
            if (c.Step < 0)
                EnterColStage(e, ColStage.Awakening);
            float frac = e.MaxHp > 0f ? e.Hp / e.MaxHp : 1f;
            TrackPlayers(c, dt);
            TickColHazards(e, c, dt);

            if (c.TransT > 0f)
            {
                c.TransT -= dt;
                if (c.TransT <= 0f)
                {
                    c.TransT = 0f;
                    EnterColStage(e, c.TransTo);
                    StageStartBurst(e, ColStageColor(c.Stage));
                }
                e.Phase = ColShift;
                return;
            }
            if (c.Stage == ColStage.Awakening && frac <= ColCrumbleAt) { StartColTransition(e, ColStage.Crumbling); return; }
            if (c.Stage == ColStage.Crumbling && frac <= ColMoltenAt) { StartColTransition(e, ColStage.MoltenCore); return; }
            if (c.Stage == ColStage.MoltenCore && frac <= ColLastAt && c.ChargeLeft <= 0f)
                EnterColStage(e, ColStage.LastStand);  // straight away, as the others do

            c.TurnT += dt;
            c.DazeT = Math.Max(0f, c.DazeT - dt);
            ColCharge(e, c, dt);
            c.ActT -= dt;
            if (c.ActT <= 0f)
                ColAct(e, c, tpos);
            if (c.Stage == ColStage.LastStand)
            {
                c.MeteorT -= dt;
                if (c.MeteorT <= 0f)
                {
                    c.MeteorT = 3f;
                    var p = ColTarget(c);
                    if (p != null)
                        ThrowDrop(e, c, p.Pos, ColDrop.LastMeteor);
                }
            }
            e.Phase = (byte)c.Turn;
            if (c.Stage != ColStage.LastStand && c.TurnT >= c.TurnLen && c.ChargeLeft <= 0f && c.ChargeWarn <= 0f)
                NextColTurn(e, c);
        }

        static Rgba ColStageColor(ColStage s) => s == ColStage.Crumbling ? new Rgba(0.88f, 0.64f, 0.35f) : s == ColStage.MoltenCore ? new Rgba(1f, 0.48f, 0.23f)
            : s == ColStage.LastStand ? new Rgba(1f, 0.31f, 0.23f) : new Rgba(0.78f, 0.62f, 0.45f);

        void EnterColStage(Enemy e, ColStage stage)
        {
            var c = ColOf(e);
            c.Stage = stage;
            c.Step = -1;
            c.ChargeWarn = c.ChargeLeft = 0f;
            c.MeteorT = 1.5f;
            if (stage == ColStage.Crumbling)
            {
                // Its armour falls off as four plates of cover.
                c.Slabs.Clear();
                for (int i = 0; i < SlabSpots.Length; i++)
                {
                    bool upright = i >= 2;
                    c.Slabs.Add(new ColSlab { Pos = SlabSpots[i], W = upright ? SlabH : SlabW, H = upright ? SlabW : SlabH, Hp = SlabHp });
                    BroadcastFx(FxKind.Boom, SlabSpots[i], ColStageColor(ColStage.Awakening), 80f, 0.5f, Sfx.None);
                }
                Sounds.Add(Sfx.BigExplode);
            }
            else
                c.Slabs.Clear();
            NextColTurn(e, c);
            DoLog("the Colossus: " + stage);
        }

        void StartColTransition(Enemy e, ColStage to)
        {
            var c = ColOf(e);
            c.TransT = ColTransTime;
            c.TransTo = to;
            c.Rings.Clear();
            c.Drops.Clear();
            c.Cracks.Clear();
            c.Pillars.Clear();
            c.ChargeWarn = c.ChargeLeft = c.DazeT = 0f;
            ClearEnemyBullets();
            session.Broadcast(Proto.End(Proto.Begin(Msg.ClearEnemyBullets)));
            BroadcastFx(FxKind.Ring, e.Pos, ColStageColor(to), 900f, 0.8f, Sfx.BossChime);
            e.Phase = ColShift;
            DoLog("the Colossus: changing to " + to);
        }

        void NextColTurn(Enemy e, ColossusState c)
        {
            var turns = ColTurns[(int)c.Stage];
            c.Step = (c.Step + 1) % turns.Length;
            c.Turn = turns[c.Step];
            c.TurnLen = ColTimes[(int)c.Stage][c.Step];
            c.TurnT = 0f;
            c.ActT = 0.3f;
            c.Fired = 0;
            if (c.Turn == ColTurn.Stomps || c.Turn == ColTurn.Last)
                c.GapA = Rand(0f, MathX.Tau);
            if (c.Turn == ColTurn.Rockslide)
            {
                c.SlideEdge = (c.SlideEdge + 1) % 2;
                c.SlideGap = rng.Next(10);
            }
            e.Phase = (byte)c.Turn;
        }

        /// The players it goes for, in turn (one each for meteors and boulders, so a team shares them).
        Player ColTarget(ColossusState c)
        {
            Player pick = null;
            int alive = 0;
            foreach (var p in Players.Values)
                if (!p.Downed)
                    alive++;
            if (alive == 0)
                return null;
            int k = c.NextTarget++ % alive;
            foreach (var p in Players.Values)
                if (!p.Downed && k-- == 0)
                    pick = p;
            return pick;
        }

        /// Players' velocities (the host only sees positions), so boulders can lead them.
        void TrackPlayers(ColossusState c, float dt)
        {
            foreach (var p in Players.Values)
            {
                V2 last;
                if (c.LastPos.TryGetValue(p.Id, out last) && dt > 0f)
                {
                    V2 v = (p.Pos - last) / dt, old;
                    c.Vel.TryGetValue(p.Id, out old);
                    c.Vel[p.Id] = old + (v - old) * Math.Min(1f, dt * 6f);
                }
                c.LastPos[p.Id] = p.Pos;
            }
        }

        /// The current turn's next move.
        void ColAct(Enemy e, ColossusState c, V2 tpos)
        {
            float aim = (tpos - e.Pos).Angle;
            switch (c.Turn)
            {
                case ColTurn.Stomps:  // three stomps 2 s apart, each gap 60 degrees round from the last
                    c.ActT = 2f;
                    c.GapA += MathX.Tau / 6f;
                    AddRing(e, c, c.GapA, float.NaN);
                    break;
                case ColTurn.Boulders:
                {
                    c.ActT = 1.2f;
                    var p = ColTarget(c);
                    if (p != null)
                    {
                        V2 v;
                        c.Vel.TryGetValue(p.Id, out v);
                        ThrowDrop(e, c, p.Pos + v.LimitLength(240f) * 1.1f, ColDrop.Boulder);
                    }
                    break;
                }
                case ColTurn.Fissures:
                    if (c.Fired < 2)
                    {
                        c.ActT = 2.5f;
                        c.Fired++;
                        c.Cracks.Add(new ColCrack { From = e.Pos, Ang = aim, Max = CrackMax, Seed = rng.Next(1000) });
                        BroadcastFx(FxKind.Boom, e.Pos, ColStageColor(ColStage.Awakening), e.Radius * 1.6f, 0.4f, Sfx.BossPulse);
                    }
                    else
                        c.ActT = 99f;
                    break;
                case ColTurn.Charge:
                    if (c.Fired < 2 && c.ChargeLeft <= 0f && c.ChargeWarn <= 0f && c.DazeT <= 0f)
                    {
                        c.ActT = 3f;
                        c.Fired++;
                        c.ChargeWarn = ColChargeWarn;
                        c.ChargeDir = aim;
                        Sounds.Add(Sfx.BossCharge);
                    }
                    else
                        c.ActT = 0.2f;
                    break;
                case ColTurn.Rockslide:  // rows of 13 rocks from one edge, a 3-rock gap that shifts along
                {
                    c.ActT = 1.4f;
                    const int rows = 13;
                    int gap = c.SlideGap;
                    c.SlideGap = (c.SlideGap + 2) % (rows - 2);
                    for (int i = 0; i < rows; i++)
                    {
                        if (i >= gap && i < gap + 3)
                            continue;
                        if (c.SlideEdge == 0)
                            ColShot(e, new V2((i + 0.5f) * Width / rows, -10f), MathX.Pi / 2f, 180f, Defs.BigShot, 6f);
                        else
                            ColShot(e, new V2(-10f, (i + 0.5f) * Height / rows), 0f, 180f, Defs.BigShot, 10f);
                    }
                    Sounds.Add(Sfx.Throw);
                    break;
                }
                case ColTurn.Aftershocks:  // rings of shots with two gaps that turn 20 degrees a ring
                    c.ActT = 0.7f;
                    c.ShockA += 20f * MathX.Pi / 180f;
                    for (int i = 0; i < 30; i++)
                    {
                        float a = MathX.Tau * i / 30f;
                        if (AngleGap(a, c.ShockA) < 0.32f || AngleGap(a, c.ShockA + MathX.Pi) < 0.32f)
                            continue;
                        EnemyShot(e, a, 170f, Defs.SmallShot);
                    }
                    BroadcastFx(FxKind.Ring, e.Pos, ColStageColor(ColStage.Crumbling), 120f, 0.35f, Sfx.BossPulse);
                    break;
                case ColTurn.LavaStomps:  // a stomp every 1.5 s with gaps to your sides, and a pool where it stood
                    c.ActT = 1.5f;
                    AddRing(e, c, aim + MathX.Pi / 2f, aim - MathX.Pi / 2f);
                    SpawnPatchAt(PatchKind.Lava, e.Pos, LavaWarn, LavaWarn + LavaLife, 90f, 90f);
                    break;
                case ColTurn.Meteors:  // two volleys of three, 0.5 s apart
                {
                    if (c.Fired >= 6)
                    {
                        c.ActT = 99f;
                        break;
                    }
                    c.ActT = c.Fired % 3 == 2 ? 2.4f : 0.5f;
                    c.Fired++;
                    var p = ColTarget(c);
                    if (p != null)
                        ThrowDrop(e, c, p.Pos, ColDrop.Meteor);
                    break;
                }
                case ColTurn.CaveIn:  // ten pillars anywhere, two by each player
                    c.ActT = 99f;
                    for (int k = 0; k < 10; k++)
                        DropPillar(c, RandomInArena(80f));
                    foreach (var p in Players.Values)
                        if (!p.Downed)
                            for (int k = 0; k < 2; k++)
                                DropPillar(c, ClampToArena(p.Pos + V2.FromAngle(Rand(0f, MathX.Tau)) * 45f, 60f));
                    Sounds.Add(Sfx.BossCharge);
                    break;
                case ColTurn.Last:  // two gaps, each stomp turned a quarter turn and a bit
                    c.ActT = 1.6f;
                    c.GapA += MathX.Pi / 2f + 0.16f;
                    AddRing(e, c, c.GapA, c.GapA + MathX.Pi);
                    break;
                default:
                    c.ActT = 0.2f;
                    break;
            }
        }

        static float AngleGap(float a, float b) => Math.Abs(MathX.Fmod(a - b + MathX.Pi, MathX.Tau) - MathX.Pi);

        /// A stomp: a ring of push blocks from its feet with one gap (gap2 NaN) or two.
        void AddRing(Enemy e, ColossusState c, float gap1, float gap2)
        {
            var r = new StompRing { Center = e.Pos, Radius = e.Radius + 6f, Warn = RingWarn, Gap1 = MathX.Fmod(gap1, MathX.Tau), Gaps = 1 };
            if (!float.IsNaN(gap2))
            {
                r.Gap2 = MathX.Fmod(gap2, MathX.Tau);
                r.Gaps = 2;
            }
            c.Rings.Add(r);
            Sounds.Add(Sfx.BossPulse);
        }

        void ColShot(Enemy e, V2 pos, float ang, float speed, byte kind, float life = 8f)
        {
            SpawnBullet(kind, pos, V2.FromAngle(ang) * speed, 0f, life, Defs.BulletDamage(kind), 0, true).Source = e.Type;
        }

        void ThrowDrop(Enemy e, ColossusState c, V2 at, byte kind)
        {
            at = ClampToArena(at, 40f);
            c.Drops.Add(new ColDrop { Pos = at, From = e.Pos, Dur = kind == ColDrop.Boulder ? 1.1f : 1.2f, Radius = kind == ColDrop.Boulder ? 60f : 70f, Kind = kind });
            Sounds.Add(Sfx.Throw);
        }

        void DropPillar(ColossusState c, V2 at) => c.Drops.Add(new ColDrop { Pos = at, From = at, Dur = 1.2f, Radius = 40f, Kind = ColDrop.Pillar });

        /// The charge: aim (a red line) for a second, then run until a wall, a plate (which breaks and dazes it) or
        /// its full length; it stomps where it stops.
        void ColCharge(Enemy e, ColossusState c, float dt)
        {
            if (c.ChargeWarn > 0f)
            {
                c.ChargeWarn -= dt;
                if (c.ChargeWarn <= 0f)
                    c.ChargeLeft = ColChargeLength;
                return;
            }
            if (c.ChargeLeft <= 0f)
                return;
            c.ChargeLeft -= ColChargeSpeed * dt;
            V2 next = e.Pos + V2.FromAngle(c.ChargeDir) * ColChargeSpeed * dt;
            bool stop = c.ChargeLeft <= 0f || ClampToArena(next, e.Radius).DistanceTo(next) > 1f;
            for (int i = c.Slabs.Count - 1; i >= 0; i--)
                if (InSlab(c.Slabs[i], next, e.Radius - 6f))
                {
                    BroadcastFx(FxKind.Boom, c.Slabs[i].Pos, new Rgba(1f, 0.8f, 0.35f), 90f, 0.5f, Sfx.BigExplode);
                    c.Slabs.RemoveAt(i);
                    c.DazeT = ColDazeTime;
                    stop = true;
                }
            if (stop)
            {
                c.ChargeLeft = 0f;
                AddRing(e, c, Rand(0f, MathX.Tau), float.NaN);
            }
        }

        public static bool InSlab(ColSlab s, V2 p, float pad) =>
            p.X > s.Pos.X - s.W / 2f - pad && p.X < s.Pos.X + s.W / 2f + pad && p.Y > s.Pos.Y - s.H / 2f - pad && p.Y < s.Pos.Y + s.H / 2f + pad;

        /// Host: rings spread and hurt, things land, fissures race and erupt, pillars shatter, lava burns.
        void TickColHazards(Enemy e, ColossusState c, float dt)
        {
            float ringDmg = 14f + Wave * 0.4f;
            AdvanceRings(c, dt);
            foreach (var r in c.Rings)
            {
                if (r.Warn > 0f)
                    continue;
                foreach (var p in Players.Values)
                {
                    if (p.Downed || !InRing(r, p.Pos, GateBulletR + p.Def.HitR))
                        continue;
                    if (r.Hit.Add(p.Id))
                        Damage(p, ringDmg, "The Colossus (stomp)");
                    V2 out1 = r.Center + (p.Pos - r.Center).Normalized() * (r.Radius + GateBulletR + p.Def.HitR + 2f);
                    if (ClampToArena(out1, p.Def.HitR).DistanceTo(out1) > 1f && r.Rolled.Add(p.Id))
                    {
                        p.InvulnT = 0f;
                        Damage(p, ringDmg, "The Colossus (stomp)");
                    }
                }
            }
            // Landings
            for (int i = c.Drops.Count - 1; i >= 0; i--)
            {
                var d = c.Drops[i];
                d.T += dt;
                if (d.T < d.Dur)
                    continue;
                c.Drops.RemoveAt(i);
                float dmg = d.Kind == ColDrop.Boulder || d.Kind == ColDrop.Pillar ? 30f : 40f;
                foreach (var p in Players.Values)
                    if (!p.Downed && p.InvulnT <= 0f && p.Pos.DistanceTo(d.Pos) < d.Radius + p.Def.HitR)
                        Damage(p, dmg, d.Kind == ColDrop.Pillar ? "The Colossus (cave-in)" : d.Hot ? "The Colossus (meteor)" : "The Colossus (boulder)");
                switch (d.Kind)
                {
                    case ColDrop.Boulder:
                        for (int k = 0; k < 10; k++)
                            ColShot(e, d.Pos, MathX.Tau * k / 10f, 140f, Defs.SmallShot);
                        break;
                    case ColDrop.Meteor:
                    case ColDrop.LastMeteor:
                        for (int k = 0; k < 14; k++)
                            ColShot(e, d.Pos, MathX.Tau * k / 14f, 130f, Defs.BigShot);
                        if (d.Kind == ColDrop.Meteor)
                            SpawnPatchAt(PatchKind.Lava, d.Pos, LavaWarn, LavaWarn + LavaLife, 70f, 70f);
                        break;
                    case ColDrop.Pillar:
                        c.Pillars.Add(new ColPillar { Pos = d.Pos, T = PillarLife });
                        break;
                }
                BroadcastFx(FxKind.Boom, d.Pos, d.Hot ? new Rgba(1f, 0.48f, 0.23f) : ColStageColor(ColStage.Awakening), d.Radius + 20f, 0.4f, d.Kind == ColDrop.Pillar ? Sfx.Hit : Sfx.Explode);
            }
            // Fissures: race out, wait, erupt (shots out of both sides every 45 px)
            for (int i = c.Cracks.Count - 1; i >= 0; i--)
            {
                var k = c.Cracks[i];
                if (GrowCrack(k, dt))
                    continue;
                k.Wait += dt;
                if (k.Wait < CrackWait)
                    continue;
                c.Cracks.RemoveAt(i);
                V2 dir = V2.FromAngle(k.Ang);
                for (float s = CrackStep; s < k.Len; s += CrackStep)
                {
                    V2 at = k.From + dir * s;
                    ColShot(e, at, k.Ang + MathX.Pi / 2f, 120f, Defs.SmallShot);
                    ColShot(e, at, k.Ang - MathX.Pi / 2f, 120f, Defs.SmallShot);
                }
                BroadcastFx(FxKind.Bolt, k.From, k.From + dir * k.Len, new Rgba(1f, 0.6f, 0.3f), 0f, 0.3f, Sfx.Explode);
            }
            // Pillars shatter
            for (int i = c.Pillars.Count - 1; i >= 0; i--)
            {
                var pl = c.Pillars[i];
                pl.T -= dt;
                if (pl.T > 0f)
                    continue;
                c.Pillars.RemoveAt(i);
                for (int k = 0; k < 6; k++)
                {
                    float a = MathX.Tau * k / 6f + pl.Pos.X * 0.01f;
                    ColShot(e, pl.Pos + V2.FromAngle(a) * PillarR, a, 150f, Defs.SmallShot);
                }
                BroadcastFx(FxKind.Boom, pl.Pos, ColStageColor(ColStage.Awakening), 50f, 0.3f, Sfx.Hit);
            }
            // Lava
            c.LavaTickT -= dt;
            if (c.LavaTickT <= 0f)
            {
                c.LavaTickT = 0.25f;
                foreach (var p in Players.Values)  // once a tick, however many pools overlap under you
                {
                    if (p.Downed || p.InvulnT > 0f)
                        continue;
                    foreach (var pt in Patches.Values)
                        if (pt.Kind == PatchKind.Lava && pt.Active && p.Pos.DistanceTo(pt.Pos) < pt.Radius + p.Def.HitR)
                        {
                            Damage(p, LavaDps * 0.25f, "The Colossus (lava)");
                            break;
                        }
                }
            }
        }

        /// Moves the rings out (every game: the host, and clients between snapshots); gone once off the arena.
        static void AdvanceRings(ColossusState c, float dt)
        {
            foreach (var r in c.Rings)
            {
                if (r.Warn > 0f)
                    r.Warn -= dt;
                else
                    r.Radius += RingSpeed * dt;
            }
            c.Rings.RemoveAll(r => RingBack(r) > RingReach(r) + 20f);
        }

        /// How far a ring has to spread to clear the whole arena: to the farthest corner from where it stomped.
        public static float RingReach(StompRing r)
        {
            float x = Math.Max(r.Center.X, Width - r.Center.X), y = Math.Max(r.Center.Y, Height - r.Center.Y);
            return (float)Math.Sqrt(x * x + y * y);
        }

        /// A fissure racing out; false once it has stopped (its full length, or the arena's edge).
        static bool GrowCrack(ColCrack k, float dt)
        {
            if (k.Len >= k.Max)
                return false;
            k.Len = Math.Min(k.Max, k.Len + CrackSpeed * dt);
            V2 tip = k.From + V2.FromAngle(k.Ang) * k.Len;
            if (tip.X < 0f || tip.Y < 0f || tip.X > Width || tip.Y > Height)
                k.Max = k.Len;
            return true;
        }

        /// The ring's band of push blocks (both rows) at this distance from its centre, with `pad` on either side.
        public static float RingBack(StompRing r) => r.Radius - (GateRows - 1) * GateRowGap;

        public static bool InRingGap(StompRing r, V2 p, float pad)
        {
            V2 rel = p - r.Center;
            float d = rel.Length;
            if (d < 1f)
                return false;
            float a = rel.Angle, slack = pad / d;
            return AngleGap(a, r.Gap1) < RingGapHalf - slack || (r.Gaps == 2 && AngleGap(a, r.Gap2) < RingGapHalf - slack);
        }

        public static bool InRing(StompRing r, V2 p, float pad)
        {
            float d = p.DistanceTo(r.Center);
            return r.Warn <= 0f && d > RingBack(r) - pad && d < r.Radius + pad && !InRingGap(r, p, pad);
        }

        /// Your own game: a ring shoves you back in if you walk into it, carries you out if it catches you, and
        /// rolls over you (you end up inside it) if it pins you against the edge. Plates and pillars are solid.
        void ColossusPush(Player me, V2 before)
        {
            var e = TheColossus();
            if (e == null || me.Downed)
                return;
            var c = e.Colossus;
            foreach (var r in c.Rings)
            {
                float pad = GateBulletR + me.Def.HitR;
                if (!InRing(r, me.Pos, pad))
                    continue;
                V2 dir = (me.Pos - r.Center).Normalized();
                bool outside = before.DistanceTo(r.Center) > (r.Radius + RingBack(r)) / 2f;
                V2 outPos = r.Center + dir * (r.Radius + pad + 2f), inPos = r.Center + dir * Math.Max(0f, RingBack(r) - pad - 2f);
                me.Pos = outside && ClampToArena(outPos, me.Def.HitR).DistanceTo(outPos) <= 1f ? outPos : inPos;
            }
            foreach (var s in c.Slabs)
            {
                float pad = me.Def.HitR;
                if (!InSlab(s, me.Pos, pad))
                    continue;
                // Out the nearest side.
                float l = me.Pos.X - (s.Pos.X - s.W / 2f - pad), rr = s.Pos.X + s.W / 2f + pad - me.Pos.X;
                float t = me.Pos.Y - (s.Pos.Y - s.H / 2f - pad), b = s.Pos.Y + s.H / 2f + pad - me.Pos.Y;
                float m = Math.Min(Math.Min(l, rr), Math.Min(t, b));
                me.Pos = m == l ? new V2(me.Pos.X - l, me.Pos.Y) : m == rr ? new V2(me.Pos.X + rr, me.Pos.Y) : m == t ? new V2(me.Pos.X, me.Pos.Y - t) : new V2(me.Pos.X, me.Pos.Y + b);
            }
            foreach (var pl in c.Pillars)
            {
                float reach = PillarR + me.Def.HitR, d = me.Pos.DistanceTo(pl.Pos);
                if (d < reach)
                    me.Pos = pl.Pos + (d > 0.5f ? (me.Pos - pl.Pos) / d : V2.FromAngle(me.Aim + MathX.Pi)) * reach;
            }
        }

        /// Host, in Collide: plates and pillars stop bullets from both sides; enemy hits wear a plate down.
        bool ColossusCoverStops(Bullet b)
        {
            if (colCover == null)
                return false;
            var c = colCover;
            for (int i = 0; i < c.Slabs.Count; i++)
            {
                var s = c.Slabs[i];
                if (!InSlab(s, b.Pos, b.Radius))
                    continue;
                if (Defs.IsEnemyShot(b.Kind) && --s.Hp <= 0)
                {
                    BroadcastFx(FxKind.Boom, s.Pos, ColStageColor(ColStage.Awakening), 70f, 0.4f, Sfx.Hit);
                    c.Slabs.RemoveAt(i);
                }
                return true;
            }
            foreach (var pl in c.Pillars)
                if (b.Pos.DistanceTo(pl.Pos) < PillarR + b.Radius)
                    return true;
            return false;
        }
        ColossusState colCover;  // set for each Collide: the Colossus's cover, if it has any up

        void FindColCover()
        {
            colCover = null;
            var e = TheColossus();
            if (e != null && (e.Colossus.Slabs.Count > 0 || e.Colossus.Pillars.Count > 0))
                colCover = e.Colossus;
        }

        /// Its rock volley: 3 aimed piercing shots, between turns in the first two stages and all through the last.
        void ColossusFire(Enemy e, float aim)
        {
            var c = ColOf(e);
            bool volley = c.TransT <= 0f && c.ChargeLeft <= 0f && (c.Stage == ColStage.LastStand
                || (c.Stage == ColStage.Awakening && (c.Turn == ColTurn.Rest || c.Turn == ColTurn.Boulders))
                || (c.Stage == ColStage.Crumbling && c.Turn == ColTurn.Rest));
            if (!volley)
            {
                e.FireT = 0.2f;
                return;
            }
            e.FireT = 1.2f;
            for (int i = -1; i <= 1; i++)
                EnemyShot(e, aim + i * 0.18f, 170f, Defs.PierceShot);
        }

        // --- Sync -------------------------------------------------------------------------------------

        void SyncColossus(NetSync s, Enemy e)
        {
            bool has = e.Colossus != null;
            s.Bool(ref has);
            if (!has)
                return;
            var c = e.Colossus ?? (e.Colossus = new ColossusState { Step = 0 });
            int stage = (int)c.Stage, turn = (int)c.Turn, transTo = (int)c.TransTo;
            s.Byte(ref stage);
            s.Byte(ref turn);
            s.Byte(ref transTo);
            s.Float(ref c.TurnT);
            s.Float(ref c.TransT);
            s.Float(ref c.DazeT);
            s.Float(ref c.ChargeWarn);
            s.Float(ref c.ChargeLeft);
            s.Float(ref c.ChargeDir);
            if (s.Reading)
            {
                c.Stage = (ColStage)stage;
                c.Turn = (ColTurn)turn;
                c.TransTo = (ColStage)transTo;
            }
            SyncList(s, c.Rings, (q, r) => { q.Vec(ref r.Center); q.Float(ref r.Radius); q.Float(ref r.Warn); q.Float(ref r.Gap1); q.Float(ref r.Gap2); q.Byte(ref r.Gaps); });
            SyncList(s, c.Drops, (q, d) => { q.Vec(ref d.Pos); q.Vec(ref d.From); q.Float(ref d.T); q.Float(ref d.Dur); q.Float(ref d.Radius); q.Byte(ref d.Kind); });
            SyncList(s, c.Cracks, (q, k) => { q.Vec(ref k.From); q.Float(ref k.Ang); q.Float(ref k.Len); q.Float(ref k.Max); q.Short(ref k.Seed); });
            SyncList(s, c.Slabs, (q, b) => { q.Vec(ref b.Pos); q.Float(ref b.W); q.Float(ref b.H); q.Byte(ref b.Hp); });
            SyncList(s, c.Pillars, (q, p) => { q.Vec(ref p.Pos); q.Float(ref p.T); });
        }

        static void SyncList<T>(NetSync s, List<T> list, Action<NetSync, T> each) where T : new()
        {
            int n = list.Count;
            s.Byte(ref n);
            if (s.Reading)
            {
                list.Clear();
                for (int i = 0; i < n; i++)
                {
                    var x = new T();
                    each(s, x);
                    list.Add(x);
                }
            }
            else
                foreach (var x in list)
                    each(s, x);
        }

        /// Clients, each frame: rings spread, fissures race, shadows grow and the clocks run between snapshots.
        void UpdateColossusView(float dt)
        {
            if (IsHost)
                return;
            foreach (var e in Enemies.Values)
            {
                var c = e.Colossus;
                if (e.Type != EnemyType.Colossus || c == null)
                    continue;
                AdvanceRings(c, dt);
                foreach (var k in c.Cracks)
                    GrowCrack(k, dt);
                foreach (var d in c.Drops)
                    d.T = Math.Min(d.Dur, d.T + dt);
                foreach (var p in c.Pillars)
                    p.T = Math.Max(0f, p.T - dt);
                c.TurnT += dt;
                c.TransT = Math.Max(0f, c.TransT - dt);
                c.DazeT = Math.Max(0f, c.DazeT - dt);
                c.ChargeWarn = Math.Max(0f, c.ChargeWarn - dt);
            }
        }
    }
}
