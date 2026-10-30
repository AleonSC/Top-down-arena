using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Lich's stage of the fight, by its health: three stages, then a last stand.
    public enum LichStage : byte { Crypt, Harvest, Undeath, LastStand }

    /// What it's doing now. Each stage loops its turns, one mechanic at a time, with breathers (Rest) between.
    public enum LichTurn : byte { Rest, Fans, Rings, Raise, Sweep, Prison, Spirits, Hop, Hands, Storm, Rite }

    /// A bone prison: spikes rising in a ring round where a player stood, with one gap; they fire inward when up.
    public class BonePrison
    {
        public V2 Center;
        public float Gap, T;  // the gap's angle from the middle; seconds since it began to rise
        public bool Fired;    // host
    }

    /// A wailing spirit: circles the Lich, then dives at where a player was.
    public class LichSpirit
    {
        public V2 Pos;
        public bool Diving;
        public float A, Delay;  // host: its angle round the Lich; when it dives
        public V2 To;           // host: where it's diving
    }

    /// Everything about the Lich's fight its body doesn't carry. The host runs it; clients get it in the snapshot
    /// (SyncLich) to draw the graves, the soul sweep, the prisons, the spirits and the stage change.
    public class LichState
    {
        public LichStage Stage, TransTo;
        public LichTurn Turn;
        public float TurnT, TransT;      // seconds into the turn; > 0: changing stage (can't be hurt), counting down
        public float Window;             // > 0: its phylacteries are broken and it can be hurt, for this long
        public byte SweepOn;             // the soul sweep: 0 off, 1 warning, 2 live
        public float SweepA;             // its angle
        public float FadeT;              // > 0: blinking to another grave (can't be hurt)
        public V2 BlinkTo;
        public readonly List<BonePrison> Prisons = new List<BonePrison>();
        public readonly List<LichSpirit> Spirits = new List<LichSpirit>();

        // Host only.
        public int Step = -1, Fired;
        public float TurnLen, Rebuild, SweepFrom, SweepDir, SweepT, FanT, RingT, RiseT, HandT, HopT;
    }

    /// The Lich (wave 35): a fight in three stages by its health, like the Warden's, then a last stand.
    ///   The Crypt (100-65%): one phylactery at the grave farthest from the players keeps it safe; break it for a 12 s
    ///     window, then another rises. Soul fans, grave rings, and the dead rising from graves.
    ///   Soul Harvest (65-30%): three phylacteries in a triangle round the middle, joined by soul beams that hurt to cross;
    ///     break all three for the window. Soul sweep (a draining beam swung toward you), bone prisons, wailing spirits.
    ///   Undeath (30-10%): no phylacteries; a spectre that blinks from grave to grave bursting rings of orbs, dead hands
    ///     bursting up under you, and a two-armed spiral of skulls.
    ///   Last stand (below 10%): the final rite in the middle: the dead rise from every grave, fans and rings together.
    /// Six graves stand in fixed places for the whole fight (GravePos).
    public partial class Game
    {
        // Phase (replicated) tells clients what to draw and the HUD what to say.
        public const byte LichNormal = 0, LichGuarded = 1, LichFaded = 2, LichShift = 3;
        const float LichHarvestAt = 0.65f, LichUndeathAt = 0.3f, LichLastAt = 0.1f, LichTransTime = 2f;
        public const float LichWindow = 12f;                       // how long it can be hurt once its phylacteries break
        const float TriangleR = 380f, TriangleSquash = 0.8f, TrianglePhylHp = 0.75f;  // stage 2's phylacteries: where, and 450 of 600 health
        public const float SoulBeamWidth = 12f;                    // the beams between them
        public const float LichSweepLen = 900f, LichSweepWarn = 0.8f, LichSweepArc = 2.1f, LichSweepSpin = 1.22f, LichSweepWidth = 16f;  // 120 degrees at 70 a second
        public const float PrisonR = 110f, PrisonRise = 1.2f, PrisonLast = 1.6f;
        public const int PrisonSpikes = 14;
        public const float SpiritOrbit = 220f, SpiritSpin = 1.6f, SpiritSpeed = 520f, SpiritR = 10f;
        const float HopFade = 0.5f, GraveStand = 90f;              // a blink's fade; how far from its grave the spectre stands
        public const float HandRadius = 50f, HandDelay = 0.8f;
        public const byte HazardLightning = 0, HazardHand = 1;

        static readonly LichTurn[][] LichTurns =
        {
            new[] { LichTurn.Fans, LichTurn.Rest, LichTurn.Rings, LichTurn.Rest, LichTurn.Raise, LichTurn.Rest },
            new[] { LichTurn.Sweep, LichTurn.Rest, LichTurn.Prison, LichTurn.Rest, LichTurn.Spirits, LichTurn.Rest },
            new[] { LichTurn.Hop, LichTurn.Rest, LichTurn.Hands, LichTurn.Rest, LichTurn.Storm, LichTurn.Rest },
            new[] { LichTurn.Rite },
        };
        static readonly float[][] LichTimes =
        {
            new[] { 5f, 1f, 6f, 1f, 4f, 1.5f },
            new[] { 6f, 1f, 5f, 1f, 6f, 1.5f },
            new[] { 6f, 1f, 5f, 1f, 6f, 1.5f },
            new[] { 999f },
        };
        static readonly float[] GraveFx = { 0.15f, 0.5f, 0.85f, 0.15f, 0.5f, 0.85f }, GraveFy = { 0.21f, 0.13f, 0.21f, 0.78f, 0.87f, 0.78f };
        public const int GraveCount = 6;

        /// Where grave i stands (inside the arena's current walls).
        public V2 GravePos(int i)
        {
            V2 lo = ArenaMin, hi = ArenaMax;
            return new V2(lo.X + (hi.X - lo.X) * GraveFx[i], lo.Y + (hi.Y - lo.Y) * GraveFy[i]);
        }

        LichState LichOf(Enemy e) => e.Lich ?? (e.Lich = new LichState());

        public static bool LichImmune(Enemy e) => e.Type == EnemyType.Lich && (e.Phase == LichGuarded || e.Phase == LichFaded || e.Phase == LichShift);

        static byte LichPhaseOf(LichState s, bool guarded) => s.TransT > 0f ? LichShift : s.FadeT > 0f ? LichFaded : guarded ? LichGuarded : LichNormal;

        List<Enemy> LichPhylacteries(Enemy e)
        {
            var list = new List<Enemy>();
            foreach (var o in Enemies.Values)
                if (o.Type == EnemyType.Phylactery && o.ParentId == e.Id && o.Hp > 0f)
                    list.Add(o);
            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            return list;
        }

        void LichSpecial(Enemy e, float dt)
        {
            var s = LichOf(e);
            if (s.Step < 0)
                EnterLichStage(e, LichStage.Crypt);
            float frac = e.MaxHp > 0f ? e.Hp / e.MaxHp : 1f;
            V2 middle = new V2(Width / 2f, Height / 2f);

            if (s.TransT > 0f)
            {
                s.TransT -= dt;
                e.Pos += (middle - e.Pos).LimitLength(300f * dt);
                if (s.TransT <= 0f)
                {
                    s.TransT = 0f;
                    EnterLichStage(e, s.TransTo);
                    StageStartBurst(e, LichColor(s.Stage));
                }
                e.Phase = LichPhaseOf(s, false);
                return;
            }
            if (s.Stage == LichStage.Crypt && frac <= LichHarvestAt) { StartLichTransition(e, LichStage.Harvest); return; }
            if (s.Stage == LichStage.Harvest && frac <= LichUndeathAt) { StartLichTransition(e, LichStage.Undeath); return; }
            if (s.Stage == LichStage.Undeath && frac <= LichLastAt)
                EnterLichStage(e, LichStage.LastStand);  // straight away, as the others do

            s.TurnT += dt;

            // Its phylacteries keep it safe (stages 1-2); breaking them all opens a window, then they rise again.
            bool guarded = false;
            if (s.Stage <= LichStage.Harvest)
            {
                guarded = LichPhylacteries(e).Count > 0;
                if (!guarded)
                {
                    if (s.Window <= 0f && s.Rebuild <= 0f)
                    {
                        s.Window = s.Rebuild = LichWindow;
                        BroadcastFx(FxKind.Ring, e.Pos, new Rgba(1f, 0.8f, 0.35f), e.Radius + 90f, 0.6f, Sfx.BossChime);
                    }
                    s.Window = Math.Max(0f, s.Window - dt);
                    s.Rebuild -= dt;
                    if (s.Rebuild <= 0f)
                    {
                        RaisePhylacteries(e, s);
                        guarded = true;
                    }
                }
                else
                    s.Window = 0f;
            }

            // Where it is: drifting along the top (stages 1-2), at a grave (Undeath), in the middle (the rite).
            if (s.Stage <= LichStage.Harvest)
            {
                var goal = new V2(Width / 2f + (float)Math.Sin(e.T * 0.45f) * Width * 0.3f, 150f + (float)Math.Sin(e.T * 0.7f) * 50f);
                e.Pos += (goal - e.Pos).LimitLength(60f * dt);
            }
            else if (s.Stage == LichStage.LastStand)
                e.Pos += (middle - e.Pos).LimitLength(200f * dt);
            if (s.FadeT > 0f)
            {
                s.FadeT -= dt;
                if (s.FadeT <= 0f)
                {
                    s.FadeT = 0f;
                    e.Pos = s.BlinkTo;
                    float off = Rand(0f, MathX.Tau);
                    for (int i = 0; i < 20; i++)
                        EnemyShot(e, off + MathX.Tau * i / 20f, 110f, Defs.BigShot);
                    BroadcastFx(FxKind.Ring, e.Pos, LichColor(LichStage.Undeath), 90f, 0.45f, Sfx.SpellArcane);
                }
            }
            e.Phase = LichPhaseOf(s, guarded);

            TickLichTurn(e, s, dt);
            TickLichHazards(e, s, dt);
            if (s.TurnT >= s.TurnLen)
                NextLichTurn(e, s);
        }

        static Rgba LichColor(LichStage stage) => stage == LichStage.Harvest ? new Rgba(0.37f, 0.85f, 1f) : stage == LichStage.Undeath ? new Rgba(0.7f, 0.55f, 1f)
            : stage == LichStage.LastStand ? new Rgba(1f, 0.42f, 0.6f) : new Rgba(0.43f, 1f, 0.69f);

        void EnterLichStage(Enemy e, LichStage stage)
        {
            var s = LichOf(e);
            s.Stage = stage;
            s.Step = -1;
            s.Window = s.Rebuild = 0f;
            s.FadeT = 0f;
            s.Prisons.Clear();
            s.Spirits.Clear();
            s.SweepOn = 0;
            if (stage <= LichStage.Harvest)
                RaisePhylacteries(e, s);
            else
                RemoveChildren(e, EnemyType.Phylactery);
            if (stage == LichStage.Undeath)
                e.Pos = GraveSpot(1);
            NextLichTurn(e, s);
            DoLog("the Lich: " + stage);
        }

        void StartLichTransition(Enemy e, LichStage to)
        {
            var s = LichOf(e);
            s.TransT = LichTransTime;
            s.TransTo = to;
            s.Prisons.Clear();
            s.Spirits.Clear();
            s.SweepOn = 0;
            s.FadeT = 0f;
            RemoveChildren(e, EnemyType.Phylactery);
            ClearHands();
            ClearEnemyBullets();
            session.Broadcast(Proto.End(Proto.Begin(Msg.ClearEnemyBullets)));
            BroadcastFx(FxKind.Ring, e.Pos, LichColor(to), 900f, 0.8f, Sfx.BossChime);
            DoLog("the Lich: changing to " + to);
        }

        void RemoveChildren(Enemy e, EnemyType type)
        {
            var gone = new List<int>();
            foreach (var o in Enemies.Values)
                if (o.Type == type && o.ParentId == e.Id)
                    gone.Add(o.Id);
            foreach (int id in gone)
                Enemies.Remove(id);
        }

        void ClearHands()
        {
            var gone = new List<int>();
            foreach (var h in Hazards.Values)
                if (h.Kind == HazardHand)
                    gone.Add(h.Id);
            foreach (int id in gone)
                Hazards.Remove(id);
        }

        /// Stage 1: one phylactery at the grave farthest from the players. Stage 2: three in a triangle round the middle.
        void RaisePhylacteries(Enemy e, LichState s)
        {
            RemoveChildren(e, EnemyType.Phylactery);
            s.Window = s.Rebuild = 0f;
            var color = Defs.Get(EnemyType.Phylactery).Color;
            if (s.Stage == LichStage.Crypt)
            {
                int far = 0;
                float best = -1f;
                for (int i = 0; i < GraveCount; i++)
                {
                    float d = float.MaxValue;
                    foreach (var p in Players.Values)
                        if (!p.Downed)
                            d = Math.Min(d, p.Pos.DistanceTo(GravePos(i)));
                    if (d > best) { best = d; far = i; }
                }
                V2 at = GraveSpot(far);
                SpawnEnemyAt(EnemyType.Phylactery, at, 0.8f, e.Id);
                BroadcastFx(FxKind.Ring, at, color, 60f, 0.6f, Sfx.Summon);
                return;
            }
            V2 middle = new V2(Width / 2f, Height / 2f);
            for (int k = 0; k < 3; k++)
            {
                V2 at = middle + new V2((float)Math.Cos(-MathX.Pi / 2f + MathX.Tau * k / 3f), (float)Math.Sin(-MathX.Pi / 2f + MathX.Tau * k / 3f) * TriangleSquash) * TriangleR;
                var ph = SpawnEnemyAt(EnemyType.Phylactery, ClampToArena(at, 40f), 0.8f, e.Id);
                ph.MaxHp *= TrianglePhylHp;
                ph.Hp = ph.MaxHp;
                BroadcastFx(FxKind.Ring, ph.Pos, LichColor(LichStage.Harvest), 60f, 0.6f, Sfx.Summon);
            }
        }

        /// Where the Lich stands at a grave: a little way from it toward the middle.
        V2 GraveSpot(int i)
        {
            V2 g = GravePos(i), middle = new V2(Width / 2f, Height / 2f);
            return g + (middle - g).Normalized() * GraveStand;
        }

        void NextLichTurn(Enemy e, LichState s)
        {
            var turns = LichTurns[(int)s.Stage];
            s.Step = (s.Step + 1) % turns.Length;
            s.Turn = turns[s.Step];
            s.TurnLen = LichTimes[(int)s.Stage][s.Step];
            s.TurnT = 0f;
            s.Fired = 0;
            s.SweepOn = 0;
            s.HandT = s.HopT = 0f;
            s.FanT = 1f; s.RingT = 2f; s.RiseT = 1f;
            e.FireT = 0.3f;
            if (s.Turn == LichTurn.Raise)
            {
                // Three graves at random raise two of the dead each (at most 6 up).
                var order = new List<int> { 0, 1, 2, 3, 4, 5 };
                for (int k = 0; k < 3; k++)
                {
                    int pick = (int)Rand(0f, order.Count - 0.001f);
                    RaiseDead(e, order[pick], 2, 6);
                    order.RemoveAt(pick);
                }
            }
            if (s.Turn == LichTurn.Spirits)
                for (int k = 0; k < 6; k++)
                    s.Spirits.Add(new LichSpirit { Pos = e.Pos, A = MathX.Tau * k / 6f, Delay = 1.3f + 0.4f * k });
        }

        void RaiseDead(Enemy e, int grave, int each, int cap)
        {
            V2 g = GravePos(grave);
            BroadcastFx(FxKind.Ring, g, new Rgba(0.43f, 1f, 0.69f), 40f, 0.9f, Sfx.Summon);
            for (int k = 0; k < each && CountChildren(e, EnemyType.Grunt) < cap; k++)
                SpawnEnemyAt(EnemyType.Grunt, ClampToArena(g + new V2(k == 0 ? -18f : 18f, 34f), 20f), 1f, e.Id);  // they climb out for 1 s
        }

        /// The turns' own events (its aimed shots and rings are in LichFire).
        void TickLichTurn(Enemy e, LichState s, float dt)
        {
            switch (s.Turn)
            {
                case LichTurn.Sweep:
                    // Twice: a warning line, then a draining beam swung 120 degrees across toward the nearest player.
                    if (s.SweepOn == 0 && s.Fired < 2 && s.TurnT >= 0.3f + 3f * s.Fired)
                    {
                        s.Fired++;
                        var p = NearestPlayer(e.Pos);
                        float toP = p != null ? (p.Pos - e.Pos).Angle : MathX.Pi / 2f;
                        s.SweepDir = Rand(0f, 1f) < 0.5f ? 1f : -1f;
                        s.SweepFrom = toP - s.SweepDir * LichSweepArc / 2f;
                        s.SweepA = s.SweepFrom;
                        s.SweepT = 0f;
                        s.SweepOn = 1;
                        BroadcastSound(Sfx.BossCharge);
                    }
                    if (s.SweepOn > 0)
                    {
                        s.SweepT += dt;
                        if (s.SweepT >= LichSweepWarn)
                        {
                            s.SweepOn = 2;
                            float swung = (s.SweepT - LichSweepWarn) * LichSweepSpin;
                            s.SweepA = s.SweepFrom + s.SweepDir * Math.Min(LichSweepArc, swung);
                            if (swung >= LichSweepArc)
                                s.SweepOn = 0;
                        }
                    }
                    break;
                case LichTurn.Prison:
                    // Twice: a ring of spikes rises round each player, the gap facing away from the Lich.
                    if (s.Fired < 2 && s.TurnT >= 0.3f + 2.5f * s.Fired)
                    {
                        s.Fired++;
                        foreach (var p in Players.Values)
                            if (!p.Downed)
                                s.Prisons.Add(new BonePrison { Center = p.Pos, Gap = (p.Pos - e.Pos).Angle });
                        BroadcastSound(Sfx.Clang);
                    }
                    break;
                case LichTurn.Hop:
                    s.HopT -= dt;
                    if (s.HopT <= 0f && s.FadeT <= 0f)
                    {
                        s.HopT = 2f;
                        int at = 0;
                        float best = float.MaxValue;
                        for (int i = 0; i < GraveCount; i++)
                            if (e.Pos.DistanceTo(GraveSpot(i)) < best) { best = e.Pos.DistanceTo(GraveSpot(i)); at = i; }
                        int to = (at + 1 + (int)Rand(0f, GraveCount - 1.001f)) % GraveCount;
                        s.BlinkTo = GraveSpot(to);
                        s.FadeT = HopFade;
                    }
                    break;
                case LichTurn.Hands:
                    s.HandT -= dt;
                    if (s.HandT <= 0f)
                    {
                        s.HandT = 0.9f;
                        foreach (var p in Players.Values)
                            if (!p.Downed)
                                AddHazard(p.Pos, HandRadius, HandDelay).Kind = HazardHand;
                    }
                    break;
                case LichTurn.Rite:
                    s.RiseT -= dt;
                    if (s.RiseT <= 0f)
                    {
                        s.RiseT = 6f;
                        for (int i = 0; i < GraveCount; i++)
                            RaiseDead(e, i, 2, 10);
                    }
                    break;
            }
        }

        /// Prisons, spirits, the sweep and the soul beams: moving them and hurting players.
        void TickLichHazards(Enemy e, LichState s, float dt)
        {
            float dmg = 12f + Wave * 0.3f;
            // Bone prisons: when the spikes are up, each fires inward.
            foreach (var q in s.Prisons)
            {
                q.T += dt;
                if (q.T >= PrisonRise && !q.Fired)
                {
                    q.Fired = true;
                    for (int k = 0; k < PrisonSpikes; k++)
                    {
                        V2 dir = V2.FromAngle(PrisonSpikeAngle(q, k)), at = q.Center + dir * PrisonR;
                        SpawnBullet(Defs.SmallShot, at, dir * -200f, 0f, 2f, Defs.BulletDamage(Defs.SmallShot), 0, true).Source = e.Type;
                    }
                }
            }
            s.Prisons.RemoveAll(q => q.T >= PrisonLast);
            // Wailing spirits: out to circle the Lich, then a dive each; touching one hurts.
            foreach (var sp in s.Spirits)
            {
                sp.A += SpiritSpin * dt;
                sp.Delay -= dt;
                if (!sp.Diving)
                {
                    V2 goal = e.Pos + V2.FromAngle(sp.A) * SpiritOrbit;
                    sp.Pos += (goal - sp.Pos) * Math.Min(1f, dt * 4f);
                    if (sp.Delay <= 0f)
                    {
                        var p = RandomLivingPlayer();
                        sp.Diving = true;
                        sp.To = p != null ? p.Pos : e.Pos;
                    }
                }
                else
                {
                    sp.Pos += (sp.To - sp.Pos).LimitLength(SpiritSpeed * dt);
                    if (sp.Pos.DistanceTo(sp.To) < 4f)
                    {
                        sp.Delay = -99f;  // spent
                        for (int k = 0; k < 4; k++)
                        {
                            V2 dir = V2.FromAngle(MathX.Pi / 4f + MathX.Tau * k / 4f);
                            SpawnBullet(Defs.SmallShot, sp.Pos, dir * 160f, 0f, 4f, Defs.BulletDamage(Defs.SmallShot), 0, true).Source = e.Type;
                        }
                        BroadcastFx(FxKind.Ring, sp.Pos, LichColor(LichStage.Harvest), 40f, 0.35f, Sfx.None);
                    }
                }
                foreach (var p in Players.Values)
                    if (!p.Downed && p.InvulnT <= 0f && p.Pos.DistanceTo(sp.Pos) < SpiritR + p.Def.HitR)
                        Damage(p, dmg, "The Lich (wailing spirit)");
            }
            s.Spirits.RemoveAll(sp => sp.Delay <= -90f);
            // The soul sweep and the phylacteries' beams.
            foreach (var p in Players.Values)
            {
                if (p.Downed || p.InvulnT > 0f)
                    continue;
                if (s.SweepOn == 2 && OnSegment(e.Pos, e.Pos + V2.FromAngle(s.SweepA) * LichSweepLen, p.Pos, LichSweepWidth + p.Def.HitR))
                {
                    Damage(p, dmg + 4f, "The Lich (soul sweep)");
                    continue;
                }
                if (s.Stage == LichStage.Harvest)
                {
                    var ph = LichPhylacteries(e);
                    if (ph.Count >= 2)
                        for (int i = 0; i < ph.Count; i++)
                        {
                            var a = ph[i];
                            var b = ph[(i + 1) % ph.Count];
                            if (ph.Count == 2 && i == 1)
                                break;  // two left: one beam between them
                            if (!IsSpawning(a) && !IsSpawning(b) && OnSegment(a.Pos, b.Pos, p.Pos, SoulBeamWidth / 2f + p.Def.HitR))
                            {
                                Damage(p, dmg, "The Lich (soul beam)");
                                break;
                            }
                        }
                }
            }
        }

        public static float PrisonSpikeAngle(BonePrison q, int k) => q.Gap + MathX.Tau * (k + 1f) / (PrisonSpikes + 1f);

        /// Is pos within reach of the segment from a to b?
        public static bool OnSegment(V2 a, V2 b, V2 pos, float reach)
        {
            V2 ab = b - a, ap = pos - a;
            float l2 = ab.X * ab.X + ab.Y * ab.Y;
            float u = l2 > 0f ? Math.Max(0f, Math.Min(1f, (ap.X * ab.X + ap.Y * ab.Y) / l2)) : 0f;
            return pos.DistanceTo(a + ab * u) < reach;
        }

        Player NearestPlayer(V2 from)
        {
            Player best = null;
            foreach (var p in Players.Values)
                if (!p.Downed && (best == null || p.Pos.DistanceSq(from) < best.Pos.DistanceSq(from)))
                    best = p;
            return best;
        }

        Player RandomLivingPlayer()
        {
            var alive = new List<Player>();
            foreach (var p in Players.Values)
                if (!p.Downed)
                    alive.Add(p);
            return alive.Count == 0 ? null : alive[(int)Rand(0f, alive.Count - 0.001f)];
        }

        /// A dead hand bursts up: it hurts players on it and throws 8 small shots.
        void HandBursts(Hazard h)
        {
            float dmg = 25f + Wave * 0.3f;
            foreach (var p in Players.Values)
                if (!p.Downed && p.InvulnT <= 0f && p.Pos.DistanceTo(h.Pos) < h.Radius)
                    Damage(p, dmg, "The Lich (dead hand)");
            float off = Rand(0f, MathX.Tau);
            for (int k = 0; k < 8; k++)
            {
                V2 dir = V2.FromAngle(off + MathX.Tau * k / 8f);
                SpawnBullet(Defs.SmallShot, h.Pos, dir * 150f, 0f, 4f, Defs.BulletDamage(Defs.SmallShot), 0, true).Source = EnemyType.Lich;
            }
            BroadcastFx(FxKind.Boom, h.Pos, new Rgba(0.91f, 0.89f, 0.8f), h.Radius, 0.3f, Sfx.Clang);
        }

        void LichFire(Enemy e, float aim)
        {
            var s = LichOf(e);
            if (s.TransT > 0f || s.FadeT > 0f)
            {
                e.FireT = 0.2f;
                return;
            }
            switch (s.Turn)
            {
                case LichTurn.Fans:  // a fan of 5 aimed piercing shots
                    e.FireT = 0.8f;
                    LichFan(e, aim);
                    break;
                case LichTurn.Rings:  // a slow ring of 16 big orbs, each turned 12 degrees
                    e.FireT = 1.5f;
                    LichRing(e);
                    break;
                case LichTurn.Storm:  // a two-armed spiral that reverses halfway
                    e.FireT = 0.09f;
                    e.Ang += (s.TurnT < s.TurnLen / 2f ? 1f : -1f) * 0.244f;
                    EnemyShot(e, e.Ang, 150f, Defs.SmallShot);
                    EnemyShot(e, e.Ang + MathX.Pi, 150f, Defs.SmallShot);
                    break;
                case LichTurn.Rite:  // fans every second, rings every two (the dead rise in TickLichTurn)
                    e.FireT = 0.1f;
                    s.FanT -= 0.1f;
                    s.RingT -= 0.1f;
                    if (s.FanT <= 0f) { s.FanT = 1f; LichFan(e, aim); }
                    if (s.RingT <= 0f) { s.RingT = 2f; LichRing(e); }
                    break;
                default:  // it holds fire (raising, the sweep, prisons, spirits, hops, hands, breathers)
                    e.FireT = 0.2f;
                    break;
            }
        }

        void LichFan(Enemy e, float aim)
        {
            for (int i = -2; i <= 2; i++)
                EnemyShot(e, aim + i * 0.15f, 190f, Defs.PierceShot);
        }

        void LichRing(Enemy e)
        {
            e.Ang += 12f * MathX.Pi / 180f;
            for (int i = 0; i < 16; i++)
                EnemyShot(e, e.Ang + MathX.Tau * i / 16f, 90f, Defs.BigShot);
        }

        void SyncLich(NetSync s, Enemy e)
        {
            bool has = e.Lich != null;
            s.Bool(ref has);
            if (!has)
                return;
            var q = e.Lich ?? (e.Lich = new LichState { Step = 0 });
            int stage = (int)q.Stage, turn = (int)q.Turn, transTo = (int)q.TransTo, sweep = q.SweepOn;
            s.Byte(ref stage);
            s.Byte(ref turn);
            s.Byte(ref transTo);
            s.Byte(ref sweep);
            s.Float(ref q.TurnT);
            s.Float(ref q.TransT);
            s.Float(ref q.Window);
            s.Float(ref q.SweepA);
            s.Float(ref q.FadeT);
            s.Vec(ref q.BlinkTo);
            int prisons = q.Prisons.Count, spirits = q.Spirits.Count;
            s.Byte(ref prisons);
            s.Byte(ref spirits);
            if (s.Reading)
            {
                q.Stage = (LichStage)stage;
                q.Turn = (LichTurn)turn;
                q.TransTo = (LichStage)transTo;
                q.SweepOn = (byte)sweep;
                while (q.Prisons.Count < prisons) q.Prisons.Add(new BonePrison());
                if (q.Prisons.Count > prisons) q.Prisons.RemoveRange(prisons, q.Prisons.Count - prisons);
                while (q.Spirits.Count < spirits) q.Spirits.Add(new LichSpirit());
                if (q.Spirits.Count > spirits) q.Spirits.RemoveRange(spirits, q.Spirits.Count - spirits);
            }
            foreach (var p in q.Prisons)
            {
                s.Vec(ref p.Center);
                s.Float(ref p.Gap);
                s.Float(ref p.T);
            }
            foreach (var sp in q.Spirits)
            {
                s.Vec(ref sp.Pos);
                s.Bool(ref sp.Diving);
            }
        }

        /// Clients, each frame: the turn clock and the prisons' rise run between snapshots.
        void UpdateLichView(float dt)
        {
            if (IsHost)
                return;
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Lich && e.Lich != null)
                {
                    e.Lich.TurnT += dt;
                    foreach (var p in e.Lich.Prisons)
                        p.T += dt;
                }
        }
    }
}
