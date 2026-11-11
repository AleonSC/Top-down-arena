using System;

namespace Arena
{
    /// The Security Guard (Docs/Mockups/security-guard.html; drawn in Game/SecurityLook.cs): a Warden-faction shooter with a
    /// service pistol. It keeps SecurityRange away, turns toward where you will be (a straight-line lead from how you are moving,
    /// like the Leader's), plants its feet for SecurityWindUp s with a red lane on the floor to the lead point, then fires one
    /// tracer round (Defs.TracerRound) and starts over: semi-auto, one every SecurityEvery s. Seven shots empty the magazine;
    /// the reload takes SecurityReload s, during which it stands still, doesn't shoot and is easy to rush.
    ///   Enemy.Phase is the rounds left (SecurityMag..1) and 0 while it reloads; the host's FireT counts the gap to the next shot
    ///   (or the reload), and Enemy.Winding is the lock-on (replicated). Clients time the recoil, the casing and the reload ring
    ///   from the age of the Phase they see (Enemy.SeenT).
    /// It only shoots once within SecurityEngage of its target (the countdown holds at SecurityEvery until then), then one round every SecurityEvery s.
    /// It doesn't wait for a shooter's turn (Readability.cs): its magazine is its pacing.
    public partial class Game
    {
        public const float SecurityWindUp = 0.25f, SecurityEvery = 1.5f, SecurityReload = 2.2f, SecurityRange = 300f, SecurityEngage = 380f,
            SecurityTurn = 2.8f, SecurityBulletSpeed = 340f, SecurityAfterReload = 0.75f;
        public const byte SecurityMag = 7;

        /// It stands still while it locks on and while it reloads; otherwise it holds its distance and strafes.
        V2 SecurityMove(Enemy e, V2 dir, float dist)
        {
            if (e.Winding || e.Phase == 0)
                return V2.Zero;
            return KeepDistance(dir, dist, SecurityRange) + dir.Orthogonal() * e.Strafe * 0.7f;
        }

        /// The angle that meets a player moving as they are now, counting the lock-on time. A decoy (a Shadow Clone, Vanish)
        /// gets aimed at as it stands.
        float SecurityLeadAngle(Enemy e, V2 tpos)
        {
            Player p;
            V2 rel = tpos - e.Pos;
            if (!Players.TryGetValue(e.TargetId, out p) || p.Pos.DistanceTo(tpos) > 1f)
                return rel.Angle;
            V2 v = p.Motion;
            if (v.Length > 450f)
                v = v.Normalized() * 450f;
            V2 ahead = rel + v * SecurityWindUp;
            return (ahead + v * LeadTime(ahead, v, SecurityBulletSpeed)).Angle;
        }

        /// It turns at SecurityTurn rad/s toward the lead point (toward you, while it reloads); locked while it winds up.
        void SecurityFace(Enemy e, V2 tpos, float dt)
        {
            if (e.Winding)
                return;
            float want = e.Phase == 0 ? (tpos - e.Pos).Angle : SecurityLeadAngle(e, tpos);
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
