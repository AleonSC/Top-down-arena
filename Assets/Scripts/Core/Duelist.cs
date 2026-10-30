using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Duelist's stage of the fight, by its health: three stages, then a last stand.
    public enum DuelStage : byte { EnGarde, Masquerade, ToTheDeath, LastStand }

    /// What it's doing now. Each stage loops its turns, one technique at a time, with breathers (Rest) between.
    public enum DuelTurn : byte { Rest, Volleys, Lunge, Guard, Blink, Masquerade, Crossing, LungeChain, Salute, Flurry, Flourish }

    /// A dash in progress: a lunge, a corner-to-corner crossing, or the strike after a salute.
    public enum DuelDash : byte { None, Lunge, Cross, Salute }

    /// A blade left hanging where the Duelist dashed: it fires two small shots out sideways when its time runs out.
    public class DuelBlade
    {
        public V2 Pos;
        public float Ang, T, Speed;
    }

    /// Everything about the Duelist's fight its body doesn't carry. The host runs it; clients get it in the
    /// snapshot (SyncDuel) to draw the warning lines, the guard, the salute, the stagger, the blades and the circle.
    public class DuelState
    {
        public DuelStage Stage, TransTo;
        public DuelTurn Turn;
        public float TurnT, TransT;      // seconds into the turn; > 0: changing stage (can't be hurt), counting down
        public DuelDash Dash;
        public float DashWarn;           // > 0: the warning line is showing, the dash hasn't started
        public V2 DashFrom;
        public float DashAng, DashLen, DashDone;
        public float SaluteT;            // > 0: sword raised, about to strike
        public float StaggerT;           // > 0: a parried salute knocked it off balance (open: takes more damage)
        public float Ring;               // the dueling circle's radius round the middle (0: none)
        public readonly List<DuelBlade> Blades = new List<DuelBlade>();

        // Host only.
        public int Step = -1, Done, Volley, RealSlot, MaskShot, Diagonal;
        public float TurnLen, MaskAng, MaskT, FlourishT;
        public bool Connected;           // the salute's strike reached someone (so no rake at the end)
    }

    /// The Duelist (wave 30): a duel in three stages by its health, like the Warden's, then a last stand.
    ///   En Garde (100-65%): it strafes as before. Volleys (fan / three red aimed shots), Lunge (a warning line, then
    ///     a fast dash that leaves blades firing sideways), Guard (your shots hitting its front bounce back at you;
    ///     it turns slowly, so get behind it), Blink (a decoy left behind).
    ///   Masquerade (65-30%): three lookalike decoys circle you with it, firing in turn; only its shots are solid,
    ///     the decoys' are hollow. Crossing (an X of corner-to-corner dashes leaving blades), Lunge chain.
    ///   To the Death (30-10%): a dueling circle 360 px round the middle; Salute (sword up, then a lunge: a perfect
    ///     parry knocks it off balance for 2.5 s at +50% damage; dodged, it rakes the spot), Flurry, Guard.
    ///   Last stand (below 10%): the circle closes to 280 px; a spiral, and a salute every 2.5 s.
    public partial class Game
    {
        const float DuelMasqueradeAt = 0.65f, DuelDeathAt = 0.3f, DuelLastAt = 0.1f, DuelTransTime = 2f;
        const float DuelKeep = 280f;
        // Lunges and crossings: warning, speed, length; blades dropped every so far, firing after so long.
        const float DuelLungeWarn = 0.6f, ChainWarn = 0.4f, DuelLungeSpeed = 1400f, DuelLungeLength = 520f, LungeBladeEvery = 85f, LungeBladeHang = 0.3f, LungeBladeSpeed = 180f;
        const float CrossWarn = 0.5f, CrossSpeed = 1300f, CrossInset = 120f, CrossBladeEvery = 70f, CrossBladeHang = 0.8f, CrossBladeSpeed = 200f;
        // The guard: its front, and how fast it turns to face you.
        public const float GuardHalfArc = 70f * MathX.Pi / 180f;
        const float GuardTurn = 90f * MathX.Pi / 180f, GuardTurnFast = 140f * MathX.Pi / 180f, GuardBounceSpeed = 320f;
        // The masquerade: four of them round you, firing in turn.
        public const float MaskRadius = 320f, MaskSquash = 0.8f;
        const float MaskSpin = 0.5f, MaskEvery = 0.5f, MaskShotSpeed = 300f;
        // The dueling circle, the salute and the stagger.
        public const float DuelRingRadius = 360f, DuelRingLast = 280f;
        const float RingDamage = 15f, RingClose = 400f;
        const float SaluteTell = 0.8f, SaluteTellLast = 0.5f, SaluteSpeed = 1500f, FlourishEvery = 2.5f;
        public const float StaggerTime = 2.5f, DuelOpenMult = 1.5f;

        static readonly DuelTurn[][] DuelTurns =
        {
            new[] { DuelTurn.Volleys, DuelTurn.Rest, DuelTurn.Lunge, DuelTurn.Rest, DuelTurn.Guard, DuelTurn.Rest, DuelTurn.Blink },
            new[] { DuelTurn.Masquerade, DuelTurn.Rest, DuelTurn.Crossing, DuelTurn.Rest, DuelTurn.LungeChain, DuelTurn.Rest },
            new[] { DuelTurn.Salute, DuelTurn.Rest, DuelTurn.Flurry, DuelTurn.Rest, DuelTurn.Guard, DuelTurn.Rest },
            new[] { DuelTurn.Flourish },
        };
        static readonly float[][] DuelTimes =
        {
            new[] { 5f, 1f, 4f, 1f, 3f, 0.5f, 1f },
            new[] { 8f, 1f, 5f, 1f, 4f, 1.5f },
            new[] { 5.5f, 1f, 4f, 1f, 2.5f, 1.5f },
            new[] { 999f },
        };
        // When each turn's moves happen (seconds into the turn).
        static readonly float[] LungeAt = { 0f, 1.3f, 2.6f }, ChainAt = { 0f, 1f, 2f }, CrossAt = { 0f, 2.4f }, SaluteAt = { 0f, 2.2f },
            FlurryAt = { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 2f, 2.2f, 2.4f, 2.6f, 2.8f }, BlinkAt = { 0f }, NoMoves = new float[0];

        DuelState DuelOf(Enemy e) => e.Duel ?? (e.Duel = new DuelState());

        public static bool DuelImmune(Enemy e) => e.Type == EnemyType.Duelist && e.Duel != null && e.Duel.TransT > 0f;

        /// Knocked off balance by a parried salute: it takes 50% more damage.
        public static bool DuelOpen(Enemy e) => e.Type == EnemyType.Duelist && e.Duel != null && e.Duel.StaggerT > 0f;

        public static bool DuelGuarding(Enemy e) => e.Type == EnemyType.Duelist && e.Duel != null && e.Duel.Turn == DuelTurn.Guard && e.Duel.StaggerT <= 0f && e.Duel.TransT <= 0f;

        /// Its raised blade stops shots (and swings) from in front of it.
        bool DuelGuardBlocks(Enemy e, V2 from) =>
            DuelGuarding(e) && Math.Abs(MathX.Fmod((from - e.Pos).Angle - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi) < GuardHalfArc;

        static float[] DuelMoves(DuelTurn t)
        {
            switch (t)
            {
                case DuelTurn.Lunge: return LungeAt;
                case DuelTurn.LungeChain: return ChainAt;
                case DuelTurn.Crossing: return CrossAt;
                case DuelTurn.Salute: return SaluteAt;
                case DuelTurn.Flurry: return FlurryAt;
                case DuelTurn.Blink: return BlinkAt;
                default: return NoMoves;
            }
        }

        /// Host: where it goes (MoveEnemy moves it; dashes move it here in DuelistSpecial).
        V2 DuelistMove(Enemy e, V2 tpos, V2 dir, float dist, ref float speed)
        {
            var s = DuelOf(e);
            V2 middle = new V2(Width / 2f, Height / 2f);
            if (s.TransT > 0f || s.Stage == DuelStage.LastStand)
            {
                speed = s.TransT > 0f ? 420f : 300f;
                return e.Pos.DistanceTo(middle) > 6f ? (middle - e.Pos).Normalized() : V2.Zero;
            }
            if (s.Dash != DuelDash.None || s.SaluteT > 0f || s.StaggerT > 0f || s.Turn == DuelTurn.Guard)
                return V2.Zero;  // planted (dashes move it themselves)
            if (s.Turn == DuelTurn.Masquerade)
            {
                speed = 340f;
                V2 goal = MaskSpot(s, tpos, s.RealSlot);
                return e.Pos.DistanceTo(goal) > 4f ? (goal - e.Pos) / 40f : V2.Zero;
            }
            return KeepDistance(dir, dist, DuelKeep) + dir.Orthogonal() * e.Strafe * 0.8f;  // as it always has
        }

        static V2 MaskSpot(DuelState s, V2 center, int slot)
        {
            float a = s.MaskAng + MathX.Tau * slot / 4f;
            return center + new V2((float)Math.Cos(a), (float)Math.Sin(a) * MaskSquash) * MaskRadius;
        }

        /// Host, every tick: stages, turns, dashes, salutes, the guard, the masquerade, blades and the circle.
        void DuelistSpecial(Enemy e, V2 tpos, float dt)
        {
            var s = DuelOf(e);
            if (s.Step < 0)
                EnterDuelStage(e, DuelStage.EnGarde);
            float frac = e.MaxHp > 0f ? e.Hp / e.MaxHp : 1f;

            TickDuelBlades(s, dt, true);
            FireGuardBounces();
            if (s.TransT > 0f)
            {
                s.TransT -= dt;
                if (s.TransT <= 0f)
                {
                    s.TransT = 0f;
                    EnterDuelStage(e, s.TransTo);
                    StageStartBurst(e, s.Stage == DuelStage.Masquerade ? new Rgba(0.71f, 0.61f, 1f) : new Rgba(1f, 0.42f, 0.5f));
                }
                e.Phase = (byte)s.Turn;
                return;
            }
            bool busy = s.Dash != DuelDash.None || s.SaluteT > 0f;
            if (s.Stage == DuelStage.EnGarde && frac <= DuelMasqueradeAt && !busy) { StartDuelTransition(e, DuelStage.Masquerade); return; }
            if (s.Stage == DuelStage.Masquerade && frac <= DuelDeathAt && !busy) { StartDuelTransition(e, DuelStage.ToTheDeath); return; }
            if (s.Stage == DuelStage.ToTheDeath && frac <= DuelLastAt && !busy)
                EnterDuelStage(e, DuelStage.LastStand);  // straight away, as the others do

            s.TurnT += dt;
            if (s.StaggerT > 0f)
                s.StaggerT = Math.Max(0f, s.StaggerT - dt);

            // The dueling circle closes in to its size; leaning on it hurts.
            float ringGoal = s.Stage == DuelStage.ToTheDeath ? DuelRingRadius : s.Stage == DuelStage.LastStand ? DuelRingLast : 0f;
            s.Ring = ringGoal <= 0f ? 0f : Math.Max(ringGoal, (s.Ring <= 0f ? 1000f : s.Ring) - RingClose * dt);
            if (s.Ring > 0f && s.Ring <= ringGoal + 1f)
                foreach (var p in Players.Values)
                    if (!p.Downed && p.InvulnT <= 0f && p.Pos.DistanceTo(new V2(Width / 2f, Height / 2f)) > s.Ring - p.Def.BodyR - 3f)
                        Damage(p, RingDamage, "The Duelist (dueling circle)");

            // This turn's moves, on time (missed while it reels from a parry).
            var moves = DuelMoves(s.Turn);
            while (s.Done < moves.Length && s.TurnT >= moves[s.Done])
            {
                if (s.StaggerT <= 0f)
                    DuelMove(e, s, s.Done, tpos);
                s.Done++;
            }
            if (s.Turn == DuelTurn.Flourish && s.StaggerT <= 0f && !busy && (s.FlourishT -= dt) <= 0f)
            {
                s.FlourishT = FlourishEvery;
                StartSalute(e, s, SaluteTellLast);
            }

            // The salute: sword up, then the strike.
            if (s.SaluteT > 0f && s.StaggerT <= 0f)
            {
                e.Facing = (tpos - e.Pos).Angle;
                s.SaluteT -= dt;
                if (s.SaluteT <= 0f)
                {
                    s.SaluteT = 0f;
                    StartDash(e, s, DuelDash.Salute, (tpos - e.Pos).Angle, e.Pos.DistanceTo(tpos) + 40f, 0f);
                    s.Connected = false;
                }
            }
            if (s.Dash != DuelDash.None)
                TickDuelDash(e, s, dt);

            // The guard turns to face you, slowly.
            if (DuelGuarding(e))
            {
                float lim = (s.Stage == DuelStage.ToTheDeath ? GuardTurnFast : GuardTurn) * dt;
                float diff = MathX.Fmod((tpos - e.Pos).Angle - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi;
                e.Facing += Math.Max(-lim, Math.Min(lim, diff));
            }
            else if (s.Dash == DuelDash.None && s.SaluteT <= 0f && s.StaggerT <= 0f)
                e.Facing = (tpos - e.Pos).Angle;

            // The masquerade: the decoys keep their places round you, and they all take turns to fire.
            if (s.Turn == DuelTurn.Masquerade)
            {
                s.MaskAng += MaskSpin * dt;
                foreach (var d in Enemies.Values)
                    if (d.Type == EnemyType.Decoy && d.ParentId == e.Id && d.Hp > 0f)
                    {
                        V2 goal = MaskSpot(s, tpos, (int)d.Ang);
                        d.Pos = ClampToArena(d.Pos + (goal - d.Pos).LimitLength(400f * dt), d.Radius);
                        d.Facing = (tpos - d.Pos).Angle;
                    }
                if ((s.MaskT -= dt) <= 0f && s.TurnT > 0.6f)
                {
                    s.MaskT = MaskEvery;
                    int k = s.MaskShot++ % 4;
                    if (k == s.RealSlot)
                        EnemyShot(e, (tpos - e.Pos).Angle, MaskShotSpeed, Defs.PierceShot);
                    else
                        foreach (var d in Enemies.Values)
                            if (d.Type == EnemyType.Decoy && d.ParentId == e.Id && (int)d.Ang == k && d.Hp > 0f && !IsSpawning(d))
                            {
                                V2 dir = (tpos - d.Pos).Normalized();
                                SpawnBullet(Defs.HollowShot, d.Pos + dir * d.Radius, dir * MaskShotSpeed, 0f, 6f, 0f, 0, true).Source = e.Type;
                                break;
                            }
                }
            }

            // Inside the circle.
            if (s.Ring > 0f)
                e.Pos = InDuelRing(e.Pos, e.Radius, s.Ring);
            e.Phase = (byte)s.Turn;
            if (s.Stage != DuelStage.LastStand && s.TurnT >= s.TurnLen && s.Dash == DuelDash.None && s.SaluteT <= 0f)
                NextDuelTurn(e, s);
        }

        /// One of this turn's timed moves.
        void DuelMove(Enemy e, DuelState s, int k, V2 tpos)
        {
            switch (s.Turn)
            {
                case DuelTurn.Lunge:
                case DuelTurn.LungeChain:
                    StartDash(e, s, DuelDash.Lunge, (tpos - e.Pos).Angle, DuelLungeLength, s.Turn == DuelTurn.Lunge ? DuelLungeWarn : ChainWarn);
                    break;
                case DuelTurn.Crossing:
                {
                    if (k == 0)
                        s.Diagonal = rng.Next(2);
                    bool first = (k == 0) == (s.Diagonal == 0);
                    V2 from = first ? new V2(CrossInset, CrossInset) : new V2(Width - CrossInset, CrossInset);
                    V2 to = first ? new V2(Width - CrossInset, Height - CrossInset) : new V2(CrossInset, Height - CrossInset);
                    BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, 50f, 0.3f, Sfx.None);
                    e.Pos = e.Disp = from;
                    BroadcastFx(FxKind.Ring, from, e.Def.Color, 60f, 0.3f, Sfx.BossShift);
                    StartDash(e, s, DuelDash.Cross, (to - from).Angle, from.DistanceTo(to), CrossWarn);
                    break;
                }
                case DuelTurn.Salute:
                    StartSalute(e, s, SaluteTell);
                    break;
                case DuelTurn.Flurry:
                {
                    // A slash: a 70-degree arc of 9 small shots, left of you then right, stepping in.
                    float aim = (tpos - e.Pos).Angle + (k % 2 == 1 ? 0.45f : -0.45f);
                    for (int i = 0; i < 9; i++)
                        EnemyShot(e, aim + (i - 4) * (1.22f / 8f), 240f, Defs.SmallShot);
                    e.Pos = ClampToArena(e.Pos + (tpos - e.Pos).Normalized() * 30f, e.Radius);
                    BroadcastFx(FxKind.Slash, e.Pos, e.Pos + V2.FromAngle(aim) * 90f, new Rgba(1f, 0.85f, 0.87f), 0.6f, 0.2f, k % 5 == 0 ? Sfx.SwingHeavy : Sfx.SwingLight);
                    break;
                }
                case DuelTurn.Blink:
                {
                    // Today's blink: a decoy left where it stood, and it reappears away from you.
                    var d = SpawnEnemyAt(EnemyType.Decoy, e.Pos, 0f, 0);
                    d.PhaseT = 3f;
                    d.FireT = 99f;
                    d.Facing = e.Facing;
                    V2 from = e.Pos;
                    e.Pos = e.Disp = FarFromPlayers(e.Radius);
                    BroadcastFx(FxKind.Ring, from, e.Def.Color, 50f, 0.3f, Sfx.None);
                    BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, 60f, 0.3f, Sfx.BossShift);
                    break;
                }
            }
        }

        void StartSalute(Enemy e, DuelState s, float tell)
        {
            s.SaluteT = tell;
            BroadcastFx(FxKind.Ring, e.Pos, new Rgba(1f, 0.8f, 0.35f), 70f, tell, Sfx.BossChime);
        }

        void StartDash(Enemy e, DuelState s, DuelDash kind, float ang, float len, float warn)
        {
            s.Dash = kind;
            s.DashWarn = warn;
            s.DashFrom = e.Pos;
            s.DashAng = ang;
            s.DashLen = len;
            s.DashDone = 0f;
            e.Facing = ang;
            if (warn <= 0f)
                Sounds.Add(Sfx.BossCharge);
        }

        void TickDuelDash(Enemy e, DuelState s, float dt)
        {
            if (s.DashWarn > 0f)
            {
                s.DashWarn -= dt;
                if (s.DashWarn <= 0f)
                    BroadcastFx(FxKind.Ring, e.Pos, new Rgba(1f, 0.3f, 0.42f), 50f, 0.2f, Sfx.BossCharge);
                return;
            }
            float speed = s.Dash == DuelDash.Cross ? CrossSpeed : s.Dash == DuelDash.Salute ? SaluteSpeed : DuelLungeSpeed;
            float every = s.Dash == DuelDash.Cross ? CrossBladeEvery : LungeBladeEvery;
            float before = s.DashDone;
            s.DashDone = Math.Min(s.DashLen, s.DashDone + speed * dt);
            V2 dir = V2.FromAngle(s.DashAng), next = s.DashFrom + dir * s.DashDone;
            V2 held = ClampToArena(next, e.Radius);
            if (s.Ring > 0f)
                held = InDuelRing(held, e.Radius, s.Ring);
            bool stopped = held.DistanceTo(next) > 1f;
            e.Pos = held;
            // Blades left along the way (not the salute's strike).
            if (s.Dash != DuelDash.Salute)
                for (float at = (float)Math.Floor(before / every + 1f) * every; at <= s.DashDone; at += every)
                    s.Blades.Add(new DuelBlade
                    {
                        Pos = s.DashFrom + dir * at, Ang = s.DashAng,
                        T = s.Dash == DuelDash.Cross ? CrossBladeHang : LungeBladeHang, Speed = s.Dash == DuelDash.Cross ? CrossBladeSpeed : LungeBladeSpeed,
                    });
            if (s.Dash == DuelDash.Salute)
                foreach (var p in Players.Values)
                {
                    if (p.Downed)
                        continue;
                    float d = p.Pos.DistanceTo(e.Pos);
                    // A perfect parry as it arrives knocks it off balance (ParryContact still blocks the hit).
                    if (p.ParryActive && d < e.Radius + p.Def.BodyR + ParryReach + 6f && InParryArc(p, e.Pos, e.Radius) && PerfectNow(p))
                    {
                        s.StaggerT = StaggerTime;
                        s.Dash = DuelDash.None;
                        s.Connected = true;
                        Recorder?.Parry(p, 0f, true, false);
                        BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.8f, 0.35f), 90f, 0.4f, Sfx.Clang);
                        DoLog(p.Name + " parried the Duelist's salute");
                        return;
                    }
                    if (d < e.Radius + p.Def.HitR + 10f)
                        s.Connected = true;
                }
            if (stopped || s.DashDone >= s.DashLen)
            {
                if (s.Dash == DuelDash.Salute && !s.Connected)
                {
                    // Dodged: it rakes the spot.
                    Player near = NearestPlayer(e.Pos);
                    float aim = near != null ? (near.Pos - e.Pos).Angle : s.DashAng;
                    for (int i = 0; i < 9; i++)
                        EnemyShot(e, aim + (i - 4) * 0.16f, 230f, Defs.SmallShot);
                }
                s.Dash = DuelDash.None;
            }
        }

        /// Blades count down; on the host they fire two small shots out sideways and go (clients just draw them).
        void TickDuelBlades(DuelState s, float dt, bool host)
        {
            for (int i = s.Blades.Count - 1; i >= 0; i--)
            {
                var bl = s.Blades[i];
                bl.T -= dt;
                if (bl.T > 0f)
                    continue;
                if (host)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        V2 dir = V2.FromAngle(bl.Ang + side * MathX.Pi / 2f);
                        SpawnBullet(Defs.SmallShot, bl.Pos, dir * bl.Speed, 0f, 8f, Defs.BulletDamage(Defs.SmallShot), 0, true).Source = EnemyType.Duelist;
                    }
                s.Blades.RemoveAt(i);
            }
        }

        static V2 InDuelRing(V2 pos, float r, float ring)
        {
            V2 middle = new V2(Width / 2f, Height / 2f), d = pos - middle;
            float lim = ring - r;
            return d.LengthSq > lim * lim ? middle + d.Normalized() * lim : pos;
        }

        /// The dueling circle round the middle, if there is one (every game: players keep themselves inside it).
        public float DuelRingNow()
        {
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Duelist && e.Duel != null && e.Duel.TransT <= 0f && e.Duel.Ring > 0f)
                    return e.Duel.Ring;
            return 0f;
        }

        V2 KeepInDuelRing(V2 pos, float r)
        {
            float ring = DuelRingNow();
            return ring > 0f ? InDuelRing(pos, r, ring) : pos;
        }

        void EnterDuelStage(Enemy e, DuelStage stage)
        {
            var s = DuelOf(e);
            s.Stage = stage;
            s.Step = -1;
            s.FlourishT = 1.5f;
            NextDuelTurn(e, s);
            DoLog("the Duelist: " + stage);
        }

        void StartDuelTransition(Enemy e, DuelStage to)
        {
            var s = DuelOf(e);
            s.TransT = DuelTransTime;
            s.TransTo = to;
            s.Dash = DuelDash.None;
            s.SaluteT = s.StaggerT = 0f;
            s.Blades.Clear();
            ClearDuelDecoys(e, false);
            ClearEnemyBullets();
            session.Broadcast(Proto.End(Proto.Begin(Msg.ClearEnemyBullets)));
            BroadcastFx(FxKind.Ring, e.Pos, to == DuelStage.Masquerade ? new Rgba(0.71f, 0.61f, 1f) : new Rgba(1f, 0.42f, 0.5f), 900f, 0.8f, Sfx.BossChime);
            DoLog("the Duelist: changing to " + to);
        }

        void NextDuelTurn(Enemy e, DuelState s)
        {
            if (s.Turn == DuelTurn.Masquerade)
                ClearDuelDecoys(e, true);
            var turns = DuelTurns[(int)s.Stage];
            s.Step = (s.Step + 1) % turns.Length;
            s.Turn = turns[s.Step];
            s.TurnLen = DuelTimes[(int)s.Stage][s.Step];
            s.TurnT = 0f;
            s.Done = 0;
            e.FireT = 0.4f;
            if (s.Turn == DuelTurn.Masquerade)
            {
                // It and three lookalikes take places round its target; only it is real.
                Player p = NearestPlayer(e.Pos);
                V2 center = p != null ? p.Pos : new V2(Width / 2f, Height / 2f);
                s.RealSlot = rng.Next(4);
                s.MaskAng = (e.Pos - center).Angle - MathX.Tau * s.RealSlot / 4f;
                s.MaskT = MaskEvery;
                s.MaskShot = rng.Next(4);
                BroadcastFx(FxKind.Ring, e.Pos, new Rgba(0.71f, 0.61f, 1f), 70f, 0.35f, Sfx.Summon);
                for (int k = 0; k < 4; k++)
                {
                    if (k == s.RealSlot)
                        continue;
                    var d = SpawnEnemyAt(EnemyType.Decoy, e.Pos, 0.4f, e.Id);
                    d.MaxHp = d.Hp = 1f;  // one hit shatters it
                    d.PhaseT = 99f;
                    d.FireT = 99f;
                    d.Ang = k;
                }
            }
            e.Phase = (byte)s.Turn;
        }

        /// The masquerade's decoys: at the end of the turn each pops into a ring (or, changing stage, just winks out).
        void ClearDuelDecoys(Enemy e, bool pop)
        {
            scratchIds.Clear();
            foreach (var d in Enemies.Values)
                if (d.Type == EnemyType.Decoy && d.ParentId == e.Id)
                    scratchIds.Add(d.Id);
            foreach (int id in scratchIds)
            {
                var d = Enemies[id];
                if (pop && d.Hp > 0f)
                    DecoyRing(d, 12, 140f);
                Enemies.Remove(id);
            }
        }

        void DecoyRing(Enemy d, int n, float speed)
        {
            for (int i = 0; i < n; i++)
                EnemyShot(d, MathX.Tau * i / n, speed, Defs.SmallShot);
            BroadcastFx(FxKind.Ring, d.Pos, new Rgba(0.71f, 0.61f, 1f), 50f, 0.3f, Sfx.None);
        }

        /// A masquerade decoy shot down shatters into a ring (Combat.cs, as it dies).
        void DecoyShatter(Enemy d)
        {
            if (d.Type == EnemyType.Decoy && d.ParentId != 0)
                DecoyRing(d, 12, 150f);
        }

        /// Its guard sends a blocked shot back at whoever fired it.
        void DuelGuardBounce(Enemy e, Bullet b)
        {
            if (b.Owner == 0 || !DuelGuardBlocks(e, b.Pos))
                return;
            Player shooter;
            V2 at = e.Pos + (b.Pos - e.Pos).Normalized() * (e.Radius + 10f);
            float aim = Players.TryGetValue(b.Owner, out shooter) ? (shooter.Pos - at).Angle : (b.Pos - e.Pos).Angle;
            guardBounces.Add(new Bullet { Pos = at, Vel = V2.FromAngle(aim + Rand(-0.06f, 0.06f)) * GuardBounceSpeed });  // fired next tick (Collide is walking the bullets)
        }
        readonly List<Bullet> guardBounces = new List<Bullet>();

        void FireGuardBounces()
        {
            foreach (var b in guardBounces)
                SpawnBullet(Defs.SmallShot, b.Pos, b.Vel, 0f, 6f, Defs.BulletDamage(Defs.SmallShot), 0, true).Source = EnemyType.Duelist;
            guardBounces.Clear();
        }

        void DuelistFire(Enemy e, float aim)
        {
            var s = DuelOf(e);
            if (s.TransT > 0f || s.StaggerT > 0f || s.Dash != DuelDash.None || s.SaluteT > 0f)
            {
                e.FireT = 0.2f;
                return;
            }
            switch (s.Turn)
            {
                case DuelTurn.Volleys:  // as it always has: three red aimed shots, every third volley a fan of 11
                    e.FireT = 0.9f;
                    s.Volley++;
                    if (s.Volley % 3 == 0)
                        for (int i = 0; i < 11; i++)
                            EnemyShot(e, aim + (i - 5) * 0.1f, 230f, Defs.SmallShot);
                    else
                        for (int k = 0; k < 3; k++)
                            EnemyShot(e, aim, 260f + k * 40f, Defs.PierceShot);
                    break;
                case DuelTurn.Flourish:  // a three-armed spiral of blades
                    e.FireT = 0.12f;
                    e.Ang += 0.25f;
                    for (int k = 0; k < 3; k++)
                        EnemyShot(e, e.Ang + MathX.Tau * k / 3f, 150f, Defs.SmallShot);
                    break;
                default:  // the rest are timed moves (DuelistSpecial)
                    e.FireT = 0.2f;
                    break;
            }
        }

        void SyncDuel(NetSync s, Enemy e)
        {
            bool has = e.Duel != null;
            s.Bool(ref has);
            if (!has)
                return;
            var q = e.Duel ?? (e.Duel = new DuelState { Step = 0 });
            int stage = (int)q.Stage, turn = (int)q.Turn, transTo = (int)q.TransTo, dash = (int)q.Dash;
            s.Byte(ref stage);
            s.Byte(ref turn);
            s.Byte(ref transTo);
            s.Byte(ref dash);
            s.Float(ref q.TurnT);
            s.Float(ref q.TransT);
            s.Float(ref q.DashWarn);
            s.Vec(ref q.DashFrom);
            s.Float(ref q.DashAng);
            s.Float(ref q.DashLen);
            s.Float(ref q.DashDone);
            s.Float(ref q.SaluteT);
            s.Float(ref q.StaggerT);
            s.Float(ref q.Ring);
            int n = q.Blades.Count;
            s.Byte(ref n);
            if (s.Reading)
            {
                q.Stage = (DuelStage)stage;
                q.Turn = (DuelTurn)turn;
                q.TransTo = (DuelStage)transTo;
                q.Dash = (DuelDash)dash;
                while (q.Blades.Count < n)
                    q.Blades.Add(new DuelBlade());
                if (q.Blades.Count > n)
                    q.Blades.RemoveRange(n, q.Blades.Count - n);
            }
            for (int i = 0; i < n; i++)
            {
                var bl = q.Blades[i];
                s.Vec(ref bl.Pos);
                s.Float(ref bl.Ang);
                s.Float(ref bl.T);
            }
        }

        /// Clients, each frame: the clocks run between snapshots.
        void UpdateDuelView(float dt)
        {
            if (IsHost)
                return;
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Duelist && e.Duel != null)
                {
                    var s = e.Duel;
                    s.TurnT += dt;
                    s.DashWarn = Math.Max(0f, s.DashWarn - dt);
                    s.SaluteT = Math.Max(0f, s.SaluteT - dt);
                    s.StaggerT = Math.Max(0f, s.StaggerT - dt);
                    s.TransT = Math.Max(0f, s.TransT - dt);
                    TickDuelBlades(s, dt, false);
                }
        }
    }
}
