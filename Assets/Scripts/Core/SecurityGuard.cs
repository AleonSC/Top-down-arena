using System;

namespace Arena
{
    /// The Security Guard (Docs/Mockups/security-guard.html; drawn in Game/SecurityLook.cs): a Warden-faction shooter with a
    /// service pistol. It keeps SecurityRange away and strafes, keeping one side for SecurityStrafeMin..Max s (Enemy.PhaseT
    /// counts it down), turns its pistol straight at you (no lead), then locks it for SecurityWindUp s with a red lane on the
    /// floor while it keeps walking at SecurityAimPace, and fires one tracer round (Defs.TracerRound): semi-auto, one every
    /// SecurityEvery s. Seven shots empty the magazine; the reload takes SecurityReload s, during which it eases to a stop
    /// (SecurityEase s each way), doesn't shoot and is easy to rush.
    ///   Enemy.Phase is the rounds left (SecurityMag..1) and 0 while it reloads; the host's FireT counts the gap to the next shot
    ///   (or the reload), and Enemy.Winding is the lock-on (replicated). Clients time the recoil, the casing and the reload ring
    ///   from the age of the Phase they see (Enemy.SeenT).
    /// It only shoots once within SecurityEngage of its target (the countdown holds at SecurityEvery until then), then one round every SecurityEvery s.
    /// It doesn't wait for a shooter's turn (Readability.cs): its magazine is its pacing.
    public partial class Game
    {
        public const float SecurityWindUp = 0.25f, SecurityEvery = 1.5f, SecurityReload = 2.2f, SecurityRange = 300f, SecurityEngage = 380f,
            SecurityTurn = 4f, SecurityBulletSpeed = 340f, SecurityAfterReload = 0.75f, SecurityAimPace = 0.6f, SecurityEase = 0.3f,
            SecurityStrafeMin = 2f, SecurityStrafeMax = 4f;
        public const byte SecurityMag = 7;

        /// It holds its distance and strafes, slowing to SecurityAimPace while it locks on; it eases to a stop for the reload
        /// and back up as the new magazine goes in.
        V2 SecurityMove(Enemy e, V2 dir, float dist, float dt, float strafe, float aimStrafe)  // (a Hive Needler strafes harder: ShootBrains.cs)
        {
            e.PhaseT -= dt;
            if (e.PhaseT <= 0f)
            {
                e.Strafe = -e.Strafe;  // commit to a side for a while instead of the usual random flips
                e.PhaseT = Rand(SecurityStrafeMin, SecurityStrafeMax);
            }
            V2 v = KeepDistance(dir, dist, SecurityRange) + dir.Orthogonal() * e.Strafe * strafe;
            if (e.Phase == 0)
            {
                float down = MathX.Clamp01((e.FireT - (SecurityReload - SecurityEase)) / SecurityEase), up = MathX.Clamp01(1f - e.FireT / SecurityEase);
                return v.LimitLength(1f) * Math.Max(down, up);
            }
            return e.Winding ? v.LimitLength(1f) * SecurityAimPace * aimStrafe : v;
        }

        /// It turns at SecurityTurn rad/s toward you (no lead); locked while it winds up.
        void SecurityFace(Enemy e, V2 tpos, float dt)
        {
            if (e.Winding)
                return;
            float want = (tpos - e.Pos).Angle;
            float diff = MathX.Fmod(want - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi;
            e.Facing += Math.Max(-SecurityTurn * dt, Math.Min(SecurityTurn * dt, diff));
        }

        /// FireT ran out: seat a new magazine if it was reloading, otherwise fire the round it is pointing along.
        void SecurityFire(Enemy e)
        {
            if (e.Phase == 0)
            {
                e.Phase = SecurityMag;
                e.FireT = SecurityAfterReload;
                return;
            }
            V2 muzzle = e.Pos + V2.FromAngle(e.Facing) * (e.Radius + 14f);
            EnemyShot(e, e.Facing, SecurityBulletSpeed, Defs.TracerRound);
            BroadcastFx(FxKind.Spark, muzzle, new Rgba(1f, 0.96f, 0.8f), 14f, 0.12f, Sfx.ShotPistol);
            e.Phase--;
            if (e.Phase == 0)
            {
                e.FireT = SecurityReload;  // empty: the magazine drops
                BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(e.Facing + 0.9f) * (e.Radius + 2f), new Rgba(0.8f, 0.85f, 0.9f), 10f, 0.15f, Sfx.Clang);
            }
            else
                e.FireT = SecurityEvery;
        }
    }
}
