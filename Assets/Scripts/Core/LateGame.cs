using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// A warning circle on the ground; when its timer runs out, lightning strikes it.
    public class Hazard
    {
        public int Id;
        public V2 Pos;
        public float Radius, T, Duration;
        public byte Kind;  // HazardLightning (the Storm Herald), HazardHand (the Lich's dead hands) or HazardCaller / HazardDownburst (a Stormcaller's, Squall.cs)
    }

    /// Waves 16-50: the late-game enemies, the bosses from wave 25 on, and winning at wave 50 (host only).
    public partial class Game
    {
        public bool Endless;  // kept playing after beating wave 50
        public readonly Dictionary<int, Hazard> Hazards = new Dictionary<int, Hazard>();
        int nextHazardId = 1;

        static bool IsLateGame(EnemyType t) => Defs.Get(t).Late;

        /// Too heavy to slow down, shove around or knock back.
        static bool Heavy(Enemy e) => e.Def.IsBoss || e.Def.Heavy;

        /// The Devourer heals from your shots while it's hungry (its Hungry turn, Devourer.cs).
        public static bool Hungry(Enemy e) => e.Type == EnemyType.Devourer && e.Phase == 1;

        // --- Winning ------------------------------------------------------------

        void Win()
        {
            State = MatchState.Victory;
            Zones.Clear();
            Hazards.Clear();
            Cages.Clear();
            HostClearEnemyBullets();
            BroadcastRunStats();
            RunOver(true);  // RunSaves.cs
            DoLog("victory at wave " + Wave + ", score " + Score);
        }

        /// Host, from the victory screen: keep going past wave 50.
        public void ContinueEndless()
        {
            if (!IsHost || State != MatchState.Victory || Hunt != null)
                return;  // (a Boss Hunt is over once its boss is)
            Endless = true;
            WaveCleared();
        }

        // --- Movement -----------------------------------------------------------

        V2 LateMove(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            switch (e.Type)
            {
                case EnemyType.Colossus:
                    return ColossusMove(e, dir, dist, ref speed);  // Colossus.cs
                case EnemyType.Duelist:
                    return DuelistMove(e, tpos, dir, dist, ref speed);  // Duelist.cs
                case EnemyType.Devourer:
                    return DevourMove(e, dir, dist, ref speed);  // Devourer.cs
                case EnemyType.Lich:
                    return V2.Zero;  // it moves itself: drifting, blinking between graves (Lich.cs)
                case EnemyType.StormHerald:
                    if (HeraldImmune(e))
                        return V2.Zero;  // holds still while it changes phase
                    return (HeraldGoal(e) - e.Pos) / 60f;  // along the top, or the middle for the last stand (Herald.cs)
                case EnemyType.Eclipse:
                    if (EclipseImmune(e))
                        return V2.Zero;  // holds still while it changes phase
                    return (EclipseGoal(e) - e.Pos) / 60f;  // orbiting the middle, or in it for Totality (Eclipse.cs)
                default:  // Decoy, Phylactery stay put
                    return V2.Zero;
            }
        }

        // --- Mechanics ----------------------------------------------------------

        public const float ShadeWarn = 0.5f, ShadeBurstRadius = 40f, MortarMarkRadius = 40f;  // the Shade's blink warning ring (how long, how big) and the Mortar landing ring

        void LateSpecial(Enemy e, V2 tpos, float dt)
        {
            e.PhaseT -= dt;
            switch (e.Type)
            {
                case EnemyType.Colossus:
                    ColossusSpecial(e, tpos, dt);  // Colossus.cs: its phases, turns, stomp rings, plates and lava
                    break;
                case EnemyType.Duelist:
                    DuelistSpecial(e, tpos, dt);  // Duelist.cs: its phases, turns, dashes and salutes
                    break;
                case EnemyType.Lich:
                    LichSpecial(e, dt);  // Lich.cs
                    break;
                case EnemyType.StormHerald:
                    HeraldSpecial(e, tpos, dt);  // its phases and turns (Herald.cs)
                    break;
                case EnemyType.Devourer:
                    DevourSpecial(e, tpos, dt);  // its phases and turns (Devourer.cs)
                    break;
                case EnemyType.Eclipse:
                    EclipseSpecial(e, tpos, dt);  // its five phases and turns (Eclipse.cs)
                    break;
            }
        }

        void LateFire(Enemy e, V2 tpos, float aim)
        {
            switch (e.Type)
            {
                case EnemyType.Colossus:
                    ColossusFire(e, aim);  // Colossus.cs: its rock volley, between turns
                    break;
                case EnemyType.Duelist:
                    DuelistFire(e, aim);  // Duelist.cs
                    break;
                case EnemyType.Lich:
                    LichFire(e, aim);  // Lich.cs
                    break;
                case EnemyType.StormHerald:
                    HeraldFire(e, tpos, aim);  // Herald.cs
                    break;
                case EnemyType.Devourer:
                    DevourFire(e, tpos, aim);  // Devourer.cs
                    break;
                case EnemyType.Eclipse:
                    EclipseFire(e, aim);  // its breathers (Eclipse.cs)
                    break;
                default:  // Decoy, Phylactery and gravity wells don't shoot
                    e.FireT = 99f;
                    break;
            }
        }

        // --- Helpers ------------------------------------------------------------

        int CountChildren(Enemy parent, EnemyType type)
        {
            int n = 0;
            foreach (var o in Enemies.Values)
                if (o.ParentId == parent.Id && o.Type == type)
                    n++;
            return n;
        }

        /// A spot at least 250 px from every living player (best of a few tries).
        V2 FarFromPlayers(float radius)
        {
            V2 best = ArenaMid;
            float bestDist = -1f;
            for (int t = 0; t < 12; t++)
            {
                var pos = new V2(Rand(radius + 60f, Width - radius - 60f), Rand(radius + 60f, Height - radius - 60f));
                float d = float.MaxValue;
                foreach (var p in Players.Values)
                    if (!p.Downed)
                        d = Math.Min(d, p.Pos.DistanceTo(pos));
                if (d > bestDist)
                {
                    best = pos;
                    bestDist = d;
                }
                if (d >= 250f)
                    break;
            }
            return best;
        }

        // --- Lightning strikes --------------------------------------------------

        Hazard AddHazard(V2 pos, float radius, float delay)
        {
            var h = new Hazard { Id = nextHazardId++, Pos = ClampToArena(pos, 10f), Radius = radius, T = delay, Duration = delay };
            Hazards[h.Id] = h;
            return h;
        }

        void TickHazards(float dt)
        {
            var done = hazardLists.Rent();
            foreach (var h in Hazards.Values)
            {
                h.T -= dt;
                if (h.T <= 0f)
                    done.Add(h);
            }
            foreach (var h in done)
            {
                if (h.Kind == HazardHand)
                {
                    HandBursts(h);  // Lich.cs
                    Hazards.Remove(h.Id);
                    continue;
                }
                if (h.Kind == HazardCaller || h.Kind == HazardDownburst)
                {
                    CallerStrike(h);  // a Stormcaller's or a Thunderhead's (Squall.cs)
                    Hazards.Remove(h.Id);
                    continue;
                }
                float dmg = 25f;  // the difficulty's per-wave damage growth scales it (Difficulty.cs)
                foreach (var p in Players.Values)
                    if (Hittable(p) && p.Pos.CloserThan(h.Pos, h.Radius))
                        Damage(p, dmg, "Storm Herald (lightning)");
                var color = new Rgba(0.7f, 0.85f, 1f);
                BroadcastFx(FxKind.Bolt, new V2(h.Pos.X + Rand(-60f, 60f), -20f), h.Pos, color, 0f, 0.25f, Sfx.SpellZap);
                BroadcastFx(FxKind.Boom, h.Pos, color, h.Radius, 0.3f, Sfx.None);
                Hazards.Remove(h.Id);
            }
            hazardLists.Return(done);
        }

        void SyncHazards(NetSync s)
        {
            int n = Hazards.Count;
            s.Byte(ref n);
            if (s.Reading)
            {
                Hazards.Clear();
                for (int i = 0; i < n; i++)
                {
                    var h = new Hazard();
                    SyncHazard(s, h);
                    Hazards[h.Id] = h;
                }
            }
            else
                foreach (var h in Hazards.Values)
                    SyncHazard(s, h);
        }

        static void SyncHazard(NetSync s, Hazard h)
        {
            s.Int(ref h.Id);
            s.Vec(ref h.Pos);
            s.Float(ref h.Radius);
            s.Float(ref h.T);
            s.Float(ref h.Duration);
            s.Byte(ref h.Kind);
        }
        /// (the Shade, ReaperFaction.cs: moved from LateMove)
        V2 LateMove_Shade(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            return KeepDistance(dir, dist, 250f) + dir.Orthogonal() * e.Strafe * 0.7f;
        }

        /// (moved from LateMove, GravityWell)
        V2 LateMove_GravityWell(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            // Holds its corner, bobbing a little.
            var goal = e.Waypoint + V2.FromAngle(e.T * 0.8f + e.Id) * 12f;
            return (goal - e.Pos) / 30f;
        }

        /// (the Shade: moved from LateSpecial)
        void LateSpecial_Shade(Enemy e, V2 tpos, float dt)
        {
            e.PhaseT -= dt;
            // Blink somewhere near the target, then burst. The spot is marked first (a Target ring, ShadeWarn s, the
            // same "it lands here" ring as the bombs and blasts); Phase 1 is that warning.
            if (e.Phase == 0 && e.PhaseT <= 0f)
            {
                e.Waypoint = ClampToArena(tpos + RandDir() * Rand(220f, 300f), e.Radius);
                for (int i = 0; i < 24 && NearSolid(e.Waypoint, e.Radius + 8f); i++)  // not into a wall or pillar (LayoutRules.cs)
                    e.Waypoint = ClampToArena(tpos + RandDir() * Rand(220f, 300f), e.Radius);
                e.Phase = 1;
                e.PhaseT = ShadeWarn;
                BroadcastFx(FxKind.Target, e.Waypoint, e.Def.Color, ShadeBurstRadius, ShadeWarn, Sfx.None);
            }
            else if (e.Phase == 1 && e.PhaseT <= 0f)
            {
                e.Phase = 0;
                e.PhaseT = Rand(3f, 4.5f);
                V2 from = e.Pos;
                e.Pos = e.Waypoint;
                e.Disp = e.Pos;
                BroadcastFx(FxKind.Ring, from, e.Def.Color, 30f, 0.3f, Sfx.None);
                BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, 40f, 0.3f, Sfx.Blink);
                for (int i = 0; i < 8; i++)  // the blink ring: 8 at 115 (was 12 at 130)
                    EnemyShot(e, MathX.Tau * i / 8f, 115f, Defs.SmallShot);
            }
        }

        /// (moved from LateSpecial, Decoy)
        void LateSpecial_Decoy(Enemy e, V2 tpos, float dt)
        {
            e.PhaseT -= dt;
            if (e.PhaseT <= 0f)
            {
                for (int i = 0; i < 16; i++)
                    EnemyShot(e, MathX.Tau * i / 16f, 140f, Defs.SmallShot);
                e.Hp = 0f;  // pops; decoys give no score, XP or drops
            }
        }

        /// (the Shade: moved from LateFire)
        void LateFire_Shade(Enemy e, V2 tpos, float aim)
        {
            e.FireT = 1.5f;
            EnemyShot(e, aim - 0.05f, 200f, Defs.SmallShot);
            EnemyShot(e, aim + 0.05f, 200f, Defs.SmallShot);
        }

        /// Every late-game enemy's clock (what's left of the old LateSpecial for those with no special of their own).
        static void LateClock(Enemy e, float dt) => e.PhaseT -= dt;

        // --- Brains (Docs/EnemyBehaviours.md step 8f) ------------------------------------------------------------------

        static void LateBrains(Brain[] t)
        {
            var clock = new CodeSpecial((g, e, p, dt) => LateClock(e, dt));
            var shade = new Brain { Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.LateMove_Shade(e, p, d, di, dt, ref sp)), Special = new CodeSpecial((g, e, p, dt) => g.LateSpecial_Shade(e, p, dt)), Fire = new CodeFire((g, e, p, a) => g.LateFire_Shade(e, p, a)) };
            t[(int)EnemyType.Shade] = shade;
            var still = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => V2.Zero);
            t[(int)EnemyType.Decoy] = new Brain { Move = still, Special = new CodeSpecial((g, e, p, dt) => g.LateSpecial_Decoy(e, p, dt)), Fire = new HoldFire() };
            t[(int)EnemyType.Phylactery] = new Brain { Move = still, Special = clock, Fire = new HoldFire() };
            t[(int)EnemyType.Morsel] = new Brain { Move = still, Special = clock, Fire = new HoldFire() };
            t[(int)EnemyType.GravityWell] = new Brain { Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.LateMove_GravityWell(e, p, d, di, dt, ref sp)), Special = clock, Fire = new HoldFire() };
        }
    }
}
