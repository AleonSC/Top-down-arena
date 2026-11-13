using System;

namespace Arena
{
    /// The Devourer's faction, The Larder (Docs/Approaches.md "The Larder", mockup Docs/Mockups/devourer-faction.html).
    /// Step 1 is three enemies that share their Warden counterpart's behaviour through HiveKin's table: the Gnawling plays
    /// like a Rioter, the Butcher like a Guard and the Gulper like a Thug (a Brute). Each adds one trick that teaches a turn
    /// of the Devourer fight:
    ///   Butcher (its Snap bite): within ChopTrigger of its target it stops and raises its cleaver for ChopWind, then chops
    ///     a half-circle ChopReach in front (ChopDamage), then rests ChopEvery.
    ///   Gulper (its Inhale): within GulpRange it stops, its cheeks swell for GulpWind, then it breathes in for GulpTime: a
    ///     cone (GulpHalf either side, GulpReach long) pulls you toward its mouth at GulpPull (DevourPull, on your own game).
    ///     Reach the mouth and it bites (GulpBite) and the breath ends. Step out of the cone sideways.
    /// The state is Enemy.LarderStage (0 idle, 1 winding up, 2 acting) and LarderT (seconds left in it), replicated by
    /// SyncLarder; the facing is locked while it's busy (LarderFaceLocked), so the cone and the chop show where they hit.
    public static class LarderKin
    {
        public static bool Is(EnemyType t) => t >= EnemyType.Gnawling && t <= EnemyType.Gulper;
    }

    public partial class Enemy
    {
        public byte LarderStage;  // a Butcher's or Gulper's attack: 0 idle, 1 winding up, 2 chopping / breathing in (replicated)
        public float LarderT;     // seconds left in the stage (replicated)
        public float LarderCd;    // host: until it may attack again
    }

    public partial class Game
    {
        public const float ChopTrigger = 70f, ChopReach = 110f, ChopWind = 0.4f, ChopFlash = 0.15f, ChopEvery = 1.6f, ChopDamage = 14f;
        public const float GulpRange = 220f, GulpWind = 0.6f, GulpTime = 1f, GulpEvery = 4f, GulpReach = 200f, GulpHalf = 0.61f, GulpPull = 120f, GulpBite = 20f;
        public const byte LarderIdle = 0, LarderWinding = 1, LarderActing = 2;

        static bool LarderAttacks(EnemyType t) => t == EnemyType.Butcher || t == EnemyType.Gulper;

        /// Standing still: winding up, chopping or breathing in.
        public static bool LarderPlanted(Enemy e) => LarderAttacks(e.Type) && e.LarderStage != LarderIdle;

        /// Its facing stays put while it's busy, so the chop and the cone land where they showed.
        public static bool LarderFaceLocked(Enemy e) => LarderPlanted(e);

        /// A Gulper breathing in.
        public static bool Gulping(Enemy e) => e.Type == EnemyType.Gulper && e.LarderStage == LarderActing && e.Hp > 0f;

        /// How far through the wind-up, 0 to 1 (drawing: the cleaver going up, the cheeks swelling).
        public static float LarderWind(Enemy e) => e.LarderStage == LarderWinding
            ? MathX.Clamp01(1f - e.LarderT / (e.Type == EnemyType.Gulper ? GulpWind : ChopWind)) : 0f;

        /// Is `p` in a cone `half` either side of `face`, within `reach` of `from`?
        static bool InCone(V2 from, float face, float half, float reach, V2 p, float pad)
        {
            V2 to = p - from;
            float d = to.Length;
            if (d > reach + pad)
                return false;
            if (d < pad)
                return true;
            return Math.Abs(MathX.Fmod(to.Angle - face + MathX.Pi, MathX.Tau) - MathX.Pi) < half;
        }

        /// Host, in Special: starts, runs and ends a Butcher's chop or a Gulper's breath.
        void LarderSpecial(Enemy e, V2 tpos, float dt)
        {
            bool gulper = e.Type == EnemyType.Gulper;
            e.LarderCd -= dt;
            if (e.LarderStage == LarderIdle)
            {
                if (e.LarderCd > 0f || e.SpawnT > 0f || e.Pos.DistanceTo(tpos) > (gulper ? GulpRange : ChopTrigger + e.Radius))
                    return;
                e.Facing = (tpos - e.Pos).Angle;
                e.LarderStage = LarderWinding;
                e.LarderT = gulper ? GulpWind : ChopWind;
                return;
            }
            e.LarderT -= dt;
            if (e.LarderStage == LarderWinding)
            {
                if (e.LarderT > 0f)
                    return;
                e.LarderStage = LarderActing;
                e.LarderT = gulper ? GulpTime : ChopFlash;
                if (!gulper)
                    Chop(e);
                else
                    BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(e.Facing) * e.Radius, new Rgba(1f, 0.75f, 0.8f), 20f, 0.25f, Sfx.BossWhoosh);
                return;
            }
            if (gulper && GulpBit(e))
                e.LarderT = 0f;  // it bit: the breath ends
            if (e.LarderT > 0f)
                return;
            e.LarderStage = LarderIdle;
            e.LarderCd = gulper ? GulpEvery - GulpWind - GulpTime : ChopEvery;
        }

        /// Host: the Butcher's chop lands on everyone in the half-circle in front.
        void Chop(Enemy e)
        {
            foreach (var p in Players.Values)
                if (!p.Downed && p.InvulnT <= 0f && p.SwallowT <= 0f && InCone(e.Pos, e.Facing, MathX.Pi / 2f, ChopReach, p.Pos, p.Def.HitR))
                    Damage(p, ChopDamage, "Butcher");
            BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(e.Facing) * (e.Radius + 14f), new Rgba(1f, 0.92f, 0.92f), 26f, 0.2f, Sfx.SwingHeavy);
        }

        /// Host: whoever reaches a breathing Gulper's mouth is bitten. True when it bit someone.
        bool GulpBit(Enemy e)
        {
            V2 mouth = e.Pos + V2.FromAngle(e.Facing) * e.Radius;
            foreach (var p in Players.Values)
                if (!p.Downed && p.InvulnT <= 0f && p.SwallowT <= 0f && p.Pos.DistanceTo(mouth) < p.Def.HitR + 12f)
                {
                    Damage(p, GulpBite, "Gulper");
                    BroadcastFx(FxKind.Spark, mouth, new Rgba(1f, 0.35f, 0.42f), 26f, 0.25f, Sfx.BossChomp);
                    return true;
                }
            return false;
        }

        /// Your own game (from DevourPull): a breathing Gulper's cone pulls you toward its mouth.
        V2 GulpPullAt(V2 at)
        {
            foreach (var e in Enemies.Values)
            {
                if (!Gulping(e))
                    continue;
                V2 to = e.Pos - at;
                float d = to.Length;
                if (d < e.Radius || !InCone(e.Pos, e.Facing, GulpHalf, GulpReach, at, 0f))
                    continue;
                return to / d * GulpPull;
            }
            return V2.Zero;
        }

        void SyncLarder(NetSync s, Enemy e)
        {
            s.Byte(ref e.LarderStage);
            if (e.LarderStage != LarderIdle)
                s.Float(ref e.LarderT);
        }
    }
}
