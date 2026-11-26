using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Soldier DPS's Railgun (from Docs/Mockups/railgun-rework.html).
    ///   Fire: hold to charge, let go to fire (a ChargeWeapon, like the Mender's Bow: BowCharge, BowPerfect). The shot is
    ///     an instant beam that ricochets off the arena's edge, walls and pillars (RailBounces: 1 for a tap, 3 at full),
    ///     hitting each enemy once along the whole line. The charge sets its damage (RailDamage), width (RailWidth) and
    ///     how many enemies it passes (RailPierce, every one from RailPierceAllAt). A perfect release hits RailPerfectBonus
    ///     harder. From RailPathMin charge it leaves the line charged (the ion trail, KitWeapons.cs) for RailPathTime.
    ///     Each shot heats the gun (RailHeatGain, up to RailHeatMax; Player.RailHeat). Multishot: one more bounce per extra
    ///     projectile, and 12% harder.
    ///   Alt (Msg.WeaponDash WeaponAlt.HeatDump): Heat Dump. With at least DumpMin heat, empties the meter into a cone of fire in
    ///     front of you (DumpHalf either side of your aim): a hit, a burn and a shove that all grow with the heat dumped,
    ///     and it slows what it burns. DumpCd between dumps.
    /// Looks: FxKind.RailBeam (one per stretch of the beam) and FxKind.HeatDump in Game/RailBeamLook.cs; the gun and the
    /// heat gauge in Game/DpsGunLooks.cs.
    public partial class Game
    {
        public const float RailFullDraw = 1.1f;  // seconds to a full charge (fire-rate upgrades shorten it)
        public const float RailDmgTap = 24f, RailDmgFull = 120f, RailPerfectBonus = 1.25f;
        public const float RailWideTap = 4f, RailWideFull = 26f;  // the beam's full width, px
        public const float RailPierceAllAt = 0.7f;  // from this charge the beam passes every enemy
        public const float RailPathMin = 0.25f;     // below this charge, no charged path
        public const float RailPathPerfect = 2f;    // a perfect shot's path lasts this long
        public const float RailReachMax = 4000f;    // the whole line, bounces and all, at most
        public const float RailBeamFx = 0.32f;      // the beam's flash (the charged path is drawn on after it)
        public const float RailExtraDmg = 0.12f;    // each extra projectile: 12% harder (and one more bounce)
        public const float RailHeatMax = 100f, RailHeatTap = 5f, RailHeatFull = 20f, RailHeatPerfect = 5f;

        public const float DumpMin = 10f, DumpCd = 0.8f, DumpHalf = 0.61f;  // the cone is 70 degrees
        public const float DumpReachBase = 140f, DumpReachPer = 180f;       // at 0 heat, plus this at full heat
        public const float DumpHitBase = 15f, DumpHitPer = 75f;
        public const float DumpBurnBase = 6f, DumpBurnPer = 24f, DumpBurnTime = 3f, DumpBurnSlow = 0.6f;
        public const float DumpShoveBase = 60f, DumpShovePer = 200f;
        public const float DumpFx = 0.55f;

        static readonly Rgba RailColor = new Rgba(0.55f, 0.91f, 1f), RailGold = new Rgba(1f, 0.8f, 0.35f);
        public static readonly Rgba HeatColor = new Rgba(1f, 0.48f, 0.18f);

        public static float RailDamage(float c) => RailDmgTap + (RailDmgFull - RailDmgTap) * (float)Math.Pow(MathX.Clamp01(c), 1.2);
        public static float RailWidth(float c) => RailWideTap + (RailWideFull - RailWideTap) * (float)Math.Pow(MathX.Clamp01(c), 1.5);
        public static int RailPierce(float c) => c >= RailPierceAllAt ? 999 : 2 + (int)(c * 2.5f);
        public static int RailBounces(float c, int extra) => 1 + (int)(MathX.Clamp01(c) * 2f + 0.2f) + extra;
        public static float RailPathTime(float c, bool perfect) => c < RailPathMin ? 0f : perfect ? RailPathPerfect : 0.55f + 0.95f * c;
        public static float RailPathDps(float c) => 15f + 15f * c;
        public static float RailHeatGain(float c, bool perfect) => RailHeatTap + (RailHeatFull - RailHeatTap) * c * c + (perfect ? RailHeatPerfect : 0f);

        public static float DumpReach(float heat) => DumpReachBase + DumpReachPer * heat / RailHeatMax;
        public static float DumpHit(float heat) => DumpHitBase + DumpHitPer * heat / RailHeatMax;
        public static float DumpBurn(float heat) => DumpBurnBase + DumpBurnPer * heat / RailHeatMax;
        public static float DumpShove(float heat) => DumpShoveBase + DumpShovePer * heat / RailHeatMax;

        public int RailShots, RailBounceHits, HeatDumps;  // tests

        static void SetRailHeat(Player p, float v)
        {
            p.RailHeatF = Math.Max(0f, Math.Min(RailHeatMax, v));
            p.RailHeat = (byte)Math.Ceiling(p.RailHeatF);
        }

        // --- Fire: the charged ricochet beam ------------------------------------------------------

        readonly List<Enemy> railHits = new List<Enemy>();
        readonly HashSet<int> railHit = new HashSet<int>();

        /// Host: the released shot (FireVolley). `dmg` is the damage multiplier, `extra` the extra projectiles.
        void FireRailgun(Player p, V2 muzzle, V2 dir, float dmg, int extra)
        {
            float c = BowCharge(p, p.BowShotDraw);
            bool perfect = BowPerfect(p, p.BowShotDraw);
            dmg *= 1f + RailExtraDmg * extra;  // (Multishot: harder, and a bounce more)
            float hit = RailDamage(c) * (perfect ? RailPerfectBonus : 1f) * dmg;
            float width = RailWidth(c) * p.Stats.ShotSizeMult, half = width * 0.5f;  // (Magnum Rounds)
            int pierce = RailPierce(c) + p.Stats.Pierce, bounces = RailBounces(c, extra), passed = 0;
            float path = RailPathTime(c, perfect);
            path = path > 0f ? path + p.Stats.RailTrailBonus : 0f;  // (Supercharge: longer, hotter)
            float pathDps = RailPathDps(c) * dmg * p.Stats.RailBurnMult;
            Rgba col = perfect ? RailGold : RailColor;
            col.A = Math.Min(1f, 0.5f + c * 0.5f);  // (the look reads the charge from it)
            railHit.Clear();
            V2 from = muzzle;
            float left = RailReachMax;
            RailShots++;
            for (int seg = 0; seg <= bounces && left > 1f; seg++)
            {
                RailCast(from, dir, left, out float len, out V2 normal);
                float end = len;
                bool stopped = false;
                railHits.Clear();
                foreach (var e in Enemies.Values)
                {
                    if (!Targetable(e) || railHit.Contains(e.Id))
                        continue;
                    V2 rel = e.Pos - from;
                    float along = rel.Dot(dir);
                    if (along < -e.Radius || along > len + e.Radius || Math.Abs(rel.Dot(dir.Orthogonal())) > half + e.Radius)
                        continue;
                    railHits.Add(e);
                }
                railHits.Sort((a, b) => (a.Pos - from).Dot(dir).CompareTo((b.Pos - from).Dot(dir)));
                foreach (var e in railHits)
                {
                    railHit.Add(e.Id);
                    if (seg > 0)
                        RailBounceHits++;
                    if (!e.Def.IsBoss)
                        KnockEnemy(e, dir * (6f + 18f * c));
                    WeaponHurt(e, hit, p.Id);
                    if (++passed >= pierce)
                    {
                        end = Math.Max(0f, Math.Min(len, (e.Pos - from).Dot(dir)));
                        stopped = true;
                        break;
                    }
                }
                V2 to = from + dir * end;
                // Pos -> Pos2: this stretch; Radius = the beam's width; Dur = the flash, or the charged path after it;
                // Color: gold for a perfect shot, alpha = 0.5 + charge / 2.
                BroadcastFx(FxKind.RailBeam, from, to, col, width, Math.Max(RailBeamFx, path), seg == 0 ? Sfx.None : Sfx.Pick);
                if (path > 0f)
                    RailPath(p.Id, from, dir, end, path, pathDps, half);
                left -= end;
                if (stopped || seg == bounces)
                    break;
                dir = dir - normal * (2f * dir.Dot(normal));  // bounce
                from = to + normal * 0.5f;
            }
            SetRailHeat(p, p.RailHeatF + RailHeatGain(c, perfect));
            if (perfect)
                BroadcastFx(FxKind.Spark, muzzle, RailGold, 26f, 0.3f, Sfx.Pick);
        }

        /// How far the beam runs before its first wall (the aim line).
        public float RailFirstReach(Player p)
        {
            V2 d = V2.FromAngle(p.Aim);
            RailCast(p.Pos + d * p.Def.BodyR, d, RailReachMax, out float len, out _);
            return p.Def.BodyR + len;
        }

        /// The beam's line with its bounces off walls (not what it would hit), for the aim preview: `pts` gets the corners.
        public void RailLine(V2 from, V2 dir, int bounces, List<V2> pts)
        {
            pts.Clear();
            pts.Add(from);
            float left = RailReachMax;
            for (int seg = 0; seg <= bounces && left > 1f; seg++)
            {
                RailCast(from, dir, left, out float len, out V2 normal);
                V2 to = from + dir * len;
                pts.Add(to);
                left -= len;
                dir = dir - normal * (2f * dir.Dot(normal));
                from = to + normal * 0.5f;
            }
        }

        /// The first wall, pillar or arena edge on the line from `from` along `dir` (unit), within `max`:
        /// how far it is and the surface's normal (facing back toward `from`).
        void RailCast(V2 from, V2 dir, float max, out float dist, out V2 normal)
        {
            V2 lo = ArenaMin, hi = ArenaMax;
            float tx = dir.X > 1e-6f ? (hi.X - from.X) / dir.X : dir.X < -1e-6f ? (lo.X - from.X) / dir.X : float.MaxValue;
            float ty = dir.Y > 1e-6f ? (hi.Y - from.Y) / dir.Y : dir.Y < -1e-6f ? (lo.Y - from.Y) / dir.Y : float.MaxValue;
            if (tx < ty) { dist = tx; normal = new V2(dir.X > 0f ? -1f : 1f, 0f); }
            else { dist = ty; normal = new V2(0f, dir.Y > 0f ? -1f : 1f); }
            dist = Math.Max(0f, dist);
            var d = ActiveLayout;
            if (d != null)
                foreach (var s in d.Solids)
                {
                    if (s.Edge || !Counts(s))
                        continue;  // (the edge wall is the arena's edge, done above)
                    if (s.Round)
                    {
                        V2 rel = from - s.Pos;
                        float b = rel.Dot(dir), cc = rel.Dot(rel) - s.R * s.R, disc = b * b - cc;
                        if (cc <= 0f || disc < 0f)
                            continue;  // (starting inside one: let it out)
                        float t = -b - (float)Math.Sqrt(disc);
                        if (t > 0.001f && t < dist)
                        {
                            dist = t;
                            normal = (from + dir * t - s.Pos).Normalized();
                        }
                        continue;
                    }
                    float tmin = float.MinValue, tmax = float.MaxValue;
                    V2 n = V2.Zero;
                    bool miss = false;
                    for (int axis = 0; axis < 2 && !miss; axis++)
                    {
                        float o = axis == 0 ? from.X : from.Y, dd = axis == 0 ? dir.X : dir.Y;
                        float a0 = (axis == 0 ? s.Pos.X - s.HW : s.Pos.Y - s.HH), a1 = (axis == 0 ? s.Pos.X + s.HW : s.Pos.Y + s.HH);
                        if (Math.Abs(dd) < 1e-6f)
                        {
                            miss = o < a0 || o > a1;
                            continue;
                        }
                        float t0 = (a0 - o) / dd, t1 = (a1 - o) / dd, side = -1f;
                        if (t0 > t1) { float q = t0; t0 = t1; t1 = q; side = 1f; }
                        if (t0 > tmin) { tmin = t0; n = axis == 0 ? new V2(side, 0f) : new V2(0f, side); }
                        tmax = Math.Min(tmax, t1);
                    }
                    if (!miss && tmax >= tmin && tmin > 0.001f && tmin < dist)
                    {
                        dist = tmin;
                        normal = n;
                    }
                }
            dist = Math.Min(dist, max);
        }

        /// The charged stretch a beam leaves (the ion trail, already there along its whole length).
        void RailPath(int owner, V2 from, V2 dir, float len, float time, float dps, float half)
        {
            if (len < 1f)
                return;
            ionTrails.Add(new Trail { Owner = owner, From = from, Dir = dir, Speed = 1e6f, Len = len, Dps = dps, Time = time, Width = Math.Max(IonTrailWidth, half + 4f) });
        }

        // --- Alt: Heat Dump --------------------------------------------------------------------------

        float dumpLocalCd;

        /// Your own game: Alt with the Railgun dumps the heat, if there's enough.
        bool LocalHeatDump(Player me, V2 aimAt, bool pressed, float dt)
        {
            dumpLocalCd = Math.Max(0f, dumpLocalCd - dt);
            if (!pressed || me.Weapon.Id != WeaponId.Railgun || dumpLocalCd > 0f || me.RailHeat < DumpMin)
                return false;
            dumpLocalCd = DumpCd;
            if (IsHost)
                HostWeaponDash(MyId, WeaponAlt.HeatDump, aimAt);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(WeaponAlt.HeatDump);
                w.Write(aimAt);
                session.SendToHost(Proto.End(w));
            }
            return true;
        }

        /// Host: empties the heat into the cone of fire.
        void HostHeatDump(Player p, V2 target)
        {
            if (p.Weapon.Id != WeaponId.Railgun || p.Downed || p.StunT > 0f || p.DumpCd > 0f || p.RailHeatF < DumpMin || State == MatchState.GameOver)
                return;
            float heat = p.RailHeatF, aim = (target - p.Pos).LengthSq > 1f ? (target - p.Pos).Angle : p.Aim;
            float reach = DumpReach(heat) * AreaMult(p), hit = DumpHit(heat) * p.Stats.DmgMult, burn = DumpBurn(heat) * p.Stats.DmgMult, shove = DumpShove(heat);
            V2 fwd = V2.FromAngle(aim);
            foreach (var e in AllFoes(by: p.Id))
            {
                V2 rel = e.Pos - p.Pos;
                float d = rel.Length;
                if (d > reach + e.Radius)
                    continue;
                if (d > 1f && Math.Abs(MathX.WrapPi(rel.Angle - aim)) > DumpHalf + (float)Math.Asin(Math.Min(1f, e.Radius / d)))
                    continue;
                e.BurnT = Math.Max(e.BurnT, DumpBurnTime);
                e.BurnDps = Math.Max(e.BurnDps, burn);
                e.BurnBy = p.Id;
                Cauterize(e, p.Id);  // fire on a bleed (Cauterize.cs)
                if (!e.Def.IsBoss)
                {
                    Slow(e, DumpBurnTime, DumpBurnSlow);
                    KnockEnemy(e, (d > 1f ? rel / d : fwd) * shove * (e.Def.Heavy ? 0.35f : 1f));
                }
                WeaponHurt(e, hit, p.Id);
            }
            HeatDumps++;
            SetRailHeat(p, 0f);
            p.DumpCd = DumpCd;
            // Pos = the shooter, Pos2 = the cone's far middle, Radius = the heat dumped (0-100).
            BroadcastFx(FxKind.HeatDump, p.Pos, p.Pos + fwd * reach, HeatColor, heat, DumpFx, Sfx.ShotFireball);
        }

    }
}
