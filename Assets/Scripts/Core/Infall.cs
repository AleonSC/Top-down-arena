using System;

namespace Arena
{
    /// The Singularity's faction, the Infall (Docs/SingularityFaction.md, mockup Docs/Mockups/singularity-faction.html; drawn
    /// in Game/InfallLook.cs). Each enemy previews one thing the boss tests. Step 1 has three:
    ///   Debris (the Pull): melee rubble that falls at you on an inward spiral. It keeps its side (Enemy.Strafe) and turns
    ///     in from DebrisSpiral rad off your line when far to straight at you within DebrisStraight px.
    ///   Acolyte (bent shots): keeps AcolyteRange and strafes; every AcolyteEvery s it fires three slow orbs, the middle one
    ///     straight and the outer two AcolyteSpread rad out, curving (Bullet.AngVel) so all three meet where you stood.
    ///   Pulsar (Lasers): Phase 0 walks in to PulsarRange; 1 plants and flickers its two beams (one each way along Facing)
    ///     for PulsarWarn s, starting PulsarLead rad behind you; 2 the beams are on and sweep PulsarArc toward and past you
    ///     over PulsarSweep s (Strafe is the way round); 3 rests PulsarRest s, then plants again if you're near. Phase and
    ///     Facing are in the snapshot, so clients draw the beams with no extra fields (the flicker times from Enemy.SeenT).
    public partial class Game
    {
        public const float DebrisSpiral = 1.05f, DebrisStraight = 90f, DebrisFar = 420f;
        public const float AcolyteRange = 280f, AcolyteEvery = 2.4f, AcolyteSpread = 0.5f, AcolyteOrbSpeed = 150f;
        public const float PulsarRange = 300f, PulsarWarn = 1f, PulsarSweep = 4f, PulsarRest = 2.5f, PulsarArc = MathX.Pi / 2f,
            PulsarLead = 0.5f, PulsarBeamLen = 900f, PulsarBeamWidth = 14f;
        float PulsarDamage => 16f;  // a touch; the difficulty's per-wave damage growth scales it, like the Beam Sentry's

        public static bool IsInfall(EnemyType t) => t >= EnemyType.Debris && t <= EnemyType.Pulsar;

        /// The Pulsar keeps its beams' line while they flicker and sweep.
        public static bool PulsarLocked(Enemy e) => e.Type == EnemyType.Pulsar && (e.Phase == 1 || e.Phase == 2);

        V2 InfallMove(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            switch (e.Type)
            {
                case EnemyType.Debris:
                {
                    // Off your line by up to DebrisSpiral when far, straight in up close: an inward spiral.
                    float k = MathX.Clamp01((dist - DebrisStraight) / (DebrisFar - DebrisStraight));
                    float a = DebrisSpiral * k;
                    return dir * (float)Math.Cos(a) + dir.Orthogonal() * e.Strafe * (float)Math.Sin(a);
                }
                case EnemyType.Acolyte:
                    return KeepDistance(dir, dist, AcolyteRange) + dir.Orthogonal() * e.Strafe * 0.6f;
                case EnemyType.Pulsar:
                    if (e.Phase == 1 || e.Phase == 2)
                        return V2.Zero;  // planted while its beams flicker and sweep
                    return KeepDistance(dir, dist, PulsarRange) + dir.Orthogonal() * e.Strafe * 0.3f;
            }
            return dir;
        }

        void InfallSpecial(Enemy e, V2 tpos, float dt)
        {
            if (e.Type != EnemyType.Pulsar)
                return;
            e.PhaseT -= dt;
            float dist = tpos.DistanceTo(e.Pos), aim = (tpos - e.Pos).Angle;
            switch (e.Phase)
            {
                case 0:
                case 3:
                    if (e.Phase == 3 && e.PhaseT > 0f)
                        break;
                    if (e.T > 1.2f && dist < PulsarRange + 90f)
                    {
                        // Plant: the beams start a little behind you and sweep through you.
                        e.Strafe = e.Strafe >= 0f ? 1f : -1f;
                        e.Facing = aim - e.Strafe * PulsarLead;
                        e.Phase = 1;
                        e.PhaseT = PulsarWarn;
                        BroadcastSound(Sfx.BossCharge);
                    }
                    else
                        e.Phase = 0;
                    break;
                case 1:
                    if (e.PhaseT <= 0f)
                    {
                        e.Phase = 2;
                        e.PhaseT = PulsarSweep;
                    }
                    break;
                case 2:
                    e.Facing += e.Strafe * PulsarArc / PulsarSweep * dt;
                    foreach (var p in Players.Values)
                        if (!p.Downed && p.InvulnT <= 0f && PulsarBeamDistance(e, p.Pos) < PulsarBeamWidth / 2f + p.Def.HitR)
                            Damage(p, PulsarDamage, e.Def.Name + " (beam)");
                    if (e.PhaseT <= 0f)
                    {
                        e.Phase = 3;
                        e.PhaseT = PulsarRest / TraitFireMult(e);
                    }
                    break;
            }
        }

        /// How far a point is from either of a Pulsar's two beams (one each way along its Facing).
        public static float PulsarBeamDistance(Enemy e, V2 p)
        {
            V2 u = V2.FromAngle(e.Facing) * PulsarBeamLen;
            return SegmentDistance(p, e.Pos - u, e.Pos + u);
        }

        /// Returns true when it handled the shot.
        bool InfallFire(Enemy e, V2 tpos, float aim)
        {
            switch (e.Type)
            {
                case EnemyType.Pulsar:
                    e.FireT = 99f;  // its beams are its attack (InfallSpecial)
                    return true;
                case EnemyType.Acolyte:
                    e.FireT = AcolyteEvery;
                    AcolyteVolley(e, tpos, aim);
                    return true;
            }
            return false;
        }

        /// Three slow orbs: one straight, two that leave AcolyteSpread out either side and bend back in to meet it where
        /// you stood. A shot turning at a steady rate runs along a circle; to cross the chord d after setting off at angle
        /// s to it, it turns 2s over an arc of d·s/sin(s).
        void AcolyteVolley(Enemy e, V2 tpos, float aim)
        {
            float d = Math.Max(120f, Math.Min(600f, tpos.DistanceTo(e.Pos) - e.Radius));
            float s = AcolyteSpread, arc = d * s / (float)Math.Sin(s), time = arc / AcolyteOrbSpeed, turn = 2f * s / time;
            EnemyShot(e, aim, AcolyteOrbSpeed, Defs.SmallShot);
            for (int side = -1; side <= 1; side += 2)
            {
                float ang = aim + side * s;
                V2 dir = V2.FromAngle(ang);
                // Gone a little after the meeting point, so it doesn't loop round the arena.
                var b = SpawnBullet(Defs.SmallShot, e.Pos + dir * e.Radius, dir * AcolyteOrbSpeed, -side * turn, time * 1.5f + 0.4f, Defs.BulletDamage(Defs.SmallShot), 0, false);
                b.Source = e.Type;
                b.Shooter = e.Id;
                b.Look = b.Sent.Look = LookFor(e, ang, -side * turn);
                NoteVolley(e, b);
            }
            BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(aim) * (e.Radius + 6f), new Rgba(1f, 0.47f, 0.86f), 14f, 0.15f, Sfx.EnemyShot);
        }
    }
}
