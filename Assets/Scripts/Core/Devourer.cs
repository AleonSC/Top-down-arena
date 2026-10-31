using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Devourer's stage of the fight, by its health: three stages, then a last stand.
    public enum DevourStage : byte { Appetite, Gorging, Ravenous, LastStand }

    /// What it's doing now. Each stage loops its turns, one attack at a time, with breathers (Rest) between.
    public enum DevourTurn : byte { Rest, Spiral, Snap, Hungry, Inhale, Bile, Burrow, Frenzy, Jaws, Vomit, LastMeal }

    /// Everything about the Devourer's fight its body doesn't carry. The host runs it; clients get it in the snapshot
    /// (SyncDevour) to draw the lunge lines, the inhale cone, the burrow ripple, the jaws and the teeth, and to pull
    /// their own player into its mouth.
    public class DevourState
    {
        public DevourStage Stage, TransTo;
        public DevourTurn Turn;
        public float TurnT, TransT;  // seconds into the turn; > 0: changing stage (can't be hurt), counting down
        public bool Lunging;         // a lunge: the warning line shows for LungeWarn, then it dashes LungeLen along LungeAng
        public float LungeWarn, LungeAng, LungeLen;
        public V2 LungeFrom;
        public float InhaleT;        // > 0: breathing in (seconds left); the cone points along the body's Facing
        public float BurrowT;        // > 0: underground (seconds since it dived); it surfaces at BurrowTime
        public V2 JawsAt;            // the Jaws close on this spot: the teeth show for JawsT, then fly
        public bool JawsSide;        // true: the rows come from the left and right instead of above and below
        public float JawsT;
        public float Spin;           // the last stand's ring of teeth
        public int SwallowId;        // the player inside it (0 for none)

        // Host only.
        public int Step = -1, Fired, Who;
        public float TurnLen, ActT, LungeDone, SwallowT, SwallowTaken, StomachT, BileTickT;
        public int BurrowTarget;
        public bool LungeRing;
        public byte MawHit;          // the last shot that reached it (BlocksShot): 1 into the Maw, 2 on its back
    }

    /// The Devourer (wave 45): a fight in three stages by its health, like the Herald's, then a last stand. Each stage
    /// turns its hunger into something new.
    ///   Appetite (100-65%): it keeps its distance as it always has. Spiral (its old double spiral), Snap (lunging
    ///     bites along a warning line), Hungry (your shots heal it, as before, while it spits morsels and crawls over to
    ///     eat them; shoot them or step on them first).
    ///   Gorging (65-30%): bigger. Inhale (a cone that pulls you into its mouth; whoever reaches it is swallowed: hidden
    ///     inside, hurt each second, until allies' doubled shots or 3 s spit them out), Bile (lobbed pools of acid),
    ///     Burrow (a ripple chases you underground and erupts).
    ///   Ravenous (30-10%): the Maw, its mouth always open on one side: shots into it heal it, shots on its back do
    ///     more; it turns slowly to the nearest player. Frenzy (lunges that end in rings), Jaws (two rows of teeth close
    ///     on you), Vomit (a sweeping spray of bile).
    ///   Last stand (below 10%): the Last meal in the middle: it pulls everyone in, a ring of teeth turns around it,
    ///     its spiral comes back.
    /// Built from the mockup in Docs/Mockups/devourer-3-stage.html. Drawn in DevourerLook.cs.
    public partial class Game
    {
        const float DevourGorgeAt = 0.65f, DevourRavenAt = 0.3f, DevourLastAt = 0.1f, DevourTransTime = 2f;
        public const float BiteReach = 110f, DevourLungeSpeed = 1000f, SnapWarn = 0.7f, FrenzyWarn = 0.55f;
        public const float MorselEat = 0.01f;  // of its health, for each morsel it eats
        const float MorselCrawl = 90f, DevourKeep = 260f;
        public const float InhaleTime = 2f, InhaleReach = 600f, InhaleHalf = 0.61f, InhalePullNear = 200f, InhalePullFar = 80f;
        public const float SwallowTime = 3f, SwallowFreeAt = 300f;
        public const float BileRadius = 90f, BileFlight = 1f, BileLife = 6f, BileDps = 12f;
        public const float BurrowTime = 2.5f, BurrowChase = 1.9f, BurrowSpeed = 260f, EruptRadius = 100f;
        public const float MawHalf = MathX.Pi / 4f, MawTurn = MathX.Pi / 3f, MawTurnSolo = 35f * MathX.Pi / 180f, MawBackMult = 1.25f;
        public const float JawsWarn = 0.6f, JawsReach = 320f, JawsSpeed = 140f;
        public const float TeethRadius = 170f, TeethSpin = 1f, ToothHit = 22f, LastMealPull = 55f;

        static readonly DevourTurn[][] DevourTurns =
        {
            new[] { DevourTurn.Spiral, DevourTurn.Rest, DevourTurn.Snap, DevourTurn.Rest, DevourTurn.Hungry, DevourTurn.Rest },
            new[] { DevourTurn.Inhale, DevourTurn.Rest, DevourTurn.Bile, DevourTurn.Rest, DevourTurn.Burrow, DevourTurn.Rest },
            new[] { DevourTurn.Frenzy, DevourTurn.Rest, DevourTurn.Jaws, DevourTurn.Rest, DevourTurn.Vomit, DevourTurn.Rest },
            new[] { DevourTurn.LastMeal },
        };
        static readonly float[][] DevourTimes =
        {
            new[] { 6f, 1f, 5f, 1f, 5f, 1.5f },
            new[] { 5f, 1f, 6f, 1f, 7f, 1.5f },
            new[] { 6f, 1f, 6f, 1f, 5f, 1.5f },
            new[] { 999f },
        };

        DevourState DevourOf(Enemy e) => e.Devour ?? (e.Devour = new DevourState());

        /// Changing stage, or underground: it can't be hurt.
        public static bool DevourImmune(Enemy e) => e.Type == EnemyType.Devourer && e.Devour != null && (e.Devour.TransT > 0f || e.Devour.BurrowT > 0f);

        /// Underground: shots pass over it, and its body doesn't hurt or shove.
        public static bool DevourUnder(Enemy e) => e.Type == EnemyType.Devourer && e.Devour != null && e.Devour.BurrowT > 0f;

        /// Breathing in, or in its last stand: touching its mouth means being swallowed, so it doesn't shove you out.
        public static bool DevourSwallowing(Enemy e) => e.Type == EnemyType.Devourer && e.Devour != null && e.Devour.TransT <= 0f
            && (e.Devour.InhaleT > 0f || e.Devour.Stage == DevourStage.LastStand);

        /// It grows as it eats: 50 px, 58 in the Gorging, 64 from the Ravenous on.
        public static float DevourRadius(Enemy e)
        {
            var stage = e.Devour == null ? DevourStage.Appetite : e.Devour.TransT > 0f ? e.Devour.TransTo : e.Devour.Stage;
            return stage == DevourStage.Appetite ? e.Def.Radius : stage == DevourStage.Gorging ? e.Def.Radius + 8f : e.Def.Radius + 14f;
        }

        /// Bites, the eruption and the teeth: a little more each wave (40 at wave 45).
        float BiteDamage => 13f + Wave * 0.6f;

        /// Is `from` inside the Maw, the 90-degree cone its mouth faces?
        public static bool InMaw(Enemy e, V2 from) => Math.Abs(MathX.Fmod((from - e.Pos).Angle - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi) < MawHalf;

        static Rgba DevourColor(DevourStage stage) => stage == DevourStage.Gorging ? new Rgba(0.78f, 0.88f, 0.35f)
            : stage == DevourStage.Ravenous ? new Rgba(1f, 0.24f, 0.37f) : stage == DevourStage.LastStand ? new Rgba(1f, 0.16f, 0.29f) : new Rgba(1f, 0.35f, 0.48f);

        Enemy FindDevourer()
        {
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Devourer && e.Devour != null && e.Hp > 0f)
                    return e;
            return null;
        }

        /// Where it goes (MoveEnemy moves it; lunges and burrowing move it in DevourSpecial).
        V2 DevourMove(Enemy e, V2 dir, float dist, ref float speed)
        {
            var s = DevourOf(e);
            if (s.TransT > 0f || s.Lunging || s.BurrowT > 0f || s.InhaleT > 0f || s.SwallowId != 0 && s.Stage != DevourStage.LastStand)
                return V2.Zero;  // planted
            if (s.Stage == DevourStage.LastStand)
            {
                V2 middle = new V2(Width / 2f, Height / 2f);
                speed = 160f;
                return e.Pos.DistanceTo(middle) > 6f ? (middle - e.Pos).Normalized() : V2.Zero;
            }
            if (s.Turn == DevourTurn.Hungry)
            {
                Enemy m = NearestMorsel(e.Pos);
                if (m != null)
                {
                    speed = MorselCrawl;
                    return (m.Pos - e.Pos).Normalized();
                }
            }
            return KeepDistance(dir, dist, DevourKeep);  // as it always has
        }

        Enemy NearestMorsel(V2 from)
        {
            Enemy best = null;
            foreach (var m in Enemies.Values)
                if (m.Type == EnemyType.Morsel && m.Hp > 0f && (best == null || m.Pos.DistanceSq(from) < best.Pos.DistanceSq(from)))
                    best = m;
            return best;
        }

        /// The nearest player still outside it.
        Player NearestFree(V2 from)
        {
            Player best = null;
            foreach (var p in Players.Values)
                if (!p.Downed && p.SwallowT <= 0f && (best == null || p.Pos.DistanceSq(from) < best.Pos.DistanceSq(from)))
                    best = p;
            return best;
        }

        /// The next player in turn (lunges, inhales and burrows take turns between players).
        Player NextDevourTarget(DevourState s)
        {
            var alive = AlivePlayersList();
            alive.RemoveAll(p => p.Id == s.SwallowId);
            if (alive.Count == 0)
                return FirstAlive();
            alive.Sort((a, b) => a.Id.CompareTo(b.Id));
            return alive[s.Who++ % alive.Count];
        }

        void DevourSpecial(Enemy e, V2 tpos, float dt)
        {
            var s = DevourOf(e);
            if (s.Step < 0)
                EnterDevourStage(e, DevourStage.Appetite);
            float frac = e.MaxHp > 0f ? e.Hp / e.MaxHp : 1f;
            e.Radius = DevourRadius(e);

            if (s.TransT > 0f)
            {
                s.TransT -= dt;
                if (s.TransT <= 0f)
                {
                    s.TransT = 0f;
                    EnterDevourStage(e, s.TransTo);
                    StageStartBurst(e, DevourColor(s.Stage));
                }
                return;
            }
            if (!s.Lunging && s.BurrowT <= 0f)
            {
                if (s.Stage == DevourStage.Appetite && frac <= DevourGorgeAt) { StartDevourTransition(e, DevourStage.Gorging); return; }
                if (s.Stage == DevourStage.Gorging && frac <= DevourRavenAt) { StartDevourTransition(e, DevourStage.Ravenous); return; }
                if (s.Stage == DevourStage.Ravenous && frac <= DevourLastAt)
                    EnterDevourStage(e, DevourStage.LastStand);  // straight away, as the others do
            }

            s.TurnT += dt;
            TickSwallowed(e, s, dt);
            TickBile(dt);
            if (s.Lunging)
                TickLunge(e, s, dt);
            if (s.BurrowT > 0f)
                TickBurrow(e, s, dt);
            if (s.InhaleT > 0f)
            {
                s.InhaleT = Math.Max(0f, s.InhaleT - dt);
                SwallowAtMouth(e, s);
            }
            if (s.JawsT > 0f)
            {
                s.JawsT -= dt;
                if (s.JawsT <= 0f)
                    CloseJaws(e, s);
            }
            if (s.Turn == DevourTurn.Hungry)
                EatMorsels(e);
            StepOnMorsels();

            // Facing: the Maw turns slowly towards the nearest player; otherwise it looks at whoever it's after.
            Player near = NearestFree(e.Pos);
            if (!s.Lunging && s.InhaleT <= 0f && s.Turn != DevourTurn.Vomit && near != null)
            {
                float want = (near.Pos - e.Pos).Angle;
                if (s.Stage == DevourStage.Ravenous)
                {
                    // 60 degrees a second; alone, 35 (at 60 it would outrun anyone circling it from range)
                    float diff = MathX.Fmod(want - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi, turn = (Players.Count > 1 ? MawTurn : MawTurnSolo) * dt;
                    e.Facing += Math.Max(-turn, Math.Min(turn, diff));
                }
                else if (s.Turn == DevourTurn.Hungry)
                {
                    Enemy m = NearestMorsel(e.Pos);
                    e.Facing = m != null ? (m.Pos - e.Pos).Angle : want;
                }
                else
                    e.Facing = want;
            }

            if (s.Stage == DevourStage.LastStand)
            {
                s.Spin += TeethSpin * dt;
                PullCenter = e.Pos;
                PullStrength = LastMealPull;
                SwallowAtMouth(e, s);
                foreach (var p in Players.Values)
                {
                    if (p.Downed || p.InvulnT > 0f || p.SwallowT > 0f)
                        continue;
                    for (int k = 0; k < 12; k++)
                        if (ToothOn(k) && p.Pos.DistanceTo(ToothPos(e.Pos, s.Spin, k)) < ToothHit + p.Def.HitR)
                        {
                            Damage(p, BiteDamage * 0.75f, "The Devourer (teeth)");
                            break;
                        }
                }
            }

            s.ActT -= dt;
            if (s.ActT <= 0f)
                switch (s.Turn)
                {
                    case DevourTurn.Snap:
                        s.ActT = 1.6f;
                        if (s.Fired++ < 3)
                            StartLunge(e, s, NextDevourTarget(s), SnapWarn, false);
                        break;
                    case DevourTurn.Frenzy:
                        s.ActT = 1.4f;
                        if (s.Fired++ < 4)
                            StartLunge(e, s, NextDevourTarget(s), FrenzyWarn, true);
                        break;
                    case DevourTurn.Inhale:
                        s.ActT = 0.1f;
                        if (s.InhaleT <= 0f && s.SwallowId == 0 && (s.Fired == 0 || s.Fired == 1 && s.TurnT >= 2.5f))
                        {
                            s.Fired++;
                            var q = NextDevourTarget(s);
                            if (q != null)
                                e.Facing = (q.Pos - e.Pos).Angle;
                            s.InhaleT = InhaleTime;
                            BroadcastFx(FxKind.Ring, e.Pos, DevourColor(DevourStage.Gorging), e.Radius + 40f, 0.4f, Sfx.BossCharge);
                        }
                        break;
                    case DevourTurn.Bile:
                        s.ActT = 1.5f;
                        foreach (var p in AlivePlayersList())
                            if (p.SwallowT <= 0f)
                                SpawnPatchAt(PatchKind.Bile, ClampToArena(p.Pos, 60f), BileFlight, BileFlight + BileLife, BileRadius, BileRadius);
                        SpawnPatchAt(PatchKind.Bile, RandomInArena(120f), BileFlight, BileFlight + BileLife, BileRadius, BileRadius);
                        Sounds.Add(Sfx.BossPulse);
                        break;
                    case DevourTurn.Burrow:
                        s.ActT = 0.1f;
                        if (s.BurrowT <= 0f && (s.Fired == 0 || s.Fired == 1 && s.TurnT >= 3.5f))
                        {
                            s.Fired++;
                            var q = NextDevourTarget(s);
                            s.BurrowTarget = q != null ? q.Id : 0;
                            s.BurrowT = 0.001f;
                            BroadcastFx(FxKind.Ring, e.Pos, new Rgba(0.85f, 0.64f, 0.35f), 90f, 0.4f, Sfx.Explode);
                        }
                        break;
                    case DevourTurn.Jaws:
                        s.ActT = 3f;
                        {
                            var q = NextDevourTarget(s);
                            if (q != null)
                            {
                                bool side = q.Pos.Y < JawsReach - 40f || q.Pos.Y > Height - JawsReach + 40f;  // near the top or bottom: rows from the sides
                                s.JawsSide = side;
                                s.JawsAt = q.Pos;
                                s.JawsT = JawsWarn;
                                Sounds.Add(Sfx.BossCharge);
                            }
                        }
                        break;
                    default:
                        s.ActT = 0.1f;
                        break;
                }
            if (s.Stage != DevourStage.LastStand && s.TurnT >= s.TurnLen && !s.Lunging && s.BurrowT <= 0f && s.InhaleT <= 0f)
                NextDevourTurn(e, s);
        }

        static bool ToothOn(int k) => k != 0 && k != 6;  // ten teeth in twelve places: two gaps across from each other

        public static V2 ToothPos(V2 center, float spin, int k) => center + V2.FromAngle(spin + MathX.Tau * k / 12f) * TeethRadius;

        void StartLunge(Enemy e, DevourState s, Player q, float warn, bool ring)
        {
            if (q == null)
                return;
            float ang = (q.Pos - e.Pos).Angle;
            V2 dir = V2.FromAngle(ang);
            float len = e.Pos.DistanceTo(q.Pos) + 60f;
            V2 end = ClampToArena(e.Pos + dir * len, e.Radius);
            s.Lunging = true;
            s.LungeWarn = warn;
            s.LungeFrom = e.Pos;
            s.LungeAng = ang;
            s.LungeLen = Math.Max(0f, (end - e.Pos).Length);
            s.LungeDone = 0f;
            s.LungeRing = ring;
            e.Facing = ang;
        }

        /// Host: the warning line, then the dash, then a bite in a half-circle in front of it.
        void TickLunge(Enemy e, DevourState s, float dt)
        {
            if (s.LungeWarn > 0f)
            {
                s.LungeWarn -= dt;
                if (s.LungeWarn <= 0f)
                    Sounds.Add(Sfx.BossCharge);
                return;
            }
            V2 dir = V2.FromAngle(s.LungeAng);
            s.LungeDone = Math.Min(s.LungeLen, s.LungeDone + DevourLungeSpeed * dt);
            e.Pos = ClampToArena(s.LungeFrom + dir * s.LungeDone, e.Radius);
            if (s.LungeDone < s.LungeLen)
                return;
            s.Lunging = false;
            foreach (var p in Players.Values)
                if (!p.Downed && p.InvulnT <= 0f && p.SwallowT <= 0f && p.Pos.DistanceTo(e.Pos) < e.Radius + BiteReach + p.Def.HitR
                    && Math.Abs(MathX.Fmod((p.Pos - e.Pos).Angle - s.LungeAng + MathX.Pi, MathX.Tau) - MathX.Pi) < MathX.Pi / 2f + 0.2f)
                    Damage(p, BiteDamage, "The Devourer (bite)");
            BroadcastFx(FxKind.Ring, e.Pos + dir * e.Radius, new Rgba(1f, 0.55f, 0.63f), BiteReach, 0.3f, Sfx.Hit);
            if (s.LungeRing)
                for (int i = 0; i < 12; i++)
                    EnemyShot(e, s.LungeAng + MathX.Tau * i / 12f, 170f, Defs.SmallShot);
        }

        /// Host: underground, a ripple chases its target, stops, flashes, then it erupts.
        void TickBurrow(Enemy e, DevourState s, float dt)
        {
            s.BurrowT += dt;
            if (s.BurrowT < BurrowChase)
            {
                Player q;
                if (!Players.TryGetValue(s.BurrowTarget, out q) || q.Downed || q.SwallowT > 0f)
                    q = NearestFree(e.Pos);
                if (q != null)
                    e.Pos = ClampToArena(e.Pos + (q.Pos - e.Pos).LimitLength(BurrowSpeed * dt), e.Radius);
                return;
            }
            if (s.BurrowT < BurrowTime)
                return;
            s.BurrowT = 0f;
            foreach (var p in Players.Values)
                if (!p.Downed && p.InvulnT <= 0f && p.SwallowT <= 0f && p.Pos.DistanceTo(e.Pos) < EruptRadius + p.Def.HitR)
                    Damage(p, BiteDamage, "The Devourer (eruption)");
            float off = Rand(0f, MathX.Tau);
            for (int i = 0; i < 16; i++)
                EnemyShot(e, off + MathX.Tau * i / 16f, 170f, Defs.SmallShot);
            BroadcastFx(FxKind.Boom, e.Pos, new Rgba(0.85f, 0.64f, 0.35f), EruptRadius + 20f, 0.45f, Sfx.BigExplode);
        }

        /// Host: the Jaws' two rows of teeth fly at where the player was.
        void CloseJaws(Enemy e, DevourState s)
        {
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 7; i++)
                {
                    float a = (s.JawsSide ? (side < 0 ? MathX.Pi : 0f) : side * MathX.Pi / 2f) + (i - 3) * 0.24f;
                    V2 at = s.JawsAt + V2.FromAngle(a) * JawsReach, dir = (s.JawsAt - at).Normalized();
                    SpawnBullet(Defs.ToothShot, at, dir * JawsSpeed, 0f, 4.6f, Defs.BulletDamage(Defs.ToothShot), 0, true).Source = e.Type;
                }
            Sounds.Add(Sfx.Clang);
        }

        /// The teeth of the Jaws, before they fly: where each of the fourteen starts.
        public static V2 JawsTooth(DevourState s, int side, int i)
        {
            float a = (s.JawsSide ? (side < 0 ? MathX.Pi : 0f) : side * MathX.Pi / 2f) + (i - 3) * 0.24f;
            return s.JawsAt + V2.FromAngle(a) * JawsReach;
        }

        /// Host: during the Hungry turn it eats any morsel it reaches, healing for each.
        void EatMorsels(Enemy e)
        {
            Enemy m = NearestMorsel(e.Pos);
            if (m == null || m.Pos.DistanceTo(e.Pos) > e.Radius + m.Radius + 4f || IsSpawning(m))
                return;
            e.Hp = Math.Min(e.MaxHp, e.Hp + e.MaxHp * MorselEat);
            Enemies.Remove(m.Id);  // eaten (no kill, no reward)
            BroadcastFx(FxKind.Ring, m.Pos, new Rgba(1f, 0.69f, 0.44f), 50f, 0.4f, Sfx.Hit);
            DoLog("the Devourer ate a morsel");
        }

        /// Host: a player walking over a morsel squashes it.
        void StepOnMorsels()
        {
            foreach (var m in Enemies.Values)
            {
                if (m.Type != EnemyType.Morsel || m.Hp <= 0f || IsSpawning(m))
                    continue;
                foreach (var p in Players.Values)
                    if (!p.Downed && p.SwallowT <= 0f && p.Pos.DistanceTo(m.Pos) < m.Radius + p.Def.BodyR)
                    {
                        m.Hp = 0f;
                        m.LastHitBy = p.Id;
                        BroadcastFx(FxKind.Ring, m.Pos, new Rgba(0.42f, 0.89f, 0.54f), 36f, 0.3f, Sfx.None);
                        break;
                    }
            }
        }

        void SpitMorsels(Enemy e)
        {
            for (int k = 0; k < 4; k++)
            {
                V2 at = ClampToArena(e.Pos + V2.FromAngle(Rand(0f, MathX.Tau)) * Rand(220f, 440f), 60f);
                var m = SpawnEnemyAt(EnemyType.Morsel, at, 0.3f, e.Id);
                m.FireT = 99f;
            }
            BroadcastFx(FxKind.Ring, e.Pos, new Rgba(1f, 0.69f, 0.44f), e.Radius + 30f, 0.35f, Sfx.None);
        }

        /// Host: whoever touches its mouth while it breathes in (or in its last stand) is swallowed, one at a time.
        void SwallowAtMouth(Enemy e, DevourState s)
        {
            if (s.SwallowId != 0)
                return;
            foreach (var p in Players.Values)
                if (!p.Downed && p.Pos.DistanceTo(e.Pos) < e.Radius + p.Def.HitR + 6f)
                {
                    s.SwallowId = p.Id;
                    s.SwallowT = SwallowTime;
                    s.SwallowTaken = 0f;
                    s.StomachT = 0f;
                    p.SwallowT = SwallowTime;
                    p.Pos = e.Pos;
                    BroadcastFx(FxKind.Ring, e.Pos, DevourColor(DevourStage.Gorging), 90f, 0.4f, Sfx.BossChime);
                    DoLog(p.Name + " was swallowed by the Devourer");
                    return;
                }
        }

        /// Host: the swallowed player is hurt each second inside it, until 3 s pass or allies do enough damage.
        void TickSwallowed(Enemy e, DevourState s, float dt)
        {
            if (s.SwallowId == 0)
                return;
            Player p;
            if (!Players.TryGetValue(s.SwallowId, out p))
            {
                s.SwallowId = 0;
                return;
            }
            s.SwallowT -= dt;
            p.SwallowT = Math.Max(0.05f, s.SwallowT);
            p.Pos = e.Pos;
            p.BossContactT = Math.Max(p.BossContactT, 0.5f);  // its body doesn't hurt you from inside
            s.StomachT -= dt;
            if (s.StomachT <= 0f && !p.Downed)
            {
                s.StomachT = 1f;
                stomach = true;
                Damage(p, s.Stage == DevourStage.LastStand ? 20f : 10f, "The Devourer (swallowed)");
                stomach = false;
            }
            if (p.Downed || s.SwallowT <= 0f || s.SwallowTaken >= SwallowFreeAt)
                SpitOut(e, s);
        }

        bool stomach;  // the swallowed player's own damage gets past Damage's swallowed check

        void SpitOut(Enemy e, DevourState s)
        {
            Player p;
            int id = s.SwallowId;
            s.SwallowId = 0;
            if (!Players.TryGetValue(id, out p))
                return;
            p.SwallowT = 0f;
            p.InvulnT = Math.Max(p.InvulnT, 1f);
            V2 dir = V2.FromAngle(e.Facing);
            MovePlayer(p, ClampToArena(e.Pos + dir * (e.Radius + 170f), p.Def.BodyR + 4f));
            for (int i = 0; i < 12; i++)
                EnemyShot(e, e.Facing + MathX.Tau * i / 12f, 170f, Defs.SmallShot);
            BroadcastFx(FxKind.Ring, p.Pos, DevourColor(DevourStage.Gorging), 60f, 0.4f, Sfx.Explode);
            DoLog(p.Name + " was spat out by the Devourer");
        }

        /// The Devourer is gone (or the match ended): anyone inside it comes back out where it was.
        void ReleaseSwallowed(Enemy e)
        {
            if (e.Devour != null && e.Devour.SwallowId != 0)
                SpitOut(e, e.Devour);
            foreach (var p in Players.Values)
                p.SwallowT = 0f;
        }

        /// Host: anyone still marked as swallowed with no Devourer holding them (it went without dying, say a dev
        /// tool's wave skip) is let go, so nobody stays hidden and unable to shoot.
        void LetGoOfSwallowed()
        {
            foreach (var p in Players.Values)
            {
                if (p.SwallowT <= 0f)
                    continue;
                bool held = false;
                foreach (var e in Enemies.Values)
                    held |= e.Type == EnemyType.Devourer && e.Devour != null && e.Devour.SwallowId == p.Id;
                if (!held)
                    p.SwallowT = 0f;
            }
        }

        /// Host: bile pools hurt whoever stands in them (once a tick, however many overlap).
        void TickBile(float dt)
        {
            var s = FindDevourer()?.Devour;
            if (s == null)
                return;
            s.BileTickT -= dt;
            if (s.BileTickT > 0f)
                return;
            s.BileTickT = 0.25f;
            foreach (var p in Players.Values)
            {
                if (p.Downed || p.InvulnT > 0f || p.SwallowT > 0f)
                    continue;
                foreach (var pt in Patches.Values)
                    if (pt.Kind == PatchKind.Bile && pt.Active && p.Pos.DistanceTo(pt.Pos) < pt.Radius + p.Def.HitR)
                    {
                        Damage(p, BileDps * 0.25f, "The Devourer (bile)");
                        break;
                    }
            }
        }

        /// HurtEnemy: the Maw heals it from the front and hurts it more from behind; with a player inside it, allies'
        /// hits do double (and count towards spitting them out). Returns false when the hit fed it instead.
        bool DevourHit(Enemy e, ref float dmg, int attacker)
        {
            if (e.Type != EnemyType.Devourer || e.Devour == null)
                return true;
            var s = e.Devour;
            byte side = s.MawHit;
            s.MawHit = 0;
            if (s.Stage == DevourStage.Ravenous && side == 1)
            {
                e.Hp = Math.Min(e.MaxHp, e.Hp + dmg * 0.5f);  // into its mouth
                return false;
            }
            if (s.Stage == DevourStage.Ravenous && side == 2)
                dmg *= MawBackMult;
            if (s.SwallowId != 0 && attacker > 0 && attacker != s.SwallowId)
            {
                dmg *= 2f;
                s.SwallowTaken += dmg;
            }
            return true;
        }

        /// BlocksShot: underground or changing stage blocks the hit; otherwise remember which side it came from.
        bool DevourBlocks(Enemy e, V2 from)
        {
            if (e.Type != EnemyType.Devourer || e.Devour == null)
                return false;
            if (DevourImmune(e))
                return true;
            e.Devour.MawHit = (byte)(InMaw(e, from) ? 1 : 2);
            return false;
        }

        /// Your own game: the inhale pulls you towards its mouth when you're in the cone.
        public V2 DevourPull(V2 at)
        {
            foreach (var e in Enemies.Values)
            {
                if (e.Type != EnemyType.Devourer || e.Devour == null || e.Devour.InhaleT <= 0f || e.Devour.TransT > 0f || e.Hp <= 0f)
                    continue;
                V2 to = e.Pos - at;
                float d = to.Length;
                if (d >= InhaleReach || d < 1f)
                    continue;
                if (Math.Abs(MathX.Fmod((-to).Angle - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi) >= InhaleHalf)
                    continue;
                return to / d * (InhalePullNear - (InhalePullNear - InhalePullFar) * d / InhaleReach);
            }
            return V2.Zero;
        }

        /// Your own game: inside it, you go where it goes.
        bool PinSwallowed(Player me)
        {
            if (me.SwallowT <= 0f || me.Downed)
                return false;
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Devourer)
                {
                    me.Pos = e.Pos;
                    return true;
                }
            return false;
        }

        void EnterDevourStage(Enemy e, DevourStage stage)
        {
            var s = DevourOf(e);
            s.Stage = stage;
            s.Step = -1;
            s.Lunging = false;
            s.InhaleT = 0f;
            s.JawsT = 0f;
            e.Radius = DevourRadius(e);
            NextDevourTurn(e, s);
            DoLog("the Devourer: " + stage);
        }

        void StartDevourTransition(Enemy e, DevourStage to)
        {
            var s = DevourOf(e);
            if (s.SwallowId != 0)
                SpitOut(e, s);
            s.TransT = DevourTransTime;
            s.TransTo = to;
            s.Lunging = false;
            s.InhaleT = 0f;
            s.BurrowT = 0f;
            s.JawsT = 0f;
            e.Phase = 0;
            RemoveAll(EnemyType.Morsel);
            ClearBile();
            ClearEnemyBullets();
            session.Broadcast(Proto.End(Proto.Begin(Msg.ClearEnemyBullets)));
            BroadcastFx(FxKind.Ring, e.Pos, DevourColor(to), 900f, 0.8f, Sfx.BossChime);
            DoLog("the Devourer: changing to " + to);
        }

        /// Bile pools go when the stage changes (every game drops its own; nothing is sent).
        void ClearBile()
        {
            var gone = new List<int>();
            foreach (var pt in Patches.Values)
                if (pt.Kind == PatchKind.Bile)
                    gone.Add(pt.Id);
            foreach (int id in gone)
                Patches.Remove(id);
        }

        void NextDevourTurn(Enemy e, DevourState s)
        {
            if (s.Turn == DevourTurn.Hungry)
                RemoveAll(EnemyType.Morsel);  // what it didn't eat rots away
            var turns = DevourTurns[(int)s.Stage];
            s.Step = (s.Step + 1) % turns.Length;
            s.Turn = turns[s.Step];
            s.TurnLen = DevourTimes[(int)s.Stage][s.Step];
            s.TurnT = 0f;
            s.ActT = 0.3f;
            s.Fired = 0;
            e.Phase = (byte)(s.Turn == DevourTurn.Hungry ? 1 : 0);  // Hungry(e): your shots heal it
            if (s.Turn == DevourTurn.Hungry)
                SpitMorsels(e);
            e.FireT = 0.3f;
        }

        void DevourFire(Enemy e, V2 tpos, float aim)
        {
            var s = DevourOf(e);
            if (s.TransT > 0f || s.BurrowT > 0f || s.SwallowId != 0 && s.Stage != DevourStage.LastStand)
            {
                e.FireT = 0.2f;
                return;
            }
            switch (s.Turn)
            {
                case DevourTurn.Spiral:  // its old double spiral
                case DevourTurn.LastMeal:  // the same, at half the rate
                    e.FireT = s.Turn == DevourTurn.Spiral ? 0.08f : 0.16f;
                    e.Ang += 0.17f;
                    EnemyShot(e, e.Ang, 140f, Defs.SmallShot);
                    EnemyShot(e, e.Ang + MathX.Pi, 140f, Defs.SmallShot);
                    break;
                case DevourTurn.Hungry:  // its old hungry rings of big shots
                    e.FireT = 1.2f;
                    s.Fired++;
                    for (int i = 0; i < 20; i++)
                        EnemyShot(e, MathX.Tau * (i + (s.Fired % 2) * 0.5f) / 20f, 90f, Defs.BigShot);
                    break;
                case DevourTurn.Vomit:  // a spray from its mouth, sweeping 70 degrees either side of you and back
                {
                    e.FireT = 0.07f;
                    e.Facing = aim + (float)Math.Sin(s.TurnT * 1.6f) * 1.22f;
                    V2 mouth = e.Pos + V2.FromAngle(e.Facing) * e.Radius;
                    for (int k = 0; k < 2; k++)
                    {
                        V2 dir = V2.FromAngle(e.Facing + Rand(-0.12f, 0.12f));
                        SpawnBullet(Defs.BileShot, mouth, dir * 260f, 0f, 4f, Defs.BulletDamage(Defs.BileShot), 0, true).Source = e.Type;
                    }
                    break;
                }
                default:  // breathers fire nothing; its bites, inhales, bile and the rest come from DevourSpecial
                    e.FireT = 0.2f;
                    break;
            }
        }

        void SyncDevour(NetSync s, Enemy e)
        {
            bool has = e.Devour != null;
            s.Bool(ref has);
            if (!has)
                return;
            var q = e.Devour ?? (e.Devour = new DevourState { Step = 0 });
            int stage = (int)q.Stage, turn = (int)q.Turn, transTo = (int)q.TransTo;
            s.Byte(ref stage);
            s.Byte(ref turn);
            s.Byte(ref transTo);
            s.Float(ref q.TurnT);
            s.Float(ref q.TransT);
            s.Bool(ref q.Lunging);
            if (q.Lunging)
            {
                s.Float(ref q.LungeWarn);
                s.Float(ref q.LungeAng);
                s.Float(ref q.LungeLen);
                s.Vec(ref q.LungeFrom);
            }
            s.Float(ref q.InhaleT);
            s.Float(ref q.BurrowT);
            s.Float(ref q.JawsT);
            if (q.JawsT > 0f)
            {
                s.Vec(ref q.JawsAt);
                s.Bool(ref q.JawsSide);
            }
            s.Float(ref q.Spin);
            s.Int(ref q.SwallowId);
            if (s.Reading)
            {
                q.Stage = (DevourStage)stage;
                q.Turn = (DevourTurn)turn;
                q.TransTo = (DevourStage)transTo;
            }
        }

        /// Clients, each frame: the clocks run between snapshots, and bile pools go when the stage changes.
        void UpdateDevourView(float dt)
        {
            if (IsHost)
                return;
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Devourer && e.Devour != null)
                {
                    var s = e.Devour;
                    s.TurnT += dt;
                    s.LungeWarn = Math.Max(0f, s.LungeWarn - dt);
                    s.InhaleT = Math.Max(0f, s.InhaleT - dt);
                    s.JawsT = Math.Max(0f, s.JawsT - dt);
                    if (s.BurrowT > 0f)
                        s.BurrowT += dt;
                    if (s.Stage == DevourStage.LastStand && s.TransT <= 0f)
                        s.Spin += TeethSpin * dt;
                    if (s.TransT > 0f)
                        ClearBile();
                }
        }
    }
}
